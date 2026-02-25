using System.Text.Json.Serialization;

namespace CwAssetManager.ApiClients.ConnectWiseControl.Models;

/// <summary>
/// Typed representation of a ConnectWise Control (ScreenConnect) Session object
/// as documented in the Session Manager API Reference.
/// <para>
/// The Session Manager API is accessed via JSON-RPC at
/// <c>POST {serverUrl}/Services/PageService.ashx</c>.
/// </para>
/// </summary>
public sealed class CwControlSession
{
    /// <summary>Unique GUID that identifies this session (primary key).</summary>
    [JsonPropertyName("SessionID")]
    public Guid SessionId { get; init; }

    /// <summary>Display name of the session, also the machine name for Access sessions.</summary>
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    /// <summary>
    /// Session type — only <c>Access</c> sessions represent managed endpoints.
    /// Other values: <c>Support</c>, <c>Meeting</c>.
    /// </summary>
    [JsonPropertyName("SessionType")]
    public string? SessionType { get; init; }

    /// <summary>Host (technician) assigned to this session at creation.</summary>
    [JsonPropertyName("Host")]
    public string? Host { get; init; }

    /// <summary>Whether the session is publicly joinable.</summary>
    [JsonPropertyName("IsPublic")]
    public bool IsPublic { get; init; }

    /// <summary>The guest join code (null for public sessions).</summary>
    [JsonPropertyName("Code")]
    public string? Code { get; init; }

    /// <summary>
    /// Timestamp of the last guest agent info update.
    /// Provides a proxy for "last seen" time.
    /// </summary>
    [JsonPropertyName("GuestInfoUpdateTime")]
    public DateTimeOffset? GuestInfoUpdateTime { get; init; }

    /// <summary>Active connection list — non-empty when a guest is currently connected.</summary>
    [JsonPropertyName("ActiveConnections")]
    public IReadOnlyList<CwControlConnection>? ActiveConnections { get; init; }

    /// <summary>
    /// Rich guest machine information populated by the ScreenConnect agent
    /// (operating system, hardware specs, IP addresses, serial number).
    /// </summary>
    [JsonPropertyName("GuestInfo")]
    public CwControlGuestInfo? GuestInfo { get; init; }

    /// <summary>Up to 8 custom property values set on this session.</summary>
    [JsonPropertyName("CustomPropertyValues")]
    public IReadOnlyList<string>? CustomPropertyValues { get; init; }

    /// <summary>Returns <c>true</c> when at least one guest agent connection is active.</summary>
    [JsonIgnore]
    public bool IsOnline => ActiveConnections?.Any(c =>
        string.Equals(c.ProcessType, "Guest", StringComparison.OrdinalIgnoreCase)) ?? false;
}

/// <summary>
/// Hardware and operating-system information reported by the ScreenConnect guest agent
/// (<c>SessionGuestInfo</c> in the Session Manager API reference).
/// </summary>
public sealed class CwControlGuestInfo
{
    /// <summary>Full OS name (e.g. "Windows 11 Pro").</summary>
    [JsonPropertyName("OperatingSystemName")]
    public string? OperatingSystemName { get; init; }

    /// <summary>OS version string.</summary>
    [JsonPropertyName("OperatingSystemVersion")]
    public string? OperatingSystemVersion { get; init; }

    /// <summary>CPU model name.</summary>
    [JsonPropertyName("ProcessorName")]
    public string? ProcessorName { get; init; }

    /// <summary>Logical CPU count (virtual cores).</summary>
    [JsonPropertyName("ProcessorVirtualCount")]
    public int? ProcessorVirtualCount { get; init; }

    /// <summary>Total RAM in megabytes.</summary>
    [JsonPropertyName("SystemMemoryTotalMegabytes")]
    public long? SystemMemoryTotalMegabytes { get; init; }

    /// <summary>Available (free) RAM in megabytes.</summary>
    [JsonPropertyName("SystemMemoryAvailableMegabytes")]
    public long? SystemMemoryAvailableMegabytes { get; init; }

    /// <summary>Machine's public / private IP addresses (comma-separated in some versions).</summary>
    [JsonPropertyName("GuestPrivateIpAddress")]
    public string? GuestPrivateIpAddress { get; init; }

    /// <summary>Hardware serial number as reported by the agent.</summary>
    [JsonPropertyName("GuestMachineSerialNumber")]
    public string? GuestMachineSerialNumber { get; init; }

    /// <summary>Machine domain or workgroup name.</summary>
    [JsonPropertyName("GuestMachineDomain")]
    public string? GuestMachineDomain { get; init; }
}

/// <summary>Represents a single active connection attached to a ScreenConnect session.</summary>
public sealed class CwControlConnection
{
    /// <summary>
    /// Connection type — <c>Guest</c> means the remote agent is connected;
    /// <c>Host</c> means a technician is connected.
    /// </summary>
    [JsonPropertyName("ProcessType")]
    public string? ProcessType { get; init; }

    [JsonPropertyName("ConnectionID")]
    public Guid? ConnectionId { get; init; }
}

/// <summary>JSON-RPC 2.0 wrapper used to call Session Manager methods.</summary>
public sealed class CwControlRpcRequest
{
    [JsonPropertyName("id")]
    public int Id { get; init; } = 1;

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; init; }
}

/// <summary>JSON-RPC 2.0 response envelope for Session Manager calls.</summary>
public sealed class CwControlRpcResponse<T>
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("error")]
    public CwControlRpcError? Error { get; init; }
}

/// <summary>JSON-RPC error payload.</summary>
public sealed class CwControlRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
