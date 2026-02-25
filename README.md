# CW Asset Manager

A production-grade Windows WPF desktop application (.NET 9) that aggregates and manages assets across all three ConnectWise platforms: **Manage**, **Control**, and **RMM (Asio)**.

## Features

- **Multi-provider ingestion** – polls CW Manage, Control, and RMM on a configurable schedule
- **Asset identity resolution** – deterministically merges records across providers using BiosGuid → SerialNumber → MAC → Hostname
- **Token-bucket rate limiting** – per-provider budgets, honours 429 Retry-After headers
- **Circuit breaker** – Closed / Open / HalfOpen state machine via Polly v8
- **Secure secrets** – AES-256 encrypted local store (PBKDF2 key derivation)
- **Structured logging** – Serilog with rolling JSON file sink + correlation IDs
- **MVVM UI** – CommunityToolkit.Mvvm, live queue/throughput indicators, pause/resume
- **SQLite database** – EF Core 9, migrations included

## Solution Structure

```
CwAssetManager.sln
├── src/
│   ├── CwAssetManager.Core/           # Models, interfaces, enums (net9.0)
│   ├── CwAssetManager.Data/           # EF Core + SQLite (net9.0)
│   ├── CwAssetManager.ApiClients/     # CW API clients (net9.0)
│   ├── CwAssetManager.Infrastructure/ # Rate limiting, auth, security, logging (net9.0)
│   └── CwAssetManager.App/            # WPF application (net9.0-windows)
└── tests/
    ├── CwAssetManager.Tests.Unit/      # xUnit + Moq + FluentAssertions
    └── CwAssetManager.Tests.Integration/ # WireMock.Net end-to-end tests
```

## Prerequisites

- Windows 10/11 (for the WPF app)
- .NET 9 SDK
- Visual Studio 2022 17.8+ or JetBrains Rider

## Getting Started

```bash
# Clone
git clone https://github.com/your-org/CwAssetManager.git
cd CwAssetManager

# Build portable projects (works on Linux/macOS too)
dotnet build src/CwAssetManager.Core
dotnet build src/CwAssetManager.Data
dotnet build src/CwAssetManager.Infrastructure
dotnet build src/CwAssetManager.ApiClients

# Run unit tests
dotnet test tests/CwAssetManager.Tests.Unit

# Run integration tests (requires no external services – uses WireMock)
dotnet test tests/CwAssetManager.Tests.Integration

# Build + run the WPF app (Windows only)
dotnet run --project src/CwAssetManager.App
```

## Configuration

On first launch open **⚙ Configuration** and enter your credentials for each provider. All secrets are encrypted at rest using AES-256 with a per-machine key.

| Provider | Auth method |
|---|---|
| CW Manage | Basic (companyId + publicKey:privateKey) + clientId header |
| CW Control | OAuth2 Client Credentials |
| CW RMM | API Key header |

## Architecture

See [docs/architecture.md](docs/architecture.md) for a detailed architecture overview.

## CI/CD

GitHub Actions workflows in `.github/workflows/ci.yml`:
- **ubuntu-latest** – builds all portable projects, runs unit + integration tests
- **windows-latest** – builds the WPF app

## License

MIT

