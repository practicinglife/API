using CwAssetManager.Core.Models;

namespace CwAssetManager.Infrastructure.Identity;

/// <summary>
/// Deterministically resolves an asset identity across multiple CW providers
/// by comparing hardware identifiers in priority order:
/// BiosGuid > SerialNumber > MacAddress > Hostname (normalised).
/// </summary>
public sealed class AssetIdentityResolver
{
    /// <summary>
    /// Returns true if two Machine records should be treated as the same physical asset.
    /// </summary>
    public bool AreSameMachine(Machine a, Machine b)
    {
        // Strongest identifiers first
        if (IsMatch(a.BiosGuid, b.BiosGuid)) return true;
        if (IsMatch(a.SerialNumber, b.SerialNumber)) return true;
        if (IsMatch(a.MacAddress, b.MacAddress)) return true;
        if (IsMatch(NormaliseHostname(a.Hostname), NormaliseHostname(b.Hostname))) return true;
        return false;
    }

    /// <summary>
    /// Merges provider-specific IDs from <paramref name="incoming"/> into <paramref name="existing"/>,
    /// updating hostname, IP, and status as well.
    /// </summary>
    public void Merge(Machine existing, Machine incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming.CwManageDeviceId))
            existing.CwManageDeviceId = incoming.CwManageDeviceId;
        if (!string.IsNullOrWhiteSpace(incoming.CwControlSessionId))
            existing.CwControlSessionId = incoming.CwControlSessionId;
        if (!string.IsNullOrWhiteSpace(incoming.CwRmmDeviceId))
            existing.CwRmmDeviceId = incoming.CwRmmDeviceId;
        if (!string.IsNullOrWhiteSpace(incoming.Hostname))
            existing.Hostname = incoming.Hostname;
        if (!string.IsNullOrWhiteSpace(incoming.IpAddress))
            existing.IpAddress = incoming.IpAddress;
        if (!string.IsNullOrWhiteSpace(incoming.OperatingSystem))
            existing.OperatingSystem = incoming.OperatingSystem;
        if (!string.IsNullOrWhiteSpace(incoming.MacAddress))
            existing.MacAddress = incoming.MacAddress;
        if (!string.IsNullOrWhiteSpace(incoming.SerialNumber))
            existing.SerialNumber = incoming.SerialNumber;
        if (!string.IsNullOrWhiteSpace(incoming.BiosGuid))
            existing.BiosGuid = incoming.BiosGuid;

        existing.LastSeen = DateTimeOffset.UtcNow;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        if (incoming.Status != Core.Enums.MachineStatus.Unknown)
            existing.Status = incoming.Status;
    }

    private static bool IsMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormaliseHostname(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return null;
        // Strip fully-qualified suffix (e.g. "PC01.domain.local" → "PC01")
        var dot = hostname.IndexOf('.');
        var shortName = dot > 0 ? hostname[..dot] : hostname;
        return shortName.Trim().ToUpperInvariant();
    }
}
