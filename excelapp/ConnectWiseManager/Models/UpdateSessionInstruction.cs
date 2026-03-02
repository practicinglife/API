using Newtonsoft.Json;

namespace ConnectWiseManager.Models;

// Represents a single session update request for ScreenConnect bulk API
public class UpdateSessionInstruction
{
    [JsonProperty("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    // Optional fields supported by the endpoint (serialized only when not null)
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("property1", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property1 { get; set; }

    [JsonProperty("property2", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property2 { get; set; }

    [JsonProperty("property3", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property3 { get; set; }

    [JsonProperty("property4", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property4 { get; set; }

    [JsonProperty("property5", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property5 { get; set; }

    [JsonProperty("property6", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property6 { get; set; }

    [JsonProperty("property7", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property7 { get; set; }

    [JsonProperty("property8", NullValueHandling = NullValueHandling.Ignore)]
    public string? Property8 { get; set; }
}
