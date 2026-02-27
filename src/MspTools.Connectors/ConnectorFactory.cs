using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Creates the correct <see cref="IApiConnector"/> for a given <see cref="ApiConnection"/>.
/// Add new connector types here as more platforms are integrated.
/// </summary>
public static class ConnectorFactory
{
    /// <summary>Returns an <see cref="IApiConnector"/> for the given connection configuration.</summary>
    /// <exception cref="NotSupportedException">Thrown when the connector type is not yet implemented.</exception>
    public static IApiConnector Create(ApiConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.ConnectorType switch
        {
            ConnectorType.ConnectWiseManage => new ConnectWiseManageConnector(connection),
            ConnectorType.ConnectWiseAsio => new ConnectWiseAsioConnector(connection),
            ConnectorType.ConnectWiseControl => new ConnectWiseControlConnector(connection),
            _ => throw new NotSupportedException($"Connector type '{connection.ConnectorType}' is not supported.")
        };
    }
}
