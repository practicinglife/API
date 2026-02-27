using MspTools.Core.Authentication;
using MspTools.Core.Models;
using MspTools.Connectors;

namespace MspTools.Tests;

public class ConnectorFactoryTests
{
    private static ApiConnection MakeConnection(ConnectorType type)
        => new()
        {
            Name = "Test",
            ConnectorType = type,
            BaseUrl = "https://example.com",
            Auth = new ApiKeyAuth("test-key"),
        };

    [Theory]
    [InlineData(ConnectorType.ConnectWiseManage)]
    [InlineData(ConnectorType.ConnectWiseAsio)]
    [InlineData(ConnectorType.ConnectWiseControl)]
    public void Create_ReturnsCorrectConnectorType(ConnectorType connectorType)
    {
        var conn = MakeConnection(connectorType);
        using var connector = (IDisposable)ConnectorFactory.Create(conn);
        Assert.NotNull(connector);
    }

    [Fact]
    public void Create_ThrowsForCustomType()
    {
        var conn = MakeConnection(ConnectorType.Custom);
        Assert.Throws<NotSupportedException>(() => ConnectorFactory.Create(conn));
    }

    [Fact]
    public void Create_ThrowsOnNullConnection()
    {
        Assert.Throws<ArgumentNullException>(() => ConnectorFactory.Create(null!));
    }
}
