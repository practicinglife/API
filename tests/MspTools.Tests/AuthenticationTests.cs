using MspTools.Core.Authentication;
using MspTools.Core.Models;

namespace MspTools.Tests;

public class AuthenticationTests
{
    [Fact]
    public void BasicAuth_GetHeaders_ReturnsCorrectBase64Header()
    {
        var auth = new BasicAuth("user", "pass");
        var headers = auth.GetHeaders().ToDictionary(h => h.Key, h => h.Value);

        Assert.True(headers.ContainsKey("Authorization"));
        var expected = "Basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("user:pass"));
        Assert.Equal(expected, headers["Authorization"]);
    }

    [Fact]
    public void ApiKeyAuth_GetHeaders_UsesDefaultHeaderName()
    {
        var auth = new ApiKeyAuth("my-secret-key");
        var headers = auth.GetHeaders().ToDictionary(h => h.Key, h => h.Value);

        Assert.True(headers.ContainsKey("x-api-key"));
        Assert.Equal("my-secret-key", headers["x-api-key"]);
    }

    [Fact]
    public void ApiKeyAuth_GetHeaders_UsesCustomHeaderName()
    {
        var auth = new ApiKeyAuth("token-value", "Authorization-Key");
        var headers = auth.GetHeaders().ToDictionary(h => h.Key, h => h.Value);

        Assert.True(headers.ContainsKey("Authorization-Key"));
    }

    [Fact]
    public void ConnectWiseApiKeyAuth_GetHeaders_ReturnsAuthAndClientId()
    {
        var auth = new ConnectWiseApiKeyAuth("Acme", "pubkey", "privkey", "client-123");
        var headers = auth.GetHeaders().ToDictionary(h => h.Key, h => h.Value);

        Assert.True(headers.ContainsKey("Authorization"));
        Assert.True(headers.ContainsKey("clientId"));
        Assert.Equal("client-123", headers["clientId"]);

        var expectedEncoded = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("Acme+pubkey:privkey"));
        Assert.Equal($"Basic {expectedEncoded}", headers["Authorization"]);
    }

    [Fact]
    public void BearerTokenAuth_GetHeaders_ReturnsBearerHeader()
    {
        var auth = new BearerTokenAuth("my-jwt-token");
        var headers = auth.GetHeaders().ToDictionary(h => h.Key, h => h.Value);

        Assert.Equal("Bearer my-jwt-token", headers["Authorization"]);
    }

    [Fact]
    public void BasicAuth_ThrowsOnNullUsername()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicAuth(null!, "pass"));
    }

    [Fact]
    public void ApiKeyAuth_ThrowsOnNullKey()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiKeyAuth(null!));
    }
}
