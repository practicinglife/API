# Changelog

All notable changes to ConnectWise Manager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-03

### Added
- Initial release of ConnectWise Manager
- OAuth2 authentication for ConnectWise Asio API
- Token-based authentication for ScreenConnect API
- Secure credential storage using Windows Data Protection API
- Device management interface with customizable field display
- Remote installer deployment via PowerShell scripts
- Repair script execution for ScreenConnect clients
- Custom installer builder with configurable properties
- Field mapping interface for custom fields
- Script logging and execution monitoring
- Session management controls (create, update, message)
- Multi-device selection for batch operations
- Real-time status updates and error reporting

### Security
- DPAPI encryption for stored credentials
- Secure token handling in memory
- HTTPS enforcement for all API communications
- No credential logging in error messages

### Documentation
- Comprehensive README with setup instructions
- Developer guide for contributors
- API documentation references
- Troubleshooting guide

## [Unreleased]

### Planned Features
- Automatic token refresh for Asio OAuth2
- Enhanced logging with file export
- Device filtering and search functionality
- Custom script templates
- Bulk operation status tracking
- Configuration profiles for multiple environments
- Scheduled task execution
- Email notifications for script results
