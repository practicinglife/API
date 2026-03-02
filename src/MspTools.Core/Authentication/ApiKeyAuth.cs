namespace MspTools.Core.Authentication;

/// <summary>API Key authentication, sent as a header.</summary>
public sealed class ApiKeyAuth : AuthMethod
{
    public string HeaderName { get; }
    public string ApiKey { get; }

    public ApiKeyAuth(string apiKey, string headerName = "x-api-key")
    {
        ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
        Type = AuthMethodType.ApiKey;
    }

    public override IEnumerable<KeyValuePair<string, string>> GetHeaders()
    {
        yield return new KeyValuePair<string, string>(HeaderName, ApiKey);
    }
}
