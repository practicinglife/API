namespace ConnectWiseManager.Models;

public enum StaticSource
{
    Asio = 1,
    ScreenConnect = 2
}

public class StaticDeviceRecord
{
    public int Id { get; set; }

    // Normalized key parts (columns a,b,c,d from CSV)
    public string A { get; set; } = string.Empty;
    public string B { get; set; } = string.Empty;
    public string C { get; set; } = string.Empty;
    public string D { get; set; } = string.Empty;

    // Composite match key (A|B|C|D normalized)
    public string Key { get; set; } = string.Empty;

    // Optional field E from ScreenConnect when keys match
    public string? E { get; set; }

    // Source presence flags
    public bool HasAsio { get; set; }
    public bool HasScreenConnect { get; set; }

    // Optional identifiers
    public string? EndpointId { get; set; }
    public string? ScreenConnectSessionId { get; set; }

    // Optional friendly names (for UI)
    public string? Company { get; set; }
    public string? Site { get; set; }
    public string? FriendlyName { get; set; }

    // ASIO specific - optional IP address for matching and diagnostics
    public string? IpAddress { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    public static string BuildKey(string a, string b, string c, string d)
    {
        static string N(string s) => string.IsNullOrWhiteSpace(s) ? string.Empty : new string(s.Trim().ToUpperInvariant().ToCharArray());
        return $"{N(a)}|{N(b)}|{N(c)}|{N(d)}";
    }
}
