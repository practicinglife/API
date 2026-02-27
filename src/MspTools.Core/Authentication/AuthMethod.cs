namespace MspTools.Core.Authentication;

/// <summary>Supported authentication method types for API connections.</summary>
public enum AuthMethodType
{
    ApiKey,
    BasicAuth,
    BearerToken,
    ConnectWiseApiKey
}

/// <summary>Base class for API authentication credentials.</summary>
public abstract class AuthMethod
{
    public AuthMethodType Type { get; protected set; }
    public abstract IEnumerable<KeyValuePair<string, string>> GetHeaders();
}
