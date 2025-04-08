using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Identity.Client;

using System.Windows;
using IntuneComplianceMonitor.ViewModels;
using IntuneComplianceMonitor.Services;
using System.Windows;
using Microsoft.Kiota.Abstractions;
using Microsoft.Identity.Client.Desktop;
using System.Collections.Concurrent;

namespace IntuneComplianceMonitor.Services
{
    public class IntuneService
    {
        #region Fields
        private readonly string _clientId;
        private readonly TimeSpan _delayBetweenRequests = TimeSpan.FromMilliseconds(200);
        private readonly GraphServiceClient _graphClient;
        private readonly int _maxConcurrentRequests = 5;
        // Rate limiting settings
        private readonly SemaphoreSlim _requestThrottler;
        private readonly string[] _scopes = new[] {
        "DeviceManagementManagedDevices.Read.All",
        "DeviceManagementConfiguration.Read.All"
    };
        private readonly SettingsService _settingsService;
        private readonly string _tenantId;
        private static ConcurrentDictionary<string, (object Result, DateTime Timestamp)> _methodResultCache
     = new ConcurrentDictionary<string, (object, DateTime)>();
        private const int CACHE_DURATION_MINUTES = 60; // 1 hour cache duration

        private bool TryGetCachedResult<T>(string methodName, out T result)
        {
            result = default;
            if (_methodResultCache.TryGetValue(methodName, out var cachedEntry))
            {
                if (DateTime.Now - cachedEntry.Timestamp < TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                {
                    result = (T)cachedEntry.Result;
                    return true;
                }
            }
            return false;
        }
        #endregion Fields

        #region Constructors

        public IntuneService(SettingsService settingsService)
        {
            _settingsService = settingsService;

            // Get credentials from settings
            _clientId = _settingsService.CurrentSettings.IntuneClientId;
            _tenantId = _settingsService.CurrentSettings.IntuneTenantId;

            // Validate settings
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_tenantId))
            {
                throw new InvalidOperationException("Intune API credentials are missing. Please check your settings.");
            }

            try
            {
                // Initialize MSAL authentication with Desktop extension
                var builder = PublicClientApplicationBuilder
                    .Create(_clientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, _tenantId)
                    .WithDefaultRedirectUri();

                // Add Windows embedded browser support
                var app = builder.WithWindowsEmbeddedBrowserSupport()
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

        #endregion Constructors




        #region Properties

        // Add delegate for status message updates
        public Action<string> StatusMessage { get; set; }

        #endregion Properties

        #region Methods
        private void CacheMethodResult<T>(string methodName, T result)
        {
            _methodResultCache[methodName] = (result, DateTime.Now);
        }
        public async Task<List<ConfigurationProfileViewModel>> GetAppliedConfigurationProfilesAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            var result = new List<ConfigurationProfileViewModel>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"Loading configuration profiles for device: {deviceId}");

                // First get the device details to determine platform
                var device = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.ManagedDevices[deviceId]
                        .GetAsync(cancellationToken: cancellationToken));

                // Get the device platform to filter relevant configuration profiles
                string devicePlatform = device?.OperatingSystem?.ToLower() ?? "";
                System.Diagnostics.Debug.WriteLine($"Device platform for configurations: {devicePlatform}");

                // Get all configuration states
                var statesResponse = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.ManagedDevices[deviceId]
                        .DeviceConfigurationStates
                        .GetAsync(cancellationToken: cancellationToken));

                if (statesResponse?.Value == null || !statesResponse.Value.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No configuration states found for device");
                    return result;
                }

                System.Diagnostics.Debug.WriteLine($"Found {statesResponse.Value.Count} configuration states");

                // Get profile details
                var profilesResponse = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.DeviceConfigurations
                        .GetAsync(cancellationToken: cancellationToken));

                // Build a lookup dictionary
                var profilesLookup = new Dictionary<string, Microsoft.Graph.Models.DeviceConfiguration>(StringComparer.OrdinalIgnoreCase);
                if (profilesResponse?.Value != null)
                {
                    foreach (var profile in profilesResponse.Value)
                    {
                        if (!string.IsNullOrEmpty(profile.Id))
                        {
                            profilesLookup[profile.Id] = profile;
                        }
                    }
                }

                // Match states with profiles with improved filtering
                foreach (var state in statesResponse.Value)
                {
                    // Skip "notApplicable" states
                    if (state.State?.ToString()?.Equals("notApplicable", StringComparison.OrdinalIgnoreCase) == true)
                        continue;

                    string displayName = "Unknown Profile";
                    string description = $"Status: {state.State?.ToString() ?? "Unknown"}";

                    // Try to look up the profile details
                    bool includeProfile = false;

                    if (!string.IsNullOrEmpty(state.Id) && profilesLookup.TryGetValue(state.Id, out var profile))
                    {
                        displayName = profile.DisplayName ?? displayName;
                        description = profile.Description ?? description;

                        // Filter by platform
                        string profileName = displayName.ToLower();

                        // Include profiles that match the device platform or are platform-agnostic
                        if ((devicePlatform.Contains("ios") && profileName.Contains("ios")) ||
                            (devicePlatform.Contains("android") && profileName.Contains("android")) ||
                            (devicePlatform.Contains("windows") && profileName.Contains("windows")) ||
                            (devicePlatform.Contains("mac") && (profileName.Contains("mac") || profileName.Contains("osx"))) ||
                            (!profileName.Contains("ios") && !profileName.Contains("android") &&
                             !profileName.Contains("windows") && !profileName.Contains("mac") &&
                             !profileName.Contains("osx")))
                        {
                            includeProfile = true;
                        }
                    }

                    // Skip if profile should not be included
                    if (!includeProfile)
                        continue;

                    var vm = new ConfigurationProfileViewModel
                    {
                        Id = state.Id,
                        DisplayName = displayName,
                        Description = description,
                        Status = state.State?.ToString() ?? "Unknown"
                    };

                    result.Add(vm);
                }

                System.Diagnostics.Debug.WriteLine($"Returning {result.Count} configuration profiles after filtering");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching configuration profiles: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Full error: {ex}");

                result.Add(new ConfigurationProfileViewModel
                {
                    DisplayName = "Error Loading Configuration Profiles",
                    Description = ex.Message,
                    Status = "Error"
                });
            }

            return result;
        }
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
                        var stateString = policy.State?.ToString()?.ToLower() ?? "";

                        var policyName = policy.DisplayName ?? "Unknown Policy";

                        if (stateString == "notcompliant")
                            complianceIssues.Add($"{policyName}: Non-compliant");

                        else if (stateString == "notapplicable")
                            complianceIssues.Add($"{policyName}: Not applicable");

                        else if (stateString == "compliant")
                            complianceIssues.Add($"{policyName}: Compliant");
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

        public async Task<List<CompliancePolicyStateViewModel>> GetDeviceComplianceStateWithMetadataAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            var result = new List<CompliancePolicyStateViewModel>();

            try
            {
                // First get the basic device details to determine platform
                var device = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.ManagedDevices[deviceId]
                        .GetAsync(cancellationToken: cancellationToken));

                // Get the device platform to filter by platform-specific policies
                string devicePlatform = device?.OperatingSystem?.ToLower() ?? "";
                System.Diagnostics.Debug.WriteLine($"Device platform: {devicePlatform}");

                // Get compliance policy states
                var response = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates
                        .GetAsync(cancellationToken: cancellationToken));

                if (response?.Value != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {response.Value.Count} compliance policies for this device");

                    foreach (var policyState in response.Value)
                    {
                        var stateString = policyState.State?.ToString()?.ToLower() ?? "";
                        var policyName = policyState.DisplayName?.ToLower() ?? "";

                        // Debugging: Log all policy details
                        System.Diagnostics.Debug.WriteLine($"Policy: {policyName}, State: {stateString}");

                        // Determine if the policy is relevant
                        bool isPolicyRelevant =
                            // Always include explicitly non-compliant policies
                            stateString == "noncompliant" ||

                            // Include relevant platform-specific policies
                            (
                                (devicePlatform.Contains("ios") && policyName.Contains("ios")) ||
                                (devicePlatform.Contains("android") && policyName.Contains("android")) ||
                                (devicePlatform.Contains("windows") && policyName.Contains("windows")) ||
                                (devicePlatform.Contains("mac") && (policyName.Contains("mac") || policyName.Contains("osx"))) ||
                                // Always include default policy
                                policyName.Contains("default")
                            );

                        // Skip irrelevant policies
                        if (!isPolicyRelevant &&
                            stateString != "noncompliant" &&
                            stateString != "error")
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping irrelevant policy: {policyName}");
                            continue;
                        }

                        var vm = new CompliancePolicyStateViewModel
                        {
                            PolicyId = policyState.Id,
                            DisplayName = policyState.DisplayName ?? "Unnamed Policy",
                            State = MapComplianceState(stateString),
                            UserPrincipalName = device?.UserPrincipalName ?? "Unknown",
                            LastReportedDateTime = device?.LastSyncDateTime,
                            ErrorDetails = new List<string>()
                        };

                        // Always try to get error details for non-compliant items
                        if (stateString == "noncompliant" || stateString == "error")
                        {
                            await GetNonCompliantSettingsAsync(deviceId, policyState.Id, vm.ErrorDetails, cancellationToken);
                        }

                        result.Add(vm);
                    }

                    System.Diagnostics.Debug.WriteLine($"Returning {result.Count} compliance policies after filtering");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching compliance states: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Full error: {ex}");

                // Add a placeholder item to show the error
                result.Add(new CompliancePolicyStateViewModel
                {
                    DisplayName = "Error Loading Compliance Data",
                    State = "Error",
                    ErrorDetails = new List<string> { ex.Message }
                });
            }

            return result;
        }
        // Helper method to map compliance states
        private string MapComplianceState(string rawState)
        {
            // Normalize state strings and handle edge cases
            switch (rawState.ToLower())
            {
                case "compliant":
                    return "Compliant";
                case "noncompliant":
                    return "Non-Compliant";
                case "notapplicable":
                    return "Not Applicable";
                case "unknown":
                    return "Unknown";
                case "error":
                    return "Error";
                default:
                    System.Diagnostics.Debug.WriteLine($"Unexpected compliance state: {rawState}");
                    return rawState;
            }
        }
        private async Task GetNonCompliantSettingsAsync(string deviceId, string policyId, List<string> errorDetails, CancellationToken cancellationToken)
        {
            try
            {
                var request = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = "{+baseurl}/deviceManagement/managedDevices/{managedDeviceId}/deviceCompliancePolicyStates/{policyId}/settingStates",
                    PathParameters = new Dictionary<string, object>
            {
                { "baseurl", "https://graph.microsoft.com/v1.0" },
                { "managedDeviceId", deviceId },
                { "policyId", policyId }
            }
                };

                var response = await _graphClient.RequestAdapter.SendAsync<DeviceComplianceSettingStateCollectionResponse>(
                    request,
                    DeviceComplianceSettingStateCollectionResponse.CreateFromDiscriminatorValue,
                    cancellationToken: cancellationToken
                );

                if (response?.Value != null)
                {
                    foreach (var setting in response.Value)
                    {
                        if (setting.State.ToString().ToLower() != "compliant")
                        {
                            string details = setting.State.ToString() ?? "Unknown issue";
                            string settingName = setting.Setting ?? "Unknown setting";

                            // Additional error detail extraction
                            try
                            {
                                // Use reflection to find additional error properties
                                var properties = setting.GetType().GetProperties();
                                var additionalDetails = properties
                                    .Where(p =>
                                        p.Name.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                                        p.GetValue(setting) != null)
                                    .Select(p => $"{p.Name}: {p.GetValue(setting)}");

                                details = string.Join("; ", additionalDetails.Concat(new[] { details }));
                            }
                            catch { }

                            errorDetails.Add($"{settingName}: {details}");
                        }
                    }
                }

                if (errorDetails.Count == 0)
                {
                    errorDetails.Add("No specific setting details available");
                }
            }
            catch (Exception ex)
            {
                errorDetails.Add($"Could not load setting details: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error fetching setting details: {ex.Message}");
            }
        }        // New helper method for getting detailed non-compliant settings

        public async Task<Dictionary<string, int>> GetDeviceCountsByTypeAsync()
        {
            // Try to get cached result
            if (TryGetCachedResult("DeviceCountsByType", out Dictionary<string, int> cachedCounts))
            {
                System.Diagnostics.Debug.WriteLine("Returning cached device counts by type");
                return cachedCounts;
            }

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

            // Cache the result
            CacheMethodResult("DeviceCountsByType", counts);

            return counts;
        }

        public async Task<Dictionary<string, int>> GetDeviceCountsByOwnershipAsync()
        {
            // Try to get cached result
            if (TryGetCachedResult("DeviceCountsByOwnership", out Dictionary<string, int> cachedCounts))
            {
                System.Diagnostics.Debug.WriteLine("Returning cached device counts by ownership");
                return cachedCounts;
            }

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

            // Cache the result
            CacheMethodResult("DeviceCountsByOwnership", counts);

            return counts;
        }
        public async Task<(List<DeviceViewModel>, Dictionary<string, int>)> GetNonCompliantDevicesAsync()
        {
            var result = new List<DeviceViewModel>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var allDevices = new List<ManagedDevice>();

            // Get the active devices timeframe from settings
            int activeDevicesTimeframe = _settingsService?.CurrentSettings?.ActiveDevicesTimeframeInDays ?? 30;

            const int batchSize = 1000;
            string nextLink = null;

            try
            {
                // Get initial page of results
                var initialResponse = await ExecuteWithRateLimitingAsync(async () =>
                {
                    return await _graphClient.DeviceManagement.ManagedDevices
                        .GetAsync(req =>
                        {
                            var cutoff = DateTime.UtcNow.AddDays(-activeDevicesTimeframe).ToString("o");

                            // IMPORTANT: Make sure this filter is correct - it might have changed
                            // Try without specifying compliance state first to verify connectivity
                            req.QueryParameters.Filter = $"complianceState eq 'noncompliant' and lastSyncDateTime ge {cutoff}";

                            // Debug by logging the filter
                            System.Diagnostics.Debug.WriteLine($"Non-compliant device filter: {req.QueryParameters.Filter}");

                            req.QueryParameters.Top = batchSize;
                            // Keep the select parameters to minimize data transfer
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
                }

                System.Diagnostics.Debug.WriteLine($"Total non-compliant devices retrieved: {allDevices.Count}");

                // Process the devices
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
                System.Diagnostics.Debug.WriteLine($"Error fetching non-compliant devices: {ex.Message}");
                MessageBox.Show($"Error fetching non-compliant devices: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return (result, counts);
        }

        public async Task<int> GetTotalDeviceCountAsync()
        {
            // Try to get cached result
            if (TryGetCachedResult("TotalDeviceCount", out int cachedCount))
            {
                System.Diagnostics.Debug.WriteLine("Returning cached total device count");
                return cachedCount;
            }

            int activeDevicesTimeframe = _settingsService.CurrentSettings.ActiveDevicesTimeframeInDays;
            var cutoff = DateTime.UtcNow.AddDays(-activeDevicesTimeframe).ToString("o");

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

                int totalCount = (int)(response?.OdataCount ?? 0);

                // Cache the result
                CacheMethodResult("TotalDeviceCount", totalCount);

                return totalCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching total device count: {ex.Message}");
                return 0;
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

        #endregion Methods
    }
}