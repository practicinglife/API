# ConnectWise Control API Integration

> **Spec file**: [`docs/specs/connectwise-control-session-manager.html`](../specs/connectwise-control-session-manager.html)
> Source: Session Manager API Reference (ScreenConnect Documentation)

## Overview

ConnectWise Control (ScreenConnect) exposes its session management functionality
through the **Session Manager API** — a JSON-RPC endpoint served by every ScreenConnect
installation at `POST {serverUrl}/Services/PageService.ashx`.

For asset management purposes, only **Access** sessions (`SessionType = 2`) are
retrieved — these represent persistently managed endpoints where the ScreenConnect
agent is installed.

## Authentication

Two auth modes are supported (configure one in `appsettings.json`):

### Option A — Direct API Key (recommended for server-to-server)

```
Authorization: Bearer {apiKey}
```

Set `CwControlApiKey` in `AuthConfig`. No token exchange required.

### Option B — OAuth2 Client Credentials

```
POST {tokenEndpoint}
  grant_type=client_credentials
  client_id={id}
  client_secret={secret}
```

The returned `access_token` is sent as `Authorization: Bearer {token}`.
Tokens are cached and refreshed 30 seconds before expiry.
On a 401 response the cached token is invalidated and re-fetched on the next request.

### Configuration (appsettings.json)

```json
"Providers": {
  "ConnectWiseControl": {
    "BaseUrl":         "https://{instance}.screenconnect.com",
    "CwControlApiKey": "your-api-key-here"
  }
}
```

## Session Manager JSON-RPC Endpoint

All Session Manager calls use a single endpoint:

```
POST {serverUrl}/Services/PageService.ashx
Content-Type: application/json
Authorization: Bearer {token}
```

Request body (JSON-RPC 2.0 envelope):

```json
{
  "id": 1,
  "method": "GetSessions",
  "params": { "sessionFilter": { "SessionType": 2 } }
}
```

Response envelope:

```json
{
  "id": 1,
  "result": [ /* array of Session objects */ ],
  "error": null
}
```

## Key Methods

| Method | Params | Description |
|---|---|---|
| `GetSessions` | `{ sessionFilter }` | List sessions matching a filter |
| `GetSession` | `{ sessionID }` | Single session by GUID |
| `GetSessionDetails` | `{ sessionID }` | Session with full guest details |
| `CreateSessionAsync` | `{ byHost, sessionType, name, host, isPublic, code, customPropertyValues }` | Create a Support or Meeting session |
| `UpdateSessionAsync` | `{ byHost, sessionID, name, isPublic, code, customPropertyValues }` | Update session properties |

## Session Object Fields

Fields returned by `GetSessions` / `GetSession`:

| Field | Type | Notes |
|---|---|---|
| `SessionID` | Guid | **Primary key** — stored as `CwControlSessionId` |
| `Name` | string | Machine/session display name — stored as `Hostname` |
| `SessionType` | int | 1=Support, 2=Access, 3=Meeting |
| `Host` | string | Assigned host (technician) username |
| `IsPublic` | bool | Publicly joinable |
| `Code` | string | Guest join code (null for public) |
| `GuestInfoUpdateTime` | DateTimeOffset | Last agent check-in — used as `LastSeen` |
| `ActiveConnections` | array | Non-empty when an agent (Guest) is connected |
| `GuestInfo` | object | Agent-reported hardware/OS information (see below) |
| `CustomPropertyValues` | string[] | Up to 8 custom property slots |

### `GuestInfo` Sub-Object

Populated by the ScreenConnect agent on managed Access sessions:

| Field | Type | Notes |
|---|---|---|
| `OperatingSystemName` | string | Full OS name (e.g. "Windows 11 Pro") — part of `OperatingSystem` |
| `OperatingSystemVersion` | string | Version string (e.g. "22H2") — appended to OS name |
| `ProcessorName` | string | CPU model |
| `ProcessorVirtualCount` | int | Logical CPU count |
| `SystemMemoryTotalMegabytes` | long | Total RAM (MB) |
| `SystemMemoryAvailableMegabytes` | long | Free RAM (MB) |
| `GuestPrivateIpAddress` | string | Agent-reported private IP — stored as `IpAddress` |
| `GuestMachineSerialNumber` | string | Hardware serial — stored as `SerialNumber` |
| `GuestMachineDomain` | string | Domain / workgroup |

## Online Status Detection

A session is considered **Online** when its `ActiveConnections` array contains at
least one entry with `ProcessType = "Guest"` (the remote agent connection).

## Rate Limits

Default token bucket configuration:
- **Rate**: 30 req/min (0.5 token/sec)
- **Burst**: 30 tokens
- **429 handling**: honour `Retry-After` header with exponential backoff

## Error Codes

| Code | Meaning |
|---|---|
| 401 | Invalid or expired token — re-authenticate |
| 403 | Insufficient permissions for the requested session or method |
| 404 | Session not found |
| 429 | Rate limit exceeded |
| 500 | Server error — retry with backoff |

## Integration Notes

- Use `GuestInfoUpdateTime` as the `LastSeen` timestamp — it reflects the last time
  the ScreenConnect agent reported in, which is more accurate than the request time.
- The `GuestMachineSerialNumber` from `GuestInfo` enables cross-provider deduplication
  with ConnectWise Manage (`serialNumber`) and RMM.
- For `SessionType = 2` (Access), the `Name` field is typically the machine hostname
  as configured by the agent installer.
