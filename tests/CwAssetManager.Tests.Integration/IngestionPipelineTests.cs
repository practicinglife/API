using CwAssetManager.ApiClients.ConnectWiseManage;
using CwAssetManager.ApiClients.ConnectWiseControl;
using CwAssetManager.ApiClients.ConnectWiseRmm;
using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Auth;
using CwAssetManager.Tests.Integration.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.Tests.Integration;

/// <summary>End-to-end ingestion smoke tests using WireMock mock servers.</summary>
public sealed class IngestionPipelineTests : IDisposable
{
    private readonly MockConnectWiseManageServer _manageServer = new();
    private readonly MockConnectWiseControlServer _controlServer = new();
    private readonly MockConnectWiseRmmServer _rmmServer = new();

    private IRateLimiter BuildPermissiveLimiter()
    {
        var mock = new Mock<IRateLimiter>();
        mock.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mock.Setup(r => r.AvailableTokens).Returns(100);
        mock.Setup(r => r.Capacity).Returns(100);
        return mock.Object;
    }

    [Fact]
    public async Task ManageClient_FetchesConfigurationsFromMockServer()
    {
        var http = new HttpClient { BaseAddress = new Uri(_manageServer.BaseUrl + "/v4_6_release/apis/3.0/") };
        var client = new ConnectWiseManageClient(http, BuildPermissiveLimiter(),
            NullLogger<ConnectWiseManageClient>.Instance);

        var machines = await client.GetMachinesAsync();

        machines.Should().HaveCount(3);
        machines.Should().Contain(m => m.Hostname == "WORKSTATION01");
        machines.Should().Contain(m => m.Hostname == "SERVER01");
        machines.Should().Contain(m => m.Hostname == "LAPTOP01");
    }

    [Fact]
    public async Task ManageClient_TestConnection_ReturnsTrue()
    {
        var http = new HttpClient { BaseAddress = new Uri(_manageServer.BaseUrl + "/v4_6_release/apis/3.0/") };
        var client = new ConnectWiseManageClient(http, BuildPermissiveLimiter(),
            NullLogger<ConnectWiseManageClient>.Instance);

        var result = await client.TestConnectionAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ControlClient_FetchesSessionsFromMockServer()
    {
        var http = new HttpClient { BaseAddress = new Uri(_controlServer.BaseUrl + "/") };
        var client = new ConnectWiseControlClient(http, BuildPermissiveLimiter(),
            NullLogger<ConnectWiseControlClient>.Instance);

        var machines = await client.GetMachinesAsync();

        machines.Should().HaveCount(2);
        machines.Should().Contain(m => m.Hostname == "WORKSTATION01");
        machines.Should().Contain(m => m.Hostname == "LAPTOP01");
        // Verify SessionID is mapped from the GUID field
        machines.Should().Contain(m => m.CwControlSessionId == MockConnectWiseControlServer.Session1Id);
        // Verify GuestInfo fields are mapped
        var ws = machines.Single(m => m.Hostname == "WORKSTATION01");
        ws.IpAddress.Should().Be("192.168.1.101");
        ws.SerialNumber.Should().Be("SN-CTRL-001");
        ws.OperatingSystem.Should().Contain("Windows 11 Pro");
        ws.Status.Should().Be(CwAssetManager.Core.Enums.MachineStatus.Online); // has Guest connection
        // LAPTOP01 has no active connections → Offline
        machines.Single(m => m.Hostname == "LAPTOP01")
                .Status.Should().Be(CwAssetManager.Core.Enums.MachineStatus.Offline);
    }

    [Fact]
    public async Task RmmClient_FetchesDevicesFromMockServer()
    {
        var http = new HttpClient { BaseAddress = new Uri(_rmmServer.BaseUrl + "/") };
        var client = new ConnectWiseRmmClient(http, BuildPermissiveLimiter(),
            NullLogger<ConnectWiseRmmClient>.Instance);

        var machines = await client.GetMachinesAsync();

        machines.Should().HaveCount(2);
        machines.Should().Contain(m => m.CwRmmDeviceId == "rmm-001");
        machines.Should().Contain(m => m.SerialNumber == "SN002");
    }

    [Fact]
    public async Task RmmClient_TestConnection_ReturnsTrue()
    {
        var http = new HttpClient { BaseAddress = new Uri(_rmmServer.BaseUrl + "/") };
        var client = new ConnectWiseRmmClient(http, BuildPermissiveLimiter(),
            NullLogger<ConnectWiseRmmClient>.Instance);

        var result = await client.TestConnectionAsync();
        result.Should().BeTrue();
    }

    public void Dispose()
    {
        _manageServer.Dispose();
        _controlServer.Dispose();
        _rmmServer.Dispose();
    }
}
