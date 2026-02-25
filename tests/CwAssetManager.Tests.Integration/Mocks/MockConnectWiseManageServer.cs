using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CwAssetManager.Tests.Integration.Mocks;

/// <summary>WireMock-based mock server for ConnectWise Manage API.</summary>
public sealed class MockConnectWiseManageServer : IDisposable
{
    private readonly WireMockServer _server;
    public string BaseUrl => _server.Url!;

    public MockConnectWiseManageServer()
    {
        _server = WireMockServer.Start();
        SetupDefaultRoutes();
    }

    private void SetupDefaultRoutes()
    {
        // System info
        _server.Given(Request.Create().WithPath("/v4_6_release/apis/3.0/system/info").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"version\":\"2023.1\",\"isCloud\":false}"));

        // Configurations (page 1)
        _server.Given(Request.Create()
                   .WithPath("/v4_6_release/apis/3.0/company/configurations")
                   .WithParam("page", "1")
                   .UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(SampleConfigurationsJson()));

        // Configurations page 2 – empty to terminate pagination
        _server.Given(Request.Create()
                   .WithPath("/v4_6_release/apis/3.0/company/configurations")
                   .WithParam("page", "2")
                   .UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("[]"));
    }

    private static string SampleConfigurationsJson() => """
        [
          { "id": 1, "name": "WORKSTATION01", "ipAddress": "10.0.1.1",
            "osType": "Windows 11", "status": { "name": "Active" } },
          { "id": 2, "name": "SERVER01", "ipAddress": "10.0.1.2",
            "osType": "Windows Server 2022", "status": { "name": "Active" } },
          { "id": 3, "name": "LAPTOP01", "ipAddress": "192.168.1.100",
            "osType": "Windows 11", "status": { "name": "Inactive" } }
        ]
        """;

    /// <summary>Configures a 429 rate-limit response for configuration requests.</summary>
    public void SetupRateLimitResponse()
    {
        _server.Reset();
        _server.Given(Request.Create().WithPath("*").UsingAnyMethod())
               .RespondWith(Response.Create()
                   .WithStatusCode(429)
                   .WithHeader("Retry-After", "1")
                   .WithBody("{\"message\":\"Too Many Requests\"}"));
    }

    public void Dispose() => _server.Dispose();
}
