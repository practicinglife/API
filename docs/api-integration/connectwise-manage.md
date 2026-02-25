# ConnectWise Manage API Integration

## Authentication

CW Manage uses **HTTP Basic** authentication with a composite credential:

```
Authorization: Basic Base64(companyId+publicKey:privateKey)
clientId: <your-client-id>
```

The `clientId` must be registered at <https://developer.connectwise.com>.

## Key Endpoints

| Endpoint | Description |
|---|---|
| `GET /system/info` | Version / connectivity check |
| `GET /company/configurations` | Paginated list of managed configurations |
| `GET /company/configurations/{id}` | Single configuration by ID |

## Pagination

CW Manage uses page-based pagination. The client requests `pageSize=1000&page=N` and increments until an empty array is returned.

## Rate Limits

CW Manage enforces rate limits per API key. The default configuration is 60 req/min (1 token/sec, burst 60).

## Error Codes

| Code | Meaning |
|---|---|
| 401 | Invalid credentials |
| 429 | Rate limit exceeded – honour `Retry-After` header |
| 500/503 | Transient server error – retry with back-off |
