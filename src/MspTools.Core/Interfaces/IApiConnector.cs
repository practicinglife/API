using MspTools.Core.Models;

namespace MspTools.Core.Interfaces;

/// <summary>
/// Contract that every API connector must satisfy.
/// Each connector fetches devices and companies from a specific platform
/// and normalises them into the unified model.
/// </summary>
public interface IApiConnector
{
    /// <summary>Human-readable name of the connector (e.g. "ConnectWise Manage").</summary>
    string Name { get; }

    /// <summary>The platform type this connector targets.</summary>
    ConnectorType ConnectorType { get; }

    /// <summary>
    /// Test the connection to the remote API.
    /// Returns <c>true</c> when the credentials and endpoint are valid.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetch all devices/computers from the remote platform.</summary>
    Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetch all companies/organisations from the remote platform.</summary>
    Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default);
}
