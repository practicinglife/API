using CwAssetManager.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CwAssetManager.Tests.Unit.Security;

public sealed class SecretEncryptionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SecretEncryptionService _svc;

    public SecretEncryptionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cw-test-{Guid.NewGuid():N}");
        _svc = new SecretEncryptionService(_tempDir, masterPassphrase: "test-passphrase");
    }

    [Fact]
    public async Task SetAndGet_RoundTrip_ReturnsOriginalValue()
    {
        await _svc.SetSecretAsync("key1", "super-secret-value");
        var result = await _svc.GetSecretAsync("key1");
        result.Should().Be("super-secret-value");
    }

    [Fact]
    public async Task Get_NonExistentKey_ReturnsNull()
    {
        var result = await _svc.GetSecretAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Exists_AfterSet_ReturnsTrue()
    {
        await _svc.SetSecretAsync("mykey", "myvalue");
        (await _svc.ExistsAsync("mykey")).Should().BeTrue();
    }

    [Fact]
    public async Task Exists_BeforeSet_ReturnsFalse()
    {
        (await _svc.ExistsAsync("missing-key")).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_RemovesKey()
    {
        await _svc.SetSecretAsync("to-delete", "value");
        await _svc.DeleteSecretAsync("to-delete");
        (await _svc.ExistsAsync("to-delete")).Should().BeFalse();
    }

    [Fact]
    public async Task Persistence_ReloadsFromDisk()
    {
        await _svc.SetSecretAsync("persist-key", "persist-value");

        // Create a new instance pointing to the same directory
        var svc2 = new SecretEncryptionService(_tempDir, masterPassphrase: "test-passphrase");
        var result = await svc2.GetSecretAsync("persist-key");
        result.Should().Be("persist-value");
    }

    [Fact]
    public async Task MultipleKeys_AllStoredIndependently()
    {
        await _svc.SetSecretAsync("a", "alpha");
        await _svc.SetSecretAsync("b", "beta");
        await _svc.SetSecretAsync("c", "gamma");

        (await _svc.GetSecretAsync("a")).Should().Be("alpha");
        (await _svc.GetSecretAsync("b")).Should().Be("beta");
        (await _svc.GetSecretAsync("c")).Should().Be("gamma");
    }

    [Fact]
    public async Task Overwrite_UpdatesValue()
    {
        await _svc.SetSecretAsync("key", "v1");
        await _svc.SetSecretAsync("key", "v2");
        (await _svc.GetSecretAsync("key")).Should().Be("v2");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
