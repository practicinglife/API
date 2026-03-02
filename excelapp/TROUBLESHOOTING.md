# Troubleshooting Guide

This guide helps you diagnose and resolve common issues with ConnectWise Manager.

## Table of Contents

- [Authentication Issues](#authentication-issues)
- [Connection Problems](#connection-problems)
- [Data Loading Issues](#data-loading-issues)
- [Script Execution Failures](#script-execution-failures)
- [Build and Deployment Issues](#build-and-deployment-issues)
- [Performance Problems](#performance-problems)
- [UI Issues](#ui-issues)

---

## Authentication Issues

### Issue: "Failed to authenticate with ConnectWise Asio"

**Symptoms:**
- Login fails with Asio authentication error
- Red error message appears below login form

**Possible Causes & Solutions:**

1. **Invalid Credentials**
   - **Check**: Verify Client ID and Client Secret are correct
   - **Solution**: Copy credentials directly from ConnectWise API management page
   - **Test**: Try authenticating via Postman or curl first

2. **Incorrect Base URL**
   - **Check**: Ensure URL is `https://api.connectwise.com` (no trailing slash)
   - **Solution**: Remove any trailing slashes or incorrect paths
   - **Test**: Visit the URL in a browser to verify it's accessible

3. **Missing API Scopes**
   - **Check**: Verify your OAuth2 application has required scopes
   - **Required Scopes**:
     - `platform.devices.read`
     - `platform.companies.read`
     - `platform.sites.read`
     - `platform.custom_fields_definitions.read`
   - **Solution**: Update scopes in ConnectWise API management

4. **Network/Firewall Issues**
   - **Check**: Ensure firewall allows HTTPS traffic
   - **Solution**: Add exception for `api.connectwise.com`
   - **Test**: Try `curl https://api.connectwise.com`

5. **Expired Credentials**
   - **Check**: Verify credentials haven't been revoked
   - **Solution**: Generate new credentials in ConnectWise
   - **Test**: Use new credentials immediately

### Issue: "Failed to authenticate with ScreenConnect"

**Symptoms:**
- ScreenConnect authentication fails
- Error message about Basic Auth

**Possible Causes & Solutions:**

1. **Incorrect Credentials**
   - **Check**: Username and password are correct
   - **Solution**: Reset password in ScreenConnect admin panel
   - **Test**: Log in to ScreenConnect web interface

2. **RESTful API Not Enabled**
   - **Check**: Navigate to Administration → Security in ScreenConnect
   - **Solution**: Enable "RESTful API" option
   - **Required**: Must have admin privileges

3. **Incorrect Base URL**
   - **Check**: URL should be like `https://yourdomain.screenconnect.com`
   - **Solution**: No trailing slashes, no subdirectories
   - **Test**: Access `{baseurl}/Services/PageService.ashx` in browser

4. **Account Permissions**
   - **Check**: User has API access permissions
   - **Solution**: Grant necessary permissions in user settings
   - **Required**: Script execution and session management rights

### Issue: "Credentials Not Saving"

**Symptoms:**
- "Remember Credentials" doesn't work
- Must re-enter credentials each time

**Possible Causes & Solutions:**

1. **Permissions Issue**
   - **Check**: Application has write access to %APPDATA%
   - **Location**: `%APPDATA%\ConnectWiseManager\`
   - **Solution**: Run application as administrator once

2. **Corrupted Credential File**
   - **Check**: Delete `credentials.dat`
   - **Location**: `%APPDATA%\ConnectWiseManager\credentials.dat`
   - **Solution**: Delete file and save credentials again

3. **DPAPI Issues**
   - **Check**: Windows user profile is healthy
   - **Solution**: Create new Windows user profile if corrupted
   - **Alternative**: Don't use "Remember Credentials"

---

## Connection Problems

### Issue: "Connection Timeout"

**Symptoms:**
- Operations hang and eventually timeout
- "Connection timeout" error messages

**Possible Causes & Solutions:**

1. **Network Issues**
   - **Check**: Internet connectivity
   - **Test**: Ping API endpoints
   - **Solution**: Check router/network settings

2. **API Rate Limiting**
   - **Check**: Too many requests in short time
   - **Solution**: Wait a few minutes and retry
   - **Prevention**: Implement request throttling

3. **Firewall/Proxy**
   - **Check**: Corporate firewall blocking requests
   - **Solution**: Configure proxy settings if needed
   - **Alternative**: Use VPN

### Issue: "SSL/TLS Error"

**Symptoms:**
- Certificate validation errors
- "The remote certificate is invalid" message

**Possible Causes & Solutions:**

1. **Outdated Windows**
   - **Check**: Windows Update status
   - **Solution**: Install latest Windows updates
   - **Required**: TLS 1.2 or higher

2. **Self-Signed Certificate**
   - **Check**: ScreenConnect using self-signed cert
   - **Solution**: Install proper SSL certificate
   - **Workaround**: Trust self-signed cert (not recommended)

3. **System Date/Time**
   - **Check**: System clock is accurate
   - **Solution**: Sync with time.windows.com
   - **Verify**: Check timezone settings

---

## Data Loading Issues

### Issue: "No Devices Found"

**Symptoms:**
- Agent list is empty after loading
- "No devices found" message

**Possible Causes & Solutions:**

1. **Empty Account**
   - **Check**: ConnectWise Asio account has devices
   - **Solution**: Add devices to Asio first
   - **Verify**: Log in to Asio web interface

2. **API Endpoint Issues**
   - **Check**: `/api/v1/devices` endpoint is accessible
   - **Solution**: Verify API version in base URL
   - **Test**: Use Postman to test endpoint

3. **Permission Issues**
   - **Check**: API credentials have device read permission
   - **Solution**: Update OAuth2 scopes
   - **Required**: `platform.devices.read`

4. **Filtering Issues**
   - **Check**: Device filters are not too restrictive
   - **Solution**: Clear any applied filters
   - **Test**: Try with default settings

### Issue: "Custom Fields Not Loading"

**Symptoms:**
- Field mapping page is empty
- "Load Custom Fields" doesn't work

**Possible Causes & Solutions:**

1. **Missing Scope**
   - **Check**: OAuth2 has `platform.custom_fields_definitions.read`
   - **Solution**: Add scope to API application
   - **Verify**: Re-authenticate after adding scope

2. **No Custom Fields**
   - **Check**: Account has custom field definitions
   - **Solution**: Create custom fields in Asio first
   - **Verify**: Check Asio admin panel

3. **API Version**
   - **Check**: Using correct API version
   - **Solution**: Verify base URL includes correct version
   - **Current**: Should be v1

---

## Script Execution Failures

### Issue: "Deploy Installer Failed"

**Symptoms:**
- Installer deployment shows "Failed" status
- No installer runs on target device

**Possible Causes & Solutions:**

1. **Invalid Installer URL**
   - **Check**: URL is accessible from target device
   - **Solution**: Test URL in browser on target machine
   - **Verify**: URL uses HTTPS, not HTTP

2. **Insufficient Permissions**
   - **Check**: Target device allows remote execution
   - **Solution**: Verify ScreenConnect agent permissions
   - **Required**: Script execution enabled

3. **Target Device Offline**
   - **Check**: Device is online in ScreenConnect
   - **Solution**: Wait for device to come online
   - **Alternative**: Schedule execution for later

4. **PowerShell Execution Policy**
   - **Check**: Device allows PowerShell scripts
   - **Solution**: Set execution policy to RemoteSigned
   - **Command**: `Set-ExecutionPolicy RemoteSigned`

### Issue: "Repair Script Fails"

**Symptoms:**
- Repair script execution fails
- "Failed" status in logs

**Possible Causes & Solutions:**

1. **ScreenConnect Not Installed**
   - **Check**: ScreenConnect client is installed
   - **Location**: `C:\Program Files\ScreenConnect\`
   - **Solution**: Install client first

2. **MSI File Not Found**
   - **Check**: `ScreenConnect.ClientSetup.msi` exists
   - **Solution**: Repair or reinstall ScreenConnect
   - **Alternative**: Deploy fresh installation

3. **Insufficient Rights**
   - **Check**: Script runs with admin privileges
   - **Solution**: Ensure ScreenConnect service runs as admin
   - **Required**: Administrative access on target

### Issue: "Session Creation Fails"

**Symptoms:**
- Cannot create new sessions
- "Failed" status when creating session

**Possible Causes & Solutions:**

1. **Invalid Device Name**
   - **Check**: Device name is valid and unique
   - **Solution**: Use correct device identifier
   - **Format**: No special characters

2. **API Permissions**
   - **Check**: User can create sessions
   - **Solution**: Grant session creation permission
   - **Location**: ScreenConnect user settings

3. **Session Limit Reached**
   - **Check**: ScreenConnect license allows more sessions
   - **Solution**: Upgrade license or close unused sessions
   - **Verify**: Check active session count

---

## Build and Deployment Issues

### Issue: "Cannot Build on Linux/Mac"

**Symptoms:**
- Build fails with "requires Windows" error
- WPF-related compilation errors

**Solution:**
- **Explanation**: WPF is Windows-only technology
- **Resolution**: Build on Windows machine or VM
- **Alternative**: Use Windows in the Cloud
- **CI/CD**: Use Windows-based build agents

### Issue: "Missing Dependencies"

**Symptoms:**
- NuGet restore fails
- Package reference errors

**Possible Causes & Solutions:**

1. **Outdated Package Cache**
   - **Solution**: Clear NuGet cache
   - **Command**: `dotnet nuget locals all --clear`
   - **Then**: `dotnet restore`

2. **Network Issues**
   - **Check**: Can access nuget.org
   - **Solution**: Configure NuGet proxy if needed
   - **Test**: Visit nuget.org in browser

3. **Package Version Issues**
   - **Check**: .NET 9.0 SDK is installed
   - **Solution**: Install latest .NET SDK
   - **Verify**: `dotnet --version`

### Issue: "Runtime Error on Startup"

**Symptoms:**
- Application crashes immediately
- "Could not load file or assembly" errors

**Possible Causes & Solutions:**

1. **Missing .NET Runtime**
   - **Check**: .NET 9.0 Desktop Runtime installed
   - **Solution**: Download from microsoft.com/net
   - **Required**: Desktop Runtime, not just SDK

2. **Corrupted Build**
   - **Solution**: Clean and rebuild
   - **Commands**:
     ```bash
     dotnet clean
     dotnet build --configuration Release
     ```

3. **Missing Dependencies**
   - **Check**: All DLLs in output directory
   - **Solution**: Publish with dependencies
   - **Command**: `dotnet publish --self-contained`

---

## Performance Problems

### Issue: "Slow Loading Times"

**Symptoms:**
- Long wait when loading agents
- UI becomes unresponsive

**Possible Causes & Solutions:**

1. **Large Device Count**
   - **Check**: Number of devices in account
   - **Solution**: Implement pagination (future enhancement)
   - **Workaround**: Filter devices before loading

2. **Slow API Response**
   - **Check**: API response times
   - **Solution**: Contact ConnectWise support
   - **Workaround**: Cache results temporarily

3. **Network Latency**
   - **Check**: Ping times to API endpoints
   - **Solution**: Use closer data center if available
   - **Alternative**: Implement local caching

### Issue: "High Memory Usage"

**Symptoms:**
- Application uses excessive RAM
- System becomes sluggish

**Possible Causes & Solutions:**

1. **Memory Leak**
   - **Check**: Memory usage grows over time
   - **Solution**: Restart application periodically
   - **Report**: Open GitHub issue with details

2. **Large Result Sets**
   - **Check**: Number of loaded devices
   - **Solution**: Clear device list when not needed
   - **Workaround**: Restart application

---

## UI Issues

### Issue: "Window Not Visible"

**Symptoms:**
- Application appears to start but no window
- Task Manager shows process running

**Possible Causes & Solutions:**

1. **Off-Screen Window**
   - **Cause**: Previous position was on disconnected monitor
   - **Solution**: Delete registry settings
   - **Location**: `HKCU\Software\ConnectWiseManager\`
   - **Alternative**: Alt+Space, M, arrow keys to move window

2. **Minimized to Tray**
   - **Check**: System tray icons
   - **Solution**: Click tray icon to restore
   - **Note**: Current version doesn't use tray

### Issue: "Controls Not Responding"

**Symptoms:**
- Buttons don't respond to clicks
- UI appears frozen

**Possible Causes & Solutions:**

1. **Long-Running Operation**
   - **Check**: Operation in progress
   - **Solution**: Wait for completion
   - **Prevention**: Check logs tab for activity

2. **Application Hang**
   - **Solution**: Close and restart application
   - **Prevention**: Report issue on GitHub
   - **Workaround**: Avoid rapid clicking

### Issue: "Display Scaling Issues"

**Symptoms:**
- Blurry text
- Controls appear too large/small

**Possible Causes & Solutions:**

1. **High DPI Settings**
   - **Check**: Windows display scaling
   - **Solution**: Adjust DPI settings
   - **Compatibility**: Right-click .exe → Properties → Compatibility

2. **Multiple Monitors**
   - **Check**: Different DPI settings on monitors
   - **Solution**: Use consistent scaling across monitors
   - **Windows**: Settings → Display → Scale

---

## Getting Additional Help

### Before Asking for Help

1. **Check Documentation**
   - README.md
   - DEVELOPER.md
   - QUICKSTART.md
   - This troubleshooting guide

2. **Search Existing Issues**
   - GitHub Issues tab
   - Look for similar problems
   - Check closed issues too

3. **Gather Information**
   - Application version
   - Windows version
   - Error messages (full text)
   - Steps to reproduce
   - Screenshots if applicable

### Reporting Bugs

When opening a GitHub issue, include:

1. **Environment**
   - Windows version
   - .NET version
   - Application version

2. **Problem Description**
   - What you expected
   - What actually happened
   - How to reproduce

3. **Logs and Evidence**
   - Error messages
   - Screenshots
   - Script logs (if applicable)
   - Network traces (if relevant)

4. **Attempted Solutions**
   - What you've tried
   - Results of troubleshooting steps

### Feature Requests

For feature requests, include:
- Use case description
- Expected behavior
- Benefit to other users
- Priority level

### Emergency Support

For critical production issues:
1. Check your API service status
2. Verify network connectivity
3. Review recent changes
4. Consider rollback to previous version
5. Contact ConnectWise support if API issue

---

## Diagnostic Tools

### Windows Event Viewer
- Location: `eventvwr.msc`
- Check: Application logs
- Filter: ConnectWiseManager

### Task Manager
- Check: CPU and memory usage
- Verify: Application is running
- Performance: Network activity

### PowerShell Testing
```powershell
# Test Asio API
$headers = @{
    "Authorization" = "Bearer YOUR_TOKEN"
}
Invoke-RestMethod -Uri "https://api.connectwise.com/api/v1/devices" -Headers $headers

# Test ScreenConnect API
$base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("user:pass"))
$headers = @{
    "Authorization" = "Basic $base64Auth"
}
Invoke-RestMethod -Uri "https://yourdomain.screenconnect.com/Services/PageService.ashx/GetHostSessionInfo" -Headers $headers
```

### Network Diagnostics
```bash
# Test connectivity
ping api.connectwise.com
ping yourdomain.screenconnect.com

# Test SSL
openssl s_client -connect api.connectwise.com:443

# Trace route
tracert api.connectwise.com
```

---

## Preventive Measures

1. **Keep Software Updated**
   - Windows Updates
   - .NET Runtime
   - Application updates

2. **Regular Backups**
   - Export configurations
   - Save credential information securely
   - Document custom settings

3. **Monitor Performance**
   - Check logs regularly
   - Watch for error patterns
   - Monitor API quotas

4. **Security Best Practices**
   - Rotate API credentials periodically
   - Use strong passwords
   - Keep credentials secure
   - Review access logs

5. **Testing Before Production**
   - Test on single device first
   - Verify scripts in development
   - Use test environments when available

---

## Known Issues

### Current Limitations

1. **No Token Auto-Refresh**
   - OAuth2 tokens expire
   - Must re-authenticate manually
   - Workaround: Save credentials for quick re-auth

2. **No Offline Mode**
   - Requires internet connectivity
   - Cannot work without API access
   - Planned: Local caching in future

3. **Windows Only**
   - WPF is Windows-specific
   - No Linux/Mac support
   - Alternative: Consider web version

4. **Limited Batch Operations**
   - Manual selection required
   - No bulk filters yet
   - Planned: Advanced filtering

### Planned Improvements

See CHANGELOG.md for upcoming features and fixes.

---

## Community Resources

- **GitHub Repository**: [github.com/practicinglife/excelapp](https://github.com/practicinglife/excelapp)
- **Issues**: Report bugs and request features
- **Discussions**: Ask questions and share tips
- **Wiki**: Community-maintained documentation

---

**Last Updated**: 2025-01-03  
**Version**: 1.0.0
