namespace ConnectWiseManager.Models;

public class RateLimitBucket
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTimeOffset? Reset { get; set; }
}

public class RateLimitInfo
{
    // Key format: "group.key" (e.g., "asset.partner-asset-endpoints-details")
    public Dictionary<string, RateLimitBucket> Buckets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RateLimitBucket? TryGet(string groupKey)
        => Buckets.TryGetValue(groupKey, out var b) ? b : null;
}
