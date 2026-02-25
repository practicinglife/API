# ConnectWise Manage API Integration

> **Spec file**: [`docs/specs/connectwise-manage-api.json`](../specs/connectwise-manage-api.json)
> OpenAPI 3.0.1 · Version 2025.16 · 1838 endpoints

## Authentication

CW Manage uses **HTTP Basic** authentication with a composite credential derived from
your ConnectWise developer application.

```
Authorization: Basic Base64("{companyId}+{publicKey}:{privateKey}")
clientId: <your-client-id>   # registered at developer.connectwise.com
Accept:   application/vnd.connectwise.com+json; version=2025.16
```

The `clientId` header is **required** on every request. Register a client ID at
<https://developer.connectwise.com>.

The Base64 token encodes: `{CompanyId}+{PublicKey}:{PrivateKey}` — note the literal
`+` between company ID and public key.

### Configuration (appsettings.json)

```json
"Providers": {
  "ConnectWiseManage": {
    "BaseUrl":      "https://{instance}.myconnectwise.net/v4_6_release/apis/3.0",
    "CompanyId":    "mycompany",
    "ApiPublicKey": "pub_xxxx",
    "ApiPrivateKey":"priv_yyyy",
    "ApiClientId":  "{your-guid-clientid}"
  }
}
```

## Key Endpoints

| Endpoint | Description |
|---|---|
| `GET /system/info` | Version / connectivity check — returns `{ version, isCloud, serverTimeZone }` |
| `GET /company/configurations` | Paginated list of managed configurations (asset records) |
| `GET /company/configurations/{id}` | Single configuration by integer ID |
| `GET /company/configurations/count` | Total count of matching configurations |

## The `Company.Configuration` Schema

This is the primary asset record in CW Manage. Key fields:

| Field | Type | Notes |
|---|---|---|
| `id` | integer | Record ID — stored as `CwManageDeviceId` |
| `name` | string | Display name / hostname |
| `deviceIdentifier` | string | Secondary device string ID — stored as `CwManageDeviceIdentifier` |
| `serialNumber` | string | Hardware serial |
| `macAddress` | string | Primary NIC MAC |
| `ipAddress` | string | Primary IP |
| `defaultGateway` | string | Default gateway |
| `osType` | string | OS category (e.g. "Windows Server 2022") |
| `osInfo` | string | Detailed OS version string — preferred over `osType` |
| `mobileGuid` | string | **BIOS/hardware GUID** — strongest cross-provider identity key, stored as `BiosGuid` |
| `activeFlag` | boolean | `true` = active configuration |
| `status.name` | string | Status name (e.g. "Active", "Inactive", "Retired") |
| `type.name` | string | Configuration type category |
| `tagNumber` | string | Asset tag |
| `lastLoginName` | string | Last recorded logon |

## Pagination

CW Manage supports two pagination modes. The client uses cursor-based pagination after
the first page for efficiency:

1. **First request** — `?pageSize=1000&page=1&conditions=activeFlag=true&fields=...`
2. **Subsequent requests** — `?pageSize=1000&pageId={lastId}&conditions=activeFlag=true&fields=...`
   where `lastId` is the `id` of the last record received.

Pagination ends when the returned array has fewer items than `pageSize`.

## Filtering

Use the `conditions` query parameter with CW Manage query syntax:

```
conditions=activeFlag=true
conditions=activeFlag=true AND type/name="Server"
conditions=serialNumber!=null AND macAddress!=null
```

The `fields` parameter restricts which properties are returned (reduces payload size).

## Rate Limits

CW Manage enforces rate limits per API key. Default token bucket configuration:
- **Rate**: 60 req/min (1 token/sec)
- **Burst**: 60 tokens
- **429 handling**: honour `Retry-After` header with exponential backoff

## Error Codes

| Code | Meaning |
|---|---|
| 401 | Invalid credentials — check CompanyId, PublicKey, PrivateKey, clientId |
| 403 | Insufficient permissions — check API member security role |
| 404 | Record not found |
| 429 | Rate limit exceeded — honour `Retry-After` header |
| 500/503 | Transient server error — retry with exponential backoff |

## Integration Notes

- The `mobileGuid` field is the most reliable key for cross-system deduplication; it
  corresponds to the BIOS UUID visible in Windows Management Instrumentation.
- The `deviceIdentifier` field is a provider-managed secondary identifier; use it
  alongside `id` when correlating with ConnectWise RMM.
- Use `osInfo` (detailed version string) in preference to `osType` (category).
