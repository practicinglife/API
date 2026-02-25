using CwAssetManager.Core.Enums;

namespace CwAssetManager.Core.Models;

/// <summary>Per-provider configuration including rate limit and auth settings.</summary>
public sealed class ProviderConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ProviderType Provider { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public int RequestsPerMinute { get; set; } = 60;
    public int BurstCapacity { get; set; } = 10;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
