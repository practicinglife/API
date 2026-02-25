using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CwAssetManager.ApiClients.ConnectWiseManage;
using CwAssetManager.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.Tests.Unit.ApiClients;

public sealed class ConnectWiseManageClientTests
{
    private static HttpClient BuildMockHttpClient(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseJson, status);
        return new HttpClient(handler) { BaseAddress = new Uri("https://manage.example.com/v4_6_release/apis/3.0/") };
    }

    [Fact]
    public async Task GetMachinesAsync_WithValidResponse_ReturnsMachines()
    {
        // Use the Company.Configuration schema fields from All.json (v2025.16).
        var json = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = 1, name = "PC01", ipAddress = "10.0.0.1",
                osType = "Windows 11", osInfo = "Windows 11 Pro 23H2",
                serialNumber = "SN001", macAddress = "AA:BB:CC:DD:EE:01",
                mobileGuid = "11111111-1111-1111-1111-111111111111",
                activeFlag = true,
                status = new { name = "Active" }
            },
            new
            {
                id = 2, name = "SERVER01", ipAddress = "10.0.0.2",
                osType = "Windows Server 2022", osInfo = "Windows Server 2022 Standard",
                serialNumber = "SN002", macAddress = "AA:BB:CC:DD:EE:02",
                mobileGuid = "22222222-2222-2222-2222-222222222222",
                activeFlag = true,
                status = new { name = "Active" }
            }
        });

        var rateLimiter = new Mock<IRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(BuildMockHttpClient(json), rateLimiter.Object, logger);

        var machines = await client.GetMachinesAsync();

        machines.Should().HaveCount(2);

        var pc01 = machines.Single(m => m.Hostname == "PC01");
        pc01.IpAddress.Should().Be("10.0.0.1");
        pc01.SerialNumber.Should().Be("SN001");
        pc01.MacAddress.Should().Be("AA:BB:CC:DD:EE:01");
        // osInfo takes precedence over osType
        pc01.OperatingSystem.Should().Be("Windows 11 Pro 23H2");
        // mobileGuid → BiosGuid
        pc01.BiosGuid.Should().Be("11111111-1111-1111-1111-111111111111");
        // activeFlag=true + status.name="Active" → Online
        pc01.Status.Should().Be(Core.Enums.MachineStatus.Online);
        // id → CwManageDeviceId (as string)
        pc01.CwManageDeviceId.Should().Be("1");

        machines.Single(m => m.Hostname == "SERVER01")
                .OperatingSystem.Should().Be("Windows Server 2022 Standard");
    }

    [Fact]
    public async Task GetMachinesAsync_EmptyArray_ReturnsEmpty()
    {
        var rateLimiter = new Mock<IRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(BuildMockHttpClient("[]"), rateLimiter.Object, logger);

        var machines = await client.GetMachinesAsync();
        machines.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMachinesAsync_RateLimiterDenied_ThrowsInvalidOperation()
    {
        var rateLimiter = new Mock<IRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(BuildMockHttpClient("[]"), rateLimiter.Object, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetMachinesAsync());
    }

    [Fact]
    public async Task TestConnectionAsync_200Response_ReturnsTrue()
    {
        var rateLimiter = new Mock<IRateLimiter>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(
            BuildMockHttpClient("{\"version\":\"2023.1\"}"), rateLimiter.Object, logger);

        var result = await client.TestConnectionAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ServerError_ReturnsFalse()
    {
        var rateLimiter = new Mock<IRateLimiter>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(
            BuildMockHttpClient("{}", HttpStatusCode.InternalServerError), rateLimiter.Object, logger);

        // HttpClient.GetAsync returns a 500; IsSuccessStatusCode is false → TestConnectionAsync catches and returns false
        var result = await client.TestConnectionAsync();
        result.Should().BeFalse("500 response means the server is not healthy");
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _status;

        public MockHttpMessageHandler(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _json = json;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        }
    }
}
