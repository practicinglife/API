using System.Text.Json.Serialization;

namespace CwAssetManager.ApiClients.ConnectWiseManage.Models;

/// <summary>
/// Typed representation of the <c>Company.Configuration</c> schema from the ConnectWise Manage
/// OpenAPI 3.0.1 specification (version 2025.16).
/// Only fields relevant to asset identification, status, and telemetry are mapped.
/// </summary>
public sealed class CwManageConfiguration
{
    /// <summary>Unique integer identifier within ConnectWise Manage.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Display name of the configuration (maps to Machine.Hostname).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Provider-assigned device identifier string, distinct from the integer <see cref="Id"/>.
    /// Used as a secondary key for cross-system correlation.
    /// </summary>
    [JsonPropertyName("deviceIdentifier")]
    public string? DeviceIdentifier { get; init; }

    /// <summary>Hardware serial number.</summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; init; }

    /// <summary>Model number / part number.</summary>
    [JsonPropertyName("modelNumber")]
    public string? ModelNumber { get; init; }

    /// <summary>Primary MAC address of the device.</summary>
    [JsonPropertyName("macAddress")]
    public string? MacAddress { get; init; }

    /// <summary>Primary IP address.</summary>
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; init; }

    /// <summary>Default gateway IP.</summary>
    [JsonPropertyName("defaultGateway")]
    public string? DefaultGateway { get; init; }

    /// <summary>Operating system type string (e.g. "Windows Server 2022").</summary>
    [JsonPropertyName("osType")]
    public string? OsType { get; init; }

    /// <summary>Detailed OS information string including version.</summary>
    [JsonPropertyName("osInfo")]
    public string? OsInfo { get; init; }

    /// <summary>
    /// Mobile GUID (BIOS GUID) — a hardware-level unique identifier used for
    /// deterministic cross-provider identity resolution.
    /// </summary>
    [JsonPropertyName("mobileGuid")]
    public string? MobileGuid { get; init; }

    /// <summary>Whether the configuration is currently active.</summary>
    [JsonPropertyName("activeFlag")]
    public bool ActiveFlag { get; init; }

    /// <summary>Status reference object (contains <c>name</c> field).</summary>
    [JsonPropertyName("status")]
    public CwManageReference? Status { get; init; }

    /// <summary>Configuration type reference (contains <c>name</c> field).</summary>
    [JsonPropertyName("type")]
    public CwManageReference? Type { get; init; }

    /// <summary>Company reference (contains <c>name</c> field).</summary>
    [JsonPropertyName("company")]
    public CwManageReference? Company { get; init; }

    /// <summary>Tag/asset number.</summary>
    [JsonPropertyName("tagNumber")]
    public string? TagNumber { get; init; }

    /// <summary>CPU speed string.</summary>
    [JsonPropertyName("cpuSpeed")]
    public string? CpuSpeed { get; init; }

    /// <summary>RAM description string.</summary>
    [JsonPropertyName("ram")]
    public string? Ram { get; init; }

    /// <summary>Local hard drives description.</summary>
    [JsonPropertyName("localHardDrives")]
    public string? LocalHardDrives { get; init; }

    /// <summary>Last user login name recorded by the agent.</summary>
    [JsonPropertyName("lastLoginName")]
    public string? LastLoginName { get; init; }

    /// <summary>Vendor notes free-form text.</summary>
    [JsonPropertyName("vendorNotes")]
    public string? VendorNotes { get; init; }

    /// <summary>General notes free-form text.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>Manufacturer part number.</summary>
    [JsonPropertyName("manufacturerPartNumber")]
    public string? ManufacturerPartNumber { get; init; }

    /// <summary>Additional metadata returned by the API.</summary>
    [JsonPropertyName("_info")]
    public Dictionary<string, string>? Info { get; init; }
}

/// <summary>Lightweight reference object used by CW Manage for related entity fields.</summary>
public sealed class CwManageReference
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; init; }
}
