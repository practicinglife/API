# ConnectWise Manager - Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        WPF Application                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────────┐  │
│  │ LoginWindow   │  │  MainWindow   │  │  Custom Dialogs    │  │
│  │  (OAuth2 +    │─▶│  (5 Tabs)     │  │  (Input/Messages)  │  │
│  │   Basic Auth) │  │               │  │                    │  │
│  └───────────────┘  └───────────────┘  └────────────────────┘  │
│                              │                                   │
│  ┌───────────────────────────┴─────────────────────────────┐   │
│  │                    Service Layer                         │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │  ┌──────────────────┐  ┌─────────────────────────────┐  │   │
│  │  │ CredentialService│  │   API Services              │  │   │
│  │  │  - DPAPI         │  │  ┌───────────────────────┐  │  │   │
│  │  │  - Encryption    │  │  │ AsioApiService        │  │  │   │
│  │  │  - Storage       │  │  │  - OAuth2 Flow        │  │  │   │
│  │  └──────────────────┘  │  │  - Device Management  │  │  │   │
│  │                         │  └───────────────────────┘  │  │   │
│  │                         │  ┌───────────────────────┐  │  │   │
│  │                         │  │ ScreenConnectService  │  │  │   │
│  │                         │  │  - Basic Auth         │  │  │   │
│  │                         │  │  - PowerShell Exec    │  │  │   │
│  │                         │  │  - Session Mgmt       │  │  │   │
│  │                         │  └───────────────────────┘  │  │   │
│  │                         └─────────────────────────────┘  │   │
│  └───────────────────────────────────────────────────────────┘   │
│                              │                                   │
│  ┌───────────────────────────┴─────────────────────────────┐   │
│  │                      Models                              │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │  ApiCredentials │ Device │ FieldDefinition │ ScriptExec  │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌──────────────────┐                    ┌──────────────────────┐
│ ConnectWise Asio │                    │   ScreenConnect      │
├──────────────────┤                    ├──────────────────────┤
│ OAuth2 Endpoint  │                    │ RESTful API          │
│ /oauth2/token    │                    │ Basic Auth           │
│                  │                    │                      │
│ Device API       │                    │ SendCommandToSession │
│ /api/v1/devices  │                    │ GetSessionDetails    │
│                  │                    │ AddNoteToSession     │
│ Custom Fields    │                    │ CreateSession        │
│ /api/v1/custom_* │                    │ SendMessage          │
└──────────────────┘                    └──────────────────────┘
```

## Data Flow

### Authentication Flow

```
User Input (Login)
    │
    ├─▶ Asio Credentials
    │       │
    │       ├─▶ OAuth2 Token Request
    │       │       │
    │       │       ├─▶ POST /oauth2/token
    │       │       │       (client_credentials grant)
    │       │       │
    │       │       └─▶ Access Token + Expiry
    │       │
    │       └─▶ Store in Memory
    │
    ├─▶ ScreenConnect Credentials
    │       │
    │       ├─▶ Basic Auth Header
    │       │       │
    │       │       └─▶ Base64(username:password)
    │       │
    │       └─▶ Store in Memory
    │
    └─▶ [Optional] Save to Disk (DPAPI Encrypted)
            │
            └─▶ %APPDATA%\ConnectWiseManager\credentials.dat
```

### Device Management Flow

```
Load Agents Button Click
    │
    ├─▶ AsioApiService.GetDevicesAsync()
    │       │
    │       ├─▶ GET /api/v1/devices
    │       │       Header: Authorization: Bearer {token}
    │       │
    │       └─▶ Parse JSON Response
    │               │
    │               └─▶ List<Device>
    │
    └─▶ Update DataGrid
            │
            └─▶ Display in UI
```

### Script Execution Flow

```
Deploy Installer Button Click
    │
    ├─▶ Get Selected Devices
    │
    ├─▶ For Each Device:
    │       │
    │       ├─▶ Build PowerShell Script
    │       │       │
    │       │       └─▶ #!ps
    │       │           Invoke-WebRequest "{url}"
    │       │           Start-Process installer.exe /silent
    │       │
    │       ├─▶ ScreenConnectService.SendCommandToSession()
    │       │       │
    │       │       ├─▶ POST SendCommandToSession
    │       │       │       Header: Authorization: Basic {token}
    │       │       │       Body: { sessionID, command, processType }
    │       │       │
    │       │       └─▶ Success/Failure
    │       │
    │       └─▶ Log Execution
    │               │
    │               └─▶ ScriptExecution object
    │                       │
    │                       └─▶ Display in Logs Tab
    │
    └─▶ Show Status Message
```

## Component Responsibilities

### Views (UI Layer)
- **LoginWindow**: User authentication interface
- **MainWindow**: Primary application interface with tabs
  - Agent Management Tab
  - Installer Builder Tab
  - Field Mapping Tab
  - Script Logs Tab
  - Session Management Tab

### Services (Business Logic)
- **CredentialService**: Secure credential storage and retrieval
- **AsioApiService**: ConnectWise Asio API integration
- **ScreenConnectApiService**: ScreenConnect API integration

### Models (Data)
- **ApiCredentials**: Authentication information
- **Device**: Device/agent information
- **FieldDefinition**: Field metadata
- **ScriptExecution**: Execution log data

### ViewModels (Data Binding)
- **BaseViewModel**: INotifyPropertyChanged implementation
- Supports two-way data binding
- Property change notifications

## Security Architecture

### Credential Storage

```
Plain Text Credentials
    │
    ├─▶ JSON Serialization
    │       │
    │       └─▶ UTF-8 Bytes
    │
    ├─▶ DPAPI Encryption
    │       │
    │       ├─▶ ProtectedData.Protect()
    │       │       - Scope: CurrentUser
    │       │       - Entropy: Custom key
    │       │
    │       └─▶ Encrypted Bytes
    │
    └─▶ File Write
            │
            └─▶ %APPDATA%\ConnectWiseManager\credentials.dat
```

### API Security

```
API Request
    │
    ├─▶ Asio Requests
    │       │
    │       ├─▶ HTTPS Only
    │       ├─▶ Bearer Token (OAuth2)
    │       └─▶ Token Expiry Check
    │
    └─▶ ScreenConnect Requests
            │
            ├─▶ HTTPS Only
            ├─▶ Basic Auth Header
            └─▶ No credential logging
```

## Extensibility Points

### Adding New API Integrations
1. Create interface in `Services/`
2. Implement service class
3. Register in `App.xaml.cs` DI container
4. Use in views via constructor injection

### Adding New Features
1. Create model in `Models/`
2. Add service methods
3. Create/update view
4. Bind to ViewModel properties

### Custom Fields
1. Extend `FieldDefinition` model
2. Update `AsioApiService.GetCustomFieldDefinitionsAsync()`
3. Add UI controls in Field Mapping tab
4. Persist mappings if needed

## Performance Considerations

### Async Operations
- All API calls are async
- No blocking UI operations
- Proper exception handling

### Memory Management
- ObservableCollection for data binding
- Proper disposal of HttpClient
- Service lifetime: Singleton

### UI Responsiveness
- Async/await for long operations
- Progress indicators
- Background thread for heavy work

## Error Handling Strategy

```
Try-Catch Blocks
    │
    ├─▶ Service Layer
    │       │
    │       ├─▶ Log exception
    │       ├─▶ Return null/empty
    │       └─▶ Throw custom exception
    │
    └─▶ UI Layer
            │
            ├─▶ Catch exceptions
            ├─▶ Show user-friendly message
            └─▶ Log to Logs tab
```

## Deployment Architecture

```
Development
    │
    ├─▶ Debug Build
    │       │
    │       ├─▶ Full PDB symbols
    │       └─▶ Verbose logging
    │
    └─▶ Local Testing

Release
    │
    ├─▶ Release Build
    │       │
    │       ├─▶ Optimizations enabled
    │       ├─▶ No debug symbols
    │       └─▶ Minimal logging
    │
    ├─▶ Publish
    │       │
    │       ├─▶ Self-contained (optional)
    │       └─▶ Framework-dependent (default)
    │
    └─▶ Distribution
            │
            ├─▶ MSI Installer (recommended)
            ├─▶ ClickOnce
            └─▶ MSIX Package
```

## Technology Stack

- **Framework**: .NET 9.0 (Windows-only)
- **UI**: WPF (Windows Presentation Foundation)
- **DI**: Microsoft.Extensions.DependencyInjection
- **HTTP**: Microsoft.Extensions.Http (HttpClientFactory)
- **JSON**: Newtonsoft.Json
- **Security**: System.Security.Cryptography.ProtectedData
- **Configuration**: Microsoft.Extensions.Configuration

## Future Enhancements

1. **Token Refresh**: Automatic OAuth2 token renewal
2. **Caching**: Device list caching with TTL
3. **Search/Filter**: Advanced device filtering
4. **Profiles**: Multiple environment configurations
5. **Scheduling**: Automated task execution
6. **Notifications**: Email/SMS alerts
7. **Logging**: File-based logging with rotation
8. **Updates**: Auto-update mechanism
9. **Telemetry**: Usage analytics
10. **Testing**: Unit and integration tests
