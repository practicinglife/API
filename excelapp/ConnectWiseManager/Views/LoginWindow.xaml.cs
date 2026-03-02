using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using ConnectWiseManager.Models;
using ConnectWiseManager.Services;

namespace ConnectWiseManager.Views;

public partial class LoginWindow : Window
{
    private readonly ICredentialService _credentialService;
    private readonly IAsioApiService _asioApiService;
    private readonly IScreenConnectApiService _screenConnectApiService;
    private readonly IReportingApiService _reportingApiService;

    private ApiCredentials _currentCredentials = new();
    private bool _asioConnected;
    private bool _screenConnectConnected;

    public LoginWindow()
    {
        InitializeComponent();
        
        _credentialService = App.ServiceProvider.GetService(typeof(ICredentialService)) as ICredentialService 
            ?? throw new InvalidOperationException("CredentialService not found");
        _asioApiService = App.ServiceProvider.GetService(typeof(IAsioApiService)) as IAsioApiService 
            ?? throw new InvalidOperationException("AsioApiService not found");
        _screenConnectApiService = App.ServiceProvider.GetService(typeof(IScreenConnectApiService)) as IScreenConnectApiService 
            ?? throw new InvalidOperationException("ScreenConnectApiService not found");
        _reportingApiService = App.ServiceProvider.GetService(typeof(IReportingApiService)) as IReportingApiService
            ?? throw new InvalidOperationException("ReportingApiService not found");

        LoadSavedCredentials();
    }

    private void Log(string message)
    {
        try
        {
            LoginLogTextBox.AppendText(message + System.Environment.NewLine);
            LoginLogTextBox.ScrollToEnd();
        }
        catch { }
    }

    private async void LoadSavedCredentials()
    {
        try
        {
            if (_credentialService.HasStoredCredentials())
            {
                var saved = await _credentialService.LoadCredentialsAsync();
                if (saved != null)
                {
                    _currentCredentials = saved;

                    if (saved.Asio != null)
                    {
                        AsioBaseUrlTextBox.Text = saved.Asio.BaseUrl;
                        AsioClientIdTextBox.Text = saved.Asio.ClientId;
                        AsioClientSecretPasswordBox.Password = saved.Asio.ClientSecret;
                        if (!string.IsNullOrWhiteSpace(saved.Asio.Audience)) AsioAudienceTextBox.Text = saved.Asio.Audience;
                        if (!string.IsNullOrWhiteSpace(saved.Asio.Scope)) AsioScopeTextBox.Text = saved.Asio.Scope;
                        if (!string.IsNullOrWhiteSpace(saved.Asio.TokenEndpoint)) AsioTokenEndpointTextBox.Text = saved.Asio.TokenEndpoint;
                        if (!string.IsNullOrWhiteSpace(saved.Asio.DevicesEndpointPath)) AsioDevicesEndpointTextBox.Text = saved.Asio.DevicesEndpointPath;
                    }

                    if (saved.ScreenConnect != null)
                    {
                        ScreenConnectBaseUrlTextBox.Text = saved.ScreenConnect.BaseUrl;
                        ScreenConnectUsernameTextBox.Text = saved.ScreenConnect.Username;
                        ScreenConnectPasswordBox.Password = saved.ScreenConnect.Password;
                        if (!string.IsNullOrWhiteSpace(saved.ScreenConnect.PersonalAccessToken)) ScreenConnectPatPasswordBox.Password = saved.ScreenConnect.PersonalAccessToken;
                        ScreenConnectOtpTextBox.Text = string.Empty;
                        if (!string.IsNullOrWhiteSpace(saved.ScreenConnect.SessionsEndpointPath)) ScreenConnectSessionsEndpointTextBox.Text = saved.ScreenConnect.SessionsEndpointPath;
                        ScreenConnectTrustCheckbox.IsChecked = saved.ScreenConnect.ShouldTrust;

                        // Load header-secret fields
                        if (!string.IsNullOrWhiteSpace(saved.ScreenConnect.CustomAuthHeaderName))
                            ScreenConnectHeaderNameTextBox.Text = saved.ScreenConnect.CustomAuthHeaderName;
                        if (!string.IsNullOrWhiteSpace(saved.ScreenConnect.CustomAuthHeaderValue))
                            ScreenConnectHeaderSecretPasswordBox.Password = saved.ScreenConnect.CustomAuthHeaderValue; // masked
                    }

                    if (saved.Reporting != null)
                    {
                        if (!string.IsNullOrWhiteSpace(saved.Reporting.ApiKey))
                            ReportingApiKeyPasswordBox.Password = saved.Reporting.ApiKey;
                        if (!string.IsNullOrWhiteSpace(saved.Reporting.BaseUrl))
                            ReportingBaseUrlTextBox.Text = saved.Reporting.BaseUrl;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading saved credentials: {ex.Message}", true);
            Log($"[ERROR] {ex.Message}");
        }
    }

    private async void ConnectAsioButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowStatus("Connecting to Asio...", false);
            Log("[Asio] Connecting...");

            var creds = new AsioCredentials
            {
                BaseUrl = AsioBaseUrlTextBox.Text.Trim().TrimEnd('/'),
                ClientId = AsioClientIdTextBox.Text.Trim(),
                ClientSecret = AsioClientSecretPasswordBox.Password,
                TokenEndpoint = string.IsNullOrWhiteSpace(AsioTokenEndpointTextBox.Text) ? null : AsioTokenEndpointTextBox.Text.Trim(),
                DevicesEndpointPath = string.IsNullOrWhiteSpace(AsioDevicesEndpointTextBox.Text) ? null : AsioDevicesEndpointTextBox.Text.Trim()
            };
            if (!string.IsNullOrWhiteSpace(AsioAudienceTextBox.Text)) creds.Audience = AsioAudienceTextBox.Text.Trim();
            // IMPORTANT: Only set Scope when user provided one; otherwise keep default required scopes
            if (!string.IsNullOrWhiteSpace(AsioScopeTextBox.Text)) creds.Scope = AsioScopeTextBox.Text.Trim();

            _currentCredentials.Asio = creds;

            var ok = await _asioApiService.AuthenticateAsync(_currentCredentials.Asio);
            _asioConnected = ok;
            ShowStatus(ok ? "Asio connected." : "Asio authentication failed.", !ok);
            Log(ok ? "[Asio] Connected." : "[Asio] Authentication failed.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Asio error: {ex.Message}", true);
            Log($"[Asio][ERROR] {ex.Message}");
        }
    }

    private async Task<bool> TryScreenConnectAuthAsync(ScreenConnectCredentials creds)
    {
        var ok = await _screenConnectApiService.AuthenticateAsync(creds);
        return ok;
    }

    private ScreenConnectCredentials BuildScCredsFromUi()
    {
        var headerName = string.IsNullOrWhiteSpace(ScreenConnectHeaderNameTextBox.Text)
            ? null
            : ScreenConnectHeaderNameTextBox.Text.Trim();
        var headerSecret = ScreenConnectHeaderSecretPasswordBox.Password;

        return new ScreenConnectCredentials
        {
            BaseUrl = ScreenConnectBaseUrlTextBox.Text.Trim().TrimEnd('/'),
            Username = ScreenConnectUsernameTextBox.Text.Trim(),
            Password = ScreenConnectPasswordBox.Password,
            PersonalAccessToken = string.IsNullOrWhiteSpace(ScreenConnectPatPasswordBox.Password) ? null : ScreenConnectPatPasswordBox.Password,
            SessionsEndpointPath = string.IsNullOrWhiteSpace(ScreenConnectSessionsEndpointTextBox.Text) ? null : ScreenConnectSessionsEndpointTextBox.Text.Trim(),
            OneTimePassword = string.IsNullOrWhiteSpace(ScreenConnectOtpTextBox.Text) ? null : ScreenConnectOtpTextBox.Text.Trim(),
            ShouldTrust = ScreenConnectTrustCheckbox.IsChecked == true,
            CustomAuthHeaderName = headerName,
            CustomAuthHeaderValue = string.IsNullOrWhiteSpace(headerSecret) ? null : headerSecret
        };
    }

    private async void StartSessionLoginButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If header secret provided, no MFA/username/password needed
            if (!string.IsNullOrWhiteSpace(ScreenConnectHeaderSecretPasswordBox.Password))
            {
                var credsHeader = BuildScCredsFromUi();
                if (string.IsNullOrWhiteSpace(credsHeader.CustomAuthHeaderName))
                    credsHeader.CustomAuthHeaderName = "AuthenticationSecret";
                _currentCredentials.ScreenConnect = credsHeader;
                _screenConnectConnected = true;
                ShowStatus("ScreenConnect header-secret configured.", false);
                Log("[SC] Header-secret configured; session login not required.");
                return;
            }

            ShowStatus("Starting ScreenConnect session login...", false);
            Log("[SC] Session login (login/try) starting...");

            var creds = BuildScCredsFromUi();

            // Force cookie-based session login even if PAT is filled (avoids OTP loop)
            creds.PersonalAccessToken = null;

            // Try login; if OTP required, prompt until success or timeout
            var ok = await TryScreenConnectAuthAsync(creds);
            if (!ok)
            {
                Log("[SC] Login failed or requires OTP. Prompting for OTP...");
                var start = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(3);
                while (DateTime.UtcNow - start < timeout)
                {
                    var input = Microsoft.VisualBasic.Interaction.InputBox(
                        "Enter ScreenConnect One-Time Password (leave blank to retry or Cancel to stop)",
                        "ScreenConnect MFA");

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        await Task.Delay(1500);
                        continue;
                    }

                    creds.OneTimePassword = input.Trim();
                    ok = await TryScreenConnectAuthAsync(creds);
                    if (ok)
                    {
                        _currentCredentials.ScreenConnect = creds;
                        _screenConnectConnected = true;
                        ShowStatus("ScreenConnect connected.", false);
                        Log("[SC] Session login succeeded (cookies trusted: " + creds.ShouldTrust + ").");
                        return;
                    }
                    else
                    {
                        Log("[SC] OTP invalid or expired. Wait for the email and try again.");
                        await Task.Delay(1500);
                    }
                }

                ShowStatus("ScreenConnect authentication failed or timed out waiting for OTP.", true);
                _screenConnectConnected = false;
            }
            else
            {
                _currentCredentials.ScreenConnect = creds;
                _screenConnectConnected = true;
                ShowStatus("ScreenConnect connected.", false);
                Log("[SC] Session login succeeded.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"ScreenConnect error: {ex.Message}", true);
            Log($"[SC][ERROR] {ex.Message}");
        }
    }

    private async void ConnectScreenConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If header secret provided, short-circuit and mark connected for App_Extensions usage
            if (!string.IsNullOrWhiteSpace(ScreenConnectHeaderSecretPasswordBox.Password))
            {
                var credsHeader = BuildScCredsFromUi();
                if (string.IsNullOrWhiteSpace(credsHeader.CustomAuthHeaderName))
                    credsHeader.CustomAuthHeaderName = "AuthenticationSecret";
                _currentCredentials.ScreenConnect = credsHeader;
                _screenConnectConnected = true;
                ShowStatus("ScreenConnect header-secret configured.", false);
                Log("[SC] Connected (header-secret for App_Extensions). No MFA required.");
                return;
            }

            ShowStatus("Connecting to ScreenConnect...", false);
            Log("[SC] Connecting...");

            var creds = BuildScCredsFromUi();

            // If no PAT, attempt Basic + optional OTP with cookie-flow fallback also working
            if (string.IsNullOrWhiteSpace(creds.PersonalAccessToken))
            {
                creds.OneTimePassword = null;
                var ok = await TryScreenConnectAuthAsync(creds);
                if (!ok)
                {
                    Log("[SC] Basic auth failed or requires MFA. Prompting for OTP...");
                    var start = DateTime.UtcNow;
                    var timeout = TimeSpan.FromMinutes(3);
                    while (DateTime.UtcNow - start < timeout)
                    {
                        var input = Microsoft.VisualBasic.Interaction.InputBox(
                            "Enter ScreenConnect One-Time Password (leave blank to retry or Cancel to stop)",
                            "ScreenConnect MFA",
                            ScreenConnectOtpTextBox.Text);

                        if (string.IsNullOrWhiteSpace(input))
                        {
                            await Task.Delay(1500);
                            continue;
                        }

                        creds.OneTimePassword = input.Trim();
                        ok = await TryScreenConnectAuthAsync(creds);
                        if (ok)
                        {
                            _currentCredentials.ScreenConnect = creds;
                            _screenConnectConnected = true;
                            ShowStatus("ScreenConnect connected.", false);
                            Log("[SC] Connected with MFA.");
                            return;
                        }
                        else
                        {
                            Log("[SC] OTP invalid or expired. You can wait for the email and try again.");
                            await Task.Delay(1500);
                        }
                    }

                    ShowStatus("ScreenConnect authentication failed or timed out waiting for OTP.", true);
                    _screenConnectConnected = false;
                    return;
                }
                else
                {
                    _currentCredentials.ScreenConnect = creds;
                    _screenConnectConnected = true;
                    ShowStatus("ScreenConnect connected.", false);
                    Log("[SC] Connected.");
                    return;
                }
            }
            else
            {
                var ok = await TryScreenConnectAuthAsync(creds);
                _currentCredentials.ScreenConnect = creds;
                _screenConnectConnected = ok;
                ShowStatus(ok ? "ScreenConnect connected." : "ScreenConnect authentication failed.", !ok);
                Log(ok ? "[SC] Connected with PAT." : "[SC] PAT authentication failed.");
                return;
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"ScreenConnect error: {ex.Message}", true);
            Log($"[SC][ERROR] {ex.Message}");
        }
    }

    private async void TestReportingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var apiKey = ReportingApiKeyPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ShowStatus("Reporting: enter an API key first.", true);
                return;
            }

            ShowStatus("Testing Reporting API...", false);
            Log("[Reporting] Testing connection...");

            var baseUrl = ReportingBaseUrlTextBox.Text.Trim().TrimEnd('/');
            _reportingApiService.Configure(apiKey, baseUrl);

            var companies = await _reportingApiService.GetCompaniesAsync();
            if (companies.Count > 0)
            {
                ShowStatus($"Reporting API connected — {companies.Count} companies found.", false);
                Log($"[Reporting] Connected. Companies={companies.Count}");
            }
            else
            {
                ShowStatus("Reporting API responded but returned 0 companies. Check your API key permissions.", true);
                Log("[Reporting] 0 companies returned. Key may lack permissions or base URL may be wrong.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Reporting API test failed: {ex.Message}", true);
            Log($"[Reporting][ERROR] {ex.Message}");
        }
    }

    private async void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Build Reporting credentials from UI
            var reportingKey = ReportingApiKeyPasswordBox.Password;
            if (!string.IsNullOrWhiteSpace(reportingKey))
            {
                _currentCredentials.Reporting = new ReportingCredentials
                {
                    ApiKey = reportingKey,
                    BaseUrl = ReportingBaseUrlTextBox.Text.Trim().TrimEnd('/')
                };
                // Configure the service now so MainWindow has it ready
                _reportingApiService.Configure(reportingKey, _currentCredentials.Reporting.BaseUrl);
            }

            if (RememberCredentialsCheckBox.IsChecked == true)
            {
                if (_currentCredentials.Asio == null && !string.IsNullOrWhiteSpace(AsioBaseUrlTextBox.Text))
                {
                    var creds = new AsioCredentials
                    {
                        BaseUrl = AsioBaseUrlTextBox.Text.Trim().TrimEnd('/'),
                        ClientId = AsioClientIdTextBox.Text.Trim(),
                        ClientSecret = AsioClientSecretPasswordBox.Password,
                        TokenEndpoint = string.IsNullOrWhiteSpace(AsioTokenEndpointTextBox.Text) ? null : AsioTokenEndpointTextBox.Text.Trim(),
                        DevicesEndpointPath = string.IsNullOrWhiteSpace(AsioDevicesEndpointTextBox.Text) ? null : AsioDevicesEndpointTextBox.Text.Trim()
                    };
                    if (!string.IsNullOrWhiteSpace(AsioAudienceTextBox.Text)) creds.Audience = AsioAudienceTextBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(AsioScopeTextBox.Text)) creds.Scope = AsioScopeTextBox.Text.Trim();

                    _currentCredentials.Asio = creds;
                }

                if (_currentCredentials.ScreenConnect == null && !string.IsNullOrWhiteSpace(ScreenConnectBaseUrlTextBox.Text))
                {
                    _currentCredentials.ScreenConnect = BuildScCredsFromUi();
                    // Never persist OTP
                    _currentCredentials.ScreenConnect.OneTimePassword = null;
                }

                await _credentialService.SaveCredentialsAsync(_currentCredentials);
                Log("[Creds] Saved (OTP never stored, Reporting API key encrypted via DPAPI).");
            }

            var main = new MainWindow(_currentCredentials);
            main.Show();
            this.Close();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error continuing: {ex.Message}", true);
            Log($"[ERROR] {ex.Message}");
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            isError ? System.Windows.Media.Colors.Red : System.Windows.Media.Colors.Green);
        StatusTextBlock.Visibility = Visibility.Visible;
    }

    private void ApplyCommonHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!client.DefaultRequestHeaders.UserAgent.Any())
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConnectWiseManager/1.0");
    }
}
