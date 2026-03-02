using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ConnectWiseManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ConnectWiseManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ConnectWiseManager.Services;

public class AsioApiService : IAsioApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private AsioCredentials? _credentials;

    private readonly Dictionary<string, string> _endpointNameCache = new(StringComparer.OrdinalIgnoreCase);

    // Simple rate-limit gate across requests
    private DateTime _rateLimitedUntilUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _rateLock = new(1, 1);

    private int _lastRateLimitRemaining = -1;
    private int _lastRateLimitLimit = -1;
    private DateTimeOffset? _lastRateLimitReset;
    // Adjusted HasQuota: only exhausted when a positive limit exists and remaining <= 0
    public bool HasQuota() => !IsRateLimitedNow() && (_lastRateLimitLimit <= 0 || _lastRateLimitRemaining > 0);

    public AsioApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private async Task EnsureRateWindowAsync()
    {
        await _rateLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (_rateLimitedUntilUtc > now)
            {
                var delay = _rateLimitedUntilUtc - now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }
            }
        }
        finally
        {
            _rateLock.Release();
        }
    }

    private async Task SetRateLimitedAsync(HttpResponseMessage resp)
    {
        TimeSpan wait = TimeSpan.FromSeconds(30);
        if (resp.Headers.TryGetValues("Retry-After", out var values))
        {
            var v = values.FirstOrDefault();
            if (int.TryParse(v, out var seconds))
            {
                wait = TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 120));
            }
            else if (DateTimeOffset.TryParse(v, out var when))
            {
                var until = when.UtcDateTime - DateTime.UtcNow;
                if (until > TimeSpan.Zero) wait = until;
            }
        }

        await _rateLock.WaitAsync();
        try
        {
            var candidate = DateTime.UtcNow.Add(wait);
            if (candidate > _rateLimitedUntilUtc)
            {
                _rateLimitedUntilUtc = candidate;
            }
        }
        finally
        {
            _rateLock.Release();
        }
    }

    public bool IsRateLimitedNow() => DateTime.UtcNow < _rateLimitedUntilUtc;

    private static string NormalizeBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        return url.TrimEnd('/');
    }

    private static string BuildBasicAuth(string clientId, string clientSecret)
    {
        var bytes = Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}");
        return Convert.ToBase64String(bytes);
    }

    private static IEnumerable<string> GetCandidateTokenEndpoints(AsioCredentials c)
    {
        if (!string.IsNullOrWhiteSpace(c.TokenEndpoint))
        {
            yield return c.TokenEndpoint!;
            yield break;
        }

        var baseUrl = c.BaseUrl.TrimEnd('/');
        yield return $"{baseUrl}/v1/token";
        yield return $"{baseUrl}/oauth2/token";
        yield return $"{baseUrl}/connect/token";
        yield return $"{baseUrl}/oauth/token";
    }

    private static bool IsV1JsonEndpoint(string endpoint)
        => endpoint.Contains("/v1/token", StringComparison.OrdinalIgnoreCase);

    private static HttpContent BuildFormContent(AsioCredentials c, bool useBasicHeaderOnly)
    {
        var form = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        };

        if (!string.IsNullOrWhiteSpace(c.Scope))
        {
            form["scope"] = c.Scope;
        }
        if (!string.IsNullOrWhiteSpace(c.Audience))
        {
            // Some providers require 'audience' (Auth0) or legacy 'resource' (AAD v1)
            form["audience"] = c.Audience!;
            form["resource"] = c.Audience!;
        }

        if (!useBasicHeaderOnly)
        {
            form["client_id"] = c.ClientId;
            form["client_secret"] = c.ClientSecret;
        }

        return new FormUrlEncodedContent(form);
    }

    private static HttpContent BuildFormContentNoScope(AsioCredentials c, bool useBasicHeaderOnly)
    {
        var form = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        };
        if (!useBasicHeaderOnly)
        {
            form["client_id"] = c.ClientId;
            form["client_secret"] = c.ClientSecret;
        }
        return new FormUrlEncodedContent(form);
    }

    private static HttpContent BuildJsonContent(AsioCredentials c)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = c.ClientId,
            ["client_secret"] = c.ClientSecret,
        };
        if (!string.IsNullOrWhiteSpace(c.Scope))
        {
            payload["scope"] = c.Scope;
        }
        if (!string.IsNullOrWhiteSpace(c.Audience))
        {
            payload["audience"] = c.Audience!;
            payload["resource"] = c.Audience!;
        }
        var json = JsonConvert.SerializeObject(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static HttpContent BuildJsonContentNoScope(AsioCredentials c)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = c.ClientId,
            ["client_secret"] = c.ClientSecret,
        };
        var json = JsonConvert.SerializeObject(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string? TryExtractAccessToken(string body)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            var token = JsonConvert.DeserializeObject<JToken>(body);
            if (token is JObject obj)
            {
                var at = obj["access_token"]?.ToString();
                if (!string.IsNullOrWhiteSpace(at)) return at;
                // Some providers use camelCase or token property alternative names
                at = obj["token"]?.ToString();
                if (!string.IsNullOrWhiteSpace(at) && at.Length > 20) return at;
            }
        }
        catch { }
        return null;
    }

    private static string? TryExtractError(string body)
    {
        try
        {
            var token = JsonConvert.DeserializeObject<JToken>(body);
            if (token is JObject obj)
            {
                var err = obj["error"]?.ToString();
                var desc = obj["error_description"]?.ToString();
                if (!string.IsNullOrWhiteSpace(err) || !string.IsNullOrWhiteSpace(desc))
                {
                    return string.Join(" ", new[] { err, desc }.Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<bool> AuthenticateAsync(AsioCredentials credentials)
    {
        try
        {
            // Normalize URL
            credentials.BaseUrl = NormalizeBaseUrl(credentials.BaseUrl);
            var baseUrl = credentials.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) { System.Diagnostics.Debug.WriteLine("ASIO auth: base URL missing."); return false; }

            // Choose single endpoint (explicit override or /v1/token)
            var endpoint = !string.IsNullOrWhiteSpace(credentials.TokenEndpoint)
                ? credentials.TokenEndpoint!.Trim()
                : baseUrl.TrimEnd('/') + "/v1/token";

            // Build initial JSON payload (omit scope if empty)
            HttpContent BuildJson(bool includeScope)
            {
                var dict = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = credentials.ClientId?.Trim() ?? string.Empty,
                    ["client_secret"] = credentials.ClientSecret?.Trim() ?? string.Empty
                };
                if (includeScope && !string.IsNullOrWhiteSpace(credentials.Scope)) dict["scope"] = credentials.Scope.Trim();
                var json = JsonConvert.SerializeObject(dict);
                return new StringContent(json, Encoding.UTF8, "application/json");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Attempt 1: JSON body without Basic, with scope (if provided)
            System.Diagnostics.Debug.WriteLine($"ASIO auth attempt endpoint={endpoint} scope={(string.IsNullOrWhiteSpace(credentials.Scope) ? "<none>" : credentials.Scope)} basic=FALSE payload=json");
            var resp = await client.PostAsync(endpoint, BuildJson(includeScope: true));
            var body = await resp.Content.ReadAsStringAsync();
            var access = TryExtractAccessToken(body);
            if (resp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(access))
            {
                credentials.AccessToken = access;
                if (JsonConvert.DeserializeObject<JToken>(body) is JObject obj && obj["expires_in"] != null)
                {
                    var expiresIn = obj["expires_in"]!.Value<int>();
                    credentials.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                }
                _credentials = credentials;
                System.Diagnostics.Debug.WriteLine("ASIO auth succeeded (json no-basic)." );
                return true;
            }

            string? parsedError = TryExtractError(body);
            System.Diagnostics.Debug.WriteLine($"ASIO auth failure step1 status={(int)resp.StatusCode} reason={resp.ReasonPhrase} errorBodySnippet={body[..Math.Min(body.Length,200)]}" + (parsedError != null ? $" parsedError={parsedError}" : string.Empty));

            // Short-circuit on tenant lock (423) or service not implemented (503) – no retry will help
            if ((int)resp.StatusCode == 423)
            {
                System.Diagnostics.Debug.WriteLine("ASIO auth: 423 Locked – tenant or client not provisioned. Contact provider.");
                return false;
            }
            if ((int)resp.StatusCode == 503)
            {
                System.Diagnostics.Debug.WriteLine("ASIO auth: 503 Service unavailable/not implemented for token endpoint.");
                // If user overrode endpoint this may be wrong; advise
                return false;
            }

            bool looksInvalidScope = parsedError?.IndexOf("invalid_scope", StringComparison.OrdinalIgnoreCase) >= 0;
            bool looksInvalidClient = parsedError?.IndexOf("invalid_client", StringComparison.OrdinalIgnoreCase) >= 0;

            // Attempt 2: JSON body without Basic, WITHOUT scope (if step1 failed & scope provided)
            if (!string.IsNullOrWhiteSpace(credentials.Scope) && !looksInvalidClient)
            {
                System.Diagnostics.Debug.WriteLine("ASIO auth retry dropping scope.");
                var respNoScope = await client.PostAsync(endpoint, BuildJson(includeScope: false));
                var body2 = await respNoScope.Content.ReadAsStringAsync();
                var access2 = TryExtractAccessToken(body2);
                if (respNoScope.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(access2))
                {
                    credentials.AccessToken = access2;
                    if (JsonConvert.DeserializeObject<JToken>(body2) is JObject obj2 && obj2["expires_in"] != null)
                    {
                        var expiresIn2 = obj2["expires_in"]!.Value<int>();
                        credentials.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn2);
                    }
                    credentials.Scope = string.Empty; // reset scope since dropping succeeded
                    _credentials = credentials;
                    System.Diagnostics.Debug.WriteLine("ASIO auth succeeded (json no-basic no-scope)." );
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"ASIO auth failure step2 status={(int)respNoScope.StatusCode} reason={respNoScope.ReasonPhrase} body={body2[..Math.Min(body2.Length,200)]}");
                if ((int)respNoScope.StatusCode == 423 || (int)respNoScope.StatusCode == 503) return false;
                parsedError = TryExtractError(body2) ?? parsedError;
                looksInvalidClient = parsedError?.IndexOf("invalid_client", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Attempt 3: JSON body WITH Basic header (if allowed and not invalid_client already)
            if (credentials.UseBasicClientAuth && !looksInvalidClient)
            {
                var basic = BuildBasicAuth(credentials.ClientId ?? string.Empty, credentials.ClientSecret ?? string.Empty);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                System.Diagnostics.Debug.WriteLine("ASIO auth retry with Basic header (json)." );
                var respBasic = await client.PostAsync(endpoint, BuildJson(includeScope: string.IsNullOrWhiteSpace(credentials.Scope))); // if scope was invalid we dropped it
                var body3 = await respBasic.Content.ReadAsStringAsync();
                var access3 = TryExtractAccessToken(body3);
                if (respBasic.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(access3))
                {
                    credentials.AccessToken = access3;
                    if (JsonConvert.DeserializeObject<JToken>(body3) is JObject obj3 && obj3["expires_in"] != null)
                    {
                        var expiresIn3 = obj3["expires_in"]!.Value<int>();
                        credentials.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn3);
                    }
                    _credentials = credentials;
                    System.Diagnostics.Debug.WriteLine("ASIO auth succeeded (json with-basic)." );
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"ASIO auth failure step3 status={(int)respBasic.StatusCode} reason={respBasic.ReasonPhrase} body={body3[..Math.Min(body3.Length,200)]}");
                parsedError = TryExtractError(body3) ?? parsedError;
                if ((int)respBasic.StatusCode == 423 || (int)respBasic.StatusCode == 503) return false;
            }

            // Final diagnostic summary
            System.Diagnostics.Debug.WriteLine("ASIO auth failed after simplified sequence." + (parsedError != null ? $" error={parsedError}" : string.Empty));
            if (parsedError?.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) == true)
                System.Diagnostics.Debug.WriteLine("Hint: verify Client ID/Secret (invalid_client). Recreate secret if needed.");
            if (parsedError?.Contains("invalid_scope", StringComparison.OrdinalIgnoreCase) == true)
                System.Diagnostics.Debug.WriteLine("Hint: remove scope or use minimal scope like 'platform.devices.read'.");
            if (parsedError?.Contains("unauthorized_client", StringComparison.OrdinalIgnoreCase) == true)
                System.Diagnostics.Debug.WriteLine("Hint: client may lack permission for client_credentials grant – contact provider.");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ASIO auth exception (hardened): {ex.Message}");
            return false;
        }
    }

    public async Task<List<FieldDefinition>> GetAvailableFieldsAsync()
    {
        var fields = new List<FieldDefinition>
        {
            new() { Id = "computerName", Name = "computerName", DisplayName = "Computer Name", Type = "string", IsCustomField = false },
            new() { Id = "companyName", Name = "companyName", DisplayName = "Company Name", Type = "string", IsCustomField = false },
            new() { Id = "siteName", Name = "siteName", DisplayName = "Site Name", Type = "string", IsCustomField = false },
            new() { Id = "macAddress", Name = "macAddress", DisplayName = "MAC Address", Type = "string", IsCustomField = false },
            new() { Id = "deviceType", Name = "deviceType", DisplayName = "Device Type", Type = "string", IsCustomField = false },
            new() { Id = "operatingSystem", Name = "operatingSystem", DisplayName = "Operating System", Type = "string", IsCustomField = false },
            new() { Id = "status", Name = "status", DisplayName = "Status", Type = "string", IsCustomField = false },
            new() { Id = "lastSeen", Name = "lastSeen", DisplayName = "Last Seen", Type = "datetime", IsCustomField = false }
        };

        try
        {
            var custom = await GetCustomFieldDefinitionsAsync();
            fields.AddRange(custom);
        }
        catch { }

        return fields;
    }

    private static string GetString(JToken token, params string[] names)
    {
        foreach (var n in names)
        {
            var v = token[n];
            if (v != null && v.Type != JTokenType.Null)
            {
                var s = v.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        return string.Empty;
    }

    private static string GetStringDeep(JToken token, params string[] paths)
    {
        foreach (var p in paths)
        {
            // Try top-level property first
            var direct = token[p];
            if (direct != null && direct.Type != JTokenType.Null)
            {
                var s = direct.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            // Then JPath selection
            var jt = token.SelectToken(p);
            if (jt != null && jt.Type != JTokenType.Null)
            {
                var s = jt.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        return string.Empty;
    }

    private static DateTime? GetDate(JToken token, params string[] names)
    {
        foreach (var n in names)
        {
            var v = token[n];
            if (v != null && v.Type != JTokenType.Null)
            {
                if (DateTime.TryParse(v.ToString(), out var dt)) return dt;
            }
        }
        return null;
    }

    private static string FirstNonEmpty(IEnumerable<string?> values)
        => values.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;

    private static JArray? ExtractArray(JToken root)
    {
        if (root is JArray arr) return arr;
        if (root is JObject obj)
        {
            if (obj["data"] is JArray a1) return a1;
            if (obj["items"] is JArray a2) return a2;
            if (obj["results"] is JArray a3) return a3;
            if (obj["endpoints"] is JArray a4) return a4;
        }
        return null;
    }

    private async Task<Dictionary<string, string>> GetCompaniesMapAsync(HttpClient client)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await EnsureRateWindowAsync();
            var url = $"{_credentials!.BaseUrl.TrimEnd('/')}/api/platform/v1/company/companies";
            var resp = await client.GetAsync(url);
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
            {
                await SetRateLimitedAsync(resp);
                return map;
            }
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Companies GET failed {resp.StatusCode} at {url}: {err}");
                return map;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var root = JsonConvert.DeserializeObject<JToken>(content);
            if (root == null) return map;
            var array = ExtractArray(root) ?? (root as JArray);
            if (array == null && root is JObject ro && ro["companies"] is JArray alt) array = alt;
            if (array == null) return map;

            foreach (var c in array)
            {
                var id = c["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var friendly = c["friendlyName"]?.ToString();
                var name = c["name"]?.ToString();
                var label = !string.IsNullOrWhiteSpace(friendly) ? friendly : (name ?? id);
                map[id] = label;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Companies map exception: {ex.Message}");
        }
        return map;
    }

    private async Task<Dictionary<string, string>> GetSitesMapAsync(HttpClient client)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await EnsureRateWindowAsync();
            var url = $"{_credentials!.BaseUrl.TrimEnd('/')}/api/platform/v1/company/sites";
            var resp = await client.GetAsync(url);
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
            {
                await SetRateLimitedAsync(resp);
                return map;
            }
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Sites GET failed {resp.StatusCode} at {url}: {err}");
                return map;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var root = JsonConvert.DeserializeObject<JToken>(content);
            if (root == null) return map;
            var array = ExtractArray(root) ?? (root as JArray);
            if (array == null && root is JObject ro && ro["sites"] is JArray alt) array = alt;
            if (array == null) return map;

            foreach (var s in array)
            {
                var id = s["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var friendly = s["friendlyName"]?.ToString();
                var name = s["name"]?.ToString();
                var label = !string.IsNullOrWhiteSpace(friendly) ? friendly : (name ?? id);
                map[id] = label;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sites map exception: {ex.Message}");
        }
        return map;
    }

    private async Task<string?> GetEndpointFriendlyNameAsync(HttpClient client, string endpointId)
    {
        if (_endpointNameCache.TryGetValue(endpointId, out var cached))
        {
            return cached;
        }

        try
        {
            await EnsureRateWindowAsync();
            var url = $"{_credentials!.BaseUrl.TrimEnd('/')}/api/platform/v1/device/endpoints/{endpointId}";
            var resp = await client.GetAsync(url);
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
            {
                await SetRateLimitedAsync(resp);
                var body429 = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Endpoint detail GET throttled at {url}: {body429}");
                return null;
            }
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Endpoint detail GET failed {resp.StatusCode} at {url}: {err}");
                return null;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var o = JsonConvert.DeserializeObject<JObject>(content);
            var friendly = o?["friendlyName"]?.ToString();
            if (string.IsNullOrWhiteSpace(friendly))
            {
                friendly = o?.SelectToken("system.systemName")?.ToString();
            }

            if (!string.IsNullOrWhiteSpace(friendly))
            {
                _endpointNameCache[endpointId] = friendly!;
            }

            return friendly;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Endpoint detail exception for {endpointId}: {ex.Message}");
            return null;
        }
    }

    private static List<Device> ConvertSnapshots(IEnumerable<DeviceSnapshot> snaps)
    {
        var devices = new List<Device>();
        foreach (var s in snaps)
        {
            if (string.IsNullOrWhiteSpace(s.EndpointId)) continue;
            var macs = (s.Network ?? new List<NetworkAdapterSnapshot>())
                .Select(n => new string((n.MacAddress ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();
            devices.Add(new Device
            {
                Id = s.EndpointId,
                ComputerName = s.ComputerName ?? string.Empty,
                CompanyName = s.CompanyName ?? s.CompanyId ?? string.Empty,
                SiteName = s.SiteName ?? s.SiteId ?? string.Empty,
                OperatingSystem = s.OperatingSystem ?? string.Empty,
                MacAddress = macs.FirstOrDefault() ?? string.Empty,
                AllMacAddresses = macs,
                LastSeen = s.CapturedAtUtc
            });
        }
        return devices;
    }

    private static List<Device> LoadSnapshotDevices()
    {
        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            db.EnsureSchemaUpgrades();
            var latest = db.DeviceSnapshots
                .Include(x => x.Network)
                .OrderByDescending(x => x.CapturedAtUtc)
                .ToList();
            // Keep only newest per EndpointId
            var byId = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in latest)
            {
                if (string.IsNullOrWhiteSpace(s.EndpointId)) continue;
                if (!byId.ContainsKey(s.EndpointId)) byId[s.EndpointId] = s;
            }
            return ConvertSnapshots(byId.Values);
        }
        catch { return new List<Device>(); }
    }

    public async Task<List<Device>> GetDevicesAsync(List<string>? selectedFields = null)
    {
        if (_credentials?.AccessToken == null) return LoadSnapshotDevices();
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var baseUrl = _credentials.BaseUrl.TrimEnd('/');
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(_credentials.DevicesEndpointPath))
            {
                if (_credentials.DevicesEndpointPath!.StartsWith("http", StringComparison.OrdinalIgnoreCase)) candidates.Add(_credentials.DevicesEndpointPath!);
                else candidates.Add($"{baseUrl}/{_credentials.DevicesEndpointPath!.TrimStart('/')}");
            }
            candidates.Add($"{baseUrl}/api/platform/v1/device/endpoints");

            foreach (var url in candidates)
            {
                try
                {
                    await EnsureRateWindowAsync();
                    var response = await client.GetAsync(url);
                    ParseRateHeaders(response, "devices");
                    if (_lastRateLimitLimit > 0 && _lastRateLimitRemaining == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("ASIO devices early bail: limit>0 and remaining=0 (exhausted)");
                        return LoadSnapshotDevices();
                    }
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503)
                    {
                        await SetRateLimitedAsync(response);
                        return LoadSnapshotDevices();
                    }
                    if (!response.IsSuccessStatusCode) continue;

                    var content = await response.Content.ReadAsStringAsync();
                    var root = JsonConvert.DeserializeObject<JToken>(content);
                    if (root == null) continue;
                    var array = ExtractArray(root);
                    if (array == null || array.Count == 0) continue;

                    var devices = new List<Device>();
                    foreach (var deviceJson in array)
                    {
                        var computerName = FirstNonEmpty(new[] { GetString(deviceJson, "computerName", "name", "hostname", "deviceName", "friendlyName", "endpointName"), GetStringDeep(deviceJson, "system.systemName", "systemName") });
                        var mac = FirstNonEmpty(new[] { GetString(deviceJson, "macAddress", "mac", "primaryMacAddress"), GetStringDeep(deviceJson, "network.primaryMacAddress") });
                        if (string.IsNullOrWhiteSpace(mac))
                        {
                            mac = deviceJson.SelectTokens("networks[*].macAddress").Concat(deviceJson.SelectTokens("interfaces[*].macAddress")).Select(t => t?.ToString()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
                        }
                        devices.Add(new Device
                        {
                            Id = FirstNonEmpty(new[] { GetString(deviceJson, "id", "endpointId", "deviceId", "device_id") }),
                            ComputerName = computerName,
                            CompanyName = GetString(deviceJson, "companyName", "company"),
                            SiteName = GetString(deviceJson, "siteName", "site"),
                            MacAddress = mac,
                            DeviceType = GetString(deviceJson, "deviceType", "type"),
                            OperatingSystem = GetString(deviceJson, "operatingSystem", "os"),
                            Status = GetString(deviceJson, "status", "state"),
                            LastSeen = GetDate(deviceJson, "lastSeen", "lastSeenAt")
                        });
                    }

                    // Reduce detail lookups due to rate limit pressure
                    int performed = 0; const int maxDetailLookups = 5;
                    foreach (var d in devices)
                    {
                        if (_lastRateLimitRemaining >= 0 && _lastRateLimitRemaining <= (_lastRateLimitLimit / 10)) break; // preserve tail quota
                        if (performed >= maxDetailLookups) break;
                        if (!string.IsNullOrWhiteSpace(d.ComputerName) && !string.IsNullOrWhiteSpace(d.MacAddress)) continue;
                        if (string.IsNullOrWhiteSpace(d.Id)) continue;
                        if (!HasQuota()) break;
                        var friendly = await GetEndpointFriendlyNameAsync(client, d.Id);
                        if (!string.IsNullOrWhiteSpace(friendly)) { d.ComputerName = friendly; performed++; }
                    }
                    return devices;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Devices GET exception for {url}: {ex.Message}");
                }
            }
            return LoadSnapshotDevices();
        }
        catch { return LoadSnapshotDevices(); }
    }

    // Company-pair paging fallback: use global list and perform optional detail enrichment to satisfy interface
    public async Task<List<Device>> GetDevicesByCompanyPairsAsync(int companyPageLimit = 200, int endpointPageLimit = 500, bool enrichDetails = true, int maxDetailPerCompany = 0, CancellationToken ct = default)
    {
        // Use global list first
        var list = await GetDevicesAsync();
        if (list.Count == 0)
        {
            // Already fell back inside GetDevicesAsync, but double-check
            list = LoadSnapshotDevices();
            return list;
        }
        if (!enrichDetails) return list;

        // Skip enrichment when currently rate-limited or bucket shows 0 remaining
        try
        {
            var rl = await GetRateLimitAsync();
            var bucket = rl.TryGet("asset.partner-asset endpoints-details") ?? rl.TryGet("service.rate-limit");
            if (IsRateLimitedNow() || (bucket != null && bucket.Limit > 0 && bucket.Remaining == 0))
            {
                return list;
            }
        }
        catch { }

        if (maxDetailPerCompany == 0) maxDetailPerCompany = 40;

        int enriched = 0;
        foreach (var d in list)
        {
            ct.ThrowIfCancellationRequested();
            if (enriched >= maxDetailPerCompany) break;
            if (IsRateLimitedNow()) break; // stop on rate limit

            var needName = string.IsNullOrWhiteSpace(d.ComputerName);
            var needCompany = string.IsNullOrWhiteSpace(d.CompanyName) || Guid.TryParse(d.CompanyName, out _);
            var needSite = string.IsNullOrWhiteSpace(d.SiteName) || Guid.TryParse(d.SiteName, out _);
            var needMac = string.IsNullOrWhiteSpace(d.MacAddress) && (d.AllMacAddresses == null || d.AllMacAddresses.Count == 0);
            if (!(needName || needCompany || needSite || needMac)) continue;
            if (string.IsNullOrWhiteSpace(d.Id)) continue;
            var detail = await GetEndpointDetailAsync(d.Id);
            if (detail == null)
            {
                if (IsRateLimitedNow()) break; // abort on rate-limit signal
                continue;
            }
            if (!string.IsNullOrWhiteSpace(detail.ComputerName)) d.ComputerName = detail.ComputerName;
            if (!string.IsNullOrWhiteSpace(detail.OperatingSystem)) d.OperatingSystem = detail.OperatingSystem;
            if (!string.IsNullOrWhiteSpace(detail.CompanyName)) d.CompanyName = detail.CompanyName!;
            if (!string.IsNullOrWhiteSpace(detail.SiteName)) d.SiteName = detail.SiteName!;
            var macs = (detail.Network ?? new List<NetworkAdapter>())
                .Select(n => new string((n.MacAddress ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
            if (macs.Count > 0)
            {
                d.AllMacAddresses = macs;
                if (string.IsNullOrWhiteSpace(d.MacAddress)) d.MacAddress = macs.First();
            }
            enriched++;
            await Task.Delay(100, ct);
        }
        return list;
    }

    public async Task<List<FieldDefinition>> GetCustomFieldDefinitionsAsync()
    {
        if (_credentials?.AccessToken == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await EnsureRateWindowAsync();
            var response = await client.GetAsync($"{_credentials.BaseUrl.TrimEnd('/')}/api/platform/v1/custom-field/definitions");
            if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503)
            {
                await SetRateLimitedAsync(response);
                return new List<FieldDefinition>();
            }
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var fieldsJson = JsonConvert.DeserializeObject<JToken>(content);
                var fields = new List<FieldDefinition>();

                var array = ExtractArray(fieldsJson!);
                if (array != null)
                {
                    foreach (var fieldJson in array)
                    {
                        var field = new FieldDefinition
                        {
                            Id = fieldJson["id"]?.ToString() ?? "",
                            Name = fieldJson["name"]?.ToString() ?? "",
                            DisplayName = fieldJson["name"]?.ToString() ?? "",
                            Type = fieldJson["type"]?.ToString() ?? "",
                            IsCustomField = true
                        };
                        fields.Add(field);
                    }
                }

                return fields;
            }

            return new List<FieldDefinition>();
        }
        catch
        {
            return new List<FieldDefinition>();
        }
    }

    public async Task<Dictionary<string, string>> GetCustomFieldValuesAsync(string deviceId)
    {
        if (_credentials?.AccessToken == null)
        {
            throw new InvalidOperationException("Not authenticated");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await EnsureRateWindowAsync();
            var url = $"{_credentials.BaseUrl.TrimEnd('/')}/api/platform/v1/device/endpoints/{deviceId}/custom-fields";
            var response = await client.GetAsync(url);
            if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503)
            {
                await SetRateLimitedAsync(response);
                return new Dictionary<string, string>();
            }
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var valuesJson = JsonConvert.DeserializeObject<JToken>(content);
                var values = new Dictionary<string, string>();

                var array = ExtractArray(valuesJson!);
                if (array != null)
                {
                    foreach (var valueJson in array)
                    {
                        var fieldId = valueJson["attributeID"]?.ToString();
                        var value = valueJson["value"]?.ToString();
                        if (!string.IsNullOrEmpty(fieldId))
                        {
                            values[fieldId] = value ?? string.Empty;
                        }
                    }
                }

                return values;
            }

            return new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public async Task<EndpointDetail?> GetEndpointDetailAsync(string endpointId)
    {
        if (_credentials?.AccessToken == null) throw new InvalidOperationException("Not authenticated");
        if (!HasQuota()) return null; // early bail when no quota
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await EnsureRateWindowAsync();
            var url = $"{_credentials.BaseUrl.TrimEnd('/')}/api/platform/v1/device/endpoints/{endpointId}";
            var resp = await client.GetAsync(url);
            ParseRateHeaders(resp, "detail");
            if (_lastRateLimitLimit > 0 && _lastRateLimitRemaining == 0)
            {
                System.Diagnostics.Debug.WriteLine("ASIO detail bail: limit>0 and remaining=0 (exhausted)");
                return null;
            }

            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
            {
                await SetRateLimitedAsync(resp);
                var err429 = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Endpoint detail throttled {endpointId}: {err429.Substring(0, Math.Min(err429.Length,120))}");
                return null;
            }
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Endpoint detail failed {resp.StatusCode} {endpointId}: {err.Substring(0, Math.Min(err.Length,120))}");
                return null;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var o = JsonConvert.DeserializeObject<JObject>(content) ?? new JObject();
            var detail = new EndpointDetail
            {
                EndpointId = endpointId,
                ComputerName = o.Value<string>("friendlyName") ?? o.SelectToken("system.systemName")?.ToString() ?? string.Empty,
                Domain = o.SelectToken("system.domain")?.ToString(),
                Username = o.SelectToken("users[0].username")?.ToString(),
                OperatingSystem = o.SelectToken("os.product")?.ToString() ?? o.SelectToken("os.version")?.ToString() ?? string.Empty,
                WindowsDirectory = o.SelectToken("os.windowsDirectory")?.ToString(),
                Manufacturer = o.SelectToken("system.manufacturer")?.ToString() ?? o.SelectToken("baseBoard.manufacturer")?.ToString(),
                Model = o.SelectToken("system.model")?.ToString() ?? o.SelectToken("baseBoard.model")?.ToString(),
                Processor = o.SelectToken("processors[0].product")?.ToString(),
                CompanyId = o.SelectToken("company.id")?.ToString() ?? o.SelectToken("tenant.id")?.ToString(),
                CompanyName = o.SelectToken("company.friendlyName")?.ToString() ?? o.SelectToken("company.name")?.ToString(),
                SiteId = o.SelectToken("site.id")?.ToString() ?? o.SelectToken("location.id")?.ToString(),
                SiteName = o.SelectToken("site.friendlyName")?.ToString() ?? o.SelectToken("site.name")?.ToString()
            };

            var lastBoot = o.SelectToken("systemState.startupStatus.lastBootUpTimeUTC")?.ToObject<DateTime?>();
            if (lastBoot != null)
            {
                var span = DateTime.UtcNow - lastBoot.Value;
                detail.Uptime = $"{(int)span.TotalDays} days, {span.Hours} hours, {span.Minutes} minutes";
            }
            var totalMemBytes = o.SelectToken("memory.physicalTotalBytes")?.ToObject<long?>();
            if (totalMemBytes != null) detail.PhysicalMemoryGb = Math.Round(totalMemBytes.Value / 1024.0 / 1024.0 / 1024.0, 1);
            var virtMemBytes = o.SelectToken("memory.virtualAvailableBytes")?.ToObject<long?>();
            if (virtMemBytes != null) detail.VirtualMemoryGb = Math.Round(virtMemBytes.Value / 1024.0 / 1024.0 / 1024.0, 1);

            if (o.SelectToken("networks") is JArray nets)
            {
                foreach (var n in nets)
                {
                    var na = new NetworkAdapter
                    {
                        Type = n["type"]?.ToString() ?? (n["ipEnabled"]?.ToObject<bool?>() == true ? "adapter" : null),
                        Description = n["product"]?.ToString() ?? n["description"]?.ToString(),
                        MacAddress = n["macAddress"]?.ToString(),
                        Ip = n["ipv4"]?.ToString() ?? (n["ipv4List"] as JArray)?.FirstOrDefault()?.ToString(),
                        Netmask = n["subnetMask"]?.ToString() ?? (n["subnetMasks"] as JArray)?.FirstOrDefault()?.ToString(),
                        Gateway = (n["defaultIPGateways"] as JArray)?.FirstOrDefault()?.ToString() ?? n["defaultIPGateway"]?.ToString(),
                        DhcpServer = n["dhcpServer"]?.ToString()
                    };
                    if (n["dnsServers"] is JArray dnsArr)
                    {
                        foreach (var d in dnsArr) if (d != null) na.DnsServers.Add(d.ToString());
                    }
                    detail.Network.Add(na);
                }
            }
            try { await SaveEndpointDetailSnapshotAsync(detail); } catch { }
            return detail;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Endpoint detail exception: {ex.Message}");
            return null;
        }
    }

    // Save detail snapshot (offline fallback support)
    private async Task SaveEndpointDetailSnapshotAsync(EndpointDetail detail)
    {
        try
        {
            using var db = new ConnectWiseManager.Data.AppDbContext();
            db.Database.EnsureCreated();
            db.EnsureSchemaUpgrades();
            var existing = db.DeviceSnapshots.Include(x => x.Network).FirstOrDefault(x => x.EndpointId == detail.EndpointId)
                           ?? new DeviceSnapshot { EndpointId = detail.EndpointId };
            if (existing.Id == 0) db.DeviceSnapshots.Add(existing);
            existing.CapturedAtUtc = DateTime.UtcNow;
            existing.ComputerName = detail.ComputerName ?? string.Empty;
            existing.Domain = detail.Domain;
            existing.Username = detail.Username;
            existing.OperatingSystem = detail.OperatingSystem ?? string.Empty;
            existing.WindowsDirectory = detail.WindowsDirectory;
            existing.CompanyId = detail.CompanyId;
            existing.CompanyName = detail.CompanyName;
            existing.SiteId = detail.SiteId;
            existing.SiteName = detail.SiteName;
            existing.Manufacturer = detail.Manufacturer;
            existing.Model = detail.Model;
            existing.Uptime = detail.Uptime;
            existing.Processor = detail.Processor;
            existing.PhysicalMemoryGb = detail.PhysicalMemoryGb;
            existing.VirtualMemoryGb = detail.VirtualMemoryGb;
            existing.Network ??= new List<NetworkAdapterSnapshot>();
            existing.Network.Clear();
            foreach (var n in detail.Network)
            {
                existing.Network.Add(new NetworkAdapterSnapshot
                {
                    Description = n.Description,
                    MacAddress = n.MacAddress,
                    Ip = n.Ip,
                    Netmask = n.Netmask,
                    Gateway = n.Gateway,
                    DnsServers = n.DnsServers != null && n.DnsServers.Count > 0 ? string.Join(',', n.DnsServers.Where(x => !string.IsNullOrWhiteSpace(x))) : null,
                    DhcpServer = n.DhcpServer
                });
            }
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot save failed for {detail.EndpointId}: {ex.Message}");
        }
    }

    public async Task<RateLimitInfo> GetRateLimitAsync()
    {
        var info = new RateLimitInfo();
        try
        {
            if (_credentials?.AccessToken == null) return info;
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var url = $"{_credentials.BaseUrl.TrimEnd('/')}/api/platform/v1/device/endpoints?limit=1";
            await EnsureRateWindowAsync();
            var resp = await client.GetAsync(url);
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
            {
                await SetRateLimitedAsync(resp);
                System.Diagnostics.Debug.WriteLine($"Rate limit probe got {(int)resp.StatusCode}; marked as rate-limited.");
            }
            var bucket = new RateLimitBucket();
            if (resp.Headers.TryGetValues("X-RateLimit-Limit", out var l) && int.TryParse(l.FirstOrDefault(), out var limit)) bucket.Limit = limit; else bucket.Limit = -1; // -1 unknown
            if (resp.Headers.TryGetValues("X-RateLimit-Remaining", out var r) && int.TryParse(r.FirstOrDefault(), out var rem)) bucket.Remaining = rem; else bucket.Remaining = -1; // -1 unknown
            if (resp.Headers.TryGetValues("X-RateLimit-Reset", out var rs) && long.TryParse(rs.FirstOrDefault(), out var epoch))
            {
                try { bucket.Reset = DateTimeOffset.FromUnixTimeSeconds(epoch); } catch { }
            }
            info.Buckets["service.rate-limit"] = bucket;
            System.Diagnostics.Debug.WriteLine($"RateLimit probe: limit={bucket.Limit} remaining={bucket.Remaining} reset={bucket.Reset?.ToUnixTimeSeconds() ?? 0}");
            // update internal trackers when headers present
            if (bucket.Limit >= 0) _lastRateLimitLimit = bucket.Limit;
            if (bucket.Remaining >= 0) _lastRateLimitRemaining = bucket.Remaining;
            if (bucket.Reset != null) _lastRateLimitReset = bucket.Reset;
            return info;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RateLimit probe exception: {ex.Message}");
            return info;
        }
    }

    public async Task<int> BatchEnrichFromMappingAsync(IEnumerable<Device> devices, int chunkSize = 50)
    {
        // Not used in current UI; keep as no-op
        await Task.CompletedTask;
        return 0;
    }

    public async Task<int> BatchLookupCompaniesAsync(IEnumerable<Device> devices, int chunkSize = 50)
    {
        if (_credentials?.AccessToken == null) return 0;
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in devices)
        {
            if (!string.IsNullOrWhiteSpace(d.CompanyName) && Guid.TryParse(d.CompanyName, out _)) idSet.Add(d.CompanyName);
        }
        if (idSet.Count == 0) return 0;

        var map = await GetCompaniesMapAsync(client);
        int changed = 0;
        foreach (var d in devices)
        {
            if (!string.IsNullOrWhiteSpace(d.CompanyName) && map.TryGetValue(d.CompanyName, out var label))
            {
                d.CompanyName = label; changed++;
            }
        }
        return changed;
    }

    public async Task<int> BatchLookupSitesAsync(IEnumerable<Device> devices, int chunkSize = 50)
    {
        if (_credentials?.AccessToken == null) return 0;
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in devices)
        {
            if (!string.IsNullOrWhiteSpace(d.SiteName) && Guid.TryParse(d.SiteName, out _)) idSet.Add(d.SiteName);
        }
        if (idSet.Count == 0) return 0;

        var map = await GetSitesMapAsync(client);
        int changed = 0;
        foreach (var d in devices)
        {
            if (!string.IsNullOrWhiteSpace(d.SiteName) && map.TryGetValue(d.SiteName, out var label))
            {
                d.SiteName = label; changed++;
            }
        }
        return changed;
    }

    public bool SupportsMappingsLookup => true;

    public async Task<List<CompanyEndpointActiveNic>> GetCompanyActiveNicsAsync(string companyKey, bool keyIsId = true, int pageLimit = 500, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return new List<CompanyEndpointActiveNic>();
    }

    public async Task<(int inserted, int updated)> SaveCompanyActiveNicsAsync(IEnumerable<CompanyEndpointActiveNic> rows)
    {
        await Task.CompletedTask;
        return (0, 0);
    }

    public async Task<(int inserted, int updated)> SyncCompanyActiveNicsAsync(string companyKey, bool keyIsId = true, int pageLimit = 500, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return (0, 0);
    }
    public async Task<List<Device>> GetDevicesMinimalAsync(int pageLimit = 5000, CancellationToken ct = default)
    {
        var results = new List<Device>();
        if (_credentials?.AccessToken == null) return results;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var baseUrl = _credentials.BaseUrl.TrimEnd('/');

            // Start with a generous page size if supported
            string? nextUrl = $"{baseUrl}/api/platform/v1/device/endpoints?limit=500";
            int page = 1;
            while (!ct.IsCancellationRequested && !IsRateLimitedNow() && results.Count < pageLimit && !string.IsNullOrWhiteSpace(nextUrl))
            {
                await EnsureRateWindowAsync();
                // Append page param if not present
                var url = nextUrl;
                if (!url.Contains("page=", StringComparison.OrdinalIgnoreCase))
                {
                    url += (url.Contains("?") ? "&" : "?") + $"page={page}";
                }
                var resp = await client.GetAsync(url, ct);
                if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
                {
                    await SetRateLimitedAsync(resp);
                    break;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    System.Diagnostics.Debug.WriteLine($"Minimal devices GET failed {resp.StatusCode} at {url}: {err.Substring(0, Math.Min(err.Length,150))}");
                    break;
                }
                ParseRateHeaders(resp, "minimal");
                var content = await resp.Content.ReadAsStringAsync(ct);
                var root = JsonConvert.DeserializeObject<JToken>(content);
                var array = ExtractArray(root!);
                if (array == null || array.Count == 0) break;

                foreach (var deviceJson in array)
                {
                    if (results.Count >= pageLimit) break;
                    ct.ThrowIfCancellationRequested();
                    var id = FirstNonEmpty(new[] { GetString(deviceJson, "id", "endpointId", "deviceId", "device_id") });
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var company = GetString(deviceJson, "companyName", "company");
                    var site = GetString(deviceJson, "siteName", "site");
                    var friendly = FirstNonEmpty(new[] { GetString(deviceJson, "friendlyName", "computerName", "name"), GetStringDeep(deviceJson, "system.systemName") });
                    results.Add(new Device
                    {
                        Id = id,
                        CompanyName = company,
                        SiteName = site,
                        ComputerName = friendly
                    });
                }

                // Determine next URL via Link header or response body common patterns
                nextUrl = null;
                if (resp.Headers.TryGetValues("Link", out var linkVals))
                {
                    var link = linkVals.FirstOrDefault();
                    // parse <url>; rel="next"
                    if (!string.IsNullOrWhiteSpace(link))
                    {
                        var parts = link.Split(',');
                        foreach (var p in parts)
                        {
                            var seg = p.Trim();
                            if (seg.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                            {
                                var start = seg.IndexOf('<'); var end = seg.IndexOf('>');
                                if (start >= 0 && end > start) nextUrl = seg.Substring(start + 1, end - start - 1);
                            }
                        }
                    }
                }
                if (nextUrl == null && root is JObject obj)
                {
                    nextUrl = obj.SelectToken("links.next")?.ToString() ?? obj["nextLink"]?.ToString();
                }
                page++;
            }
            System.Diagnostics.Debug.WriteLine($"Minimal devices aggregated count={results.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDevicesMinimalAsync exception: {ex.Message}");
        }
        return results;
    }

    private void ParseRateHeaders(HttpResponseMessage resp, string context)
    {
        try
        {
            int limit = -1, remaining = -1; DateTimeOffset? reset = null;
            string[] limitNames = { "X-RateLimit-Limit", "X-Rate-Limit-Limit", "Ratelimit-Limit" };
            string[] remainingNames = { "X-RateLimit-Remaining", "X-Rate-Limit-Remaining", "Ratelimit-Remaining" };
            string[] resetNames = { "X-RateLimit-Reset", "X-Rate-Limit-Reset", "Ratelimit-Reset" };
            foreach (var name in limitNames)
            {
                if (resp.Headers.TryGetValues(name, out var vals) && int.TryParse(vals.FirstOrDefault(), out var v)) { limit = v; break; }
            }
            foreach (var name in remainingNames)
            {
                if (resp.Headers.TryGetValues(name, out var vals) && int.TryParse(vals.FirstOrDefault(), out var v)) { remaining = v; break; }
            }
            foreach (var name in resetNames)
            {
                if (resp.Headers.TryGetValues(name, out var vals) && long.TryParse(vals.FirstOrDefault(), out var epoch)) { reset = DateTimeOffset.FromUnixTimeSeconds(epoch); break; }
            }
            if (limit >= 0) _lastRateLimitLimit = limit; else if (_lastRateLimitLimit < 0) _lastRateLimitLimit = limit;
            if (remaining >= 0) _lastRateLimitRemaining = remaining; else if (_lastRateLimitRemaining < 0) _lastRateLimitRemaining = remaining;
            if (reset != null) _lastRateLimitReset = reset;
            var rateHeaders = resp.Headers.Where(h => h.Key.StartsWith("X-Rate", StringComparison.OrdinalIgnoreCase) || h.Key.StartsWith("Rate", StringComparison.OrdinalIgnoreCase) || h.Key.StartsWith("Ratelimit", StringComparison.OrdinalIgnoreCase))
                                          .Select(h => h.Key + "=" + string.Join(';', h.Value)).ToList();
            System.Diagnostics.Debug.WriteLine($"Rate headers ({context}): limit={_lastRateLimitLimit} remaining={_lastRateLimitRemaining} reset={_lastRateLimitReset?.ToUnixTimeSeconds() ?? 0} raw=[{string.Join(" | ", rateHeaders)}]");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseRateHeaders exception: {ex.Message}");
        }
    }
}
