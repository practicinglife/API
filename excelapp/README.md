# ConnectWise Manager

A WPF application for managing ConnectWise Asio devices and ScreenConnect remote sessions.

## Overview

ConnectWise Manager is a desktop application that integrates with ConnectWise Asio and ScreenConnect APIs to provide a unified interface for:
- Device management and monitoring
- Remote installer deployment
- Script execution and repair
- Session management
- Custom field mapping

## Features

### 1. Authentication Setup
- Secure OAuth2 authentication for ConnectWise Asio
- Token-based authentication for ScreenConnect
- Encrypted credential storage using Windows Data Protection API (DPAPI)
- Option to remember credentials for future sessions

### 2. Device Management
- Browse and filter devices from ConnectWise Asio
- Display customizable fields including:
  - Computer Name
  - Company Name
  - Site Name
  - MAC Address
  - Device Type
  - Operating System
  - Status
  - Last Seen
- Support for custom field definitions

### 3. Remote Operations
- **Installer Deployment**: Deploy ScreenConnect agents to selected devices via PowerShell
- **Repair Script Execution**: Execute repair scripts for ScreenConnect clients
- **Session Management**: Create, monitor, and control remote sessions
- **Message Broadcasting**: Send messages to selected devices

### 4. Installer Builder
- Interactive UI for building custom ScreenConnect installer commands
- Support for custom properties:
  - Company
  - Site
  - Department
  - Device Type
- Generates PowerShell scripts with installer deployment logic

### 5. Field Mapping
- Map ConnectWise Asio custom fields to local field definitions
- View and manage custom field values per device
- Support for custom field definitions API

### 6. Script Logging
- Real-time logging of script execution status
- Track deployment success/failure rates
- View detailed execution logs and errors
- Session detail retrieval

### 7. Session Management
- Create new ScreenConnect sessions with custom properties
- Update session properties
- Add notes to sessions
- Send messages to active sessions

## Requirements

- Windows 10/11 (WPF requires Windows)
- .NET 9.0 or later
- ConnectWise Asio account with API access
- ScreenConnect instance with API access

## API Scopes Required

### ConnectWise Asio
- `platform.devices.read` - Read device information
- `platform.companies.read` - Read company information
- `platform.sites.read` - Read site information
- `platform.custom_fields_definitions.read` - Read custom field definitions

### ScreenConnect
- RESTful API access with appropriate permissions
- Access to the following endpoints:
  - `SendCommandToSession` - Execute PowerShell commands
  - `GetSessionDetails` - Retrieve session information
  - `AddNoteToSession` - Add notes to sessions
  - `CreateSession` - Create new sessions
  - `SendMessageToSession` - Send messages
  - `UpdateSessionCustomProperties` - Update session properties

## Building the Application

### Prerequisites
1. Install Visual Studio 2022 or later with .NET Desktop Development workload
2. Ensure .NET 9.0 SDK is installed

### Build Steps
1. Clone the repository:
   ```bash
   git clone https://github.com/practicinglife/excelapp.git
   cd excelapp
   ```

2. Open the solution in Visual Studio:
   ```bash
   start ConnectWiseManager.sln
   ```

3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

4. Build the solution:
   ```bash
   dotnet build ConnectWiseManager.sln --configuration Release
   ```

5. Run the application:
   ```bash
   dotnet run --project ConnectWiseManager/ConnectWiseManager.csproj
   ```

## Configuration

### First-Time Setup
1. Launch the application
2. Enter your ConnectWise Asio credentials:
   - Base URL (e.g., `https://api.connectwise.com`)
   - Client ID
   - Client Secret
3. Enter your ScreenConnect credentials:
   - Base URL (e.g., `https://yourdomain.screenconnect.com`)
   - Username
   - Password
4. Check "Remember Credentials" to securely store them for future use
5. Click "Login" to authenticate

### Credential Storage
Credentials are encrypted using Windows Data Protection API (DPAPI) and stored in:
```
%APPDATA%\ConnectWiseManager\credentials.dat
```

## Usage

### Managing Agents
1. Navigate to the "Agent Management" tab
2. Select fields to display from the dropdown
3. Click "Load Agents" to retrieve the device list
4. Select one or more devices for operations

### Deploying Installers
1. Select target devices in the Agent Management tab
2. Click "Deploy Installer"
3. Enter the installer URL when prompted
4. Monitor progress in the Script Logs tab

### Executing Repair Scripts
1. Select target devices
2. Click "Execute Repair Script"
3. Confirm the action
4. Check the logs for execution status

### Building Custom Installers
1. Navigate to the "Installer Builder" tab
2. Fill in the custom fields:
   - Company
   - Site
   - Department
   - Device Type
3. Enter the installer URL
4. Click "Build Installer Command"
5. Copy the generated PowerShell script

### Managing Sessions
1. Navigate to the "Session Management" tab
2. Enter a session ID
3. Use the available actions:
   - Get Session Details
   - Add Note
   - Update Properties

## Architecture

### Project Structure
```
ConnectWiseManager/
├── Models/              # Data models
│   ├── ApiCredentials.cs
│   ├── Device.cs
│   ├── FieldDefinition.cs
│   └── ScriptExecution.cs
├── Services/            # API service layer
│   ├── ICredentialService.cs
│   ├── CredentialService.cs
│   ├── IAsioApiService.cs
│   ├── AsioApiService.cs
│   ├── IScreenConnectApiService.cs
│   └── ScreenConnectApiService.cs
├── ViewModels/          # MVVM view models
│   └── BaseViewModel.cs
├── Views/               # WPF views
│   ├── LoginWindow.xaml
│   ├── LoginWindow.xaml.cs
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
├── App.xaml            # Application resources
└── App.xaml.cs         # Application startup
```

### Key Components

#### Services
- **CredentialService**: Manages secure credential storage and retrieval
- **AsioApiService**: Handles ConnectWise Asio API interactions
- **ScreenConnectApiService**: Manages ScreenConnect API calls

#### Models
- **ApiCredentials**: Stores authentication information
- **Device**: Represents a device from ConnectWise Asio
- **FieldDefinition**: Defines available and custom fields
- **ScriptExecution**: Tracks script execution logs

#### Views
- **LoginWindow**: Authentication and credential management
- **MainWindow**: Main application interface with tabbed navigation

## Security Considerations

- All credentials are encrypted using DPAPI before storage
- API tokens are stored in memory only during the session
- HTTPS is required for all API communications
- Credentials are never logged or exposed in error messages

## Troubleshooting

### Authentication Fails
- Verify your API credentials are correct
- Check that your account has the required API scopes
- Ensure your firewall allows HTTPS traffic
- Verify the Base URLs are correct (no trailing slashes)

### No Devices Displayed
- Confirm you have devices in your ConnectWise Asio account
- Check that the API endpoints are accessible
- Verify your OAuth2 scopes include `platform.devices.read`

### Script Execution Fails
- Ensure the target device has ScreenConnect installed
- Verify the session ID is correct
- Check that your ScreenConnect account has script execution permissions
- Review the Script Logs tab for detailed error messages

### Build Errors
- Ensure you're building on Windows (WPF is Windows-only)
- Verify .NET 9.0 SDK is installed
- Check that all NuGet packages are restored
- Try cleaning and rebuilding the solution

## Contributing

Contributions are welcome! Please follow these guidelines:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is provided as-is for demonstration and educational purposes.

## Support

For issues and questions:
- Open an issue on GitHub
- Check existing issues for solutions
- Review the troubleshooting section

## API Documentation

### ConnectWise Asio API
- [ConnectWise Platform API Documentation](https://developer.connectwise.com/)
- OAuth2 endpoint: `/oauth2/token`
- Devices endpoint: `/api/v1/devices`
- Custom fields: `/api/v1/custom_fields_definitions`

### ScreenConnect API
- [ScreenConnect RESTful API Documentation](https://docs.connectwise.com/ConnectWise_Control_Documentation/Get_started/Host_page/Administration/Security/RESTful_API)
- Command execution: `SendCommandToSession`
- Session management: `GetSessionDetails`, `CreateSession`
- Notes and messaging: `AddNoteToSession`, `SendMessageToSession`

## Changelog

### Version 1.0.0 (Initial Release)
- OAuth2 authentication for ConnectWise Asio
- Token-based authentication for ScreenConnect
- Device listing with customizable fields
- Installer deployment functionality
- Repair script execution
- Custom installer builder
- Field mapping interface
- Script logging and monitoring
- Session management controls
- Secure credential storage
