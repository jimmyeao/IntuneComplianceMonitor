using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Identity.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntuneComplianceMonitor.ViewModels;
using IntuneComplianceMonitor.Services;
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

        // Rate limiting settings
        private readonly SemaphoreSlim _requestThrottler;
        private readonly int _maxConcurrentRequests = 5;
        private readonly TimeSpan _delayBetweenRequests = TimeSpan.FromMilliseconds(200);

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

                // Initialize the request throttler
                _requestThrottler = new SemaphoreSlim(_maxConcurrentRequests);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Graph client: {ex.Message}", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        // Wrapper method to enforce rate limiting
        private async Task<T> ExecuteWithRateLimitingAsync<T>(Func<Task<T>> graphOperation, CancellationToken cancellationToken = default)
        {
            await _requestThrottler.WaitAsync(cancellationToken);
            try
            {
                // Add a small delay to prevent bursts of requests
                await Task.Delay(_delayBetweenRequests, cancellationToken);

                // Execute the actual Graph operation
                return await graphOperation();
            }
            finally
            {
                _requestThrottler.Release();
            }
        }

        public async Task<(List<DeviceViewModel>, Dictionary<string, int>)> GetNonCompliantDevicesAsync()
        {
            var result = new List<DeviceViewModel>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // Increase timeout for large environments

            var allDevices = new List<ManagedDevice>();

            // Handle large datasets with batching
            const int batchSize = 1000; // Microsoft Graph typically limits to 1000 items per request
            string nextLink = null;

            try
            {
                // Get initial page of results
                var initialResponse = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices
                        .GetAsync(req =>
                        {
                            var cutoff = DateTime.UtcNow.AddDays(-30).ToString("o");
                            req.QueryParameters.Filter = $"complianceState eq 'noncompliant' and lastSyncDateTime ge {cutoff}";
                            req.QueryParameters.Top = batchSize;
                            // Select only needed fields to reduce payload size
                            req.QueryParameters.Select = new[] {
                                "id", "deviceName", "userPrincipalName", "operatingSystem",
                                "osVersion", "managedDeviceOwnerType", "lastSyncDateTime",
                                "serialNumber", "manufacturer", "model"
                            };
                        }, cancellationToken: cancellationTokenSource.Token);
                });

                // Process initial page
                if (initialResponse?.Value != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Retrieved first page with {initialResponse.Value.Count} devices");
                    allDevices.AddRange(initialResponse.Value);
                }

                nextLink = initialResponse?.OdataNextLink;
                int pageCount = 1;

                // Process subsequent pages
                while (!string.IsNullOrEmpty(nextLink))
                {
                    var nextPageResponse = await ExecuteWithRateLimitingAsync(async () =>
                    {
                        return await _graphClient.DeviceManagement.ManagedDevices
                            .WithUrl(nextLink)
                            .GetAsync(cancellationToken: cancellationTokenSource.Token);
                    });

                    if (nextPageResponse?.Value != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Retrieved page {++pageCount} with {nextPageResponse.Value.Count} devices");
                        allDevices.AddRange(nextPageResponse.Value);
                    }

                    nextLink = nextPageResponse?.OdataNextLink;

                    // Update progress for UI
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage?.Invoke($"Loading devices... ({allDevices.Count} loaded)");
                    });
                }

                System.Diagnostics.Debug.WriteLine($"Total devices retrieved: {allDevices.Count}");

                // Process the devices (convert to view models)
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
                        ComplianceIssues = new List<string> { "Loading..." }
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

        public async Task<int> GetTotalDeviceCountAsync()
        {
            var cutoff = DateTime.UtcNow.AddDays(-30).ToString("o");

            try
            {
                var response = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices
                        .GetAsync(req =>
                        {
                            req.QueryParameters.Count = true;
                            req.QueryParameters.Top = 1;
                            req.QueryParameters.Filter = $"lastSyncDateTime ge {cutoff}";
                        });
                });

                return (int)(response?.OdataCount ?? 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching total device count: {ex.Message}");
                return 0;
            }
        }

        public async Task<Dictionary<string, int>> GetDeviceCountsByOwnershipAsync()
        {
            var ownershipTypes = new[] { "company", "personal" };
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var cutoff = DateTime.UtcNow.AddDays(-30).ToString("o");

            foreach (var type in ownershipTypes)
            {
                var response = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices
                        .GetAsync(req =>
                        {
                            req.QueryParameters.Count = true;
                            req.QueryParameters.Top = 1;
                            req.QueryParameters.Filter = $"managedDeviceOwnerType eq '{type}' and lastSyncDateTime ge {cutoff}";
                        });
                });

                counts[type] = (int)(response?.OdataCount ?? 0);
            }

            return counts;
        }

        public async Task<Dictionary<string, int>> GetDeviceCountsByTypeAsync()
        {
            var osTypes = new[] { "windows", "macos", "ios", "android" };
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var cutoff = DateTime.UtcNow.AddDays(-30).ToString("o");

            foreach (var os in osTypes)
            {
                var response = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices
                        .GetAsync(req =>
                        {
                            req.QueryParameters.Count = true;
                            req.QueryParameters.Top = 1;
                            req.QueryParameters.Filter = $"operatingSystem eq '{os}' and lastSyncDateTime ge {cutoff}";
                        });
                });

                counts[os] = (int)(response?.OdataCount ?? 0);
            }

            return counts;
        }

        // Add delegate for status message updates
        public Action<string> StatusMessage { get; set; }

        public async Task GetComplianceIssuesAsync(string deviceId, List<string> complianceIssues, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"Starting GetComplianceIssuesAsync for device ID {deviceId}: {DateTime.Now}");

            try
            {
                System.Diagnostics.Debug.WriteLine("Calling _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates.GetAsync()");

                var policyStates = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates
                        .GetAsync(cancellationToken: cancellationToken);
                });

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
}