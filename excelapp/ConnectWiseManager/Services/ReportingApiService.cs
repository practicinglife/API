using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ConnectWiseManager.Models;
using System.Diagnostics;
using System.Linq;

namespace ConnectWiseManager.Services
{
    public class ReportingApiService : IReportingApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public string? ApiKey { get; private set; }
        public string BaseUrl { get; private set; } = "https://itsapi.itsupport247.net";

        public ReportingApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            // Load from environment by default (security: do not hardcode secrets)
            ApiKey = Environment.GetEnvironmentVariable("REPORTING_API_KEY");
        }

        public void Configure(string? apiKey, string? baseUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(apiKey)) ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl)) BaseUrl = baseUrl!.TrimEnd('/');
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Reporting API uses Basic with blank username, API key as password
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ApiKey}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
            return client;
        }

        public async Task<List<ReportingCompany>> GetCompaniesAsync(CancellationToken ct = default)
        {
            var companies = new List<ReportingCompany>();
            if (string.IsNullOrWhiteSpace(ApiKey)) return companies;

            var client = CreateClient();
            var resp = await TryGetWithFallbackAsync(client, $"{BaseUrl}/reports/companies", new { }, ct);
            if (resp?.IsSuccessStatusCode != true) return companies;

            var payload = await resp.Content.ReadAsStringAsync(ct);
            var arr = ExtractArray(JsonConvert.DeserializeObject<JToken>(payload), "Companies", "CompanyList", "Results", "Data");
            if (arr == null)
            {
                Debug.WriteLine($"Reporting companies payload did not contain expected arrays. Body snippet: {TrimBody(payload)}");
                return companies;
            }

            foreach (var token in arr)
            {
                var company = new ReportingCompany
                {
                    Id = ReadString(token, "CompanyId", "Id", "ClientId"),
                    Name = ReadString(token, "CompanyName", "Name", "ClientName"),
                    Code = ReadString(token, "CompanyCode", "Code")
                };
                if (!string.IsNullOrWhiteSpace(company.Id) || !string.IsNullOrWhiteSpace(company.Name))
                {
                    companies.Add(company);
                }
            }

            return companies
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<ReportingSite>> GetSitesAsync(string? companyId = null, CancellationToken ct = default)
        {
            var sites = new List<ReportingSite>();
            if (string.IsNullOrWhiteSpace(ApiKey)) return sites;

            var client = CreateClient();
            var query = string.IsNullOrWhiteSpace(companyId) ? string.Empty : $"?companyId={Uri.EscapeDataString(companyId)}";
            object payload = string.IsNullOrWhiteSpace(companyId)
                ? (object)new { IncludeDisabledSites = false }
                : new { CompanyId = companyId };

            var resp = await TryGetWithFallbackAsync(client, $"{BaseUrl}/reports/sites{query}", payload, ct);
            if (resp?.IsSuccessStatusCode != true) return sites;

            var body = await resp.Content.ReadAsStringAsync(ct);
            var arr = ExtractArray(JsonConvert.DeserializeObject<JToken>(body), "Sites", "SiteList", "Results", "Data");
            if (arr == null)
            {
                Debug.WriteLine($"Reporting sites payload did not contain expected arrays. Body snippet: {TrimBody(body)}");
                return sites;
            }

            foreach (var token in arr)
            {
                var site = new ReportingSite
                {
                    SiteId = ReadString(token, "SiteId", "Id"),
                    SiteCode = ReadString(token, "SiteCode", "Code"),
                    SiteName = ReadString(token, "SiteName", "Name"),
                    CompanyId = ReadString(token, "CompanyId", "ClientId"),
                    CompanyName = ReadString(token, "CompanyName", "ClientName")
                };
                if (!string.IsNullOrWhiteSpace(site.SiteId) || !string.IsNullOrWhiteSpace(site.SiteCode))
                {
                    sites.Add(site);
                }
            }

            return sites
                .OrderBy(s => s.SiteCode ?? s.SiteName ?? s.SiteId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<Device>> GetAgentDetailsAsync(string siteCode, string? siteId = null, CancellationToken ct = default)
        {
            var list = new List<Device>();
            if (string.IsNullOrWhiteSpace(ApiKey) || (string.IsNullOrWhiteSpace(siteCode) && string.IsNullOrWhiteSpace(siteId))) return list;
            try
            {
                var client = CreateClient();
                var url = $"{BaseUrl}/reports/agent/details";
                var payload = new Dictionary<string, string?>
                {
                    ["SiteCode"] = string.IsNullOrWhiteSpace(siteCode) ? null : siteCode,
                    ["SiteId"] = string.IsNullOrWhiteSpace(siteId) ? null : siteId
                };
                var json = JsonConvert.SerializeObject(payload.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value));
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync(url, content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    Debug.WriteLine($"Reporting AgentDetails POST failed {resp.StatusCode}: {err}");
                    return list;
                }
                var respContent = await resp.Content.ReadAsStringAsync(ct);
                var root = JsonConvert.DeserializeObject<JToken>(respContent);
                var details = ExtractArray(root, "AgentDetails", "Agents", "Results", "Data");
                if (details == null)
                {
                    Debug.WriteLine($"Reporting AgentDetails returned empty payload for siteCode={siteCode} siteId={siteId}. Body snippet: {TrimBody(respContent)}");
                    return list;
                }
                foreach (var d in details)
                {
                    var dev = new Device
                    {
                        Id = ReadString(d, "MachineId", "Id"),
                        ComputerName = ReadString(d, "FriendlyName", "ResourceName", "ComputerName"),
                        CompanyName = ReadString(d, "Company", "CompanyName", "ClientName"),
                        SiteName = ReadString(d, "Site", "SiteName", "SiteCode"),
                        OperatingSystem = ReadString(d, "OS", "OperatingSystem"),
                        Status = ReadString(d, "IsCurrent", "Status"),
                        LastSeen = DateTime.TryParse(ReadString(d, "LastContact", "LastSeen"), out var ls) ? ls : (DateTime?)null,
                        MacAddress = ReadString(d, "MacAddress", "PrimaryMac")
                    };
                    list.Add(dev);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reporting GetAgentDetails exception: {ex.Message}");
            }
            return list;
        }

        private static JArray? ExtractArray(JToken? token, params string[] propertyNames)
        {
            if (token == null) return null;
            if (token is JArray arr) return arr;
            if (token is JObject obj)
            {
                foreach (var name in propertyNames)
                {
                    var prop = obj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (prop != null)
                    {
                        if (prop.Value is JArray direct) return direct;
                        var nestedMatch = ExtractArray(prop.Value, propertyNames);
                        if (nestedMatch != null) return nestedMatch;
                    }
                }
                foreach (var prop in obj.Properties())
                {
                    var fallback = ExtractArray(prop.Value, propertyNames);
                    if (fallback != null) return fallback;
                }
            }
            return null;
        }

        private static string ReadString(JToken? token, params string[] propertyNames)
        {
            if (token == null) return string.Empty;

            if (token is JValue value && value.Type != JTokenType.Object && value.Type != JTokenType.Array)
            {
                var str = value.ToString();
                return string.IsNullOrWhiteSpace(str) ? string.Empty : str.Trim();
            }

            if (token is JObject obj)
            {
                foreach (var name in propertyNames)
                {
                    var match = obj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (match != null)
                    {
                        var candidate = ReadString(match, name);
                        if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
                    }
                }
                foreach (var property in obj.Properties())
                {
                    var nested = ReadString(property.Value, propertyNames);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    var nested = ReadString(item, propertyNames);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }

            return string.Empty;
        }

        private static string TrimBody(string? body, int max = 400)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            body = body.Replace('\n', ' ').Replace("\r", string.Empty);
            return body.Length <= max ? body : body[..max] + "...";
        }

        private async Task<HttpResponseMessage?> TryGetWithFallbackAsync(HttpClient client, string url, object? fallbackPayload, CancellationToken ct)
        {
            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode || fallbackPayload == null)
                {
                    return response;
                }

                if (response.StatusCode != HttpStatusCode.MethodNotAllowed &&
                    response.StatusCode != HttpStatusCode.BadRequest &&
                    response.StatusCode != HttpStatusCode.NotFound)
                {
                    return response;
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Reporting GET {url} failed: {ex.Message}");
                if (fallbackPayload == null) return null;
            }

            if (fallbackPayload == null) return null;

            var payloadJson = JsonConvert.SerializeObject(fallbackPayload);
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            try
            {
                return await client.PostAsync(url, content, ct);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Reporting POST {url} failed: {ex.Message}");
                return null;
            }
        }
    }
}