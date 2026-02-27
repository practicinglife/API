using MspTools.Core.Authentication;

namespace MspTools.Core.Models;

/// <summary>Identifies which platform/product an API connection targets.</summary>
public enum ConnectorType
{
    ConnectWiseManage,
    ConnectWiseAsio,
    ConnectWiseControl,
    Custom
}

/// <summary>Stores configuration for a single API connection endpoint.</summary>
public class ApiConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ConnectorType ConnectorType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public AuthMethod Auth { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public DateTime LastSyncUtc { get; set; }
    public string? Notes { get; set; }
}
