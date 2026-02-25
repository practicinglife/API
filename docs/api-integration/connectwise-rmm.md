# ConnectWise RMM (Asio) API Integration

## Authentication

CW RMM uses a simple **API key header**:

```
x-api-key: <your-api-key>
```

## Key Endpoints

| Endpoint | Description |
|---|---|
| `GET /account` | Account / connectivity check |
| `GET /devices?limit=500&offset=N` | Paginated list of managed devices |
| `GET /devices/{id}` | Single device by ID |

## Device Fields

| Field | Description |
|---|---|
| `id` / `deviceId` | RMM device identifier |
| `hostname` | Machine hostname |
| `ip` / `ipAddress` | IP address |
| `macAddress` | Primary NIC MAC |
| `serialNumber` | Hardware serial |
| `os` / `operatingSystem` | OS string |
| `status` / `agentStatus` | `online` \| `offline` |

## Rate Limits

Default: 120 req/min (2 tokens/sec, burst 120).
