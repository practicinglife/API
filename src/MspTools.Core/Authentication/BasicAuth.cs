namespace MspTools.Core.Authentication;

/// <summary>HTTP Basic Authentication (username + password, Base64-encoded).</summary>
public sealed class BasicAuth : AuthMethod
{
    public string Username { get; }
    public string Password { get; }

    public BasicAuth(string username, string password)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        Type = AuthMethodType.BasicAuth;
    }

    public override IEnumerable<KeyValuePair<string, string>> GetHeaders()
    {
        var encoded = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{Username}:{Password}"));
        yield return new KeyValuePair<string, string>("Authorization", $"Basic {encoded}");
    }
}
