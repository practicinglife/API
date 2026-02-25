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
        var json = JsonSerializer.Serialize(new[]
        {
            new { id = 1, name = "PC01", ipAddress = "10.0.0.1", osType = "Windows", status = new { name = "Active" } },
            new { id = 2, name = "SERVER01", ipAddress = "10.0.0.2", osType = "Windows Server", status = new { name = "Active" } }
        });

        var rateLimiter = new Mock<IRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectWiseManageClient>.Instance;
        var client = new ConnectWiseManageClient(BuildMockHttpClient(json), rateLimiter.Object, logger);

        var machines = await client.GetMachinesAsync();

        machines.Should().HaveCount(2);
        machines[0].Hostname.Should().Be("PC01");
        machines[0].IpAddress.Should().Be("10.0.0.1");
        machines[1].Hostname.Should().Be("SERVER01");
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
