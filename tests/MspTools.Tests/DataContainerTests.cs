using MspTools.Core.Models;
using MatchType = MspTools.Core.Models.MatchType;

namespace MspTools.Tests;

public class DataContainerTests
{
    private static UnifiedDevice MakeDevice(string computerName, string companyName = "",
        string agentName = "", string siteName = "", string platform = "Platform A", Guid? connId = null)
        => new()
        {
            ComputerName = computerName,
            CompanyName = companyName,
            AgentName = agentName,
            SiteName = siteName,
            SourcePlatform = platform,
            SourceId = Guid.NewGuid().ToString(),
            ApiConnectionId = connId ?? Guid.NewGuid(),
        };

    private static UnifiedCompany MakeCompany(string companyName, string platform = "Platform A",
        Guid? connId = null, params string[] sites)
        => new()
        {
            CompanyName = companyName,
            SourcePlatform = platform,
            SourceId = Guid.NewGuid().ToString(),
            ApiConnectionId = connId ?? Guid.NewGuid(),
            SiteNames = sites.ToList(),
        };

    [Fact]
    public void IngestDevices_AddsDevicesToContainer()
    {
        var container = new DataContainer();
        container.IngestDevices(new[] { MakeDevice("PC-001"), MakeDevice("PC-002") });

        Assert.Equal(2, container.Devices.Count);
    }

    [Fact]
    public void IngestDevices_ReplacesExistingRecordsFromSameSource()
    {
        var container = new DataContainer();
        var connId = Guid.NewGuid();
        var sourceId = "src-1";

        var original = MakeDevice("PC-OLD");
        original.ApiConnectionId = connId;
        original.SourceId = sourceId;

        var updated = MakeDevice("PC-NEW");
        updated.ApiConnectionId = connId;
        updated.SourceId = sourceId;

        container.IngestDevices(new[] { original });
        container.IngestDevices(new[] { updated });

        Assert.Single(container.Devices);
        Assert.Equal("PC-NEW", container.Devices[0].ComputerName);
    }

    [Fact]
    public void SearchDevices_FiltersByComputerName()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("SERVER-01"), MakeDevice("WORKSTATION-01"), MakeDevice("SERVER-02")
        });

        var results = container.SearchDevices(computerName: "SERVER");

        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Contains("SERVER", d.ComputerName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchDevices_FiltersByCompanyName()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("PC-1", companyName: "Acme Corp"),
            MakeDevice("PC-2", companyName: "Globex"),
            MakeDevice("PC-3", companyName: "Acme Industries"),
        });

        var results = container.SearchDevices(companyName: "Acme");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void SearchDevices_FiltersByAgentName()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("PC-1", agentName: "cw-agent-001"),
            MakeDevice("PC-2", agentName: "agent-xyz"),
        });

        var results = container.SearchDevices(agentName: "cw-agent");

        Assert.Single(results);
    }

    [Fact]
    public void SearchDevices_FiltersBySiteName()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("PC-1", siteName: "Main Office"),
            MakeDevice("PC-2", siteName: "Branch Office"),
            MakeDevice("PC-3", siteName: "Main Warehouse"),
        });

        var results = container.SearchDevices(siteName: "Main");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void SearchDevices_MultipleCriteria_IsAndLogic()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("PC-1", companyName: "Acme", siteName: "Main"),
            MakeDevice("PC-2", companyName: "Acme", siteName: "Branch"),
            MakeDevice("PC-3", companyName: "Globex", siteName: "Main"),
        });

        // Only PC-1 matches both
        var results = container.SearchDevices(companyName: "Acme", siteName: "Main");

        Assert.Single(results);
        Assert.Equal("PC-1", results[0].ComputerName);
    }

    [Fact]
    public void SearchCompanies_FiltersByCompanyName()
    {
        var container = new DataContainer();
        container.IngestCompanies(new[]
        {
            MakeCompany("Acme Corp"),
            MakeCompany("Globex"),
            MakeCompany("Acme Industries"),
        });

        var results = container.SearchCompanies(companyName: "acme");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void SearchCompanies_FiltersBySiteName()
    {
        var container = new DataContainer();
        container.IngestCompanies(new[]
        {
            MakeCompany("Acme", "Platform A", null, "Main Office", "Branch"),
            MakeCompany("Globex", "Platform A", null, "HQ"),
        });

        var results = container.SearchCompanies(siteName: "Main Office");

        Assert.Single(results);
        Assert.Equal("Acme", results[0].CompanyName);
    }

    [Fact]
    public void ComputeMatches_FindsDevicesByComputerNameAcrossPlatforms()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("SERVER-01", platform: "ConnectWise Manage"),
            MakeDevice("SERVER-01", platform: "ConnectWise Asio (RMM)"),
            MakeDevice("WORKSTATION-01", platform: "ConnectWise Manage"), // no pair
        });

        container.ComputeMatches();

        Assert.Single(container.Matches);
        Assert.Equal("SERVER-01", container.Matches[0].MatchKey);
        Assert.Equal(MatchType.ComputerName, container.Matches[0].MatchType);
        Assert.Equal(2, container.Matches[0].Devices.Count);
    }

    [Fact]
    public void ComputeMatches_FindsCompaniesByNameAcrossPlatforms()
    {
        var container = new DataContainer();
        container.IngestCompanies(new[]
        {
            MakeCompany("Acme Corp", platform: "ConnectWise Manage"),
            MakeCompany("Acme Corp", platform: "ConnectWise Asio (RMM)"),
        });

        container.ComputeMatches();

        Assert.Single(container.Matches);
        Assert.Equal(MatchType.CompanyName, container.Matches[0].MatchType);
    }

    [Fact]
    public void ComputeMatches_DoesNotMatchDevicesFromSamePlatform()
    {
        var container = new DataContainer();
        container.IngestDevices(new[]
        {
            MakeDevice("SERVER-01", platform: "ConnectWise Manage"),
            MakeDevice("SERVER-01", platform: "ConnectWise Manage"), // same platform, not a cross-platform match
        });

        container.ComputeMatches();

        Assert.Empty(container.Matches);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        var container = new DataContainer();
        container.IngestDevices(new[] { MakeDevice("PC-1") });
        container.IngestCompanies(new[] { MakeCompany("Acme") });
        container.ComputeMatches();

        container.Clear();

        Assert.Empty(container.Devices);
        Assert.Empty(container.Companies);
        Assert.Empty(container.Matches);
    }

    [Fact]
    public void DataChanged_EventFiredOnIngestDevices()
    {
        var container = new DataContainer();
        bool fired = false;
        container.DataChanged += (_, _) => fired = true;

        container.IngestDevices(new[] { MakeDevice("PC-1") });

        Assert.True(fired);
    }

    [Fact]
    public void DataChanged_EventFiredOnClear()
    {
        var container = new DataContainer();
        bool fired = false;
        container.DataChanged += (_, _) => fired = true;

        container.Clear();

        Assert.True(fired);
    }

    [Fact]
    public void IngestDevices_ThrowsOnNull()
    {
        var container = new DataContainer();
        Assert.Throws<ArgumentNullException>(() => container.IngestDevices(null!));
    }
}
