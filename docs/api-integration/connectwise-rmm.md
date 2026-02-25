# ConnectWise RMM (Asio) API Integration

> **Spec file**: `docs/specs/connectwise-rmm-asio-api.yaml` *(pending upload)*
>
> The Asio REST API YAML specification has been referenced but not yet committed to
> this repository. Once added to `docs/specs/`, this document and the
> `ConnectWiseRmmClient` implementation will be updated to reflect the exact schema,
> endpoint paths, and field names from that spec.

## Current Implementation

The client is implemented based on the known Asio REST API conventions pending the
official YAML spec. Once the spec is available, endpoint paths and field names will
be reconciled with the implementation.

## Authentication

CW RMM (Asio) uses an **API key header**:

```
x-api-key: <your-api-key>
```

### Configuration (appsettings.json)

```json
"Providers": {
  "ConnectWiseRmm": {
    "BaseUrl": "https://api.asio.connectwise.com/",
    "ApiKey":  "your-asio-api-key",
    "ApiKeyHeader": "x-api-key"
  }
}
```

## Key Endpoints

| Endpoint | Description |
|---|---|
| `GET /account` | Account / connectivity check |
| `GET /devices?limit=500&offset=N` | Paginated list of managed devices (offset-based) |
| `GET /devices/{id}` | Single device by ID |

## Device Fields

| Field | Description | Mapped to |
|---|---|---|
| `id` / `deviceId` | RMM device identifier | `CwRmmDeviceId` |
| `hostname` / `name` | Machine hostname | `Hostname` |
| `ip` / `ipAddress` | Primary IP address | `IpAddress` |
| `macAddress` | Primary NIC MAC address | `MacAddress` |
| `serialNumber` | Hardware serial number | `SerialNumber` |
| `os` / `operatingSystem` | OS description string | `OperatingSystem` |
| `status` / `agentStatus` | `online` \| `offline` | `Status` |

## Pagination

Uses **offset-based** pagination: `?limit=500&offset=N`. The client increments
`offset` by `limit` until a page smaller than `limit` is returned.

## Rate Limits

Default token bucket configuration:
- **Rate**: 120 req/min (2 tokens/sec)
- **Burst**: 120 tokens
- **429 handling**: honour `Retry-After` header with exponential backoff

## Error Codes

| Code | Meaning |
|---|---|
| 401 | Invalid API key |
| 403 | Insufficient permissions |
| 404 | Device not found |
| 429 | Rate limit exceeded — honour `Retry-After` |
| 500/503 | Transient error — retry with backoff |

## TODO — Pending YAML Spec

Once `docs/specs/connectwise-rmm-asio-api.yaml` is committed:

1. Add a typed `CwRmmDevice` model in
   `src/CwAssetManager.ApiClients/ConnectWiseRmm/Models/` matching the YAML schemas.
2. Update `ConnectWiseRmmClient.cs` to use `ReadFromJsonAsync<List<CwRmmDevice>>()`.
3. Reconcile field names and add any missing fields (patch status, alert counts, etc.).
4. Update this document with the accurate endpoint paths, schemas, and auth details.
