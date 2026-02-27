namespace MspTools.Core.Authentication;

/// <summary>
/// ConnectWise Manage-style API key authentication.
/// Combines a client ID header with a Base64-encoded "CompanyId+PublicKey:PrivateKey" Authorization header.
/// </summary>
public sealed class ConnectWiseApiKeyAuth : AuthMethod
{
    public string CompanyId { get; }
    public string PublicKey { get; }
    public string PrivateKey { get; }
    public string ClientId { get; }

    public ConnectWiseApiKeyAuth(string companyId, string publicKey, string privateKey, string clientId)
    {
        CompanyId = companyId ?? throw new ArgumentNullException(nameof(companyId));
        PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        Type = AuthMethodType.ConnectWiseApiKey;
    }

    public override IEnumerable<KeyValuePair<string, string>> GetHeaders()
    {
        var encoded = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{CompanyId}+{PublicKey}:{PrivateKey}"));
        yield return new KeyValuePair<string, string>("Authorization", $"Basic {encoded}");
        yield return new KeyValuePair<string, string>("clientId", ClientId);
    }
}
