using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace CwAssetManager.Tests.Unit.Identity;

public sealed class AssetIdentityResolverTests
{
    private readonly AssetIdentityResolver _resolver = new();

    [Fact]
    public void AreSameMachine_SameBiosGuid_ReturnsTrue()
    {
        var a = new Machine { BiosGuid = "BIOS-1234" };
        var b = new Machine { BiosGuid = "BIOS-1234" };
        _resolver.AreSameMachine(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSameMachine_SameSerialNumber_ReturnsTrue()
    {
        var a = new Machine { SerialNumber = "SN-ABC" };
        var b = new Machine { SerialNumber = "SN-ABC" };
        _resolver.AreSameMachine(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSameMachine_SameMacAddress_ReturnsTrue()
    {
        var a = new Machine { MacAddress = "00:11:22:33:44:55" };
        var b = new Machine { MacAddress = "00:11:22:33:44:55" };
        _resolver.AreSameMachine(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSameMachine_SameHostname_ReturnsTrue()
    {
        var a = new Machine { Hostname = "PC01" };
        var b = new Machine { Hostname = "PC01" };
        _resolver.AreSameMachine(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSameMachine_FqdnVsShortname_ReturnsTrue()
    {
        var a = new Machine { Hostname = "PC01.domain.local" };
        var b = new Machine { Hostname = "PC01" };
        _resolver.AreSameMachine(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSameMachine_DifferentMachines_ReturnsFalse()
    {
        var a = new Machine { Hostname = "PC01", SerialNumber = "SN-1", MacAddress = "AA:BB:CC" };
        var b = new Machine { Hostname = "PC02", SerialNumber = "SN-2", MacAddress = "DD:EE:FF" };
        _resolver.AreSameMachine(a, b).Should().BeFalse();
    }

    [Fact]
    public void AreSameMachine_NullIdentifiers_ReturnsFalse()
    {
        var a = new Machine();
        var b = new Machine();
        _resolver.AreSameMachine(a, b).Should().BeFalse();
    }

    [Fact]
    public void Merge_CopiesProviderIds()
    {
        var existing = new Machine { Hostname = "PC01" };
        var incoming = new Machine
        {
            Hostname = "PC01",
            CwManageDeviceId = "MGT-123",
            CwControlSessionId = "CTL-456",
            CwRmmDeviceId = "RMM-789",
            IpAddress = "192.168.1.10",
            Status = MachineStatus.Online
        };

        _resolver.Merge(existing, incoming);

        existing.CwManageDeviceId.Should().Be("MGT-123");
        existing.CwControlSessionId.Should().Be("CTL-456");
        existing.CwRmmDeviceId.Should().Be("RMM-789");
        existing.IpAddress.Should().Be("192.168.1.10");
        existing.Status.Should().Be(MachineStatus.Online);
    }

    [Fact]
    public void Merge_DoesNotOverwriteExistingManageIdWithNull()
    {
        var existing = new Machine { CwManageDeviceId = "OLD-ID" };
        var incoming = new Machine { CwControlSessionId = "CTL-NEW" };

        _resolver.Merge(existing, incoming);

        existing.CwManageDeviceId.Should().Be("OLD-ID");
        existing.CwControlSessionId.Should().Be("CTL-NEW");
    }
}
