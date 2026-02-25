using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CwAssetManager.Tests.Integration.Mocks;

/// <summary>
/// WireMock-based mock server for the ConnectWise Control Session Manager API.
/// Serves the JSON-RPC endpoint <c>POST /Services/PageService.ashx</c> as
/// documented in the Session Manager API Reference.
/// </summary>
public sealed class MockConnectWiseControlServer : IDisposable
{
    private readonly WireMockServer _server;
    public string BaseUrl => _server.Url!;

    // Fixed GUIDs used across mock and test assertions.
    public static readonly string Session1Id = "a1b2c3d4-0001-0000-0000-000000000001";
    public static readonly string Session2Id = "a1b2c3d4-0002-0000-0000-000000000002";

    public MockConnectWiseControlServer()
    {
        _server = WireMockServer.Start();
        SetupDefaultRoutes();
    }

    private void SetupDefaultRoutes()
    {
        // OAuth2 token endpoint (used by ConnectWiseControlAuthHandler OAuth2 path).
        _server.Given(Request.Create().WithPath("/token").UsingPost())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody("{\"access_token\":\"mock-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));

        // Session Manager JSON-RPC endpoint — responds to all method calls.
        _server.Given(Request.Create().WithPath("/Services/PageService.ashx").UsingPost())
               .RespondWith(Response.Create()
                   .WithStatusCode(200)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(SampleRpcResponseJson()));
    }

    /// <summary>
    /// JSON-RPC 2.0 response with two Access sessions matching the
    /// <c>CwControlSession</c> / <c>CwControlGuestInfo</c> schema.
    /// </summary>
    private string SampleRpcResponseJson() => $$"""
        {
          "id": 1,
          "result": [
            {
              "SessionID": "{{Session1Id}}",
              "Name": "WORKSTATION01",
              "SessionType": "Access",
              "Host": "admin",
              "IsPublic": false,
              "GuestInfoUpdateTime": "2025-01-15T10:00:00Z",
              "ActiveConnections": [
                { "ProcessType": "Guest", "ConnectionID": "00000000-0000-0000-0000-000000000001" }
              ],
              "GuestInfo": {
                "OperatingSystemName": "Windows 11 Pro",
                "OperatingSystemVersion": "22H2",
                "ProcessorName": "Intel Core i7",
                "ProcessorVirtualCount": 8,
                "SystemMemoryTotalMegabytes": 16384,
                "SystemMemoryAvailableMegabytes": 8192,
                "GuestPrivateIpAddress": "192.168.1.101",
                "GuestMachineSerialNumber": "SN-CTRL-001",
                "GuestMachineDomain": "WORKGROUP"
              }
            },
            {
              "SessionID": "{{Session2Id}}",
              "Name": "LAPTOP01",
              "SessionType": "Access",
              "Host": "admin",
              "IsPublic": false,
              "GuestInfoUpdateTime": "2025-01-15T09:30:00Z",
              "ActiveConnections": [],
              "GuestInfo": {
                "OperatingSystemName": "Windows 11 Home",
                "OperatingSystemVersion": "23H2",
                "GuestPrivateIpAddress": "192.168.1.102",
                "GuestMachineSerialNumber": "SN-CTRL-002"
              }
            }
          ],
          "error": null
        }
        """;

    public void Dispose() => _server.Dispose();
}

