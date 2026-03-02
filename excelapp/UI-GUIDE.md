# User Interface Guide

This guide provides a visual walkthrough of the ConnectWise Manager application interface.

## Login Window

The first screen you'll see when launching the application.

```
┌────────────────────────────────────────────────────┐
│    ConnectWise Manager - Login                     │
├────────────────────────────────────────────────────┤
│                                                     │
│         ConnectWise Manager                         │
│       Secure API Authentication                     │
│                                                     │
│  ┌──────────────────────────────────────────────┐ │
│  │ ConnectWise Asio (OAuth2)                    │ │
│  ├──────────────────────────────────────────────┤ │
│  │ Base URL:                                    │ │
│  │ [https://api.connectwise.com              ] │ │
│  │                                              │ │
│  │ Client ID:                                   │ │
│  │ [your-client-id                           ] │ │
│  │                                              │ │
│  │ Client Secret:                               │ │
│  │ [••••••••••••••••••••••••••••••••••••••••] │ │
│  │                                              │ │
│  │ Required Scopes: platform.devices.read,     │ │
│  │ platform.companies.read, platform.sites.read│ │
│  └──────────────────────────────────────────────┘ │
│                                                     │
│  ┌──────────────────────────────────────────────┐ │
│  │ ScreenConnect (Basic Auth)                   │ │
│  ├──────────────────────────────────────────────┤ │
│  │ Base URL:                                    │ │
│  │ [https://yourdomain.screenconnect.com    ] │ │
│  │                                              │ │
│  │ Username:                                    │ │
│  │ [admin                                    ] │ │
│  │                                              │ │
│  │ Password:                                    │ │
│  │ [••••••••••••••••••••••••••••••••••••••••] │ │
│  └──────────────────────────────────────────────┘ │
│                                                     │
│  ☑ Remember Credentials         [ Login ]          │
└────────────────────────────────────────────────────┘
```

### Features:
- Two credential sections (Asio and ScreenConnect)
- Password fields are masked
- Optional credential persistence
- Clear scope requirements displayed
- Input validation before submission

## Main Window - Agent Management Tab

The primary interface for managing devices and agents.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│    ConnectWise Manager - Agent Management                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ [ Agent Management ] [ Installer Builder ] [ Field Mapping ] [ Script Logs ]│
│                      [ Session Management ]                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Field Selection                                                         ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │ Select fields to display:                                              ││
│  │ [Computer Name                               ▼]  [ Load Agents ]       ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Computer Name │ Company    │ Site      │ MAC Address  │ Device Type   │ │
│  ├───────────────┼────────────┼───────────┼──────────────┼───────────────┤ │
│  │ ☑ PC-001      │ Acme Corp  │ Main HQ   │ 00:1B:44:..  │ Workstation   │ │
│  │ ☐ PC-002      │ Acme Corp  │ Branch 1  │ 00:1B:45:..  │ Workstation   │ │
│  │ ☐ SRV-001     │ Acme Corp  │ Main HQ   │ 00:1B:46:..  │ Server        │ │
│  │ ☑ LAP-001     │ Beta Inc   │ Office    │ 00:1B:47:..  │ Laptop        │ │
│  │ ☐ PC-003      │ Beta Inc   │ Office    │ 00:1B:48:..  │ Workstation   │ │
│  │ ...                                                                     │ │
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Actions                                                                 ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │ [ Deploy Installer ] [ Execute Repair Script ] [ Create Session ]      ││
│  │ [ Send Message ]                                                        ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Features:
- Field selection dropdown
- Multi-select DataGrid with device information
- Batch operation buttons
- Checkbox selection for individual devices

## Installer Builder Tab

Build custom installer scripts with pre-filled parameters.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│    ConnectWise Manager - Installer Builder                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│ [ Agent Management ] [Installer Builder] [ Field Mapping ] [ Script Logs ]  │
│                      [ Session Management ]                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ ScreenConnect Agent Installer Builder                                   ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │                                                                         ││
│  │ Company:                                                                ││
│  │ [Acme Corporation                                                    ] ││
│  │                                                                         ││
│  │ Site:                                                                   ││
│  │ [Main Headquarters                                                   ] ││
│  │                                                                         ││
│  │ Department:                                                             ││
│  │ [IT Department                                                       ] ││
│  │                                                                         ││
│  │ Device Type:                                                            ││
│  │ [Workstation                             ▼]                            ││
│  │                                                                         ││
│  │ Installer URL:                                                          ││
│  │ [https://yourdomain.screenconnect.com/installer.exe              ]    ││
│  │                                                                         ││
│  │                  [ Build Installer Command ]                            ││
│  │                                                                         ││
│  │ Generated Command:                                                      ││
│  │ ┌──────────────────────────────────────────────────────────────────┐  ││
│  │ │#!ps                                                               │  ││
│  │ │# ScreenConnect Agent Installer                                   │  ││
│  │ │# Company: Acme Corporation                                       │  ││
│  │ │# Site: Main Headquarters                                         │  ││
│  │ │# Department: IT Department                                       │  ││
│  │ │# Device Type: Workstation                                        │  ││
│  │ │                                                                   │  ││
│  │ │$installerUrl = "https://yourdomain.screenconnect.com/..."       │  ││
│  │ │Invoke-WebRequest $installerUrl -OutFile "C:\Temp\installer.exe" │  ││
│  │ │Start-Process "C:\Temp\installer.exe" -ArgumentList "/silent..."  │  ││
│  │ └──────────────────────────────────────────────────────────────────┘  ││
│  │                                                                         ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Features:
- Input fields for custom properties
- Device type dropdown
- Installer URL configuration
- Real-time PowerShell script generation
- Copy-ready output

## Field Mapping Tab

Configure custom field mappings from ConnectWise Asio.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│    ConnectWise Manager - Field Mapping                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│ [ Agent Management ] [ Installer Builder ] [Field Mapping] [ Script Logs ]  │
│                      [ Session Management ]                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Custom Field Mapping                                                    ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │                                                                         ││
│  │ Map ConnectWise Asio custom fields to local fields                     ││
│  │                                                                         ││
│  │              [ Load Custom Fields ]                                     ││
│  │                                                                         ││
│  │ ┌──────────────────────────────────────────────────────────────────┐  ││
│  │ │ Asio Field          │ Display Name          │ Type               │  ││
│  │ ├─────────────────────┼───────────────────────┼────────────────────┤  ││
│  │ │ customField_001     │ Department            │ string             │  ││
│  │ │ customField_002     │ Cost Center           │ string             │  ││
│  │ │ customField_003     │ Purchase Date         │ datetime           │  ││
│  │ │ customField_004     │ Warranty Expiration   │ datetime           │  ││
│  │ │ customField_005     │ Asset Tag             │ string             │  ││
│  │ │ ...                                                                │  ││
│  │ └──────────────────────────────────────────────────────────────────┘  ││
│  │                                                                         ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Features:
- Load custom field definitions from Asio API
- Display field metadata (name, type)
- Editable grid for field mappings
- Support for various field types

## Script Logs Tab

Monitor script execution and view detailed logs.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│    ConnectWise Manager - Script Logs                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│ [ Agent Management ] [ Installer Builder ] [ Field Mapping ] [Script Logs]  │
│                      [ Session Management ]                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Session ID  │ Device    │ Script Type   │ Status  │ Start Time        │ │
│  ├─────────────┼───────────┼───────────────┼─────────┼───────────────────┤ │
│  │ sess-001    │ PC-001    │ Installer     │ Success │ 2025-01-03 10:15  │ │
│  │ sess-002    │ LAP-001   │ Installer     │ Success │ 2025-01-03 10:16  │ │
│  │ sess-003    │ PC-002    │ Repair Script │ Failed  │ 2025-01-03 10:20  │ │
│  │ sess-004    │ SRV-001   │ Send Message  │ Success │ 2025-01-03 10:25  │ │
│  │ ...                                                                     │ │
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Execution Details                                                       ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │ Session ID: sess-003                                                    ││
│  │ Device: PC-002                                                          ││
│  │ Status: Failed                                                          ││
│  │ Start Time: 2025-01-03 10:20:15                                         ││
│  │ End Time: 2025-01-03 10:20:45                                           ││
│  │                                                                         ││
│  │ Error: Connection timeout while executing repair script                ││
│  │ Details: The remote device did not respond within the timeout period   ││
│  │                                                                         ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Features:
- Real-time execution log table
- Success/failure status indicators
- Timestamp tracking
- Detailed error messages
- Session selection for detail view

## Session Management Tab

Control and monitor remote sessions.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│    ConnectWise Manager - Session Management                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│ [ Agent Management ] [ Installer Builder ] [ Field Mapping ] [ Script Logs ]│
│                      [Session Management]                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐│
│  │ Active Sessions                                                         ││
│  ├────────────────────────────────────────────────────────────────────────┤│
│  │                                                                         ││
│  │ Session ID:                                                             ││
│  │ [sess-001                                                            ] ││
│  │                                                                         ││
│  │ [ Get Session Details ] [ Add Note ] [ Update Properties ]             ││
│  │                                                                         ││
│  │ Session Details:                                                        ││
│  │ ┌──────────────────────────────────────────────────────────────────┐  ││
│  │ │ Session ID: sess-001                                              │  ││
│  │ │ Device Name: PC-001                                               │  ││
│  │ │ Status: Connected                                                 │  ││
│  │ │ Start Time: 2025-01-03 09:00:00                                   │  ││
│  │ │                                                                   │  ││
│  │ │ Custom Properties:                                                │  ││
│  │ │   - Company: Acme Corporation                                     │  ││
│  │ │   - Site: Main HQ                                                 │  ││
│  │ │   - Department: IT                                                │  ││
│  │ │   - Device Type: Workstation                                      │  ││
│  │ │                                                                   │  ││
│  │ │ Last Activity: 2025-01-03 10:30:00                                │  ││
│  │ │ Connection Quality: Excellent                                     │  ││
│  │ │                                                                   │  ││
│  │ └──────────────────────────────────────────────────────────────────┘  ││
│  │                                                                         ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Features:
- Session ID input
- Session detail retrieval
- Note addition capability
- Property updates
- Connection status monitoring

## Common Dialog Boxes

### Deploy Installer Dialog
```
┌──────────────────────────────────┐
│ Deploy Installer                 │
├──────────────────────────────────┤
│                                  │
│ Enter the installer URL:         │
│                                  │
│ ┌──────────────────────────────┐ │
│ │ https://yourdomain.screen... │ │
│ └──────────────────────────────┘ │
│                                  │
│        [ OK ]      [ Cancel ]    │
└──────────────────────────────────┘
```

### Confirmation Dialog
```
┌──────────────────────────────────────┐
│ Confirm Repair                       │
├──────────────────────────────────────┤
│                                      │
│ Are you sure you want to execute    │
│ the repair script on the selected   │
│ agents?                              │
│                                      │
│          [ Yes ]      [ No ]         │
└──────────────────────────────────────┘
```

### Success Message
```
┌──────────────────────────────────┐
│ Success                          │
├──────────────────────────────────┤
│                                  │
│ Installer deployment initiated   │
│                                  │
│              [ OK ]              │
└──────────────────────────────────┘
```

### Error Message
```
┌──────────────────────────────────┐
│ Error                            │
├──────────────────────────────────┤
│                                  │
│ Failed to authenticate with      │
│ ConnectWise Asio. Please check   │
│ your credentials.                │
│                                  │
│              [ OK ]              │
└──────────────────────────────────┘
```

## UI Design Principles

### Color Scheme
- **Primary**: Blue (#2196F3) for buttons and highlights
- **Success**: Green for successful operations
- **Error**: Red for errors and failures
- **Background**: White/Light Gray for clean appearance

### Layout
- Tab-based navigation for easy feature access
- GroupBox containers for logical sections
- Consistent spacing and padding (5-10px margins)
- Responsive layout with proper alignment

### Accessibility
- Clear labels for all inputs
- Keyboard navigation support (Tab, Enter, Esc)
- Visual feedback for button hover states
- Read-only fields clearly indicated
- Password masking for security

### User Experience
- Minimal clicks to perform common tasks
- Confirmation dialogs for destructive actions
- Real-time feedback and status updates
- Clear error messages with actionable guidance
- Progress indication for long operations

## Navigation Flow

```
Login Window
    │
    ├─▶ Success → Main Window
    │                │
    │                ├─▶ Agent Management Tab (default)
    │                │       │
    │                │       ├─▶ Load Agents
    │                │       ├─▶ Deploy Installer
    │                │       ├─▶ Execute Repair
    │                │       ├─▶ Create Session
    │                │       └─▶ Send Message
    │                │
    │                ├─▶ Installer Builder Tab
    │                │       │
    │                │       └─▶ Build Installer Command
    │                │
    │                ├─▶ Field Mapping Tab
    │                │       │
    │                │       └─▶ Load Custom Fields
    │                │
    │                ├─▶ Script Logs Tab
    │                │       │
    │                │       └─▶ View Execution History
    │                │
    │                └─▶ Session Management Tab
    │                        │
    │                        ├─▶ Get Session Details
    │                        ├─▶ Add Note
    │                        └─▶ Update Properties
    │
    └─▶ Failure → Show Error Message
                     │
                     └─▶ Retry Login
```

## Responsive Behavior

The application adapts to different window sizes:

- **Minimum Window Size**: 800x600 pixels
- **Recommended**: 1200x800 pixels
- **ScrollViewer**: Added to forms with many fields
- **DataGrid**: Horizontal scroll for many columns
- **Text Wrapping**: Enabled for long text content

## Status Indicators

Visual feedback is provided through:

- **Color Coding**: Green (success), Red (error), Blue (info)
- **Progress Bars**: For long-running operations (future enhancement)
- **Disabled States**: Grayed out buttons when unavailable
- **Loading Indicators**: Text messages during API calls
- **Tooltips**: Hover text for additional information (future enhancement)

---

**Note**: This is a text-based representation of the WPF UI. The actual application uses Windows Presentation Foundation with rich styling, animations, and modern controls.
