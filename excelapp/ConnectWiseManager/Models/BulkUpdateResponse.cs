using Newtonsoft.Json;

namespace ConnectWiseManager.Models;

public class BulkUpdateResponse
{
    [JsonProperty("SessionResults")] public List<BulkSessionResult> SessionResults { get; set; } = new();
    [JsonProperty("OverallStatus")] public string? OverallStatus { get; set; }
}

public class BulkSessionResult
{
    [JsonProperty("SessionId")] public string? SessionId { get; set; }
    [JsonProperty("Status")] public string? Status { get; set; }

    // SessionData keys/values are returned as strings in sample; keep flexible
    [JsonProperty("SessionData")] public Dictionary<string, string>? SessionData { get; set; }
}
