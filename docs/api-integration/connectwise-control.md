# ConnectWise Control API Integration

## Authentication

CW Control uses **OAuth2 Client Credentials** flow:

```
POST /token
  grant_type=client_credentials
  client_id=<id>
  client_secret=<secret>
```

The returned `access_token` is sent as `Authorization: Bearer <token>`. Tokens are cached and refreshed 30 seconds before expiry.

## Key Endpoints

| Endpoint | Description |
|---|---|
| `GET /Status` | Connectivity / health check |
| `GET /Session` | List all remote sessions |
| `GET /Session/{id}` | Single session by ID |

## Session Fields

| Field | Description |
|---|---|
| `SessionID` | Unique session identifier |
| `Name` | Machine display name |
| `IsOnline` | Current online status |
| `GuestOperatingSystemName` | OS reported by guest agent |
| `GuestIpAddress` | Guest IP address |

## Rate Limits

Default: 30 req/min (0.5 token/sec, burst 30).
