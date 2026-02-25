using CwAssetManager.ApiClients.ConnectWiseControl;
using CwAssetManager.ApiClients.ConnectWiseManage;
using CwAssetManager.ApiClients.ConnectWiseRmm;
using CwAssetManager.App.Services;
using CwAssetManager.App.ViewModels;
using CwAssetManager.App.Views;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using CwAssetManager.Data;
using CwAssetManager.Data.Repositories;
using CwAssetManager.Infrastructure.Auth;
using CwAssetManager.Infrastructure.Identity;
using CwAssetManager.Infrastructure.Logging;
using CwAssetManager.Infrastructure.RateLimiting;
using CwAssetManager.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;

namespace CwAssetManager.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CwAssetManager");
        Directory.CreateDirectory(appData);

        // Configure Serilog
        Log.Logger = LoggingConfiguration.CreateLogger(
            logDirectory: Path.Combine(appData, "Logs"),
            writeToConsole: false);

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                ConfigureServices(services, appData);
            })
            .Build();

        // Ensure database is created / migrated
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, string appData)
    {
        var dbPath = Path.Combine(appData, "cwassets.db");
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        services.AddScoped<IAssetRepository, MachineRepository>();
        services.AddScoped<RequestLogRepository>();

        // Security
        services.AddSingleton<ISecretsManager>(_ =>
            new SecretEncryptionService(appData));

        // Rate limiters (one per provider)
        services.AddKeyedSingleton<IRateLimiter>("Manage",
            new TokenBucketRateLimiter(new RateLimitSettings { Capacity = 60, RefillRatePerSecond = 1 }));
        services.AddKeyedSingleton<IRateLimiter>("Control",
            new TokenBucketRateLimiter(new RateLimitSettings { Capacity = 30, RefillRatePerSecond = 0.5 }));
        services.AddKeyedSingleton<IRateLimiter>("Rmm",
            new TokenBucketRateLimiter(new RateLimitSettings { Capacity = 120, RefillRatePerSecond = 2 }));

        // Auth configs (populated at runtime from ISecretsManager)
        services.AddSingleton(new AuthConfig());

        // HTTP clients
        services.AddHttpClient<ConnectWiseManageClient>();
        services.AddHttpClient<ConnectWiseControlClient>();
        services.AddHttpClient<ConnectWiseRmmClient>();

        // Auth providers / managers
        services.AddSingleton<ApiKeyAuthProvider>();
        services.AddSingleton<OAuthTokenManager>();

        // Identity resolver
        services.AddSingleton<AssetIdentityResolver>();

        // Ingestion service
        services.AddSingleton<IIngestionService, IngestionService>();
        services.AddSingleton<NavigationService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AssetDetailViewModel>();
        services.AddTransient<RequestLogViewModel>();
        services.AddTransient<ConfigurationViewModel>();
        services.AddTransient<ProviderStatusViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<AssetDetailView>();
        services.AddTransient<RequestLogView>();
        services.AddTransient<ConfigurationView>();
    }
}
