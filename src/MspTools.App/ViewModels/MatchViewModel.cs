using MspTools.Core.Models;

namespace MspTools.App.ViewModels;

/// <summary>Wraps <see cref="CrossPlatformMatch"/> for display in the Matches grid.</summary>
public sealed class MatchViewModel
{
    private readonly CrossPlatformMatch _match;

    public MatchViewModel(CrossPlatformMatch match) => _match = match;

    public string MatchKey => _match.MatchKey;
    public MatchType MatchType => _match.MatchType;
    public int DeviceCount => _match.Devices.Count;
    public int CompanyCount => _match.Companies.Count;
    public double ConfidenceScore => _match.ConfidenceScore;
    public DateTime MatchedAtUtc => _match.MatchedAtUtc;

    public string PlatformList =>
        string.Join(", ",
            _match.Devices.Select(d => d.SourcePlatform)
            .Concat(_match.Companies.Select(c => c.SourcePlatform))
            .Distinct());
}
