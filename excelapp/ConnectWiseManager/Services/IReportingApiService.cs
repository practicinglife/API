using ConnectWiseManager.Models;
using System.Threading;
using System.Net.Http;

namespace ConnectWiseManager.Services
{
    public interface IReportingApiService
    {
        string? ApiKey { get; }
        string BaseUrl { get; }
        void Configure(string? apiKey, string? baseUrl = null);
        Task<List<ReportingCompany>> GetCompaniesAsync(CancellationToken ct = default);
        Task<List<ReportingSite>> GetSitesAsync(string? companyId = null, CancellationToken ct = default);
        Task<List<Device>> GetAgentDetailsAsync(string siteCode, string? siteId = null, CancellationToken ct = default);
    }
}