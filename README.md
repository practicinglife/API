# MSP Tools — Multi-Platform Integration Hub

A WPF desktop application that integrates multiple MSP/ITSM APIs into a single platform, enabling cross-platform asset search, data ingestion, and automation.

---

## Overview

MSP Tools connects to multiple APIs simultaneously, normalises their data into a unified model, and lets you search for assets (computers, agents, companies, sites) across all platforms at once. Matching logic automatically identifies the same asset appearing in two or more systems — powering true cross-platform automation.

### Supported Platforms (out of the box)

| Platform | Auth Method | API Spec |
|----------|------------|----------|
| ConnectWise Manage (PSA) | `CompanyId + PublicKey + PrivateKey` + `clientId` header | `All.json` (v2025.16) |
| ConnectWise Asio / RMM | API Key (`x-api-key` header) | `currentPartnerAPI_228.yaml` (v2.2.8) |
| ConnectWise Control (ScreenConnect) | Basic Auth (username + password) or Bearer Token | `connectwise-control-session-manager-api.yaml` (v1.0.0) |

---

## Solution Structure

```
MspTools.slnx
├── src/
│   ├── MspTools.Core/                  # Models, interfaces, authentication, data container
│   │   ├── Authentication/
│   │   │   ├── AuthMethod.cs           # Abstract base (ApiKey, Basic, Bearer, CWApiKey)
│   │   │   ├── ApiKeyAuth.cs
│   │   │   ├── BasicAuth.cs
│   │   │   ├── BearerTokenAuth.cs
│   │   │   └── ConnectWiseApiKeyAuth.cs
│   │   ├── Interfaces/
│   │   │   ├── IApiConnector.cs        # Fetch devices & companies, test connection
│   │   │   └── IDataContainer.cs       # Ingest, search, match, clear
│   │   └── Models/
│   │       ├── ApiConnection.cs        # Connection configuration
│   │       ├── UnifiedDevice.cs        # Normalised device record
│   │       ├── UnifiedCompany.cs       # Normalised company record
│   │       ├── CrossPlatformMatch.cs   # Match result across platforms
│   │       └── DataContainer.cs        # Thread-safe in-memory store
│   ├── MspTools.Connectors/            # Per-platform HTTP connectors
│   │   ├── ConnectWiseManageConnector.cs
│   │   ├── ConnectWiseAsioConnector.cs
│   │   ├── ConnectWiseControlConnector.cs
│   │   ├── ConnectorFactory.cs         # Instantiates the right connector by type
│   │   └── JsonElementExtensions.cs    # Safe JSON parsing helpers
│   └── MspTools.App/                   # WPF application (Windows)
│       ├── App.xaml / App.xaml.cs
│       ├── Views/
│       │   ├── MainWindow.xaml         # Tabbed UI: Connections, Devices, Companies, Matches
│       │   └── MainWindow.xaml.cs
│       └── ViewModels/
│           ├── MainViewModel.cs        # Orchestrates connectors, container, commands
│           ├── RelayCommand.cs         # ICommand implementations (sync + async)
│           ├── ViewModelBase.cs        # INotifyPropertyChanged base
│           ├── ApiConnectionViewModel.cs
│           ├── NewConnectionFormViewModel.cs
│           ├── SearchCriteria.cs
│           ├── MatchViewModel.cs
│           └── CompanyViewModel.cs
└── tests/
    └── MspTools.Tests/                 # xUnit tests (net8.0, platform-independent)
        ├── AuthenticationTests.cs
        ├── DataContainerTests.cs
        └── ConnectorFactoryTests.cs
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) or later
- Windows 10/11 (for the WPF app; the Core + Connectors libraries are cross-platform)

### Build

```bash
# Build the full solution (Core + Connectors only on Linux/macOS)
dotnet build src/MspTools.Core/MspTools.Core.csproj
dotnet build src/MspTools.Connectors/MspTools.Connectors.csproj

# Build everything including the WPF app (Windows only)
dotnet build MspTools.slnx
```

### Run Tests

```bash
dotnet test tests/MspTools.Tests/MspTools.Tests.csproj
```

### Run the Application (Windows)

```bash
dotnet run --project src/MspTools.App/MspTools.App.csproj
```

---

## Key Features

### Multi-Auth Support

| Auth Type | Use Case |
|-----------|----------|
| `ConnectWiseApiKeyAuth` | ConnectWise Manage — encodes `CompanyId+PublicKey:PrivateKey` as Basic + `clientId` header |
| `ApiKeyAuth` | ConnectWise Asio RMM — `x-api-key` header |
| `BasicAuth` | ConnectWise Control — username + password |
| `BearerTokenAuth` | OAuth / JWT access tokens |

### Unified Data Model

Every connector normalises its output into:
- **`UnifiedDevice`** — `ComputerName`, `AgentName`, `CompanyName`, `SiteName`, plus OS, IP, MAC, online status
- **`UnifiedCompany`** — `CompanyName`, `SiteNames`, city/state, phone

### Cross-Platform Matching

`DataContainer.ComputeMatches()` groups devices that share the same `ComputerName` (or `AgentName`) across multiple source platforms, and groups companies by `CompanyName`. Each `CrossPlatformMatch` records which platforms are involved and a confidence score.

### Search

```csharp
// Search by any combination of fields (all optional, case-insensitive partial match)
var devices  = container.SearchDevices(computerName: "SERVER", companyName: "Acme");
var companies = container.SearchCompanies(companyName: "Acme", siteName: "Main");
```

---

## Adding a New Connector

1. Implement `IApiConnector` in `MspTools.Connectors/`.
2. Add the new `ConnectorType` enum value to `MspTools.Core/Models/ApiConnection.cs`.
3. Register it in `ConnectorFactory.Create()`.
4. Add a new `AuthMethod` subclass if the platform uses a unique auth scheme.

---

## API Reference Files

| File | Product | Description |
|------|---------|-------------|
| `All.json` | ConnectWise Manage | OpenAPI 3.0, all public REST endpoints |
| `currentPartnerAPI_228.yaml` | ConnectWise Asio (RMM) | OpenAPI 3.0, partner API v2.2.8 |
| `connectwise-control-session-manager-api.yaml` | ConnectWise Control | OpenAPI 3.0, Session Manager API |
| `Session Manager API reference - ConnectWise.html` | ConnectWise Control | Offline copy of the official API reference |
