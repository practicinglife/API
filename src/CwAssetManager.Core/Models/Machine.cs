using CwAssetManager.Core.Enums;

namespace CwAssetManager.Core.Models;

/// <summary>Represents a managed machine/asset discovered across CW providers.</summary>
public sealed class Machine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? SerialNumber { get; set; }
    public string? BiosGuid { get; set; }
    public string? CwManageDeviceId { get; set; }
    public string? CwControlSessionId { get; set; }
    public string? CwRmmDeviceId { get; set; }
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Unknown;
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AssetEvaluation> Evaluations { get; init; } = [];
}
