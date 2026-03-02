# Quick Start Guide

Get up and running with ConnectWise Manager in 5 minutes!

## Prerequisites Checklist

- [ ] Windows 10 or 11
- [ ] .NET 9.0 SDK or Runtime installed
- [ ] ConnectWise Asio API credentials (Client ID & Secret)
- [ ] ScreenConnect instance credentials (Username & Password)
- [ ] Visual Studio 2022 (for building from source)

## Step 1: Get Your API Credentials

### ConnectWise Asio
1. Log in to your ConnectWise account
2. Navigate to API Management
3. Create a new OAuth2 application
4. Note down:
   - Client ID
   - Client Secret
   - Base URL (typically `https://api.connectwise.com`)
5. Ensure these scopes are enabled:
   - `platform.devices.read`
   - `platform.companies.read`
   - `platform.sites.read`
   - `platform.custom_fields_definitions.read`

### ScreenConnect
1. Log in to your ScreenConnect instance
2. Navigate to Administration → Security
3. Enable RESTful API access
4. Create or use existing credentials:
   - Username
   - Password
   - Base URL (e.g., `https://yourdomain.screenconnect.com`)

## Step 2: Build the Application

### Option A: Build from Source (Recommended for Development)

```bash
# Clone the repository
git clone https://github.com/practicinglife/excelapp.git
cd excelapp

# Open in Visual Studio
start ConnectWiseManager.sln

# Or build from command line
dotnet build ConnectWiseManager.sln --configuration Release
```

### Option B: Use Pre-built Release (If Available)

1. Download the latest release from GitHub Releases
2. Extract the ZIP file
3. Run `ConnectWiseManager.exe`

## Step 3: First Launch

1. **Launch the Application**
   ```bash
   cd ConnectWiseManager/bin/Release/net9.0-windows
   ./ConnectWiseManager.exe
   ```

2. **Login Screen**
   - You'll see the login screen with two sections

3. **Enter ConnectWise Asio Credentials**
   - Base URL: `https://api.connectwise.com`
   - Client ID: `your-client-id`
   - Client Secret: `your-client-secret`

4. **Enter ScreenConnect Credentials**
   - Base URL: `https://yourdomain.screenconnect.com`
   - Username: `your-username`
   - Password: `your-password`

5. **Remember Credentials** (Optional)
   - Check "Remember Credentials" to save them securely
   - Uses Windows DPAPI encryption

6. **Click Login**
   - Wait for authentication to complete
   - Both APIs will be tested

## Step 4: Explore the Features

### Agent Management Tab
1. Click "Load Agents" to fetch devices
2. Select one or more devices
3. Try the available actions:
   - Deploy Installer
   - Execute Repair Script
   - Create Session
   - Send Message

### Installer Builder Tab
1. Fill in custom properties:
   - Company name
   - Site name
   - Department
   - Device type
2. Enter installer URL
3. Click "Build Installer Command"
4. Copy the generated PowerShell script

### Field Mapping Tab
1. Click "Load Custom Fields"
2. View available custom fields
3. Map fields as needed

### Script Logs Tab
1. View execution history
2. Check status of operations
3. Review detailed logs

### Session Management Tab
1. Enter a session ID
2. Get session details
3. Add notes or update properties

## Common First-Time Issues

### Issue: "Failed to authenticate with ConnectWise Asio"
**Solution**: 
- Verify your Client ID and Secret
- Check that your Base URL is correct
- Ensure your OAuth2 application has the required scopes

### Issue: "Failed to authenticate with ScreenConnect"
**Solution**:
- Verify your username and password
- Check that RESTful API is enabled in ScreenConnect
- Ensure your Base URL includes the protocol (https://)

### Issue: "No devices found"
**Solution**:
- Verify you have devices in your ConnectWise Asio account
- Check that your API credentials have permission to read devices
- Wait a moment and try again (API might be rate-limited)

### Issue: "Build failed - requires Windows"
**Solution**:
- This is a WPF application and must be built on Windows
- Use a Windows machine or VM to build the project
- Consider using Windows in the Cloud if needed

## Quick Tips

### Keyboard Shortcuts
- **Enter**: Submit login form
- **Esc**: Close dialogs
- **Ctrl+Tab**: Switch between tabs

### Best Practices
1. Always test scripts on a single device first
2. Use the Installer Builder to generate validated scripts
3. Check the Logs tab after each operation
4. Keep your API credentials secure

### Credential Storage
- Credentials are stored in: `%APPDATA%\ConnectWiseManager\credentials.dat`
- Delete this file to clear saved credentials
- File is encrypted with Windows DPAPI

### Testing Mode
For testing without real API access:
1. Enter any values in the login form
2. The app will attempt authentication
3. If it fails, you can still explore the UI
4. Some features may return empty results

## Next Steps

### Explore Advanced Features
- [ ] Create custom installer configurations
- [ ] Map custom fields from Asio
- [ ] Set up session management workflows
- [ ] Review execution logs for patterns

### Customize the Application
- [ ] Read the [Developer Guide](DEVELOPER.md)
- [ ] Review the [Architecture](ARCHITECTURE.md)
- [ ] Check the [API Documentation](README.md#api-documentation)

### Get Help
- [ ] Check [Troubleshooting](README.md#troubleshooting)
- [ ] Review [Common Issues](#common-first-time-issues)
- [ ] Open an issue on GitHub

## Video Tutorial (If Available)

Coming soon: Step-by-step video walkthrough of first-time setup and usage.

## Sample Workflow

Here's a complete workflow to get you started:

1. **Login** with your credentials
2. **Load Agents** to see your device inventory
3. **Select a test device** (single device recommended)
4. **Build an installer** in the Installer Builder tab
5. **Deploy the installer** to your test device
6. **Check the logs** to verify success
7. **Review session details** in Session Management

## Support

Need help? Here's where to look:

1. **README.md** - Comprehensive documentation
2. **DEVELOPER.md** - Technical details
3. **ARCHITECTURE.md** - System design
4. **GitHub Issues** - Report problems or ask questions

## Feedback

We'd love to hear from you!
- Open an issue for bug reports
- Submit a pull request for improvements
- Share your use cases and workflows

---

**Ready to dive deeper?** Check out the full [README](README.md) for detailed information on all features.
