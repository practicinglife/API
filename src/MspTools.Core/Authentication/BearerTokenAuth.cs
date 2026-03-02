namespace MspTools.Core.Authentication;

/// <summary>Bearer token authentication (e.g., JWT or OAuth access tokens).</summary>
public sealed class BearerTokenAuth : AuthMethod
{
    public string Token { get; }

    public BearerTokenAuth(string token)
    {
        Token = token ?? throw new ArgumentNullException(nameof(token));
        Type = AuthMethodType.BearerToken;
    }

    public override IEnumerable<KeyValuePair<string, string>> GetHeaders()
    {
        yield return new KeyValuePair<string, string>("Authorization", $"Bearer {Token}");
    }
}
