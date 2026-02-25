# Architecture

## Overview

CW Asset Manager is a WPF desktop application built on .NET 9 with a layered architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                     CwAssetManager.App (WPF)                    │
│  MainWindow → DashboardView / AssetDetailView / ConfigView ...  │
│  ViewModels (CommunityToolkit.Mvvm)                             │
│  IngestionService (orchestration)                               │
└─────────┬───────────────────────────┬───────────────────────────┘
          │                           │
┌─────────▼─────────┐   ┌────────────▼──────────────┐
│ CwAssetManager    │   │ CwAssetManager.Data        │
│ .ApiClients       │   │ (EF Core + SQLite)         │
│                   │   │ AppDbContext               │
│ ManageClient      │   │ MachineRepository          │
│ ControlClient     │   │ RequestLogRepository       │
│ RmmClient         │   └───────────────────────────┘
│ Auth handlers     │
└─────────┬─────────┘
          │
┌─────────▼──────────────────────────────────────────┐
│             CwAssetManager.Infrastructure           │
│  TokenBucketRateLimiter  CircuitBreaker             │
│  ResiliencePipelineFactory (Polly v8)               │
│  OAuthTokenManager  ApiKeyAuthProvider              │
│  SecretEncryptionService (AES-256)                  │
│  AssetIdentityResolver                              │
│  CorrelationIdEnricher  LoggingConfiguration        │
└─────────┬──────────────────────────────────────────┘
          │
┌─────────▼──────────────────────────┐
│        CwAssetManager.Core         │
│  Models  Interfaces  Enums         │
└────────────────────────────────────┘
```

## Key Design Decisions

### Rate Limiting
Each provider has its own `TokenBucketRateLimiter` instance with independently configured capacity and refill rate. The limiter is injected into each API client. When a 429 response is received, the Polly retry pipeline honours the `Retry-After` header.

### Circuit Breaker
A `CircuitBreaker` sits in front of each provider. After `failureThreshold` consecutive failures the circuit opens; after `openDuration` it transitions to HalfOpen where a single probe is allowed. On success it closes; on failure it re-opens.

### Asset Identity Resolution
`AssetIdentityResolver` uses a priority chain: **BiosGuid > SerialNumber > MacAddress > Hostname** (normalised, FQDN stripped). The `Merge()` method copies non-null provider IDs and metadata from the incoming record into the existing one.

### Secrets Storage
`SecretEncryptionService` stores a JSON dictionary encrypted with AES-256-CBC. The key is derived via PBKDF2 (SHA-256, 100k iterations) from a machine-specific passphrase (`MachineName:UserName:CwAssetManager`).

### MVVM
ViewModels inherit from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. Commands use `[RelayCommand]` source generators. DI is provided by `Microsoft.Extensions.DependencyInjection` wired in `App.xaml.cs`.
