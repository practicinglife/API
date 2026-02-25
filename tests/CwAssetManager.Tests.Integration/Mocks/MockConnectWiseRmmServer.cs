using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CwAssetManager.Tests.Integration.Mocks;

/// <summary>WireMock-based mock server for ConnectWise RMM API.</summary>
public sealed class MockConnectWiseRmmServer : IDisposable
{
    private readonly WireMockServer _server;
    public string BaseUrl => _server.Url!;

    public MockConnectWiseRmmServer()
    {
        _server = WireMockServer.Start();
        SetupDefaultRoutes();
    }

    private void SetupDefaultRoutes()
    {
        // Account check
        _server.Given(Request.Create().WithPath("/account").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"id\":\"tenant-1\",\"name\":\"Test Tenant\"}"));

        // Devices – page 1
        _server.Given(Request.Create()
                   .WithPath("/devices")
                   .WithParam("offset", "0")
                   .UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(SampleDevicesJson()));

        // Devices – page 2 empty
        _server.Given(Request.Create()
                   .WithPath("/devices")
                   .WithParam("offset", "500")
                   .UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"data\":[]}"));
    }

    private static string SampleDevicesJson() => """
        {
          "data": [
            { "id": "rmm-001", "hostname": "WORKSTATION01", "ip": "10.0.1.1",
              "macAddress": "AA:BB:CC:DD:EE:01", "serialNumber": "SN001",
              "os": "Windows 11", "status": "online" },
            { "id": "rmm-002", "hostname": "SERVER01", "ip": "10.0.1.2",
              "macAddress": "AA:BB:CC:DD:EE:02", "serialNumber": "SN002",
              "os": "Windows Server 2022", "status": "online" }
          ]
        }
        """;

    public void Dispose() => _server.Dispose();
}
