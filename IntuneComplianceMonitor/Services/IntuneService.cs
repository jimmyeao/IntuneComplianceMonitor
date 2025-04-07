using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntuneComplianceMonitor.ViewModels;
using System.Windows;

namespace IntuneComplianceMonitor.Services
{
    public class IntuneService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly string[] _scopes = new[] {
            "DeviceManagementManagedDevices.Read.All",
            "DeviceManagementConfiguration.Read.All"
        };
        private readonly string _clientId = "787fff8e-d022-495a-a3ea-d306fc23a134"; // Replace with your actual client ID
        private readonly string _tenantId = "739195a1-f5d6-4d9a-ac42-a1dbb7c7413d"; // Replace with your actual tenant ID

        public IntuneService()
        {
            try
            {
                // Initialize MSAL authentication
                var app = PublicClientApplicationBuilder
                    .Create(_clientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, _tenantId)
                    .WithDefaultRedirectUri()
                    .Build();

                // Create a token provider
                var tokenProvider = new TokenProvider(app, _scopes);

                // Create Graph client
                var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
                _graphClient = new GraphServiceClient(authProvider);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Graph client: {ex.Message}", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public async Task<(List<DeviceViewModel>, Dictionary<string, int>)> GetNonCompliantDevicesAsync()
        {
            var result = new List<DeviceViewModel>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var allDevices = new List<ManagedDevice>();

            try
            {
                var response = await _graphClient.DeviceManagement.ManagedDevices
                    .GetAsync(req =>
                    {
                        req.QueryParameters.Filter = "complianceState eq 'noncompliant'";


                        req.QueryParameters.Top = 1000;
                    }, cancellationToken: cancellationTokenSource.Token);

                while (response != null)
                {
                    if (response.Value != null)
                        allDevices.AddRange(response.Value);

                    if (response.OdataNextLink != null)
                    {
                        response = await _graphClient.DeviceManagement.ManagedDevices
                            .WithUrl(response.OdataNextLink)
                            .GetAsync(cancellationToken: cancellationTokenSource.Token);
                    }
                    else break;
                }

                foreach (var device in allDevices)
                {
                    var type = MapDeviceType(device.OperatingSystem);
                    var vm = new DeviceViewModel
                    {
                        DeviceId = device.Id,
                        DeviceName = device.DeviceName,
                        Owner = device.UserPrincipalName,
                        DeviceType = type,
                        LastCheckIn = device.LastSyncDateTime?.DateTime ?? DateTime.MinValue,
                        Ownership = device.ManagedDeviceOwnerType?.ToString() ?? "Unknown",
                        OSVersion = $"{device.OperatingSystem} {device.OsVersion}",
                        SerialNumber = device.SerialNumber,
                        Manufacturer = device.Manufacturer,
                        Model = device.Model,
                        ComplianceIssues = new List<string> { "Marked non-compliant by Intune" } // ← Skip detailed lookup
                    };

                    result.Add(vm);
                    counts[type] = counts.TryGetValue(type, out var current) ? current + 1 : 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching non-compliant devices: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return (result, counts);
        }




        private async Task GetComplianceIssuesAsync(string deviceId, List<string> complianceIssues, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"Starting GetComplianceIssuesAsync for device ID {deviceId}: {DateTime.Now}");

            try
            {
                System.Diagnostics.Debug.WriteLine("Calling _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates.GetAsync()");
                var policyStates = await _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates
                    .GetAsync(cancellationToken: cancellationToken);

                System.Diagnostics.Debug.WriteLine($"Received response from DeviceCompliancePolicyStates.GetAsync: {policyStates?.Value?.Count ?? 0} policies");

                if (policyStates?.Value != null)
                {
                    foreach (var policy in policyStates.Value)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Check if the policy state is not compliant
                        var stateString = policy.State?.ToString()?.ToLower() ?? "";
                        if (stateString != "compliant" && !string.IsNullOrEmpty(stateString))
                        {
                            // Process each policy...
                            System.Diagnostics.Debug.WriteLine($"Processing policy: {policy.DisplayName}");

                            // First, try to get the policy name
                            var policyName = policy.DisplayName ?? "Unknown Policy";

                            // Add a generic compliance issue
                            complianceIssues.Add($"{policyName}: Non-compliant");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Completed GetComplianceIssuesAsync: {DateTime.Now}");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("GetComplianceIssuesAsync was cancelled");
                throw; // Let the caller handle the cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetComplianceIssuesAsync: {ex.Message}");
                // Handle exceptions or log them
                complianceIssues.Add($"Error retrieving compliance details: {ex.Message}");
            }
        }

        private string MapDeviceType(string operatingSystem)
        {
            if (string.IsNullOrEmpty(operatingSystem))
                return "Unknown";

            operatingSystem = operatingSystem.ToLower();

            if (operatingSystem.Contains("windows"))
            {
                if (operatingSystem.Contains("mobile") || operatingSystem.Contains("phone"))
                    return "Mobile";
                return "Desktop";
            }
            else if (operatingSystem.Contains("ios"))
            {
                return "Mobile";
            }
            else if (operatingSystem.Contains("android"))
            {
                return "Mobile";
            }
            else if (operatingSystem.Contains("macos") || operatingSystem.Contains("mac os"))
            {
                return "Laptop";
            }

            return operatingSystem;
        }
    }

    // Custom token provider implementation for Microsoft Graph
    public class TokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _msalClient;
        private readonly string[] _scopes;

        public TokenProvider(IPublicClientApplication msalClient, string[] scopes)
        {
            _msalClient = msalClient;
            _scopes = scopes;
            AllowedHostsValidator = new AllowedHostsValidator();
        }

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"Starting GetAuthorizationTokenAsync: {DateTime.Now}");

            try
            {
                var accounts = await _msalClient.GetAccountsAsync();
                System.Diagnostics.Debug.WriteLine($"Found {accounts.Count()} accounts");

                AuthenticationResult result;

                try
                {
                    // Try to acquire token silently first
                    System.Diagnostics.Debug.WriteLine("Attempting to acquire token silently");
                    result = await _msalClient
                        .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                        .ExecuteAsync(cancellationToken);
                    System.Diagnostics.Debug.WriteLine("Successfully acquired token silently");
                }
                catch (MsalUiRequiredException)
                {
                    System.Diagnostics.Debug.WriteLine("Silent token acquisition failed, trying interactive");
                    // If silent acquisition fails, acquire token interactively
                    result = await _msalClient
                        .AcquireTokenInteractive(_scopes)
                        .ExecuteAsync(cancellationToken);
                    System.Diagnostics.Debug.WriteLine("Successfully acquired token interactively");
                }

                System.Diagnostics.Debug.WriteLine($"Completed GetAuthorizationTokenAsync: {DateTime.Now}");
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAuthorizationTokenAsync: {ex.Message}");
                throw;
            }
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }
    }
}