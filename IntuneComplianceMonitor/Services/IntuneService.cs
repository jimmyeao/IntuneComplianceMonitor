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

        private const int CACHE_DURATION_MINUTES = 60;
        private static ConcurrentDictionary<string, (object Result, DateTime Timestamp)> _methodResultCache
     = new ConcurrentDictionary<string, (object, DateTime)>();

        private readonly string _clientId;
        private readonly TimeSpan _delayBetweenRequests = TimeSpan.FromMilliseconds(200);
        private readonly GraphServiceClient _graphClient;
        private readonly int _maxConcurrentRequests = 5;
        private readonly SemaphoreSlim _requestThrottler;

        private readonly string[] _scopes = new[] {
        "DeviceManagementManagedDevices.Read.All",
        "DeviceManagementConfiguration.Read.All"
    };

        private readonly SettingsService _settingsService;

        private readonly string _tenantId;

        // Rate limiting settings
        private readonly TokenProvider _tokenProvider;

        #endregion Fields

        // 1 hour cache duration

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
                _tokenProvider = new TokenProvider(app, _scopes);
                var authProvider = new BaseBearerTokenAuthenticationProvider(_tokenProvider);
                _graphClient = new GraphServiceClient(authProvider);

                _tokenProvider.LoadAccountInfoAsync();
                // Initialize the request throttler
                _requestThrottler = new SemaphoreSlim(_maxConcurrentRequests);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Graph client: {ex.Message}", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        public async Task<string> EnsureUserPrincipalNameAsync()
        {
            // First try to get it from MSAL
            await _tokenProvider.LoadAccountInfoAsync();
            if (!string.IsNullOrWhiteSpace(_tokenProvider.CurrentUserPrincipalName))
            {
                return _tokenProvider.CurrentUserPrincipalName;
            }

            // If MSAL fails, call /me as fallback
            try
            {
                var me = await _graphClient.Me.GetAsync();
                if (!string.IsNullOrWhiteSpace(me?.UserPrincipalName))
                {
                    _tokenProvider.CurrentUserPrincipalName = me.UserPrincipalName;
                    System.Diagnostics.Debug.WriteLine($"[Graph] Resolved UPN via /me: {_tokenProvider.CurrentUserPrincipalName}");
                    return me.UserPrincipalName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Graph] Failed to fetch UPN via /me: {ex.Message}");
            }

            return "Unknown";
        }


        #endregion Constructors

        #region Properties

        public string CurrentUser => _tokenProvider?.CurrentUserPrincipalName ?? "Unknown";

        // Add delegate for status message updates
        public Action<string> StatusMessage { get; set; }

        public TimeSpan TimeUntilExpiry => _tokenProvider?.TimeUntilExpiry ?? TimeSpan.Zero;

        public DateTime TokenExpires => _tokenProvider?.TokenExpires ?? DateTime.MinValue;

        public TokenProvider TokenProvider => _tokenProvider;

        #endregion Properties

        #region Methods

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
                var device = await ExecuteWithRateLimitingAsync(() =>
                    _graphClient.DeviceManagement.ManagedDevices[deviceId]
                        .GetAsync(cancellationToken: cancellationToken));

                System.Diagnostics.Debug.WriteLine($"Fetched device: {device?.DeviceName} ({device?.Id})");
                System.Diagnostics.Debug.WriteLine($"UserPrincipalName: {device?.UserPrincipalName ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"UserId: {device?.UserId ?? "null"}");

                if (device == null || string.IsNullOrEmpty(device.Id))
                {
                    System.Diagnostics.Debug.WriteLine("Device object or ID is null.");
                    return result;
                }

                // Step 1: Try user-scoped endpoint if userId is available
                List<DeviceCompliancePolicyState> policyStates = null;
                var userId = device.UserId;

                if (!string.IsNullOrEmpty(userId))
                {
                    try
                    {
                        var response = await ExecuteWithRateLimitingAsync(() =>
                            _graphClient.Users[userId].ManagedDevices[deviceId].DeviceCompliancePolicyStates
                                .GetAsync(cancellationToken: cancellationToken));

                        policyStates = response?.Value?.ToList();
                        System.Diagnostics.Debug.WriteLine("User-scoped compliance state loaded.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"User-scoped compliance fetch failed, will try fallback. Error: {ex.Message}");
                    }
                }

                // Step 2: Fallback to device-level if user-scoped failed or wasn't possible
                if (policyStates == null)
                {
                    var fallbackResponse = await ExecuteWithRateLimitingAsync(() =>
                        _graphClient.DeviceManagement.ManagedDevices[deviceId].DeviceCompliancePolicyStates
                            .GetAsync(cancellationToken: cancellationToken));

                    policyStates = fallbackResponse?.Value?.ToList();
                    System.Diagnostics.Debug.WriteLine("Used fallback device-scoped compliance state.");
                }

                // Step 3: Process results
                if (policyStates != null)
                {
                    foreach (var policyState in policyStates)
                    {
                        var stateString = policyState.State?.ToString()?.ToLower() ?? "";

                        if (stateString == "unknown" || stateString == "notapplicable")
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping policy '{policyState.DisplayName}' due to state: {stateString}");
                            continue;
                        }

                        var errorDetails = new List<string>();

                        // Only fetch setting details for noncompliant or error
                        if (stateString == "noncompliant" || stateString == "error")
                        {
                            await GetNonCompliantSettingsAsync(deviceId, policyState.Id, errorDetails, cancellationToken);
                        }

                        result.Add(new CompliancePolicyStateViewModel
                        {
                            PolicyId = policyState.Id,
                            DisplayName = policyState.DisplayName ?? "Unnamed Policy",
                            State = MapComplianceState(stateString),
                            UserPrincipalName = device.UserPrincipalName ?? "Unknown",
                            LastReportedDateTime = device.LastSyncDateTime,
                            ErrorDetails = errorDetails
                        });
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching compliance states: {ex.Message}");
                result.Add(new CompliancePolicyStateViewModel
                {
                    DisplayName = "Error Loading Compliance Data",
                    State = "Error",
                    ErrorDetails = new List<string> { ex.Message }
                });
            }

            return result;
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

        private void CacheMethodResult<T>(string methodName, T result)
        {
            _methodResultCache[methodName] = (result, DateTime.Now);
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
                        // Only add non-compliant settings
                        if (setting.State.ToString().ToLower() != "compliant")
                        {
                            // Translate specific setting non-compliance
                            string translatedError = TranslateComplianceError(setting.Setting);
                            errorDetails.Add(translatedError);
                        }
                    }
                }

                if (errorDetails.Count == 0)
                {
                    errorDetails.Add("Device does not meet compliance requirements");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching setting details: {ex.Message}");
                errorDetails.Add($"Could not load setting details: {ex.Message}");
            }
        }

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

        // Helper method to map compliance states
        private string TranslateComplianceError(string errorCode)
        {
            var errorTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Specific compliance setting translations
        { "DefaultDeviceCompliancePolicy.RequireUserExistence", "No enrolled user associated with the device" },
        { "DefaultDeviceCompliancePolicy.RequireDeviceCompliancePolicyAssigned", "No compliance policy assigned to the device" },
        { "DefaultDeviceCompliancePolicy.RequireRemainContact", "Device is no longer in contact with management system" },
        
        // Fallback translations
        { "RequireUserExistence", "No enrolled user associated with the device" },
        { "RequireDeviceCompliancePolicyAssigned", "No compliance policy assigned to the device" },
        { "RequireRemainContact", "Device is no longer in contact with management system" },
        
        // Generic fallback
        { "NonCompliant", "Device does not meet compliance requirements" }
    };

            // Try exact match first
            if (errorTranslations.TryGetValue(errorCode, out var translation))
            {
                return translation;
            }

            // Try partial match
            var partialMatch = errorTranslations
                .FirstOrDefault(kvp =>
                    errorCode.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            if (partialMatch.Value != null)
            {
                return partialMatch.Value;
            }

            // If no match, return the original error code
            return errorCode;
        }

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
        public async Task<Dictionary<string, List<DeviceViewModel>>> GetDevicesGroupedByPolicyAsync(List<DeviceViewModel> devices)
        {
            var result = new Dictionary<string, List<DeviceViewModel>>(StringComparer.OrdinalIgnoreCase);
            var semaphore = new SemaphoreSlim(5); // limit concurrency

            var tasks = devices.Select(async device =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var policyIssues = new List<string>();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await GetComplianceIssuesAsync(device.DeviceId, policyIssues, timeout.Token);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var issue in policyIssues)
                        {
                            var policyName = issue.Split(':')[0]; // "Policy X: Non-compliant" → "Policy X"

                            if (!result.TryGetValue(policyName, out var list))
                            {
                                list = new List<DeviceViewModel>();
                                result[policyName] = list;
                            }

                            list.Add(device);
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error grouping device '{device.DeviceName}': {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return result;
        }

        #endregion Methods
    }
}