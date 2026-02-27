# Unified MSP Integration Platform – API Specifications

This repository contains OpenAPI 3.0 specifications for all platforms in the Unified MSP Integration Platform. The platform bridges ConnectWise RMM (Asio), ConnectWise ScreenConnect (Control), ConnectWise Manage (PSA), ESET, SentinelOne, Avast Business, and Microsoft 365 (CSP) to automate common MSP support tasks, streamline ticket resolution, and ensure data consistency across tools.

## API Specifications

| File | Platform | Version | Authentication | Description |
|------|----------|---------|----------------|-------------|
| `All.json` | ConnectWise Manage (PSA) | 2025.16 | API Keys (Public/Private) + Client ID | OpenAPI 3.0 specification for all ConnectWise Manage public REST endpoints. Base URL: `http://na.myconnectwise.net/v4_6_release/apis/3.0` |
| `currentPartnerAPI_228.yaml` | ConnectWise Asio (RMM) | 2.2.8 | OAuth 2.0 / Developer Client ID | OpenAPI 3.0 specification for the ConnectWise Asio partner APIs, covering devices, tickets, automation, OS patching, device groups, and more. |
| `connectwise-control-session-manager-api.yaml` | ConnectWise Control (ScreenConnect) | 1.0.0 | Session-based token / Basic Auth | OpenAPI 3.0 specification for the ConnectWise Control Session Manager API, covering sessions, session groups, captures, audit entries, licensing, and reports. |
| `eset-security-management-api.yaml` | ESET Security Management | 1.0.0 | OAuth 2.0 (password grant, API user) | OpenAPI 3.0 specification for the ESET Business Account / MSP API, covering device management, license provisioning, security tasks, threat data, and policy assignment. |
| `sentinelone-api.yaml` | SentinelOne Endpoint Protection | 2.1 | Static API Token (`ApiToken` header) | OpenAPI 3.0 specification for the SentinelOne Management API, covering endpoint agents, threat monitoring and response, site/group management, and activity logs. |
| `avast-business-hub-api.yaml` | Avast Business Hub | 1.0.0 | OAuth 2.0 Client Credentials (Client ID + Secret) | OpenAPI 3.0 specification for the Avast Business Hub API Gateway, covering multi-tenant company management, device monitoring, subscription activation, alerts, and user invitations. |
| `microsoft-365-csp-api.yaml` | Microsoft 365 / CSP Partner Center | 1.0.0 | OAuth 2.0 App+User (Azure AD) | OpenAPI 3.0 specification for the Microsoft Partner Center API and Microsoft Graph, covering CSP customer management, user creation, password resets, license assignment, and service health. |

## Integration Architecture

The platform uses an **event-driven orchestration** model:

1. An event occurs in a source system (RMM alert, security threat, or helpdesk ticket created).
2. The orchestrator receives the event via webhook or scheduled polling.
3. The orchestrator gathers context by querying related systems (e.g., check client entitlements in ConnectWise Manage, look up device status in SentinelOne).
4. The orchestrator triggers actions in the appropriate systems (deploy AV agent, reset password, run RMM script, create ScreenConnect session).
5. The orchestrator updates the ConnectWise Manage service ticket with the outcome for auditing and customer communication.

### Key Automation Use Cases

| Use Case | Source System | Action Systems |
|----------|--------------|---------------|
| Automated service restart | ConnectWise Asio (RMM alert) | ConnectWise Asio (run script) → ConnectWise Manage (update ticket) |
| AV deployment gap remediation | ConnectWise Asio (no AV detected) | ConnectWise Manage (check entitlement) → ESET / SentinelOne / Avast (assign license + deploy agent) → ConnectWise Manage (log outcome) |
| Automated password reset | ConnectWise Manage (ticket) | Microsoft 365 CSP Partner Center (reset password) → ConnectWise Manage (update ticket) |
| Threat context enrichment | ConnectWise Manage (ticket created) | SentinelOne / ESET / Avast (pull threat alerts for device) → ConnectWise Manage (attach context) |
| Remote support session creation | ConnectWise Manage (ticket assigned) | ConnectWise Control (create session) → ConnectWise Manage (attach session link) |

## Authentication Summary

| Platform | Auth Method | Notes |
|----------|------------|-------|
| ConnectWise Manage | API Keys (Public/Private Key pair) + `clientId` header | Create a dedicated API Member with a custom security role scoped to only the needed endpoints. |
| ConnectWise Asio (RMM) | OAuth 2.0 / Developer Client ID | Use a dedicated API integration user; prefer OAuth 2.0 over legacy username/password. |
| ConnectWise Control | Session token (cookie) or Basic Auth | Enable the REST API extension on the Control server; configure CORS to allow your integration's origin. |
| ESET Security Management | OAuth 2.0 Bearer token (password grant) | API user must be created by a Superuser/Root in the ESET MSP or Business Account console. |
| SentinelOne | Static API Token (`ApiToken <token>` header) | Create a dedicated integration user with the lowest-privilege role needed (Viewer for read-only). |
| Avast Business Hub | OAuth 2.0 Client Credentials (Client ID + Secret) | Create an "API integration" in the Avast Business Hub console to obtain credentials. |
| Microsoft 365 CSP | OAuth 2.0 App+User (Azure AD delegated) | Register an Azure AD app; use App+User flow with a CSP user holding GDAP User Administrator or Privileged Authentication Administrator role in customer tenants. |

## Microsoft CSP Roles for Automated Password Resets

To reset end-user passwords in customer Office 365 tenants via the CSP integration, the partner user or service principal must hold at least one of the following Azure AD roles in the customer tenant (via **GDAP**):

- **User Administrator** – can reset passwords for non-admin users. Use this as the default role for automated password resets; it follows the principle of least privilege.
- **Privileged Authentication Administrator** – can reset passwords for all users including other administrators. This role grants significantly broader access; only assign it when automated resets for admin accounts are specifically required, and document the justification.

> **Security note**: Always start with **User Administrator** and only escalate to **Privileged Authentication Administrator** when your use case explicitly requires resetting passwords for admin-tier accounts. Granting Privileged Authentication Administrator unnecessarily expands the blast radius if the CSP integration credentials are compromised.

Under legacy **DAP**, the **Admin Agent** role grants Global Administrator access to customer tenants. Microsoft's best practice is to migrate to GDAP with least-privilege roles.

The `microsoft-365-csp-api.yaml` spec includes the `PATCH /v1/customers/{customerTenantId}/users/{userId}/resetpassword` endpoint, which uses the Partner Center API to perform automated password resets.

## Security Best Practices

- **Least Privilege**: Grant only the minimal permissions needed to each API integration account. See the per-platform notes in the Authentication Summary above.
- **Dedicated API Credentials**: Use separate API accounts for automation, not personal user logins.
- **Secure Secret Storage**: Store API keys, tokens, and client secrets in a secrets vault (e.g., Azure Key Vault, HashiCorp Vault). Never hard-code credentials in source code or scripts.
- **Encryption in Transit**: All API communications use HTTPS. Ensure TLS certificate validation is enabled in your HTTP client.
- **Audit Logging**: Review API audit logs in each platform (e.g., ConnectWise Manage audit trail, Partner Center audit log) regularly for anomalies.
- **Rate Limiting**: Handle API rate limits gracefully; implement retry logic with exponential back-off and use pagination (e.g., `pageSize` in ConnectWise Manage) to batch large data fetches.
- **Token Rotation**: Rotate API keys and client secrets on a regular schedule and immediately upon suspected compromise.

## Source Documentation

- **ConnectWise Manage**: Public API documented at the ConnectWise developer portal.
- **ConnectWise Asio (RMM)**: Partner API documented at `https://openapi.service.itsupport247.net`.
- **ConnectWise Control Session Manager**: API reference documented in `Session Manager API reference - ConnectWise.html` (included in this repository).
- **ESET Security Management**: API documentation at `https://help.eset.com/ema/2/en-US/`.
- **SentinelOne**: API documentation at `https://usea1.sentinelone.net/api-doc/`.
- **Avast Business Hub**: API documentation at `https://businesshub.avast.com/`.
- **Microsoft Partner Center API**: Documentation at `https://learn.microsoft.com/en-us/partner-center/develop/`.
- **Microsoft Graph API**: Documentation at `https://learn.microsoft.com/en-us/graph/`.

## ConnectWise Control Session Manager API

The Session Manager API (`connectwise-control-session-manager-api.yaml`) was derived from the official ConnectWise Control Session Manager API reference documentation and covers the following areas:

- **Sessions** – Create, read, update, and filter remote support/meeting/access sessions
- **Session Groups** – Manage session group definitions and retrieve group summaries
- **Session Connections** – Connect and disconnect session participants
- **Session Events** – Add events and manage event triggers
- **Session Captures** – Append and retrieve session recording data
- **Session Audit** – Retrieve audit log entries by time range and session name
- **Hosts** – List and refresh eligible session hosts
- **Licensing** – Manage instance licenses and retrieve runtime license info
- **Reports** – Run custom queries against the session database
- **Extensions** – Programmatically configure extension settings
