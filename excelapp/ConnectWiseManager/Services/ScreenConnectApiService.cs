using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using ConnectWiseManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConnectWiseManager.Services;

public class ScreenConnectApiService : IScreenConnectApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private ScreenConnectCredentials? _credentials;

    private HttpClient? _client;
    private CookieContainer? _cookieContainer;
    private bool _useCookieAuth;

    public ScreenConnectApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private static string NormalizeBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        return url.TrimEnd('/');
    }

    private static string GetOrigin(string baseUrl)
    {
        try
        {
            var uri = new Uri(baseUrl);
            return uri.GetLeftPart(UriPartial.Authority);
        }
        catch
        {
            return baseUrl;
        }
    }

    private void ApplyCommonHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!client.DefaultRequestHeaders.Contains("X-Requested-With"))
        {
            client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }
        if (_credentials != null)
        {
            var origin = GetOrigin(_credentials.BaseUrl);
            if (client.DefaultRequestHeaders.Contains("Origin"))
                client.DefaultRequestHeaders.Remove("Origin");
            client.DefaultRequestHeaders.Add("Origin", origin);

            if (client.DefaultRequestHeaders.Referrer == null)
            {
                try { client.DefaultRequestHeaders.Referrer = new Uri(_credentials.BaseUrl); } catch { }
            }
        }
    }

    private void ApplyAuthHeaders(HttpClient client)
    {
        ApplyCommonHeaders(client);

        if (_credentials == null) return;
        if (_useCookieAuth)
        {
            client.DefaultRequestHeaders.Authorization = null;
            if (client.DefaultRequestHeaders.Contains("X-Auth-Token"))
                client.DefaultRequestHeaders.Remove("X-Auth-Token");
            if (client.DefaultRequestHeaders.Contains("X-One-Time-Password"))
                client.DefaultRequestHeaders.Remove("X-One-Time-Password");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_credentials.PersonalAccessToken))
        {
            var tokenValue = string.IsNullOrWhiteSpace(_credentials.Username)
                ? _credentials.PersonalAccessToken
                : $"{_credentials.Username}:{_credentials.PersonalAccessToken}";

            if (client.DefaultRequestHeaders.Contains("X-Auth-Token"))
                client.DefaultRequestHeaders.Remove("X-Auth-Token");
            client.DefaultRequestHeaders.Add("X-Auth-Token", tokenValue);
            client.DefaultRequestHeaders.Authorization = null;
        }
        else if (!string.IsNullOrWhiteSpace(_credentials.AuthToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _credentials.AuthToken);
            if (!string.IsNullOrWhiteSpace(_credentials.OneTimePassword))
            {
                if (client.DefaultRequestHeaders.Contains("X-One-Time-Password"))
                    client.DefaultRequestHeaders.Remove("X-One-Time-Password");
                client.DefaultRequestHeaders.Add("X-One-Time-Password", _credentials.OneTimePassword);
            }
        }
    }

    private bool HasAuthCookie(string baseUrl)
    {
        try
        {
            if (_cookieContainer == null) return false;
            var uri = new Uri(baseUrl);
            var cookies = _cookieContainer.GetCookies(uri);
            foreach (Cookie c in cookies)
            {
                if (string.Equals(c.Name, ".ASPXAUTH", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name, ".AspNetCore.Cookies", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name, "ASP.NET_SessionId", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(c.Value)) return true;
                }
            }
            return cookies.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidatePatAsync(HttpClient c, string baseUrl)
    {
        var protectedEndpoints = new List<string>
        {
            $"{baseUrl}/App_Extensions/2d558935-686a-4bd0-9991-07539f5fe749/Service.ashx/sessions-by-filter?sessionFilter=Access",
            $"{baseUrl}/Services/SessionService.ashx/GetSessions?sessionType=Access"
        };

        foreach (var url in protectedEndpoints)
        {
            try
            {
                var resp = await SendWithRetryAsync(() => c.GetAsync(url));
                if (resp.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // ignore and try next
            }
        }
        return false;
    }

    // New: validate cookie using core service first, then extension (less false negatives)
    private async Task<bool> ValidateCookieAsync(HttpClient c, string baseUrl)
    {
        var endpoints = new List<string>
        {
            $"{baseUrl}/Services/SessionService.ashx/GetSessions?sessionType=Access",
            $"{baseUrl}/App_Extensions/2d558935-686a-4bd0-9991-07539f5fe749/Service.ashx/sessions-by-filter?sessionFilter=Access"
        };

        foreach (var url in endpoints)
        {
            try
            {
                var resp = await SendWithRetryAsync(() => c.GetAsync(url));
                if (resp.IsSuccessStatusCode) return true;
                var pb = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"ScreenConnect cookie probe attempt: {(int)resp.StatusCode} {resp.ReasonPhrase} {pb}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScreenConnect cookie probe exception: {ex.Message}");
            }
        }
        return false;
    }

    public async Task<bool> AuthenticateAsync(ScreenConnectCredentials credentials)
    {
        try
        {
            credentials.BaseUrl = NormalizeBaseUrl(credentials.BaseUrl);

            if (!string.IsNullOrWhiteSpace(credentials.Username) && !string.IsNullOrWhiteSpace(credentials.Password))
            {
                credentials.AuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}"));
            }

            _credentials = credentials;
            _useCookieAuth = false;

            // Prefer PAT if provided
            if (!string.IsNullOrWhiteSpace(credentials.PersonalAccessToken))
            {
                var c = _httpClientFactory.CreateClient();
                ApplyAuthHeaders(c);
                var ok = await ValidatePatAsync(c, credentials.BaseUrl);
                return ok;
            }

            // Try cookie-based auth via TryLogin so we can call extension APIs without 403
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true
            };
            _client = new HttpClient(handler);
            ApplyCommonHeaders(_client);

            var loginUrl = $"{credentials.BaseUrl}/Services/AuthenticationService.ashx/login/try";
            var payload = new
            {
                userName = credentials.Username,
                password = credentials.Password,
                oneTimePassword = string.IsNullOrWhiteSpace(credentials.OneTimePassword) ? null : credentials.OneTimePassword,
                shouldTrust = credentials.ShouldTrust,
                securityNonce = string.Empty
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(loginUrl, content);
            if (response.IsSuccessStatusCode && HasAuthCookie(credentials.BaseUrl))
            {
                // Warm-up to finalize anti-forgery/session cookies
                try { await _client.GetAsync(credentials.BaseUrl); } catch { }

                var ok = await ValidateCookieAsync(_client, credentials.BaseUrl);
                if (!ok)
                {
                    System.Diagnostics.Debug.WriteLine("ScreenConnect cookie probe failed across endpoints; denying cookie auth.");
                    return false;
                }

                _useCookieAuth = true;
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"ScreenConnect TryLogin failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScreenConnect auth exception: {ex.Message}");
            return false;
        }
    }

    private HttpContent JsonContent(object payload)
        => new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> send)
    {
        int attempts = 0;
        int maxAttempts = 4;
        int delayMs = 500;
        var rnd = new Random();
        while (true)
        {
            attempts++;
            var resp = await send();
            if ((int)resp.StatusCode != 429 || attempts >= maxAttempts)
            {
                return resp;
            }
            var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(delayMs + rnd.Next(0, 200));
            await Task.Delay(retryAfter);
            delayMs *= 2;
        }
    }

    public async Task<bool> SendCommandToSessionAsync(string sessionId, string command)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var payload = new { sessionID = sessionId, command, processType = "#PowerShell" };
            var response = await SendWithRetryAsync(() => client.PostAsync(
                $"{_credentials.BaseUrl}/Services/PageService.ashx/AddEventToSessions",
                JsonContent(payload)));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeployInstallerAsync(string sessionId, string installerUrl)
    {
        var script = $"#!ps\nInvoke-WebRequest \"{installerUrl}\" -OutFile \"C:\\Temp\\installer.exe\"\nStart-Process \"C:\\Temp\\installer.exe\" -ArgumentList \"/silent\"\n";
        return await SendCommandToSessionAsync(sessionId, script);
    }

    public async Task<bool> ExecuteRepairScriptAsync(string sessionId)
    {
        var script = "\n#!ps\nmsiexec /f \"C:\\Program Files\\ScreenConnect\\ScreenConnect.ClientSetup.msi\" /qn\n";
        return await SendCommandToSessionAsync(sessionId, script);
    }

    public async Task<ScriptExecution?> GetSessionDetailsAsync(string sessionId)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var response = await SendWithRetryAsync(() => client.GetAsync($"{_credentials.BaseUrl}/App_Extensions/a16c05d4-86d0-40d5-b2e4-6731a96199e5/Service.ashx/session-details-by-session-id?sessionID={sessionId}"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var sessionJson = JsonConvert.DeserializeObject<JObject>(content);

                return new ScriptExecution
                {
                    SessionId = sessionId,
                    DeviceName = sessionJson?["Name"]?.ToString() ?? "",
                    Status = sessionJson?["ConnectionStatus"]?.ToString() ?? "",
                    StartTime = DateTime.UtcNow,
                    Output = sessionJson?.ToString() ?? ""
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AddNoteToSessionAsync(string sessionId, string note)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var payload = new { sessionID = sessionId, note, eventType = "Note" };
            var response = await SendWithRetryAsync(() => client.PostAsync($"{_credentials.BaseUrl}/Services/PageService.ashx/AddSessionEvent", JsonContent(payload)));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateSessionAsync(string deviceName, Dictionary<string, string> customProperties)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var payload = new { name = deviceName, customProperties };
            var response = await SendWithRetryAsync(() => client.PostAsync($"{_credentials.BaseUrl}/Services/PageService.ashx/CreateSession", JsonContent(payload)));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendMessageToSessionAsync(string sessionId, string message)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var payload = new { sessionID = sessionId, message };
            var response = await SendWithRetryAsync(() => client.PostAsync($"{_credentials.BaseUrl}/Services/PageService.ashx/SendMessage", JsonContent(payload)));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateSessionCustomPropertiesAsync(string sessionId, Dictionary<string, string> properties)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var payload = new { sessionID = sessionId, properties };
            var response = await SendWithRetryAsync(() => client.PostAsync($"{_credentials.BaseUrl}/Services/PageService.ashx/UpdateSessionCustomProperties", JsonContent(payload)));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static JArray? TryExtractArray(JToken? token)
    {
        if (token is JArray a) return a;
        if (token is JObject o)
        {
            if (o["Sessions"] is JArray a1) return a1;
            if (o["Data"] is JArray a2) return a2;
            if (o["items"] is JArray a3) return a3;
            if (o["results"] is JArray a4) return a4;
        }
        return null;
    }

    public async Task<List<ScreenConnectSession>> GetSessionsAsync()
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        var sessions = new List<ScreenConnectSession>();
        try
        {
            var client = _client ?? _httpClientFactory.CreateClient();
            ApplyAuthHeaders(client);

            var baseUrl = _credentials.BaseUrl;
            var list = new List<(string url, bool post, object? body)>();

            // Allow override from UI
            if (!string.IsNullOrWhiteSpace(_credentials.SessionsEndpointPath))
            {
                var p = _credentials.SessionsEndpointPath!;
                if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    list.Add((p, false, null));
                else
                    list.Add(($"{baseUrl}/{p.TrimStart('/')}", false, null));
            }

            // Prefer documented App_Extensions endpoints present in this instance's OpenAPI
            list.Add(($"{baseUrl}/App_Extensions/2d558935-686a-4bd0-9991-07539f5fe749/Service.ashx/sessions-by-filter?sessionFilter=Access", false, null));
            // Fallbacks (may require extra permissions)
            list.Add(($"{baseUrl}/Services/SessionService.ashx/GetSessions?sessionType=Access", false, null));
            list.Add(($"{baseUrl}/Services/SessionService.ashx/GetActiveSessions?sessionType=Access", false, null));
            list.Add(($"{baseUrl}/Services/PageService.ashx/GetSessions?sessionType=Access", false, null));
            list.Add(($"{baseUrl}/Services/PageService.ashx/GetHostSessionInfo", false, null));
            list.Add(($"{baseUrl}/Services/PageService.ashx/EnumerateSessions", true, new { sessionType = "Access" }));
            list.Add(($"{baseUrl}/Services/SessionService.ashx/EnumerateSessions", true, new { sessionType = "Access" }));

            JArray? resultArray = null;
            foreach (var (url, post, body) in list)
            {
                try
                {
                    HttpResponseMessage resp = post ? await SendWithRetryAsync(() => client.PostAsync(url, JsonContent(body ?? new { }))) : await SendWithRetryAsync(() => client.GetAsync(url));
                    if (!resp.IsSuccessStatusCode)
                    {
                        var b = await resp.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"GetSessions candidate failed: {url} => {(int)resp.StatusCode} {resp.ReasonPhrase} {b}");
                        if (resp.StatusCode == HttpStatusCode.Forbidden)
                        {
                            System.Diagnostics.Debug.WriteLine("ScreenConnect 403: ensure the account has permissions or cookie login succeeded with a persisted cookie (Origin must be trusted).");
                        }
                        continue;
                    }
                    var content = await resp.Content.ReadAsStringAsync();
                    var token = JsonConvert.DeserializeObject<JToken>(content);
                    var arr = TryExtractArray(token);
                    if (arr != null)
                    {
                        resultArray = arr;
                        break;
                    }
                    // Some endpoints may return an object with 'sessions' (lowercase)
                    if (token is JObject o && o["sessions"] is JArray a5)
                    {
                        resultArray = a5; break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetSessions candidate exception for {url}: {ex.Message}");
                }
            }

            if (resultArray == null)
            {
                System.Diagnostics.Debug.WriteLine("GetSessions could not find a working endpoint.");
                return sessions;
            }

            bool IsMac(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                var t = s.Trim();
                if (Regex.IsMatch(t, "^([0-9A-Fa-f]{2}[-:]){5}([0-9A-Fa-f]{2})$")) return true;
                if (Regex.IsMatch(t, "^[0-9A-Fa-f]{12}$")) return true;
                return false;
            }

            string ExtractMac(JToken item)
            {
                var candidatesMac = new List<string?>
                {
                    item["MacAddress"]?.ToString(),
                    item["MACAddress"]?.ToString(),
                    item["MachineAddress"]?.ToString(),
                    item["PrimaryMachineAddress"]?.ToString(),
                    item["PrimaryNetworkAddress"]?.ToString()
                };
                foreach (var c in candidatesMac)
                {
                    if (IsMac(c ?? "")) return c!;
                }
                if (item["MacAddresses"] is JArray macs)
                {
                    foreach (var m in macs)
                    {
                        var s = m?.ToString();
                        if (IsMac(s ?? "")) return s!;
                    }
                }
                if (item["Addresses"] is JArray addrs)
                {
                    foreach (var m in addrs)
                    {
                        var s = m?.ToString();
                        if (IsMac(s ?? "")) return s!;
                    }
                }
                return string.Empty;
            }

            foreach (var item in resultArray)
            {
                var id = item["SessionID"]?.ToString() ?? item["Id"]?.ToString() ?? string.Empty;
                var name = item["Name"]?.ToString() ?? item["SessionName"]?.ToString() ?? string.Empty;
                var mac = ExtractMac(item);
                var company = item["CustomProperty1"]?.ToString() ?? string.Empty;
                var site = item["CustomProperty2"]?.ToString() ?? string.Empty;

                sessions.Add(new ScreenConnectSession
                {
                    Id = id,
                    Name = name,
                    MacAddress = mac,
                    Company = company,
                    Site = site
                });
            }

            return sessions;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSessions exception: {ex.Message}");
            return sessions;
        }
    }

    public async Task<BulkUpdateResponse?> UpdateAllSessionsAsync(
        IEnumerable<UpdateSessionInstruction> sessions,
        string? returnDataCsv = null,
        string? endpointPathOrUrl = null,
        string? authHeaderName = null,
        string? authHeaderValue = null,
        CancellationToken ct = default)
    {
        var client = _client ?? _httpClientFactory.CreateClient();

        // Use credentials-specified header if explicit params not provided
        if (string.IsNullOrWhiteSpace(authHeaderName) && string.IsNullOrWhiteSpace(authHeaderValue) && _credentials != null)
        {
            authHeaderName = string.IsNullOrWhiteSpace(_credentials.CustomAuthHeaderName) ? null : _credentials.CustomAuthHeaderName;
            authHeaderValue = string.IsNullOrWhiteSpace(_credentials.CustomAuthHeaderValue) ? null : _credentials.CustomAuthHeaderValue;
        }

        bool headerOnly = !string.IsNullOrWhiteSpace(authHeaderName) && !string.IsNullOrWhiteSpace(authHeaderValue);

        // Resolve URL
        string url;
        if (!string.IsNullOrWhiteSpace(endpointPathOrUrl))
        {
            if (endpointPathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = endpointPathOrUrl;
            }
            else
            {
                if (_credentials == null)
                    throw new InvalidOperationException("BaseUrl required to build endpoint URL. Provide absolute endpointPathOrUrl or authenticate first.");
                url = $"{_credentials.BaseUrl.TrimEnd('/')}/{endpointPathOrUrl.TrimStart('/')}";
            }
        }
        else
        {
            if (_credentials == null)
                throw new InvalidOperationException("Endpoint URL not provided. Provide endpointPathOrUrl or authenticate to derive BaseUrl.");
            var baseUrl = _credentials.BaseUrl.TrimEnd('/');
            url = $"{baseUrl}/App_Extensions/711aa604-57a9-44ab-8cb5-256272ed18c3/Service.ashx/UpdateAllSessions";
        }

        // SSRF-safe minimal validation
        try
        {
            var uri = new Uri(url);
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only HTTPS endpoints are allowed for UpdateAllSessions.");
            if (_credentials != null)
            {
                var allowedHost = new Uri(_credentials.BaseUrl).Host;
                if (!string.Equals(uri.Host, allowedHost, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Endpoint host must match configured ScreenConnect BaseUrl host.");
            }
        }
        catch (UriFormatException)
        {
            throw new InvalidOperationException("Invalid endpoint URL format.");
        }

        // Build payload
        var sessionArray = new JArray();
        foreach (var s in sessions)
        {
            var obj = JObject.FromObject(s, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });
            sessionArray.Add(obj);
        }
        var outer = new JArray
        {
            new JObject { ["SessionPayload"] = sessionArray }
        };
        if (!string.IsNullOrWhiteSpace(returnDataCsv))
        {
            outer.Add(new JObject { ["ReturnData"] = returnDataCsv });
        }
        var json = outer.ToString(Formatting.None);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            // Headers
            if (headerOnly)
            {
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!request.Headers.Contains("X-Requested-With"))
                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.UserAgent.ParseAdd("ConnectWiseManager/1.0");
                // Default header name to AuthenticationSecret if not provided
                var headerName = string.IsNullOrWhiteSpace(authHeaderName) ? "AuthenticationSecret" : authHeaderName!;
                request.Headers.Add(headerName, authHeaderValue!);
            }
            else
            {
                ApplyAuthHeaders(client);
            }

            var resp = await SendWithRetryAsync(() => client.SendAsync(request, ct));
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"ScreenConnect UpdateAllSessions failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {body}");
                return null;
            }
            try
            {
                var result = JsonConvert.DeserializeObject<BulkUpdateResponse>(body);
                return result;
            }
            catch
            {
                try
                {
                    var token = JsonConvert.DeserializeObject<JToken>(body);
                    if (token is JObject o && o["SessionResults"] != null)
                    {
                        return o.ToObject<BulkUpdateResponse>();
                    }
                }
                catch { }
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScreenConnect UpdateAllSessions exception: {ex.Message}");
            return null;
        }
    }
}
