using MspTools.Core.Models;

namespace MspTools.App.ViewModels;

/// <summary>Wraps <see cref="UnifiedCompany"/> with a computed SiteCount for the grid.</summary>
public sealed class CompanyViewModel
{
    private readonly UnifiedCompany _company;
    public CompanyViewModel(UnifiedCompany company) => _company = company;

    public string CompanyName => _company.CompanyName;
    public string? CompanyIdentifier => _company.CompanyIdentifier;
    public string SourcePlatform => _company.SourcePlatform;
    public string? City => _company.City;
    public string? State => _company.State;
    public string? PhoneNumber => _company.PhoneNumber;
    public int SiteCount => _company.SiteNames.Count;
}
