using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CwAssetManager.Tests.Integration.Mocks;

/// <summary>WireMock-based mock server for ConnectWise Control API.</summary>
public sealed class MockConnectWiseControlServer : IDisposable
{
    private readonly WireMockServer _server;
    public string BaseUrl => _server.Url!;

    public MockConnectWiseControlServer()
    {
        _server = WireMockServer.Start();
        SetupDefaultRoutes();
    }

    private void SetupDefaultRoutes()
    {
        // OAuth2 token endpoint
        _server.Given(Request.Create().WithPath("/token").UsingPost())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"access_token\":\"mock-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));

        // Status endpoint
        _server.Given(Request.Create().WithPath("/Status").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"status\":\"OK\"}"));

        // Sessions
        _server.Given(Request.Create().WithPath("/Session").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(SampleSessionsJson()));
    }

    private static string SampleSessionsJson() => """
        [
          { "SessionID": "sess-001", "Name": "WORKSTATION01",
            "IsOnline": true, "GuestOperatingSystemName": "Windows 11" },
          { "SessionID": "sess-002", "Name": "LAPTOP01",
            "IsOnline": false, "GuestOperatingSystemName": "Windows 11" }
        ]
        """;

    public void Dispose() => _server.Dispose();
}
