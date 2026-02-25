using CwAssetManager.Core.Enums;

namespace CwAssetManager.Core.Models;

/// <summary>Represents a managed machine/asset discovered across CW providers.</summary>
public sealed class Machine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? SerialNumber { get; set; }

    /// <summary>
    /// BIOS/hardware GUID — sourced from <c>mobileGuid</c> in CW Manage.
    /// Used as the strongest cross-provider identity key.
    /// </summary>
    public string? BiosGuid { get; set; }

    // ---- ConnectWise Manage identifiers ----
    /// <summary>Integer record ID from CW Manage (stored as string for uniformity).</summary>
    public string? CwManageDeviceId { get; set; }

    /// <summary>
    /// The <c>deviceIdentifier</c> field from CW Manage's Company.Configuration schema.
    /// A secondary, provider-assigned device string identifier distinct from the integer ID.
    /// </summary>
    public string? CwManageDeviceIdentifier { get; set; }

    // ---- ConnectWise Control identifiers ----
    /// <summary>ScreenConnect Session GUID (from the <c>SessionID</c> field).</summary>
    public string? CwControlSessionId { get; set; }

    // ---- ConnectWise RMM identifiers ----
    public string? CwRmmDeviceId { get; set; }

    // ---- Telemetry ----
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Unknown;
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AssetEvaluation> Evaluations { get; init; } = [];
}
