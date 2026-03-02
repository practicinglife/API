using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ConnectWiseManager.Services;
using ConnectWiseManager.Data;

namespace ConnectWiseManager;

public partial class App : Application
{
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Configure dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IAsioApiService, AsioApiService>();
        services.AddSingleton<IScreenConnectApiService, ScreenConnectApiService>();
        services.AddSingleton<IReportingApiService, ReportingApiService>();
        services.AddHttpClient();

        // DbContext
        services.AddDbContext<AppDbContext>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ServiceProvider?.Dispose();
        base.OnExit(e);
    }
}
