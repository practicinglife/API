namespace MspTools.Core.Authentication;

/// <summary>
/// OAuth2 client_credentials authentication (client_id + client_secret).
/// Sends credentials as HTTP Basic auth — Base64-encoded "client_id:client_secret".
/// The optional <see cref="Scope"/> is used by connectors that require it (e.g. ConnectWise Asio).
/// </summary>
public sealed class ClientCredentialsAuth : AuthMethod
{
    public string ClientId { get; }
    public string ClientSecret { get; }
    /// <summary>Space-separated OAuth2 scopes, e.g. "platform.devices.read platform.companies.read".</summary>
    public string Scope { get; }

    public ClientCredentialsAuth(string clientId, string clientSecret, string scope = "")
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        ClientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        Scope = scope ?? string.Empty;
        Type = AuthMethodType.ClientCredentials;
    }

    public override IEnumerable<KeyValuePair<string, string>> GetHeaders()
    {
        var encoded = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        yield return new KeyValuePair<string, string>("Authorization", $"Basic {encoded}");
    }
}
