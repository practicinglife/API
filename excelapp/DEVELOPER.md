# Developer Guide

## Development Environment Setup

### Prerequisites
- Windows 10/11 (WPF is Windows-only)
- Visual Studio 2022 or later with:
  - .NET Desktop Development workload
  - WPF development tools
- .NET 9.0 SDK or later
- Git for version control

### Initial Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/practicinglife/excelapp.git
   cd excelapp
   ```

2. Open the solution in Visual Studio:
   ```bash
   start ConnectWiseManager.sln
   ```

3. Restore NuGet packages (should happen automatically in Visual Studio)

## Project Architecture

### Design Patterns
The application follows these design patterns:
- **MVVM (Model-View-ViewModel)**: Separation of UI and business logic
- **Dependency Injection**: Services are registered and injected via DI container
- **Repository Pattern**: Abstract data access through service interfaces
- **Command Pattern**: RelayCommand for UI actions

### Dependency Injection
Services are configured in `App.xaml.cs`:
```csharp
private void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<ICredentialService, CredentialService>();
    services.AddSingleton<IAsioApiService, AsioApiService>();
    services.AddSingleton<IScreenConnectApiService, ScreenConnectApiService>();
    services.AddHttpClient();
}
```

Access services via:
```csharp
var service = App.ServiceProvider.GetService(typeof(IServiceType)) as IServiceType;
```

### Security Implementation

#### Credential Storage
Credentials are encrypted using Windows Data Protection API (DPAPI):
```csharp
var encryptedBytes = ProtectedData.Protect(
    plainBytes, 
    entropy, 
    DataProtectionScope.CurrentUser
);
```

Key points:
- Uses `DataProtectionScope.CurrentUser` for user-specific encryption
- Entropy adds an additional layer of security
- Credentials stored in `%APPDATA%\ConnectWiseManager\credentials.dat`

#### API Authentication
- **Asio**: OAuth2 Client Credentials Flow
- **ScreenConnect**: Basic Authentication with Base64 encoding

## API Integration

### ConnectWise Asio

#### Authentication Flow
1. Exchange client credentials for access token:
   ```
   POST /oauth2/token
   grant_type=client_credentials
   client_id={id}
   client_secret={secret}
   scope={scopes}
   ```

2. Use Bearer token for API calls:
   ```
   Authorization: Bearer {access_token}
   ```

#### Key Endpoints
- `/api/v1/devices` - List devices
- `/api/v1/custom_fields_definitions` - Get custom field definitions
- `/api/v1/custom_fields_values` - Get custom field values

### ScreenConnect

#### Authentication
Uses Basic Authentication:
```
Authorization: Basic {base64(username:password)}
```

#### Key Endpoints
- `SendCommandToSession` - Execute PowerShell commands
- `GetSessionDetails` - Get session information
- `AddNoteToSession` - Add notes
- `CreateSession` - Create new sessions
- `SendMessageToSession` - Send messages
- `UpdateSessionCustomProperties` - Update properties

## Adding New Features

### Adding a New Service
1. Create interface in `Services/` folder:
   ```csharp
   public interface IMyService
   {
       Task<Result> DoSomethingAsync();
   }
   ```

2. Implement the service:
   ```csharp
   public class MyService : IMyService
   {
       public async Task<Result> DoSomethingAsync()
       {
           // Implementation
       }
   }
   ```

3. Register in `App.xaml.cs`:
   ```csharp
   services.AddSingleton<IMyService, MyService>();
   ```

### Adding a New Model
1. Create model in `Models/` folder:
   ```csharp
   public class MyModel
   {
       public string Id { get; set; } = string.Empty;
       public string Name { get; set; } = string.Empty;
   }
   ```

### Adding a New View
1. Create XAML view in `Views/` folder:
   ```xml
   <Window x:Class="ConnectWiseManager.Views.MyWindow"
           Title="My Window">
       <Grid>
           <!-- UI Elements -->
       </Grid>
   </Window>
   ```

2. Create code-behind:
   ```csharp
   public partial class MyWindow : Window
   {
       public MyWindow()
       {
           InitializeComponent();
       }
   }
   ```

### Adding a New ViewModel
1. Create ViewModel inheriting from BaseViewModel:
   ```csharp
   public class MyViewModel : BaseViewModel
   {
       private string _myProperty = string.Empty;
       
       public string MyProperty
       {
           get => _myProperty;
           set => SetField(ref _myProperty, value);
       }
   }
   ```

## Testing

### Unit Testing
While not currently implemented, unit tests should follow this structure:

```csharp
[TestClass]
public class AsioApiServiceTests
{
    [TestMethod]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsTrue()
    {
        // Arrange
        var service = new AsioApiService(mockHttpClientFactory);
        var credentials = new AsioCredentials { /* ... */ };
        
        // Act
        var result = await service.AuthenticateAsync(credentials);
        
        // Assert
        Assert.IsTrue(result);
    }
}
```

### Manual Testing Checklist
- [ ] Login with valid credentials
- [ ] Login with invalid credentials (should fail gracefully)
- [ ] Save and load credentials
- [ ] Load device list
- [ ] Deploy installer to device
- [ ] Execute repair script
- [ ] Build custom installer
- [ ] Map custom fields
- [ ] View script logs
- [ ] Manage sessions

## Code Style Guidelines

### C# Conventions
- Use PascalCase for public members and types
- Use camelCase for private fields (prefix with `_`)
- Use var when type is obvious
- Always use braces for control flow statements
- One statement per line

### XAML Conventions
- Use PascalCase for control names
- Suffix controls with their type (e.g., `LoginButton`, `UsernameTextBox`)
- Group related elements in GroupBox or StackPanel
- Use meaningful resource keys

### Async/Await Best Practices
- Always await async methods
- Use `ConfigureAwait(false)` in library code
- Catch and handle exceptions appropriately
- Show user-friendly error messages

## Debugging Tips

### Common Issues

#### Authentication Failures
1. Check API credentials in debugger
2. Verify network connectivity
3. Inspect HTTP response status codes
4. Review API documentation for changes

#### UI Not Updating
1. Ensure INotifyPropertyChanged is implemented
2. Check that SetField is called for property changes
3. Verify data binding in XAML
4. Use Dispatcher.Invoke for cross-thread updates

#### Memory Leaks
1. Dispose of HttpClient instances
2. Unsubscribe from events
3. Clear ObservableCollection references
4. Use weak references where appropriate

## Performance Optimization

### Best Practices
- Use async/await for I/O operations
- Cache API responses when appropriate
- Lazy-load heavy resources
- Use data virtualization for large lists
- Profile with Performance Profiler

### WPF Specific
- Use `Binding` mode appropriately (OneWay, TwoWay, etc.)
- Freeze Freezable objects when possible
- Use CompositionTarget.Rendering sparingly
- Virtualize ItemsControl with many items

## Deployment

### Building for Release
1. Set configuration to Release:
   ```bash
   dotnet build --configuration Release
   ```

2. Publish the application:
   ```bash
   dotnet publish --configuration Release --self-contained false
   ```

3. For self-contained deployment:
   ```bash
   dotnet publish --configuration Release --self-contained true --runtime win-x64
   ```

### Installation Package
Consider using:
- WiX Toolset for MSI installers
- ClickOnce for web-based deployment
- MSIX for modern deployment

## Contributing

### Pull Request Process
1. Create a feature branch from main
2. Make your changes
3. Test thoroughly
4. Update documentation
5. Submit pull request with clear description

### Code Review Guidelines
- Check for security vulnerabilities
- Verify error handling
- Ensure UI is responsive
- Confirm proper resource disposal
- Review API usage patterns

## Resources

### Official Documentation
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [WPF Documentation](https://docs.microsoft.com/dotnet/desktop/wpf/)
- [ConnectWise API](https://developer.connectwise.com/)
- [ScreenConnect API](https://docs.connectwise.com/)

### Useful Libraries
- Newtonsoft.Json - JSON serialization
- Microsoft.Extensions.DependencyInjection - DI container
- System.Security.Cryptography - Encryption

### Learning Resources
- [WPF Tutorial](https://wpf-tutorial.com/)
- [MVVM Pattern](https://docs.microsoft.com/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern)
- [Async/Await Best Practices](https://docs.microsoft.com/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

## Support

For development questions:
- Open an issue on GitHub
- Check existing documentation
- Review code comments
- Contact the maintainer
