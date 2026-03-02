using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MspTools.App.Data;
using MspTools.Connectors;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.App.ViewModels;

/// <summary>
/// Primary ViewModel for <see cref="Views.MainWindow"/>.
/// Manages API connections, coordinates data ingestion via connectors,
/// and exposes searched/filtered views of the unified data container.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IDataContainer _container;
    private readonly AppSettings _settings;
    private LocalRepository? _repository;

    // ── Tab state ──────────────────────────────────────────────────────────
    private bool _showConnections = true;
    private bool _showDevices;
    private bool _showCompanies;
    private bool _showMatches;

    // ── Busy / status ──────────────────────────────────────────────────────
    private bool _isBusy;
    private string _statusMessage = "Ready. Add connections to get started.";

    public MainViewModel() : this(new DataContainer(), AppSettings.Load()) { }

    public MainViewModel(IDataContainer container, AppSettings settings)
    {
        _container = container;
        _settings = settings;
        _container.DataChanged += (_, _) => RefreshDisplayCollections();

        SyncAllCommand = new AsyncRelayCommand(SyncAllAsync, () => IsNotBusy);
        ComputeMatchesCommand = new RelayCommand(ComputeMatches, () => IsNotBusy);
        SearchDevicesCommand = new RelayCommand(ExecuteDeviceSearch);
        SearchCompaniesCommand = new RelayCommand(ExecuteCompanySearch);
        TestConnectionCommand = new AsyncRelayCommand<ApiConnectionViewModel>(TestConnectionAsync);
        RemoveConnectionCommand = new RelayCommand<ApiConnectionViewModel>(RemoveConnection);
        OpenAddConnectionDialogCommand = new RelayCommand(OpenAddConnectionDialog);
        OpenSettingsCommand = new RelayCommand(OpenSettings);

        NewConnection = new NewConnectionFormViewModel();

        // Load persisted data in the background after startup
        _ = LoadFromDatabaseAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Tab navigation
    // ══════════════════════════════════════════════════════════════════════
    public bool ShowConnections { get => _showConnections; set { SetProperty(ref _showConnections, value); OnPropertyChanged(nameof(ConnectionsVisibility)); } }
    public bool ShowDevices { get => _showDevices; set { SetProperty(ref _showDevices, value); OnPropertyChanged(nameof(DevicesVisibility)); } }
    public bool ShowCompanies { get => _showCompanies; set { SetProperty(ref _showCompanies, value); OnPropertyChanged(nameof(CompaniesVisibility)); } }
    public bool ShowMatches { get => _showMatches; set { SetProperty(ref _showMatches, value); OnPropertyChanged(nameof(MatchesVisibility)); } }

    public Visibility ConnectionsVisibility => _showConnections ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DevicesVisibility => _showDevices ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CompaniesVisibility => _showCompanies ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MatchesVisibility => _showMatches ? Visibility.Visible : Visibility.Collapsed;

    // ══════════════════════════════════════════════════════════════════════
    // Busy / status
    // ══════════════════════════════════════════════════════════════════════
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(BusyVisibility)); } }
    public bool IsNotBusy => !_isBusy;
    public Visibility BusyVisibility => _isBusy ? Visibility.Visible : Visibility.Collapsed;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    // ══════════════════════════════════════════════════════════════════════
    // Collections
    // ══════════════════════════════════════════════════════════════════════
    public ObservableCollection<ApiConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<UnifiedDevice> Devices { get; } = new();
    public ObservableCollection<CompanyViewModel> Companies { get; } = new();
    public ObservableCollection<MatchViewModel> Matches { get; } = new();

    // Filtered views (updated after search)
    public ObservableCollection<UnifiedDevice> FilteredDevices { get; } = new();
    public ObservableCollection<CompanyViewModel> FilteredCompanies { get; } = new();

    // ══════════════════════════════════════════════════════════════════════
    // New connection form
    // ══════════════════════════════════════════════════════════════════════
    public NewConnectionFormViewModel NewConnection { get; }
    public DeviceSearchCriteria DeviceSearch { get; } = new();
    public CompanySearchCriteria CompanySearch { get; } = new();

    public IEnumerable<ConnectorType> ConnectorTypes { get; } =
        Enum.GetValues<ConnectorType>().Where(t => t != ConnectorType.Custom);

    public IEnumerable<AuthMethodType> AuthMethodTypes { get; } = Enum.GetValues<AuthMethodType>();

    // ══════════════════════════════════════════════════════════════════════
    // Commands
    // ══════════════════════════════════════════════════════════════════════
    public AsyncRelayCommand SyncAllCommand { get; }
    public RelayCommand ComputeMatchesCommand { get; }
    public RelayCommand SearchDevicesCommand { get; }
    public RelayCommand SearchCompaniesCommand { get; }
    public AsyncRelayCommand<ApiConnectionViewModel> TestConnectionCommand { get; }
    public RelayCommand<ApiConnectionViewModel> RemoveConnectionCommand { get; }
    public RelayCommand OpenAddConnectionDialogCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    // ══════════════════════════════════════════════════════════════════════
    // Command implementations
    // ══════════════════════════════════════════════════════════════════════
    private async Task SyncAllAsync()
    {
        if (_isBusy) return;
        IsBusy = true;
        StatusMessage = "Syncing all connections…";

        try
        {
            var enabled = Connections.Where(c => c.IsEnabled).ToList();
            if (enabled.Count == 0)
            {
                StatusMessage = "No enabled connections to sync.";
                return;
            }

            int synced = 0;
            foreach (var conn in enabled)
            {
                StatusMessage = $"Syncing '{conn.Name}'…";
                try
                {
                    var connector = ConnectorFactory.Create(conn.Model);
                    var devices = await connector.FetchDevicesAsync();
                    var companies = await connector.FetchCompaniesAsync();
                    _container.IngestDevices(devices);
                    _container.IngestCompanies(companies);
                    conn.LastSyncUtc = DateTime.UtcNow;
                    synced++;

                    // Persist to MySQL in the background (non-blocking)
                    if (_repository is not null)
                    {
                        var platform = connector.Name;
                        _ = Task.Run(async () =>
                        {
                            await _repository.SaveDevicesAsync(devices, platform);
                            await _repository.SaveCompaniesAsync(companies, platform);
                        });
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error syncing '{conn.Name}': {ex.Message}";
                    await Task.Delay(1500);
                }
            }

            _container.ComputeMatches();
            StatusMessage = $"Sync complete. {synced}/{enabled.Count} connections updated.";

            // Save match summary to MySQL
            if (_repository is not null)
            {
                var matchVMs = Matches.ToList();
                _ = Task.Run(() => _repository.SaveMatchesAsync(matchVMs));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFromDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.MySqlConnectionString)) return;
        try
        {
            _repository = new LocalRepository(_settings.MySqlConnectionString);
            await _repository.EnsureSchemaAsync();

            var devices = await _repository.LoadDevicesAsync();
            var companies = await _repository.LoadCompaniesAsync();

            if (devices.Count > 0 || companies.Count > 0)
            {
                _container.IngestDevices(devices);
                _container.IngestCompanies(companies);
                _container.ComputeMatches();
                StatusMessage = $"Loaded {devices.Count} devices and {companies.Count} companies from database.";
            }
        }
        catch (Exception ex)
        {
            // DB not available — app still works without it
            StatusMessage = $"Database unavailable: {ex.Message}";
            _repository = null;
        }
    }

    private void OpenSettings()
    {
        var dialog = new Views.SettingsDialog(_settings.MySqlConnectionString)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true) return;

        _settings.MySqlConnectionString = dialog.ConnectionString;
        _settings.Save();

        // Re-initialise the repository with the new connection string
        _ = LoadFromDatabaseAsync();
        StatusMessage = "Database settings saved.";
    }

    private void ComputeMatches()
    {
        _container.ComputeMatches();
        StatusMessage = $"Match computation complete. {_container.Matches.Count} matches found.";
        ShowMatches = true;
    }

    private void ExecuteDeviceSearch()
    {
        var results = _container.SearchDevices(
            string.IsNullOrWhiteSpace(DeviceSearch.ComputerName) ? null : DeviceSearch.ComputerName,
            string.IsNullOrWhiteSpace(DeviceSearch.AgentName) ? null : DeviceSearch.AgentName,
            string.IsNullOrWhiteSpace(DeviceSearch.CompanyName) ? null : DeviceSearch.CompanyName,
            string.IsNullOrWhiteSpace(DeviceSearch.SiteName) ? null : DeviceSearch.SiteName);

        FilteredDevices.Clear();
        foreach (var d in results) FilteredDevices.Add(d);
        StatusMessage = $"Device search returned {results.Count} result(s).";
    }

    private void ExecuteCompanySearch()
    {
        var results = _container.SearchCompanies(
            string.IsNullOrWhiteSpace(CompanySearch.CompanyName) ? null : CompanySearch.CompanyName,
            string.IsNullOrWhiteSpace(CompanySearch.SiteName) ? null : CompanySearch.SiteName);

        FilteredCompanies.Clear();
        foreach (var c in results) FilteredCompanies.Add(new CompanyViewModel(c));
        StatusMessage = $"Company search returned {results.Count} result(s).";
    }

    private async Task TestConnectionAsync(ApiConnectionViewModel? vm)
    {
        if (vm is null) return;
        IsBusy = true;
        StatusMessage = $"Testing connection '{vm.Name}'…";
        try
        {
            var connector = ConnectorFactory.Create(vm.Model);
            var ok = await connector.TestConnectionAsync();
            StatusMessage = ok
                ? $"✔ Connection '{vm.Name}' is healthy."
                : $"✖ Connection '{vm.Name}' test failed – check credentials and URL.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error testing '{vm.Name}': {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RemoveConnection(ApiConnectionViewModel? vm)
    {
        if (vm is null) return;
        Connections.Remove(vm);
        StatusMessage = $"Connection '{vm.Name}' removed.";
    }

    private void OpenAddConnectionDialog()
    {
        var dialog = new Views.AddConnectionDialog(NewConnection)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        // Build auth method from credentials entered in the dialog
        AuthMethod auth = NewConnection.SelectedAuthType switch
        {
            AuthMethodType.ConnectWiseApiKey => new ConnectWiseApiKeyAuth(
                NewConnection.CompanyId, NewConnection.PublicKey,
                NewConnection.PrivateKey, NewConnection.ClientId),
            AuthMethodType.BasicAuth => new BasicAuth(NewConnection.Username, NewConnection.Password),
            AuthMethodType.ClientCredentials => new ClientCredentialsAuth(NewConnection.ClientId, NewConnection.ClientSecret, NewConnection.Scope),
            AuthMethodType.ApiKey => new ApiKeyAuth(NewConnection.ApiKey, NewConnection.ApiKeyHeader),
            AuthMethodType.BearerToken => new BearerTokenAuth(NewConnection.BearerToken),
            _ => new ApiKeyAuth(NewConnection.ApiKey)
        };

        var connection = new ApiConnection
        {
            Name = string.IsNullOrWhiteSpace(NewConnection.Name)
                ? $"{NewConnection.ConnectorType} {Connections.Count + 1}"
                : NewConnection.Name,
            ConnectorType = NewConnection.ConnectorType,
            BaseUrl = NewConnection.BaseUrl,
            Auth = auth,
            IsEnabled = true,
        };

        Connections.Add(new ApiConnectionViewModel(connection));
        StatusMessage = $"Connection '{connection.Name}' added. Test it or click Sync All to fetch data.";
        NewConnection.Reset();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Refresh display collections from container
    // ══════════════════════════════════════════════════════════════════════
    private void RefreshDisplayCollections()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Devices.Clear();
            foreach (var d in _container.Devices) Devices.Add(d);

            Companies.Clear();
            foreach (var c in _container.Companies) Companies.Add(new CompanyViewModel(c));

            Matches.Clear();
            foreach (var m in _container.Matches) Matches.Add(new MatchViewModel(m));

            // Only re-run filtered searches when criteria are actively set
            bool hasDeviceCriteria = !string.IsNullOrWhiteSpace(DeviceSearch.ComputerName)
                || !string.IsNullOrWhiteSpace(DeviceSearch.AgentName)
                || !string.IsNullOrWhiteSpace(DeviceSearch.CompanyName)
                || !string.IsNullOrWhiteSpace(DeviceSearch.SiteName);

            bool hasCompanyCriteria = !string.IsNullOrWhiteSpace(CompanySearch.CompanyName)
                || !string.IsNullOrWhiteSpace(CompanySearch.SiteName);

            if (hasDeviceCriteria) ExecuteDeviceSearch();
            if (hasCompanyCriteria) ExecuteCompanySearch();

            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(Companies));
            OnPropertyChanged(nameof(Matches));
        });
    }
}

/// <summary>Async RelayCommand with typed parameter.</summary>
public sealed class AsyncRelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => !_isExecuting && (_canExecute?.Invoke(p is T t ? t : default) ?? true);

    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(p is T t ? t : default);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
public sealed class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke(p is T t ? t : default) ?? true;
    public void Execute(object? p) => _execute(p is T t ? t : default);
}
