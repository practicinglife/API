using ConnectWiseManager.Models;
using System.Threading;

namespace ConnectWiseManager.Services;

public interface IAsioApiService
{
    Task<bool> AuthenticateAsync(AsioCredentials credentials);
    Task<List<FieldDefinition>> GetAvailableFieldsAsync();
    Task<List<Device>> GetDevicesAsync(List<string>? selectedFields = null);
    /// <summary>
    /// Loads endpoints two companies at a time using cursor pagination for companies and endpoints.
    /// When enrichDetails is true and maxDetailPerCompany &lt;= 0, all endpoints for each company are detail-enriched
    /// before moving to the next pair (friendly name, OS, company/site labels, network MACs).
    /// </summary>
    Task<List<Device>> GetDevicesByCompanyPairsAsync(int companyPageLimit = 200, int endpointPageLimit = 500, bool enrichDetails = true, int maxDetailPerCompany = 0, CancellationToken ct = default);
    Task<List<FieldDefinition>> GetCustomFieldDefinitionsAsync();
    Task<Dictionary<string, string>> GetCustomFieldValuesAsync(string deviceId);
    Task<EndpointDetail?> GetEndpointDetailAsync(string endpointId);
    Task<RateLimitInfo> GetRateLimitAsync();
    bool IsRateLimitedNow();
    Task<int> BatchEnrichFromMappingAsync(IEnumerable<Device> devices, int chunkSize = 50);
    Task<int> BatchLookupCompaniesAsync(IEnumerable<Device> devices, int chunkSize = 50);
    Task<int> BatchLookupSitesAsync(IEnumerable<Device> devices, int chunkSize = 50);
    bool SupportsMappingsLookup { get; }

    Task<List<CompanyEndpointActiveNic>> GetCompanyActiveNicsAsync(string companyKey, bool keyIsId = true, int pageLimit = 500, CancellationToken ct = default);
    Task<(int inserted, int updated)> SaveCompanyActiveNicsAsync(IEnumerable<CompanyEndpointActiveNic> rows);
    Task<(int inserted, int updated)> SyncCompanyActiveNicsAsync(string companyKey, bool keyIsId = true, int pageLimit = 500, CancellationToken ct = default);
    bool HasQuota();

    // Minimal devices: only Id + Company/Site (+ optional friendly if present)
    Task<List<Device>> GetDevicesMinimalAsync(int pageLimit = 500, CancellationToken ct = default);
}
