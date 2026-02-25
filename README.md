# ConnectWise API Specifications

This repository contains OpenAPI specifications for ConnectWise products, derived from the official ConnectWise API documentation.

## Files

| File | Product | Version | Description |
|------|---------|---------|-------------|
| `All.json` | ConnectWise Manage (PSA) | 2025.16 | OpenAPI 3.0 specification for all ConnectWise Manage public REST endpoints. Base URL: `http://na.myconnectwise.net/v4_6_release/apis/3.0` |
| `currentPartnerAPI_228.yaml` | ConnectWise Asio (RMM) | 2.2.8 | OpenAPI 3.0 specification for the ConnectWise Asio partner APIs, covering devices, tickets, automation, OS patching, device groups, and more. |
| `connectwise-control-session-manager-api.yaml` | ConnectWise Control (ScreenConnect) | 1.0.0 | OpenAPI 3.0 specification for the ConnectWise Control Session Manager API, covering sessions, session groups, captures, audit entries, licensing, and reports. |

## Source Documentation

- **ConnectWise Manage**: Public API documented at the ConnectWise developer portal.
- **ConnectWise Asio**: Partner API documented at `https://openapi.service.itsupport247.net`.
- **ConnectWise Control Session Manager**: API reference documented in `Session Manager API reference - ConnectWise.html` (included in this repository).

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
