using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConnectWiseManager.Models;
using ConnectWiseManager.Services;
using ConnectWiseManager.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectWiseManager.Views;

public partial class MainWindow : Window
{
    private readonly ApiCredentials _credentials;
    private readonly IAsioApiService _asioApiService;
    private readonly IScreenConnectApiService _screenConnectApiService;
    private readonly IReportingApiService _reportingApiService;
    private readonly ObservableCollection<Device> _devices;
    private readonly ObservableCollection<FieldDefinition> _fields;
    private readonly ObservableCollection<ScriptExecution> _logs;
    private readonly ObservableCollection<ReportingCompany> _reportingCompanies;
    private readonly ObservableCollection<ReportingSite> _reportingSites;
    private readonly AppDbContext _db;

    private ListBox? _reportingCompaniesListBox;
    private ListBox? _reportingSitesListBox;

    private CancellationTokenSource? _enrichCts;
    private DebugLogWindow? _debugLogWindow;

    private List<ScreenConnectCsvRecord> _importedScreenConnect = new();
    private List<Device> _importedAgents = new();
    private List<Device> _lastAgentsNotInSc = new();
    private List<ScreenConnectCsvRecord> _lastScNotInAgents = new();

    // Track endpoint ids to detect changes
    private HashSet<string> _lastEndpointIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly record struct ReportingSiteRequest(string? SiteCode, string? SiteId);

    public MainWindow(ApiCredentials credentials)
    {
        InitializeComponent();
        
        _credentials = credentials;
        _asioApiService = App.ServiceProvider.GetService(typeof(IAsioApiService)) as IAsioApiService 
            ?? throw new InvalidOperationException("AsioApiService not found");
        _screenConnectApiService = App.ServiceProvider.GetService(typeof(IScreenConnectApiService)) as IScreenConnectApiService 
            ?? throw new InvalidOperationException("ScreenConnectApiService not found");
        _reportingApiService = App.ServiceProvider.GetService(typeof(IReportingApiService)) as IReportingApiService 
            ?? throw new InvalidOperationException("ReportingApiService not found");
        _db = App.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext 
            ?? throw new InvalidOperationException("DbContext not found");

        // Configure Reporting API from saved credentials (API key encrypted via DPAPI)
        if (credentials.Reporting is { } rpt && !string.IsNullOrWhiteSpace(rpt.ApiKey))
        {
            _reportingApiService.Configure(rpt.ApiKey, rpt.BaseUrl);
        }

        _devices = new ObservableCollection<Device>();
        _fields = new ObservableCollection<FieldDefinition>();
        _logs = new ObservableCollection<ScriptExecution>();
        _reportingCompanies = new ObservableCollection<ReportingCompany>();
        _reportingSites = new ObservableCollection<ReportingSite>();

        AgentsDataGrid.ItemsSource = _devices;
        LogsDataGrid.ItemsSource = _logs;

        _reportingCompaniesListBox = FindName("ReportingCompaniesListBox") as ListBox;
        _reportingSitesListBox = FindName("ReportingSitesListBox") as ListBox;
        if (_reportingCompaniesListBox != null) _reportingCompaniesListBox.ItemsSource = _reportingCompanies;
        if (_reportingSitesListBox != null) _reportingSitesListBox.ItemsSource = _reportingSites;


        LoadAvailableFields();

        // Ensure database is created and upgraded
        _db.Database.EnsureCreated();
        _db.EnsureSchemaUpgrades();
    }

    private async void LoadAvailableFields()
    {
        try
        {
            var fields = await _asioApiService.GetAvailableFieldsAsync();
            _fields.Clear();
            
            foreach (var field in fields)
            {
                _fields.Add(field);
            }

            FieldsComboBox.ItemsSource = _fields;
            if (_fields.Count > 0)
            {
                FieldsComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading fields: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FieldsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // reserved
    }

    private static string NormalizeMac(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var filtered = new string(s.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToUpperInvariant();
    }

    private static bool LooksLikeGuid(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Guid.TryParse(s, out _);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.WriteLine(line);
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (EnrichStatusTextBlock != null) EnrichStatusTextBlock.Text = line;
            _debugLogWindow?.AppendLine(line);
        });
    }

    private void UpdateReportingCompanies(IEnumerable<ReportingCompany> companies)
    {
        _reportingCompanies.Clear();
        foreach (var company in companies)
        {
            _reportingCompanies.Add(company);
        }
    }

    private void UpdateReportingSites(IEnumerable<ReportingSite> sites, ReportingCompany? contextCompany = null)
    {
        var selectedId = (_reportingSitesListBox?.SelectedItem as ReportingSite)?.SiteId;
        _reportingSites.Clear();
        foreach (var site in sites)
        {
            if (contextCompany != null)
            {
                site.CompanyName ??= contextCompany.Name;
                site.CompanyId ??= contextCompany.Id;
            }
            _reportingSites.Add(site);
        }
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var existing = _reportingSites.FirstOrDefault(s => string.Equals(s.SiteId, selectedId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (_reportingSitesListBox != null) _reportingSitesListBox.SelectedItem = existing;
                return;
            }
        }
        if (_reportingSites.Count > 0)
        {
            if (_reportingSitesListBox != null) _reportingSitesListBox.SelectedIndex = 0;
        }
        else if (SiteCodeTextBox != null)
        {
            SiteCodeTextBox.Text = string.Empty;
        }
    }

    private async Task LoadSitesForCompanyAsync(ReportingCompany company)
    {
        if (string.IsNullOrWhiteSpace(company?.Id)) return;
        AppendLog($"Reporting: loading sites for {company.DisplayLabel}...");
        var sites = await _reportingApiService.GetSitesAsync(company.Id);
        UpdateReportingSites(sites, company);
        AppendLog($"Reporting: {sites.Count} site(s) loaded for {company.DisplayLabel}.");
    }

    private async void LoadAgentsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // Minimal load branch: only endpoint id + company/site
            if (MinimalLoadCheckBox?.IsChecked == true)
            {
                AppendLog("Minimal ASIO load requested (Id + Company + Site). Fetching endpoints...");
                bool tokenExpiredMin = false; string? accessTokenMin = null; DateTime? tokenExpiryMin = null;
                if (_credentials.Asio != null)
                {
                    accessTokenMin = _credentials.Asio.AccessToken;
                    tokenExpiryMin = _credentials.Asio.TokenExpiry;
                    tokenExpiredMin = tokenExpiryMin.HasValue && tokenExpiryMin.Value <= DateTime.UtcNow;
                }
                if (string.IsNullOrWhiteSpace(accessTokenMin) || tokenExpiredMin)
                {
                    AppendLog(tokenExpiredMin ? "Cannot perform minimal load: token expired." : "Cannot perform minimal load: no ASIO token.");
                    return;
                }
                var minimalDevices = await _asioApiService.GetDevicesMinimalAsync(pageLimit: 5000);
                AppendLog($"Minimal ASIO devices fetched count={minimalDevices.Count}");
                if (minimalDevices.Count == 0) { AppendLog("Minimal load produced 0 devices."); return; }
                await CommitMinimalDevicesToStaticRecordsAsync(minimalDevices);
                // Bind from static records directly
                var staticDevices = BuildDevicesFromStaticRecords(includeAll: true);
                var swStatic = Stopwatch.StartNew();
                await BindDevicesAndLogAsync(staticDevices, swStatic, offline: true);
                AppendLog("Minimal load completed – import full ASIO CSV next to enrich OS/MAC then ScreenConnect CSV for matching.");
                return;
            }

            // Static-first: use imported CSV static records first, then API only for unmatched/missing
            if (StaticFirstCheckBox?.IsChecked == true)
            {
                AppendLog("Static-first load: binding devices from imported CSV static records...");
                var staticDevices = BuildDevicesFromStaticRecords(includeAll: ShowAllDevicesCheckBox?.IsChecked == true);
                var swStatic = Stopwatch.StartNew();
                await BindDevicesAndLogAsync(staticDevices, swStatic, offline: true);
                AppendLog($"Static-first bound {staticDevices.Count} device(s). Attempting API enrichment for unmatched...");

                // Determine token state
                bool tokenExpiredSf = false; string? accessTokenSf = null; DateTime? tokenExpirySf = null;
                if (_credentials.Asio != null)
                {
                    accessTokenSf = _credentials.Asio.AccessToken;
                    tokenExpirySf = _credentials.Asio.TokenExpiry;
                    tokenExpiredSf = tokenExpirySf.HasValue && tokenExpirySf.Value <= DateTime.UtcNow;
                }
                if (string.IsNullOrWhiteSpace(accessTokenSf) || tokenExpiredSf)
                {
                    AppendLog(tokenExpiredSf ? "Cannot enrich: ASIO token expired." : "Cannot enrich: no ASIO token.");
                    return;
                }

                // Identify static records missing ASIO linkage or endpoint id or MAC
                var needEnrich = _db.StaticDeviceRecords.Where(r => !r.HasAsio || string.IsNullOrWhiteSpace(r.EndpointId) || string.IsNullOrWhiteSpace(r.E)).ToList();
                if (needEnrich.Count == 0)
                {
                    AppendLog("All static records already enriched; skipping API call.");
                    return;
                }

                // Fetch devices without detail for speed/quota
                var apiDevices = await _asioApiService.GetDevicesByCompanyPairsAsync(enrichDetails: false, maxDetailPerCompany: 0);
                if (apiDevices.Count == 0)
                {
                    AppendLog("API returned 0 devices for enrichment.");
                    return;
                }

                string Trip(Device d) => $"{NormalizeLabel(d.CompanyName)}|{NormalizeLabel(d.SiteName)}|{NormalizeLabel(d.ComputerName)}";
                string TripStatic(StaticDeviceRecord r) => $"{NormalizeLabel(r.A)}|{NormalizeLabel(r.B)}|{NormalizeLabel(r.C)}";
                var apiMap = apiDevices.GroupBy(d => Trip(d)).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int updatedSf = 0;
                foreach (var r in needEnrich)
                {
                    var normA = NormalizeLabel(r.A);
                    var normB = NormalizeLabel(r.B);
                    var normC = NormalizeLabel(r.C);
                    r.A = normA; r.B = normB; r.C = normC; // keep normalized
                    if (apiMap.TryGetValue(TripStatic(r), out var dev))
                    {
                        r.HasAsio = true;
                        if (!string.IsNullOrWhiteSpace(dev.Id)) r.EndpointId = dev.Id;
                        if (!string.IsNullOrWhiteSpace(dev.MacAddress) && string.IsNullOrWhiteSpace(r.E)) r.E = dev.MacAddress;
                        if (string.IsNullOrWhiteSpace(r.Company)) r.Company = NormalizeLabel(dev.CompanyName);
                        if (string.IsNullOrWhiteSpace(r.Site)) r.Site = NormalizeLabel(dev.SiteName);
                        if (string.IsNullOrWhiteSpace(r.FriendlyName)) r.FriendlyName = NormalizeLabel(dev.ComputerName);
                        updatedSf++;
                    }
                }
                try
                {
                    if (updatedSf > 0) { await _db.SaveChangesAsync(); AppendLog($"Static-first enrichment merged {updatedSf} record(s) from API."); }
                }
                catch (Exception ex) { AppendLog($"Static-first enrichment save failed: {ex.Message}"); }

                // Rebind
                var enrichedDevices = BuildDevicesFromStaticRecords(includeAll: ShowAllDevicesCheckBox?.IsChecked == true);
                var swRebind = Stopwatch.StartNew();
                await BindDevicesAndLogAsync(enrichedDevices, swRebind, offline: true);
                AppendLog("Static-first enrichment phase completed.");
                return;
            }

            AppendLog("Starting ASIO company-pair device load...");

            // Snapshot row count for diagnostics
            try { var snapshotCount = _db.DeviceSnapshots.Count(); AppendLog($"Local snapshot rows={snapshotCount}"); }
            catch { AppendLog("Could not read local snapshot count."); }

            // Load from local cache FIRST for fast UI bind
            var swCache = Stopwatch.StartNew();
            var cachedDevices = await LoadDevicesFromLocalCacheAsync();
            await BindDevicesAndLogAsync(cachedDevices, swCache, offline: true);
            AppendLog("Local cache bound. Checking for API refresh...");

            // Determine token state
            bool tokenExpired = false; string? accessToken = null; DateTime? tokenExpiry = null;
            if (_credentials.Asio != null)
            {
                accessToken = _credentials.Asio.AccessToken;
                tokenExpiry = _credentials.Asio.TokenExpiry;
                tokenExpired = tokenExpiry.HasValue && tokenExpiry.Value <= DateTime.UtcNow;
            }

            // If token is valid, try to map friendly company/site for cached devices and persist
            if (!string.IsNullOrWhiteSpace(accessToken) && !tokenExpired && _devices.Count > 0)
            {
                bool needsMapping = _devices.Any(d => string.IsNullOrWhiteSpace(d.CompanyName) || LooksLikeGuid(d.CompanyName) || string.IsNullOrWhiteSpace(d.SiteName) || LooksLikeGuid(d.SiteName));
                if (needsMapping)
                {
                    try
                    {
                        AppendLog("Mapping friendly company/site names for cached devices...");
                        await _asioApiService.BatchLookupCompaniesAsync(_devices, chunkSize: 200);
                        await _asioApiService.BatchLookupSitesAsync(_devices, chunkSize: 200);
                        await PersistFriendlyLabelsToSnapshotsAsync(_devices);
                        await Dispatcher.InvokeAsync(() => AgentsDataGrid.Items.Refresh());
                        AppendLog("Friendly names applied to cache and persisted.");
                    }
                    catch (Exception ex) { AppendLog($"Friendly mapping skipped: {ex.Message}"); }
                }
            }

            // If no valid token, stop after showing cache
            if (string.IsNullOrWhiteSpace(accessToken) || tokenExpired)
            {
                AppendLog(tokenExpired ? "Access token expired; staying on local cache." : "No access token; staying on local cache.");
                return;
            }

            // Rate-limit probe (informational only)
            try
            {
                var rlTask = _asioApiService.GetRateLimitAsync();
                var rlCompleted = await Task.WhenAny(rlTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (rlCompleted == rlTask)
                {
                    var rl = await rlTask;
                    var assetBucket = rl.TryGet("asset.partner-asset-endpoints-details") ?? rl.TryGet("service.rate-limit");
                    if (assetBucket != null)
                    {
                        var reset = assetBucket.Reset.HasValue ? assetBucket.Reset.Value.ToLocalTime().ToString() : "unknown";
                        AppendLog($"Rate-limit: remaining={assetBucket.Remaining}/{assetBucket.Limit}, resets={reset}");
                        if (assetBucket.Limit > 0 && assetBucket.Remaining == 0) AppendLog("Rate-limit shows remaining 0; API calls may return 429.");
                        if (assetBucket.Limit == 0 && assetBucket.Remaining == 0) AppendLog("Rate-limit bucket 0/0 (unreported); proceeding with API load.");
                    }
                    else AppendLog("Rate-limit headers unavailable; proceeding...");
                }
                else AppendLog("Rate-limit probe timed out; continuing...");
            }
            catch { }

            // Primary API load (refresh after cache)
            AppendLog("Refreshing devices from API...");
            var devices = await _asioApiService.GetDevicesByCompanyPairsAsync(companyPageLimit: 200, endpointPageLimit: 500, enrichDetails: false, maxDetailPerCompany: 0);
            if (devices.Count > 0)
            {
                try
                {
                    await _asioApiService.BatchLookupCompaniesAsync(devices, chunkSize: 200);
                    await _asioApiService.BatchLookupSitesAsync(devices, chunkSize: 200);
                }
                catch { }

                var swApi = Stopwatch.StartNew();
                await BindDevicesAndLogAsync(devices, swApi, offline: devices.All(x => string.IsNullOrWhiteSpace(x.Id)));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading agents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppendLog($"Error loading agents: {ex.Message}");
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async Task CommitMinimalDevicesToStaticRecordsAsync(List<Device> minimalDevices)
    {
        int inserted = 0, updated = 0;
        try
        {
            var existing = _db.StaticDeviceRecords.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var d in minimalDevices)
            {
                var companyRaw = d.CompanyName ?? string.Empty;
                var siteRaw = d.SiteName ?? string.Empty;
                var friendlyRaw = d.ComputerName ?? string.Empty;
                string company = NormalizeLabel(companyRaw);
                string site = NormalizeLabel(siteRaw);
                string friendly = NormalizeLabel(friendlyRaw);
                var os = string.Empty; // minimal path no OS, keep blank
                var key = StaticDeviceRecord.BuildKey(company, site, friendly, os);
                if (existing.TryGetValue(key, out var rec))
                {
                    rec.HasAsio = true;
                    if (!string.IsNullOrWhiteSpace(d.Id)) rec.EndpointId = d.Id;
                    if (!string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(rec.Company)) rec.Company = company;
                    if (!string.IsNullOrWhiteSpace(site) && string.IsNullOrWhiteSpace(rec.Site)) rec.Site = site;
                    if (!string.IsNullOrWhiteSpace(friendly) && string.IsNullOrWhiteSpace(rec.FriendlyName)) rec.FriendlyName = friendly;
                    updated++;
                }
                else
                {
                    var recNew = new StaticDeviceRecord
                    {
                        A = company,
                        B = site,
                        C = friendly,
                        D = os,
                        Key = key,
                        Company = company,
                        Site = site,
                        FriendlyName = friendly,
                        HasAsio = true,
                        HasScreenConnect = false,
                        EndpointId = d.Id
                    };
                    _db.StaticDeviceRecords.Add(recNew);
                    existing[key] = recNew;
                    inserted++;
                }
            }
            await _db.SaveChangesAsync();
            AppendLog($"Minimal commit done: inserted={inserted} updated={updated} totalStatic={existing.Count}");
            // Post-commit classification counts (servers require OS later; current all treated as workstations until OS present)
            var staticRecords = _db.StaticDeviceRecords.Count();
            AppendLog($"Static device rows now={staticRecords}");
        }
        catch (Exception ex)
        {
            AppendLog($"Minimal commit failed: {ex.Message}");
        }
    }

    private async Task PersistFriendlyLabelsToSnapshotsAsync(IEnumerable<Device> devices)
    {
        var ids = devices.Select(d => d.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var namesUpper = devices.Select(d => d.ComputerName?.ToUpperInvariant()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

        var candidateSnaps = await _db.DeviceSnapshots
            .Where(s => (s.EndpointId != null && ids.Contains(s.EndpointId)) || (s.ComputerName != null && namesUpper.Contains(s.ComputerName.ToUpper())))
            .OrderByDescending(s => s.CapturedAtUtc)
            .ToListAsync();

        var latestById = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        var latestByName = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in candidateSnaps)
        {
            if (!string.IsNullOrWhiteSpace(s.EndpointId) && !latestById.ContainsKey(s.EndpointId)) latestById[s.EndpointId] = s;
            var nameU = (s.ComputerName ?? string.Empty).ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(nameU) && !latestByName.ContainsKey(nameU)) latestByName[nameU] = s;
        }

        int updates = 0;
        foreach (var d in devices)
        {
            DeviceSnapshot? snap = null;
            if (!string.IsNullOrWhiteSpace(d.Id) && latestById.TryGetValue(d.Id, out var byId)) snap = byId;
            if (snap == null && !string.IsNullOrWhiteSpace(d.ComputerName))
            {
                var nameU = d.ComputerName.ToUpperInvariant();
                if (latestByName.TryGetValue(nameU, out var byName)) snap = byName;
            }
            if (snap == null) continue;

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(d.CompanyName) && (string.IsNullOrWhiteSpace(snap.CompanyName) || LooksLikeGuid(snap.CompanyName))) { snap.CompanyName = d.CompanyName; changed = true; }
            if (!string.IsNullOrWhiteSpace(d.SiteName) && (string.IsNullOrWhiteSpace(snap.SiteName) || LooksLikeGuid(snap.SiteName))) { snap.SiteName = d.SiteName; changed = true; }
            if (changed) updates++;
        }

        if (updates > 0) { try { await _db.SaveChangesAsync(); } catch { } }
    }

    private async Task<List<Device>> LoadDevicesFromLocalCacheAsync()
    {
        try
        {
            var snaps = await _db.DeviceSnapshots.Include(s => s.Network).OrderByDescending(s => s.CapturedAtUtc).ToListAsync();
            var byId = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in snaps)
            {
                if (string.IsNullOrWhiteSpace(s.EndpointId)) continue;
                if (!byId.ContainsKey(s.EndpointId)) byId[s.EndpointId] = s;
            }
            var devices = new List<Device>();
            foreach (var s in byId.Values)
            {
                var macs = (s.Network ?? new List<NetworkAdapterSnapshot>())
                    .Select(n => new string((n.MacAddress ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())
                    .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
                devices.Add(new Device
                {
                    Id = s.EndpointId ?? string.Empty,
                    ComputerName = s.ComputerName ?? string.Empty,
                    CompanyName = s.CompanyName ?? s.CompanyId ?? string.Empty,
                    SiteName = s.SiteName ?? s.SiteId ?? string.Empty,
                    OperatingSystem = s.OperatingSystem ?? string.Empty,
                    MacAddress = macs.FirstOrDefault() ?? string.Empty,
                    AllMacAddresses = macs,
                    LastSeen = s.CapturedAtUtc
                });
            }
            return devices;
        }
        catch (Exception ex)
        {
            AppendLog($"Local cache load failed: {ex.Message}");
            return new List<Device>();
        }
    }

    private static bool OsIsWindows(string? os) => (os ?? string.Empty).IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool OsIsMac(string? os) => (os ?? string.Empty).IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0 || (os ?? string.Empty).IndexOf("darwin", StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool OsIsLinux(string? os) => (os ?? string.Empty).IndexOf("linux", StringComparison.OrdinalIgnoreCase) >= 0;

    private async Task BindDevicesAndLogAsync(List<Device> devices, Stopwatch sw, bool offline)
    {
        int totalBeforeFilter = devices.Count;
        bool skipOsFilter = ShowAllDevicesCheckBox?.IsChecked == true;
        bool IsServerOs(string? os) => (os ?? string.Empty).IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0 && OsIsWindows(os);
        bool Include(Device d)
        {
            if (skipOsFilter) return true;
            var os = d.OperatingSystem ?? string.Empty;
            if (IsServerOs(os)) return true;
            if (OsIsWindows(os) && !IsServerOs(os)) return true;
            if (OsIsMac(os)) return true;
            if (OsIsLinux(os)) return true;
            if (string.IsNullOrWhiteSpace(os)) return true;
            return false;
        }
        var filtered = devices.Where(Include).ToList();

        if (!skipOsFilter)
        {
            // FINAL FAILSAFE only when filtering active
            if (filtered.Count == 0)
            {
                AppendLog("Primary source produced 0 devices after filtering. Attempting snapshot cache fallback...");
                try
                {
                    var fallback = await LoadDevicesFromLocalCacheAsync();
                    if (fallback.Count > 0)
                    {
                        filtered = fallback.Where(Include).ToList();
                        totalBeforeFilter = fallback.Count; // adjust metrics
                        offline = true;
                        AppendLog($"Snapshot cache provided {fallback.Count} device(s). Using fallback set.");
                    }
                    else { AppendLog("Snapshot cache also empty."); }
                }
                catch { AppendLog("Snapshot fallback failed."); }
            }
        }

        // Mark matched via static DB: assume A=Company, B=Site, C=Name, D=OS
        var staticRecordsAll = _db.StaticDeviceRecords.AsNoTracking().ToList();
        var staticMap = staticRecordsAll.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
        // Build MAC index for ScreenConnect imports (E field holds SC MAC)
        var scMacIndex = staticRecordsAll.Where(r => r.HasScreenConnect && !string.IsNullOrWhiteSpace(r.E))
            .GroupBy(r => NormalizeMac(r.E))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        string BuildKey(Device d)
        {
            string A = (d.CompanyName ?? string.Empty).Trim().ToUpperInvariant();
            string B = (d.SiteName ?? string.Empty).Trim().ToUpperInvariant();
            string C = (d.ComputerName ?? string.Empty).Trim().ToUpperInvariant();
            string D = (d.OperatingSystem ?? string.Empty).Trim().ToUpperInvariant();
            return StaticDeviceRecord.BuildKey(A, B, C, D);
        }
        var tripletGroups = staticRecordsAll.GroupBy(r => ($"{(r.A ?? string.Empty).Trim().ToUpperInvariant()}|{(r.B ?? string.Empty).Trim().ToUpperInvariant()}|{(r.C ?? string.Empty).Trim().ToUpperInvariant()}"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        string BuildTriplet(Device d)
        {
            return $"{(d.CompanyName ?? string.Empty).Trim().ToUpperInvariant()}|{(d.SiteName ?? string.Empty).Trim().ToUpperInvariant()}|{(d.ComputerName ?? string.Empty).Trim().ToUpperInvariant()}";
        }
        // Build normalized triplet lookup (ignore OS, punctuation)
        var normalizedTripletGroups = staticRecordsAll.GroupBy(r => $"{NormalizeLabel(r.A)}|{NormalizeLabel(r.B)}|{NormalizeLabel(r.C)}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        string BuildNormalizedTriplet(Device d) => $"{NormalizeLabel(d.CompanyName)}|{NormalizeLabel(d.SiteName)}|{NormalizeLabel(d.ComputerName)}";
        // Build MAC index for ASIO (now stored in E when available)
        var asioMacIndex = staticRecordsAll.Where(r => r.HasAsio && !string.IsNullOrWhiteSpace(r.E))
            .GroupBy(r => NormalizeMac(r.E))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var d in filtered)
        {
            var key = BuildKey(d);
            StaticDeviceRecord? rec = null;
            staticMap.TryGetValue(key, out rec);
            bool matched = false;
            // Triplet group ignoring OS
            var tripletKey = BuildTriplet(d);
            if (!matched && tripletGroups.TryGetValue(tripletKey, out var groupOrig) && groupOrig.Any(x=>x.HasAsio)&& groupOrig.Any(x=>x.HasScreenConnect))
            {
                matched = true;
                // Populate ScreenConnectMac from any SC record if device lacks it
                var scMac = groupOrig.Where(x=>x.HasScreenConnect && !string.IsNullOrWhiteSpace(x.E)).Select(x=>x.E).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(scMac) && string.IsNullOrWhiteSpace(d.ScreenConnectMac)) d.ScreenConnectMac = scMac;
            }
            var normTriplet = BuildNormalizedTriplet(d);
            if (!matched && normalizedTripletGroups.TryGetValue(normTriplet, out var groupNorm) && groupNorm.Any(x=>x.HasAsio) && groupNorm.Any(x=>x.HasScreenConnect))
            {
                matched = true;
                // Populate ScreenConnectMac from any SC record if device lacks it
                var scMac = groupNorm.Where(x=>x.HasScreenConnect && !string.IsNullOrWhiteSpace(x.E)).Select(x=>x.E).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(scMac) && string.IsNullOrWhiteSpace(d.ScreenConnectMac)) d.ScreenConnectMac = scMac;
            }
            if (!matched && rec != null)
            {
                if (rec.HasAsio && rec.HasScreenConnect) matched = true;
                else if (rec.HasScreenConnect && !string.IsNullOrWhiteSpace(rec.EndpointId) && string.Equals(rec.EndpointId, d.Id, StringComparison.OrdinalIgnoreCase)) matched = true;
            }
            if (!matched)
            {
                var deviceMacs = new List<string>();
                if (!string.IsNullOrWhiteSpace(d.MacAddress)) deviceMacs.Add(NormalizeMac(d.MacAddress));
                if (d.AllMacAddresses != null) deviceMacs.AddRange(d.AllMacAddresses.Select(NormalizeMac));
                foreach (var m in deviceMacs.Distinct())
                {
                    bool scHas = scMacIndex.TryGetValue(m, out var scRecs);
                    bool asioHas = asioMacIndex.TryGetValue(m, out var asioRecs);
                    if (scHas && asioHas)
                    {
                        matched = true;
                        if (string.IsNullOrWhiteSpace(d.ScreenConnectMac)) d.ScreenConnectMac = scRecs!.First().E;
                        break;
                    }
                    // NEW: MAC-only match (no need for ASIO static record yet)
                    if (scHas)
                    {
                        matched = true;
                        if (string.IsNullOrWhiteSpace(d.ScreenConnectMac)) d.ScreenConnectMac = scRecs!.First().E;
                        break;
                    }
                }
            }
            d.ScreenConnectMatched = matched;
            if (!d.ScreenConnectMatched)
            {
                var hasScMac = d.AllMacAddresses != null && d.AllMacAddresses.Any(m => scMacIndex.ContainsKey(NormalizeMac(m))) || (!string.IsNullOrWhiteSpace(d.MacAddress) && scMacIndex.ContainsKey(NormalizeMac(d.MacAddress)));
                if (hasScMac)
                {
                    AppendLog($"Match miss (MAC-only) for {d.ComputerName} MAC={NormalizeMac(d.MacAddress)} company={NormalizeLabel(d.CompanyName)} site={NormalizeLabel(d.SiteName)} friendly={NormalizeLabel(d.ComputerName)}");
                }
            }
        }

        int serverCount = filtered.Count(d => IsServerOs(d.OperatingSystem));
        int winWs = filtered.Count(d => OsIsWindows(d.OperatingSystem) && !IsServerOs(d.OperatingSystem));
        int macWs = filtered.Count(d => OsIsMac(d.OperatingSystem));
        int linuxWs = filtered.Count(d => OsIsLinux(d.OperatingSystem));
        int blankWs = filtered.Count(d => string.IsNullOrWhiteSpace(d.OperatingSystem));
        int workstationCount = winWs + macWs + linuxWs + blankWs;
        var osGroups = filtered.GroupBy(d => (d.OperatingSystem ?? "").Trim())
            .Select(g => new { OS = string.IsNullOrWhiteSpace(g.Key) ? "<blank>" : g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(6).ToList();
        var osSummary = string.Join(", ", osGroups.Select(g => $"{g.OS}={g.Count}"));
        if (skipOsFilter) AppendLog($"No OS filter applied (Show All). Raw count={devices.Count}");
        else AppendLog($"Pair-paged {(offline ? "(offline cache) " : string.Empty)}load completed in {sw.ElapsedMilliseconds} ms. Raw count={devices.Count}");
        AppendLog($"Filter: before={totalBeforeFilter}, after={filtered.Count}, servers={serverCount}, workstations={workstationCount}, win-ws={winWs}, mac-ws={macWs}, linux-ws={linuxWs}, blank-ws={blankWs}" + (skipOsFilter ? " (OS filter bypassed)" : string.Empty));
        AppendLog($"OS top: {osSummary}");
        await Dispatcher.InvokeAsync(() =>
        {
            _devices.Clear();
            foreach (var d in filtered) _devices.Add(d);
            AgentsDataGrid.ItemsSource = null;
            AgentsDataGrid.ItemsSource = _devices;
            AgentsDataGrid.Items.Refresh();
        });
        AppendLog($"Bound {_devices.Count} device(s) to grid {(offline ? "(cache)" : "(API)")}.");

        // Endpoint changes => targeted MAC enrichment
        try
        {
            var currentIds = new HashSet<string>(_devices.Select(d => d.Id).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            var added = currentIds.Except(_lastEndpointIds).ToList();
            var removed = _lastEndpointIds.Except(currentIds).ToList();
            _lastEndpointIds = currentIds;
            if ((added.Count > 0 || removed.Count > 0) && BackgroundEnrichCheckBox.IsChecked == true)
            {
                if (added.Count > 0)
                {
                    AppendLog($"Endpoints changed: +{added.Count} added, -{removed.Count} removed. Triggering MAC enrichment for added endpoints...");
                    _ = StartTargetedMacEnrichmentAsync(added);
                }
            }
        }
        catch { }

        if (!offline && BackgroundEnrichCheckBox.IsChecked == true && _devices.Count > 0)
        {
            StartBackgroundEnrichment();
        }
    }

    private async Task StartTargetedMacEnrichmentAsync(IEnumerable<string> endpointIds)
    {
        if (endpointIds == null) return;
        var ids = endpointIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return;
        int degree = _asioApiService.IsRateLimitedNow() ? 1 : 2;
        var semaphore = new SemaphoreSlim(degree);
        var tasks = new List<Task>();
        foreach (var id in ids)
        {
            await semaphore.WaitAsync();
            var task = Task.Run(async () =>
            {
                try
                {
                    var detail = await _asioApiService.GetEndpointDetailAsync(id);
                    if (detail != null)
                    {
                        var macs = (detail.Network ?? new List<NetworkAdapter>())
                            .Select(n => new string((n.MacAddress ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct()
                            .ToList();
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var dev = _devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                            if (dev != null && macs.Count > 0)
                            {
                                dev.AllMacAddresses = macs;
                                if (string.IsNullOrWhiteSpace(dev.MacAddress)) dev.MacAddress = macs.First();
                            }
                        });
                    }
                }
                catch (Exception ex) { AppendLog($"MAC enrich failed for {id}: {ex.Message}"); }
                finally { semaphore.Release(); }
            });
            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
        semaphore.Dispose();
    }

    private async void StartBackgroundEnrichment()
    {
        // Guard against rate limit/no auth
        if (!_asioApiService.HasQuota())
        {
            AppendLog("[Enrich] Skipped – no quota available.");
            return;
        }

        // Assign a new CancellationTokenSource to _enrichCts before starting enrichment
        _enrichCts = new CancellationTokenSource();
        var token = _enrichCts.Token;

        var targets = _devices.Where(d => (!string.IsNullOrWhiteSpace(d.Id)) && (string.IsNullOrWhiteSpace(d.ComputerName) || string.IsNullOrWhiteSpace(d.MacAddress))).ToList();
        if (targets.Count == 0)
        {
            AppendLog("[Enrich] Nothing to enrich.");
            return;
        }
        int enriched = 0;
        foreach (var d in targets)
        {
            if (token.IsCancellationRequested)
            {
                AppendLog("[Enrich] Cancelled by user.");
                break;
            }
            if (!_asioApiService.HasQuota())
            {
                AppendLog("[Enrich] Quota exhausted – stopping.");
                break;
            }
            try
            {
                var detail = await _asioApiService.GetEndpointDetailAsync(d.Id);
                if (detail == null)
                {
                    if (!_asioApiService.HasQuota()) break; // stop if quota now exhausted
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(detail.ComputerName)) d.ComputerName = detail.ComputerName;
                if (!string.IsNullOrWhiteSpace(detail.OperatingSystem)) d.OperatingSystem = detail.OperatingSystem;
                if (!string.IsNullOrWhiteSpace(detail.CompanyName)) d.CompanyName = detail.CompanyName!;
                if (!string.IsNullOrWhiteSpace(detail.SiteName)) d.SiteName = detail.SiteName!;
                if (detail.Network.Count > 0)
                {
                    var macs = detail.Network.Select(n => new string((n.MacAddress ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant()).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
                    if (macs.Count > 0)
                    {
                        d.AllMacAddresses = macs;
                        if (string.IsNullOrWhiteSpace(d.MacAddress)) d.MacAddress = macs.First();
                    }
                }
                enriched++;
            }
            catch (Exception ex)
            {
                AppendLog($"[Enrich] Detail failed for {d.Id}: {ex.Message}");
            }
        }
        AgentsDataGrid.Items.Refresh();
        AppendLog($"[Enrich] Completed. Enriched={enriched} RemainingQuota={_asioApiService.HasQuota()}");
    }

    // Event handler stubs
    private void CancelEnrichButton_Click(object sender, RoutedEventArgs e)
    {
        _enrichCts?.Cancel();
        AppendLog("Background enrichment cancellation requested.");
    }
    private async Task<(int inserted,int updated,int skipped)> ImportStaticAsync(string filePath, StaticSource source)
    {
        var inserted = 0; var updated = 0; var skipped = 0;
        try
        {
            bool isExcel = filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);
            // Retry helper for file copy when source locked
            async Task<string> CopyWithRetryAsync(string source)
            {
                const int maxAttempts = 3;
                for (int attempt=1; attempt<=maxAttempts; attempt++)
                {
                    var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(source));
                    try
                    {
                        using (var fs = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { /* just to test accessibility */ }
                        File.Copy(source, temp, overwrite:true);
                        return temp;
                    }
                    catch (IOException ex)
                    {
                        if (attempt == maxAttempts)
                        {
                            AppendLog($"Temp copy failed after {attempt} attempts: {ex.Message}. Using original path.");
                            return source;
                        }
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Temp copy unexpected error: {ex.Message}. Using original path.");
                        return source;
                    }
                }
                return source;
            }
            List<Dictionary<string,string>> rows = new();
            if (isExcel)
            {
                string tempPath = await CopyWithRetryAsync(filePath);
                using var wb = new XLWorkbook(tempPath);
                var ws = wb.Worksheets.First();
                var headerMap = new Dictionary<int, string>();
                var lastColUsed = ws.LastColumnUsed();
                if (lastColUsed == null)
                {
                    AppendLog("Excel import failed: No columns found in worksheet.");
                    return (0, 0, 0);
                }
                int lastCol = lastColUsed.ColumnNumber();
                for (int c = 1; c <= lastCol; c++)
                {
                    var h = ws.Cell(1, c).GetString();
                    if (!string.IsNullOrWhiteSpace(h)) headerMap[c] = h.Trim();
                }
                var lastRowUsed = ws.LastRowUsed();
                if (lastRowUsed == null)
                {
                    AppendLog("Excel import failed: No rows found in worksheet.");
                    return (0, 0, 0);
                }
                int lastRow = lastRowUsed.RowNumber();
                for (int r = 2; r <= lastRow; r++)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    bool anyNonEmpty = false;
                    foreach (var kv in headerMap)
                    {
                        var v = ws.Cell(r, kv.Key).GetString();
                        if (!string.IsNullOrWhiteSpace(v)) anyNonEmpty = true;
                        dict[kv.Value] = v?.Trim() ?? string.Empty;
                    }
                    if (anyNonEmpty) rows.Add(dict);
                }
            }
            else
            {
                string tempPath = await CopyWithRetryAsync(filePath);
                var all = await File.ReadAllLinesAsync(tempPath);
                if (all.Length<2) return (0,0,0);
                var header = SplitCsvLine(all[0]).Select(h=>h.Trim().Trim('"')).ToList();
                for (int i=1;i<all.Length;i++)
                {
                    var line = all[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = SplitCsvLine(line);
                    var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    bool any=false;
                    for (int c=0;c<header.Count && c<parts.Count;c++)
                    {
                        var v = parts[c];
                        if (!string.IsNullOrWhiteSpace(v)) any=true;
                        dict[header[c]] = v.Trim();
                    }
                    if (any) rows.Add(dict);
                }
            }
            AppendLog($"Import source={source} rows read={rows.Count}");
            var existing = _db.StaticDeviceRecords.ToDictionary(r=>r.Key, StringComparer.OrdinalIgnoreCase);
            int beforeCount = 0;
            try { beforeCount = _db.StaticDeviceRecords.Count(); AppendLog($"StaticDeviceRecords count before import={beforeCount}"); } catch { AppendLog("Could not read StaticDeviceRecords pre-import count."); }
            foreach (var dict in rows)
            {
                // replace invalid nullable 'as' expressions
                string company = "", site = "", friendly = "", os = "";
                string? mac = null, endpointId = null, sessionId = null, ipAddress = null;
                if (source==StaticSource.Asio)
                {
                    company = GetFirst(dict, "Company Friendly Name","Company Name","Company","CompanyName");
                    site = GetFirst(dict, "Site Friendly Name","Site Name","Site","SiteName");
                    friendly = GetFirst(dict, "Friendly Name","Endpoint","ComputerName","Name");
                    os = NormalizeOs(GetFirst(dict, "OS","Operating System","OperatingSystem","GuestOperatingSystemName"));
                    mac = GetFirst(dict, "MAC","MacAddress","PrimaryMacAddress","GuestMac","GuestHardwareNetworkAdapterMacAddresses","GuestHardwareNetwork","GuestHardwareNe");
                    ipAddress = GetFirst(dict, "IP Address","IPAddress","Ip","IP");
                    endpointId = GetFirst(dict, "EndpointId","Endpoint ID","Id");
                }
                else
                {
                    company = GetFirst(dict, "CustomProperty1","Company","CompanyName");
                    site = GetFirst(dict, "CustomProperty2","Site","SiteName");
                    friendly = GetFirst(dict, "Name","SessionName","FriendlyName","Friendly Name");
                    os = NormalizeOs(GetFirst(dict, "GuestOperatingSystemName","OS","Operating System","OperatingSystem"));
                    mac = GetFirst(dict, "GuestMac","MacAddress","MAC","Mac","GuestHardwareNetworkAddress","GuestHardwareNetworkAdapterMacAddresses","GuestHardwareNetwork","GuestHardwareNe","GuestMacAddresses");
                    sessionId = GetFirst(dict, "SessionID","SessionId","Id");
                }
                if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(friendly)) { skipped++; continue; }
                var key = StaticDeviceRecord.BuildKey(company, site, friendly, os);
                if (existing.TryGetValue(key, out var rec))
                {
                    if (source==StaticSource.Asio) rec.HasAsio=true; else rec.HasScreenConnect=true;
                    if (!string.IsNullOrWhiteSpace(ipAddress)) rec.IpAddress = ipAddress;
                    if (source==StaticSource.ScreenConnect && !string.IsNullOrWhiteSpace(mac)) rec.E = mac; // SC MAC
                    if (source==StaticSource.Asio && !string.IsNullOrWhiteSpace(mac) && string.IsNullOrWhiteSpace(rec.E)) rec.E = mac; // capture ASIO MAC when available
                    if (endpointId!=null) rec.EndpointId = endpointId;
                    if (sessionId!=null) rec.ScreenConnectSessionId = sessionId;
                    if (!string.IsNullOrWhiteSpace(company)) rec.Company = company;
                    if (!string.IsNullOrWhiteSpace(site)) rec.Site = site;
                    if (!string.IsNullOrWhiteSpace(friendly)) rec.FriendlyName = friendly;
                    updated++;
                }
                else
                {
                    var recNew = new StaticDeviceRecord
                    {
                        A = company,
                        B = site,
                        C = friendly,
                        D = os,
                        Key = key,
                        Company = company,
                        Site = site,
                        FriendlyName = friendly,
                        HasAsio = source==StaticSource.Asio,
                        HasScreenConnect = source==StaticSource.ScreenConnect,
                        E = (!string.IsNullOrWhiteSpace(mac) ? mac : null),
                        IpAddress = ipAddress,
                        EndpointId = endpointId,
                        ScreenConnectSessionId = sessionId
                    };
                    _db.StaticDeviceRecords.Add(recNew);
                    existing[key]=recNew;
                    inserted++;
                }
            }
            try
            {
                await _db.SaveChangesAsync();
                int afterCount = _db.StaticDeviceRecords.Count();
                AppendLog($"StaticDeviceRecords commit success. After import count={afterCount} delta={afterCount - beforeCount}");
            }
            catch (Exception ex)
            {
                AppendLog($"StaticDeviceRecords commit FAILED: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Import {source} failed: {ex.Message}");
        }
        return (inserted,updated,skipped);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder(); bool inQuotes=false;
        for (int i=0;i<line.Length;i++)
        {
            char ch=line[i];
            if (ch=='"')
            {
                if (inQuotes && i+1<line.Length && line[i+1]=='"') { sb.Append('"'); i++; }
                else inQuotes=!inQuotes;
            }
            else if (ch==',' && !inQuotes) { list.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        list.Add(sb.ToString());
        return list;
    }
    private static string GetFirst(Dictionary<string,string> dict, params string[] names)
    {
        foreach(var n in names)
        {
            if (dict.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        // normalization fallback: strip spaces, make lowercase
        var normalized = dict.ToDictionary(k => Regex.Replace(k.Key, @"\s+", "").ToLowerInvariant(), v => v.Value);
        foreach (var n in names)
        {
            var key = Regex.Replace(n, @"\s+", "").ToLowerInvariant();
            if (normalized.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return string.Empty;
    }

    private async void ImportScreenConnectCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Data Files (*.csv;*.xlsx)|*.csv;*.xlsx", Title = "Import ScreenConnect" };
        if (dlg.ShowDialog() != true) return;
        AppendLog($"ScreenConnect import file: {dlg.FileName}");
        var result = await ImportStaticAsync(dlg.FileName, StaticSource.ScreenConnect);
        AppendLog($"ScreenConnect import done: inserted={result.inserted} updated={result.updated} skipped={result.skipped}");
        StandardizeLegacyScreenConnectMapping();
        MergeStaticRecordsByMac();
        MergeStaticRecordsByTriplet();
        RefreshGridFromStaticRecords();
    }

    private async void ImportAgentsByNameCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Data Files (*.csv;*.xlsx)|*.csv;*.xlsx", Title = "Import ASIO" };
        if (dlg.ShowDialog() != true) return;
        AppendLog($"ASIO import file: {dlg.FileName}");
        var result = await ImportStaticAsync(dlg.FileName, StaticSource.Asio);
        AppendLog($"ASIO import done: inserted={result.inserted} updated={result.updated} skipped={result.skipped}");
        StandardizeLegacyScreenConnectMapping();
        MergeStaticRecordsByMac();
        MergeStaticRecordsByTriplet();
        RefreshGridFromStaticRecords();
    }
    private void FindUnmatchedButton_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Find Unmatched invoked.");
        // Simple unmatched logic placeholder
        var unmatched = _devices.Where(d => !d.ScreenConnectMatched).Count();
        AppendLog($"Unmatched count={unmatched}");
    }
    private async void ExportUnmatchedButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var unmatched = _devices.Where(d => !d.ScreenConnectMatched).ToList();
            if (unmatched.Count == 0) { AppendLog("No unmatched devices to export."); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "unmatched.csv" };
            if (dlg.ShowDialog() != true) return;
            var lines = new List<string> { "Company,Site,ComputerName,OS,MAC" };
            lines.AddRange(unmatched.Select(u => $"\"{u.CompanyName}\",\"{u.SiteName}\",\"{u.ComputerName}\",\"{u.OperatingSystem}\",\"{u.MacAddress}\""));
            await File.WriteAllLinesAsync(dlg.FileName, lines, Encoding.UTF8);
            AppendLog($"Exported {unmatched.Count} unmatched to {dlg.FileName}");
        }
        catch (Exception ex) { AppendLog($"Export unmatched failed: {ex.Message}"); }
    }
    private void DeployInstallerButton_Click(object sender, RoutedEventArgs e) => AppendLog("Deploy Installer clicked (not implemented). ");
    private void ExecuteRepairButton_Click(object sender, RoutedEventArgs e) => AppendLog("Execute Repair Script clicked (not implemented). ");
    private void CreateSessionButton_Click(object sender, RoutedEventArgs e) => AppendLog("Create Session clicked (not implemented). ");
    private void SendMessageButton_Click(object sender, RoutedEventArgs e) => AppendLog("Send Message clicked (not implemented). ");
    private void RunCustomPowerShellButton_Click(object sender, RoutedEventArgs e) => AppendLog("Run Custom PowerShell clicked (not implemented). ");
    private void BuildInstallerButton_Click(object sender, RoutedEventArgs e) => AppendLog("Build Installer Command clicked (not implemented). ");
    private async void LoadCustomFieldsButton_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Load Custom Fields clicked.");
        try
        {
            var defs = await _asioApiService.GetCustomFieldDefinitionsAsync();
            FieldMappingDataGrid.ItemsSource = defs;
            AppendLog($"Loaded {defs.Count} custom field definitions.");
        }
        catch (Exception ex) { AppendLog($"Custom field load failed: {ex.Message}"); }
    }
    private async void GetSessionDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Get Session Details clicked (not implemented). ");
        await Task.CompletedTask;
    }
    private async void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Add Note clicked (not implemented). ");
        await Task.CompletedTask;
    }
    private async void UpdatePropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Update Properties clicked (not implemented). ");
        await Task.CompletedTask;
    }

    private void MergeStaticRecordsByTriplet()
    {
        try
        {
            var records = _db.StaticDeviceRecords.ToList();
            // Group ignoring OS (D) to link ASIO and ScreenConnect even when OS strings differ
            var groups = records.GroupBy(r => ($"{(r.A ?? string.Empty).Trim().ToUpperInvariant()}|{(r.B ?? string.Empty).Trim().ToUpperInvariant()}|{(r.C ?? string.Empty).Trim().ToUpperInvariant()}"), StringComparer.OrdinalIgnoreCase);
            int mergedGroups = 0;
            foreach (var g in groups)
            {
                bool anyAsio = g.Any(r => r.HasAsio);
                bool anySc = g.Any(r => r.HasScreenConnect);
                if (anyAsio && anySc)
                {
                    mergedGroups++;
                    // Promote flags to all; propagate ScreenConnect MAC where missing
                    var scMac = g.Select(r => r.E).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                    foreach (var r in g)
                    {
                        if (!r.HasAsio) r.HasAsio = true;
                        if (!r.HasScreenConnect) r.HasScreenConnect = true;
                        if (string.IsNullOrWhiteSpace(r.E) && !string.IsNullOrWhiteSpace(scMac)) r.E = scMac;
                        // Rebuild composite key with original OS part preserved but ensure consistency of A/B/C normalization
                        r.Key = StaticDeviceRecord.BuildKey(r.A, r.B, r.C, r.D);
                    }
                }
            }
            if (mergedGroups > 0) _db.SaveChanges();
            AppendLog($"Merge by triplet completed. Groups merged={mergedGroups}");
        }
        catch (Exception ex)
        {
            AppendLog($"Triplet merge failed: {ex.Message}");
        }
    }

    private void RefreshGridFromStaticRecords()
    {
        try
        {
            var staticRecords = _db.StaticDeviceRecords.AsNoTracking().ToList();
            if (staticRecords.Count == 0)
            {
                AppendLog("Static records empty – grid unchanged.");
                return;
            }
            var macGroups = staticRecords.Where(r => !string.IsNullOrWhiteSpace(r.E))
                .GroupBy(r => NormalizeMac(r.E))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var tripletGroups = staticRecords.GroupBy(r => ($"{NormalizeLabel(r.A)}|{NormalizeLabel(r.B)}|{NormalizeLabel(r.C)}"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            bool IsMatched(StaticDeviceRecord r)
            {
                bool relaxedMac = MacOnlyMatchCheckBox?.IsChecked == true;
                if (r.HasAsio && r.HasScreenConnect) return true;
                var macKey = NormalizeMac(r.E);
                if (!string.IsNullOrWhiteSpace(macKey) && macGroups.TryGetValue(macKey, out var mg))
                {
                    if (mg.Any(x=>x.HasAsio) && mg.Any(x=>x.HasScreenConnect)) return true; // strong
                    if (relaxedMac && mg.Count > 0) return true; // mac-only when relaxed
                }
                var tripletKey = $"{NormalizeLabel(r.A)}|{NormalizeLabel(r.B)}|{NormalizeLabel(r.C)}";
                if (tripletGroups.TryGetValue(tripletKey, out var tg) && tg.Any(x => x.HasAsio) && tg.Any(x => x.HasScreenConnect)) return true;
                return false;
            }
            var list = new List<Device>();
            foreach (var r in staticRecords)
            {
                var dev = new Device
                {
                    Id = r.EndpointId ?? string.Empty,
                    CompanyName = string.IsNullOrWhiteSpace(r.Company) ? r.A : r.Company!,
                    SiteName = string.IsNullOrWhiteSpace(r.Site) ? r.B : r.Site!,
                    ComputerName = string.IsNullOrWhiteSpace(r.FriendlyName) ? r.C : r.FriendlyName!,
                    OperatingSystem = r.D,
                    MacAddress = r.E ?? string.Empty,
                    ScreenConnectMatched = IsMatched(r),
                    ScreenConnectMac = r.E
                };
                list.Add(dev);
            }
            _devices.Clear();
            foreach (var d in list) _devices.Add(d);
            AgentsDataGrid.ItemsSource = null;
            AgentsDataGrid.ItemsSource = _devices;
            AgentsDataGrid.Items.Refresh();
            AppendLog($"Grid refreshed from static records count={_devices.Count}");
        }
        catch (Exception ex)
        {
            AppendLog($"Static refresh failed: {ex.Message}");
        }
    }

    private void MergeStaticRecordsByMac()
    {
        try
        {
            var records = _db.StaticDeviceRecords.ToList();
            var macGroups = records.Where(r => !string.IsNullOrWhiteSpace(r.E))
                .GroupBy(r => NormalizeMac(r.E))
                .ToList();
            int merged = 0;
            foreach (var g in macGroups)
            {
                if (g.Count() < 2) continue;
                bool anyAsio = g.Any(r => r.HasAsio);
                bool anySc = g.Any(r => r.HasScreenConnect);
                if (anyAsio && anySc)
                {
                    foreach (var r in g)
                    {
                        if (!r.HasAsio) r.HasAsio = true;
                        if (!r.HasScreenConnect) r.HasScreenConnect = true;
                    }
                    merged++;
                }
            }
            if (merged > 0) { _db.SaveChanges(); AppendLog($"MAC merge propagated flags for {merged} MAC group(s)"); }
        }
        catch (Exception ex)
        {
            AppendLog($"MAC merge failed: {ex.Message}");
        }
    }

    private void StandardizeLegacyScreenConnectMapping()
    {
        try
        {
            var records = _db.StaticDeviceRecords.ToList();
            // Build lookup of ASIO records by (Company|Site|Friendly) ignoring OS
            var asioTriplets = new HashSet<string>(records.Where(r => r.HasAsio)
                .Select(r => ($"{(r.A ?? string.Empty).Trim().ToUpperInvariant()}|{(r.B ?? string.Empty).Trim().ToUpperInvariant()}|{(r.C ?? string.Empty).Trim().ToUpperInvariant()}")), StringComparer.OrdinalIgnoreCase);
            int corrected = 0;
            foreach (var r in records.Where(r => r.HasScreenConnect && !r.HasAsio))
            {
                var tripletCurrent = $"{(r.A ?? string.Empty).Trim().ToUpperInvariant()}|{(r.B ?? string.Empty).Trim().ToUpperInvariant()}|{(r.C ?? string.Empty).Trim().ToUpperInvariant()}";
                if (asioTriplets.Contains(tripletCurrent)) continue; // already aligned with potential ASIO triplet
                // Consider swapped A/B
                var swappedTriplet = $"{(r.B ?? string.Empty).Trim().ToUpperInvariant()}|{(r.A ?? string.Empty).Trim().ToUpperInvariant()}|{(r.C ?? string.Empty).Trim().ToUpperInvariant()}";
                if (asioTriplets.Contains(swappedTriplet))
                {
                    // Swap and adjust friendly Company/Site fields
                    var oldA = r.A; var oldB = r.B; var oldCompany = r.Company; var oldSite = r.Site;
                    r.A = oldB ?? string.Empty;
                    r.B = oldA ?? string.Empty;
                    r.Company = oldSite; // legacy stored company in CustomProperty2 (Site)
                    r.Site = oldCompany; // legacy stored site in CustomProperty1 (Company)
                    // Rebuild key maintaining OS part
                    r.Key = StaticDeviceRecord.BuildKey(
                        r.A ?? string.Empty,
                        r.B ?? string.Empty,
                        r.C ?? string.Empty,
                        r.D ?? string.Empty
                    );
                    corrected++;
                }
            }
            if (corrected > 0) { _db.SaveChanges(); AppendLog($"Legacy ScreenConnect mapping corrected (swapped company/site) count={corrected}"); }
            else AppendLog("Legacy ScreenConnect mapping correction found no swapped records.");
        }
        catch (Exception ex)
        {
            AppendLog($"Legacy mapping standardization failed: {ex.Message}");
        }
    }

    private static string NormalizeLabel(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var cleaned = Regex.Replace(s!, "[^A-Za-z0-9]+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.ToUpperInvariant();
    }

    private static string NormalizeOs(string? os)
    {
        if (string.IsNullOrWhiteSpace(os)) return string.Empty;
        var s = os.Trim();
        // Remove common build/version suffix tokens like 22H2, 24H2, 25H2
        s = Regex.Replace(s, @"\b\d{2}H2\b", "", RegexOptions.IgnoreCase);
        // Collapse multiple spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private void ClearLocalCacheButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapCount = _db.DeviceSnapshots.Count();
            var nicCount = _db.NetworkAdapters.Count();
            var nicMapCount = _db.CompanyEndpointNicSnapshots.Count();
            _db.DeviceSnapshots.RemoveRange(_db.DeviceSnapshots);
            _db.NetworkAdapters.RemoveRange(_db.NetworkAdapters);
            _db.CompanyEndpointNicSnapshots.RemoveRange(_db.CompanyEndpointNicSnapshots);
            _db.SaveChanges();
            AppendLog($"Local cache cleared: DeviceSnapshots={snapCount}, NetworkAdapters={nicCount}, CompanyEndpointNicSnapshots={nicMapCount}");
        }
        catch (Exception ex)
        {
            AppendLog($"Clear cache failed: {ex.Message}");
        }
    }

    private List<Device> BuildDevicesFromStaticRecords(bool includeAll)
    {
        var staticRecords = _db.StaticDeviceRecords.AsNoTracking().ToList();
        var list = new List<Device>();
        foreach (var r in staticRecords)
        {
            var dev = new Device
            {
                Id = r.EndpointId ?? string.Empty,
                CompanyName = string.IsNullOrWhiteSpace(r.Company) ? r.A : r.Company!,
                SiteName = string.IsNullOrWhiteSpace(r.Site) ? r.B : r.Site!,
                ComputerName = string.IsNullOrWhiteSpace(r.FriendlyName) ? r.C : r.FriendlyName!,
                OperatingSystem = r.D,
                MacAddress = r.E ?? string.Empty,
                ScreenConnectMac = r.HasScreenConnect ? r.E : null,
                ScreenConnectMatched = r.HasAsio && r.HasScreenConnect,
            };
            list.Add(dev);
        }
        if (!includeAll)
        {
            bool IsServerOs(string? os) => (os ?? string.Empty).IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0 && OsIsWindows(os);
            list = list.Where(d =>
                ShowAllDevicesCheckBox?.IsChecked == true ||
                IsServerOs(d.OperatingSystem) ||
                OsIsWindows(d.OperatingSystem) || OsIsMac(d.OperatingSystem) || OsIsLinux(d.OperatingSystem) || string.IsNullOrWhiteSpace(d.OperatingSystem)
            ).ToList();
        }
        return list;
    }

    private void ClearStaticRecordsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var count = _db.StaticDeviceRecords.Count();
            _db.StaticDeviceRecords.RemoveRange(_db.StaticDeviceRecords);
            _db.SaveChanges();
            AppendLog($"Cleared StaticDeviceRecords: removed={count}");
        }
        catch (Exception ex)
        {
            AppendLog($"Clear static records failed: {ex.Message}");
        }
    }

    private async void LoadReportingCompaniesButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reportingApiService.ApiKey))
        {
            AppendLog("Reporting: API key is not configured.");
            return;
        }

        // Load from cache first
        var cached = await _db.ReportingCompanyCache.AsNoTracking().ToListAsync();
        if (cached.Count > 0)
        {
            _reportingCompanies.Clear();
            foreach (var c in cached)
            {
                _reportingCompanies.Add(new ReportingCompany
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName ?? string.Empty,
                    Code = c.CompanyCode ?? string.Empty
                });
            }
            var oldestSync = cached.Min(c => c.LastSyncUtc);
            AppendLog($"Reporting: loaded {cached.Count} cached companies (last sync: {oldestSync:g})");
            return;
        }

        // No cache - fetch from API
        AppendLog("Reporting: fetching companies from API...");
        var companies = await _reportingApiService.GetCompaniesAsync();
        
        // Cache the results
        var now = DateTime.UtcNow;
        foreach (var company in companies)
        {
            _db.ReportingCompanyCache.Add(new ReportingCompanyCache
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                CompanyCode = company.Code,
                LastSyncUtc = now,
                CreatedAtUtc = now
            });
        }
        await _db.SaveChangesAsync();
        
        _reportingCompanies.Clear();
        foreach (var company in companies) _reportingCompanies.Add(company);
        AppendLog($"Reporting: companies fetched and cached count={companies.Count}");
    }

    private async void LoadReportingSitesButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reportingApiService.ApiKey))
        {
            AppendLog("Reporting: API key is not configured.");
            return;
        }

        var selectedCompany = ReportingCompaniesListBox?.SelectedItem as ReportingCompany;
        
        // Load from cache first
        IQueryable<ReportingSiteCache> query = _db.ReportingSiteCache.AsNoTracking();
        if (selectedCompany != null && !string.IsNullOrWhiteSpace(selectedCompany.Id))
        {
            query = query.Where(s => s.CompanyId == selectedCompany.Id);
        }
        
        var cached = await query.ToListAsync();
        if (cached.Count > 0)
        {
            var sitesFromCache = cached.Select(c => new ReportingSite
            {
                SiteId = c.SiteId,
                SiteCode = c.SiteCode,
                SiteName = c.SiteName,
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName
            }).ToList();
            
            UpdateReportingSites(sitesFromCache, selectedCompany);
            var oldestSync = cached.Min(c => c.LastSyncUtc);
            AppendLog($"Reporting: loaded {cached.Count} cached sites (last sync: {oldestSync:g})");
            return;
        }

        // No cache - fetch from API
        if (selectedCompany != null)
        {
            await LoadSitesForCompanyAsync(selectedCompany);
            return;
        }

        AppendLog("Reporting: fetching all sites from API...");
        var sites = await _reportingApiService.GetSitesAsync();
        
        // Cache the results
        var now = DateTime.UtcNow;
        foreach (var site in sites)
        {
            _db.ReportingSiteCache.Add(new ReportingSiteCache
            {
                SiteId = site.SiteId,
                SiteCode = site.SiteCode,
                SiteName = site.SiteName,
                CompanyId = site.CompanyId,
                CompanyName = site.CompanyName,
                LastSyncUtc = now,
                CreatedAtUtc = now
            });
        }
        await _db.SaveChangesAsync();
        
        UpdateReportingSites(sites);
        AppendLog($"Reporting: sites fetched and cached count={sites.Count}");
    }

    private async void ImportReportingExcelButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog 
        { 
            Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls", 
            Title = "Import Reporting Data" 
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AppendLog($"Importing Reporting data from {Path.GetFileName(dlg.FileName)}...");

            var result = await ImportReportingExcelAsync(dlg.FileName);
            AppendLog($"Import complete: {result.Companies} companies, {result.Sites} sites, {result.Agents} agents");

            // Refresh UI from cache
            var cachedCompanies = await _db.ReportingCompanyCache.AsNoTracking().ToListAsync();
            _reportingCompanies.Clear();
            foreach (var c in cachedCompanies)
            {
                _reportingCompanies.Add(new ReportingCompany
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName ?? string.Empty,
                    Code = c.CompanyCode ?? string.Empty
                });
            }

            var cachedSites = await _db.ReportingSiteCache.AsNoTracking().ToListAsync();
            var sites = cachedSites.Select(c => new ReportingSite
            {
                SiteId = c.SiteId,
                SiteCode = c.SiteCode,
                SiteName = c.SiteName,
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName
            }).ToList();
            UpdateReportingSites(sites);

            AppendLog("Reporting data loaded from cache after import.");
        }
        catch (Exception ex)
        {
            AppendLog($"Import failed: {ex.Message}");
            MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void RefreshReportingFromApiButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reportingApiService.ApiKey))
        {
            AppendLog("Reporting: API key is not configured.");
            return;
        }

        var result = MessageBox.Show(
            "This will check the API for changes to companies, sites, and agents.\n\n" +
            "This will use API quota. Continue?",
            "Refresh from API",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AppendLog("Refreshing Reporting data from API...");

            var stats = await RefreshReportingDataFromApiAsync();
            AppendLog($"Refresh complete: {stats.CompaniesUpdated} companies, {stats.SitesUpdated} sites updated");

            // Reload UI
            var cachedCompanies = await _db.ReportingCompanyCache.AsNoTracking().ToListAsync();
            _reportingCompanies.Clear();
            foreach (var c in cachedCompanies)
            {
                _reportingCompanies.Add(new ReportingCompany
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName ?? string.Empty,
                    Code = c.CompanyCode ?? string.Empty
                });
            }

            var cachedSites = await _db.ReportingSiteCache.AsNoTracking().ToListAsync();
            var sites = cachedSites.Select(c => new ReportingSite
            {
                SiteId = c.SiteId,
                SiteCode = c.SiteCode,
                SiteName = c.SiteName,
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName
            }).ToList();
            UpdateReportingSites(sites);
        }
        catch (Exception ex)
        {
            AppendLog($"Refresh failed: {ex.Message}");
            MessageBox.Show($"Refresh failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async Task<(int Companies, int Sites, int Agents)> ImportReportingExcelAsync(string filePath)
    {
        int companies = 0, sites = 0, agents = 0;
        var now = DateTime.UtcNow;

        using var wb = new XLWorkbook(filePath);
        
        // Import Companies (if sheet exists)
        if (wb.Worksheets.Contains("Companies"))
        {
            var ws = wb.Worksheet("Companies");
            var rows = ws.RangeUsed()?.RowsUsed().Skip(1); // Skip header
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var companyId = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(companyId)) continue;

                    var existing = await _db.ReportingCompanyCache.FirstOrDefaultAsync(c => c.CompanyId == companyId);
                    if (existing != null)
                    {
                        existing.CompanyName = row.Cell(2).GetString().Trim();
                        existing.CompanyCode = row.Cell(3).GetString().Trim();
                        existing.LastSyncUtc = now;
                    }
                    else
                    {
                        _db.ReportingCompanyCache.Add(new ReportingCompanyCache
                        {
                            CompanyId = companyId,
                            CompanyName = row.Cell(2).GetString().Trim(),
                            CompanyCode = row.Cell(3).GetString().Trim(),
                            LastSyncUtc = now,
                            CreatedAtUtc = now
                        });
                    }
                    companies++;
                }
            }
        }

        // Import Sites (if sheet exists)
        if (wb.Worksheets.Contains("Sites"))
        {
            var ws = wb.Worksheet("Sites");
            var rows = ws.RangeUsed()?.RowsUsed().Skip(1); // Skip header
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var siteId = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(siteId)) continue;

                    var existing = await _db.ReportingSiteCache.FirstOrDefaultAsync(s => s.SiteId == siteId);
                    if (existing != null)
                    {
                        existing.SiteCode = row.Cell(2).GetString().Trim();
                        existing.SiteName = row.Cell(3).GetString().Trim();
                        existing.CompanyId = row.Cell(4).GetString().Trim();
                        existing.CompanyName = row.Cell(5).GetString().Trim();
                        existing.LastSyncUtc = now;
                    }
                    else
                    {
                        _db.ReportingSiteCache.Add(new ReportingSiteCache
                        {
                            SiteId = siteId,
                            SiteCode = row.Cell(2).GetString().Trim(),
                            SiteName = row.Cell(3).GetString().Trim(),
                            CompanyId = row.Cell(4).GetString().Trim(),
                            CompanyName = row.Cell(5).GetString().Trim(),
                            LastSyncUtc = now,
                            CreatedAtUtc = now
                        });
                    }
                    sites++;
                }
            }
        }

        // Import Agents (if sheet exists)
        if (wb.Worksheets.Contains("Agents"))
        {
            var ws = wb.Worksheet("Agents");
            var rows = ws.RangeUsed()?.RowsUsed().Skip(1); // Skip header
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var computerName = row.Cell(1).GetString().Trim();
                    var siteCode = row.Cell(5).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(computerName)) continue;

                    var key = $"{computerName}|{siteCode}";
                    var existing = await _db.ReportingAgentCache
                        .FirstOrDefaultAsync(a => a.ComputerName == computerName && a.SiteCode == siteCode);

                    if (existing != null)
                    {
                        existing.MachineId = row.Cell(2).GetString().Trim();
                        existing.CompanyName = row.Cell(3).GetString().Trim();
                        existing.SiteName = row.Cell(4).GetString().Trim();
                        existing.SiteId = row.Cell(6).GetString().Trim();
                        existing.OperatingSystem = row.Cell(7).GetString().Trim();
                        existing.Status = row.Cell(8).GetString().Trim();
                        existing.MacAddress = row.Cell(10).GetString().Trim();
                        existing.IpAddress = row.Cell(11).GetString().Trim();
                        existing.LastSyncUtc = now;
                        existing.DataSource = "Excel";
                        
                        var lastSeenStr = row.Cell(9).GetString().Trim();
                        if (DateTime.TryParse(lastSeenStr, out var lastSeen))
                            existing.LastSeen = lastSeen;
                    }
                    else
                    {
                        var agent = new ReportingAgentCache
                        {
                            ComputerName = computerName,
                            MachineId = row.Cell(2).GetString().Trim(),
                            CompanyName = row.Cell(3).GetString().Trim(),
                            SiteName = row.Cell(4).GetString().Trim(),
                            SiteCode = siteCode,
                            SiteId = row.Cell(6).GetString().Trim(),
                            OperatingSystem = row.Cell(7).GetString().Trim(),
                            Status = row.Cell(8).GetString().Trim(),
                            MacAddress = row.Cell(10).GetString().Trim(),
                            IpAddress = row.Cell(11).GetString().Trim(),
                            LastSyncUtc = now,
                            CreatedAtUtc = now,
                            DataSource = "Excel"
                        };

                        var lastSeenStr = row.Cell(9).GetString().Trim();
                        if (DateTime.TryParse(lastSeenStr, out var lastSeen))
                            agent.LastSeen = lastSeen;

                        _db.ReportingAgentCache.Add(agent);
                    }
                    agents++;
                }
            }
        }

        await _db.SaveChangesAsync();
        return (companies, sites, agents);
    }

    private async Task<(int CompaniesUpdated, int SitesUpdated)> RefreshReportingDataFromApiAsync()
    {
        var now = DateTime.UtcNow;
        int companiesUpdated = 0, sitesUpdated = 0;

        // Refresh companies
        var apiCompanies = await _reportingApiService.GetCompaniesAsync();
        foreach (var company in apiCompanies)
        {
            var existing = await _db.ReportingCompanyCache.FirstOrDefaultAsync(c => c.CompanyId == company.Id);
            if (existing != null)
            {
                if (existing.CompanyName != company.Name || existing.CompanyCode != company.Code)
                {
                    existing.CompanyName = company.Name;
                    existing.CompanyCode = company.Code;
                    existing.LastSyncUtc = now;
                    companiesUpdated++;
                }
            }
            else
            {
                _db.ReportingCompanyCache.Add(new ReportingCompanyCache
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    CompanyCode = company.Code,
                    LastSyncUtc = now,
                    CreatedAtUtc = now
                });
                companiesUpdated++;
            }
        }

        // Refresh sites
        var apiSites = await _reportingApiService.GetSitesAsync();
        foreach (var site in apiSites)
        {
            var existing = await _db.ReportingSiteCache.FirstOrDefaultAsync(s => s.SiteId == site.SiteId);
            if (existing != null)
            {
                if (existing.SiteCode != site.SiteCode || 
                    existing.SiteName != site.SiteName || 
                    existing.CompanyId != site.CompanyId)
                {
                    existing.SiteCode = site.SiteCode;
                    existing.SiteName = site.SiteName;
                    existing.CompanyId = site.CompanyId;
                    existing.CompanyName = site.CompanyName;
                    existing.LastSyncUtc = now;
                    sitesUpdated++;
                }
            }
            else
            {
                _db.ReportingSiteCache.Add(new ReportingSiteCache
                {
                    SiteId = site.SiteId,
                    SiteCode = site.SiteCode,
                    SiteName = site.SiteName,
                    CompanyId = site.CompanyId,
                    CompanyName = site.CompanyName,
                    LastSyncUtc = now,
                    CreatedAtUtc = now
                });
                sitesUpdated++;
            }
        }

        await _db.SaveChangesAsync();
        return (companiesUpdated, sitesUpdated);
    }

    private async void LoadReportingAgentsButton_Click(object sender, RoutedEventArgs e)
    {
        // Load all cached agents from database
        var cached = await _db.ReportingAgentCache.AsNoTracking().ToListAsync();
        
        if (cached.Count == 0)
        {
            AppendLog("Reporting: no cached agent data. Import Excel first or use 'Refresh from API'.");
            return;
        }

        var devices = cached.Select(a => new Device
        {
            Id = a.MachineId ?? string.Empty,
            ComputerName = a.ComputerName,
            CompanyName = a.CompanyName ?? string.Empty,
            SiteName = a.SiteName ?? string.Empty,
            OperatingSystem = a.OperatingSystem ?? string.Empty,
            Status = a.Status ?? string.Empty,
            LastSeen = a.LastSeen,
            MacAddress = a.MacAddress ?? string.Empty
        }).ToList();

        await Dispatcher.InvokeAsync(() =>
        {
            _devices.Clear();
            foreach (var device in devices)
            {
                _devices.Add(device);
            }
            AgentsDataGrid.ItemsSource = null;
            AgentsDataGrid.ItemsSource = _devices;
            AgentsDataGrid.Items.Refresh();
        });

        var oldestSync = cached.Min(a => a.LastSyncUtc);
        AppendLog($"Reporting: loaded {cached.Count} cached agents (last sync: {oldestSync:g}, source: {cached.FirstOrDefault()?.DataSource})");
    }

    private async void LoadReportingAgentsForSiteButton_Click(object sender, RoutedEventArgs e)
    {
        var siteCodeInput = SiteCodeTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(siteCodeInput))
        {
            AppendLog("Reporting: enter a SiteCode to fetch agent details.");
            return;
        }

        // Load from cache filtered by site
        var cached = await _db.ReportingAgentCache
            .AsNoTracking()
            .Where(a => a.SiteCode == siteCodeInput || a.SiteId == siteCodeInput || a.SiteName == siteCodeInput)
            .ToListAsync();

        if (cached.Count == 0)
        {
            AppendLog($"Reporting: no cached agents for site '{siteCodeInput}'. Use 'Refresh from API' to fetch.");
            return;
        }

        var devices = cached.Select(a => new Device
        {
            Id = a.MachineId ?? string.Empty,
            ComputerName = a.ComputerName,
            CompanyName = a.CompanyName ?? string.Empty,
            SiteName = a.SiteName ?? string.Empty,
            OperatingSystem = a.OperatingSystem ?? string.Empty,
            Status = a.Status ?? string.Empty,
            LastSeen = a.LastSeen,
            MacAddress = a.MacAddress ?? string.Empty
        }).ToList();

        await Dispatcher.InvokeAsync(() =>
        {
            _devices.Clear();
            foreach (var device in devices)
            {
                _devices.Add(device);
            }
            AgentsDataGrid.ItemsSource = null;
            AgentsDataGrid.ItemsSource = _devices;
            AgentsDataGrid.Items.Refresh();
        });

        AppendLog($"Reporting: loaded {cached.Count} cached agents for site '{siteCodeInput}'");
    }

    private async void SaveReportingApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ReportingApiKeyTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AppendLog("Reporting: API key cannot be empty.");
            return;
        }

        _reportingApiService.Configure(apiKey);
        _credentials.Reporting = new ReportingCredentials
        {
            ApiKey = apiKey,
            BaseUrl = _reportingApiService.BaseUrl
        };

        try
        {
            var credService = App.ServiceProvider.GetService(typeof(ICredentialService)) as ICredentialService;
            if (credService != null)
            {
                await credService.SaveCredentialsAsync(_credentials);
                AppendLog("Reporting: API key saved securely via DPAPI.");
            }
            else
            {
                AppendLog("Reporting: API key configured for this session (credential service unavailable for persist).");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Reporting: API key configured for this session but persist failed: {ex.Message}");
        }
    }

    private void OpenDebugLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (_debugLogWindow != null)
        {
            if (_debugLogWindow.WindowState == WindowState.Minimized)
            {
                _debugLogWindow.WindowState = WindowState.Normal;
            }
            _debugLogWindow.Activate();
            return;
        }

        _debugLogWindow = new DebugLogWindow
        {
            Owner = this
        };
        _debugLogWindow.Closed += (_, _) => _debugLogWindow = null;
        _debugLogWindow.Show();
    }

    private void ReportingCompaniesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // Clear sites when company selection changes
        _reportingSites.Clear();
        SiteCodeTextBox.Text = string.Empty;
    }

    private void ReportingSitesListBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ReportingSitesListBox.SelectedItem is not ReportingSite site) return;

        var code = site.SiteCode ?? site.SiteName ?? string.Empty;
        SiteCodeTextBox.Text = code;
        SiteCodeTextBox.ToolTip = string.IsNullOrWhiteSpace(site.SiteName) ? site.SiteId : $"{site.SiteName} ({site.SiteId})";
    }

    private async Task<IEnumerable<ReportingAgentCache>> LoadReportingAgentsInternalAsync()
    {
        // Remove this method - no longer needed with cache-first approach
        await Task.CompletedTask;
        return Enumerable.Empty<ReportingAgentCache>();
    }
}
