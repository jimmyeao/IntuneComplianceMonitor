using IntuneComplianceMonitor.ViewModels;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Collections.Concurrent;
using System.Windows;
using Application = System.Windows.Application;

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

        public async Task EnrichDevicesWithUserLocationAsync(List<DeviceViewModel> devices)
        {
            // Create a throttling semaphore to limit concurrent requests
            var semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent calls to respect Graph API limits
            var progressStep = 1.0 / Math.Max(1, devices.Count);
            int processedCount = 0;

            System.Diagnostics.Debug.WriteLine($"Starting location enrichment for {devices.Count} devices");

            var tasks = devices.Select(async device =>
            {
                // Skip devices without a device ID
                if (string.IsNullOrEmpty(device.DeviceId))
                    return;

                await semaphore.WaitAsync();

                try
                {
                    var userId = device.UserId; // UserId should be set when building DeviceViewModel

                    // Try to infer location from the device name or owner first
                    InferLocationFromDeviceInfo(device);

                    // If we already have a valid country from inference, we can skip the Graph call
                    if (!string.IsNullOrWhiteSpace(device.Country) &&
                        device.Country.ToLowerInvariant() != "unknown")
                    {
                        System.Diagnostics.Debug.WriteLine($"Using inferred location for {device.DeviceName}: {device.Country}");
                        return;
                    }

                    // Skip devices without a user ID
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        System.Diagnostics.Debug.WriteLine($"Device {device.DeviceName} has no user ID, skipping Graph location lookup");
                        return;
                    }

                    // Get user details from Graph API
                    try
                    {
                        var user = await ExecuteWithRateLimitingAsync(() =>
                            _graphClient.Users[userId]
                                .GetAsync(req =>
                                {
                                    // Request more fields that might contain location information
                                    req.QueryParameters.Select = new[] {
                                "country",
                                "city",
                                "officeLocation",
                                "businessPhones",
                                "mobilePhone",
                                "usageLocation",
                                "streetAddress",
                                "state",
                                "postalCode"
                                    };
                                }));

                        // Update the device properties on the UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Try multiple fields to find a country
                            var country = user?.Country;

                            // If country is empty, try usageLocation
                            if (string.IsNullOrWhiteSpace(country))
                                country = user?.UsageLocation;

                            // Fall back to the city's country if we can infer it
                            if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(user?.City))
                                country = InferCountryFromCity(user.City);

                            // Fall back to the state's country if we can infer it
                            if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(user?.State))
                                country = InferCountryFromState(user.State);

                            // Check if office location contains country information
                            if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(user?.OfficeLocation))
                                country = ExtractCountryFromOfficeLocation(user.OfficeLocation);

                            // If we still don't have a country, try again with device info
                            if (string.IsNullOrWhiteSpace(country))
                                country = InferCountryFromDeviceName(device.DeviceName);

                            // Finally set the properties
                            device.Country = !string.IsNullOrWhiteSpace(country) ? country : "Unknown";
                            device.City = !string.IsNullOrWhiteSpace(user?.City) ? user.City : "Unknown";
                            device.OfficeLocation = user?.OfficeLocation ?? "";
                        });

                        System.Diagnostics.Debug.WriteLine($"Enriched {device.DeviceName} with location: {device.Country}, {device.City}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error retrieving user {userId} details: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to enrich location for {device.DeviceName}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();

                    // Update status periodically
                    Interlocked.Increment(ref processedCount);
                    if (processedCount % 10 == 0 || processedCount == devices.Count)
                    {
                        StatusMessage?.Invoke($"Processed {processedCount} of {devices.Count} devices");
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Final status update
            StatusMessage?.Invoke($"Completed location enrichment for {devices.Count} devices");
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

        public async Task<Dictionary<string, List<DeviceViewModel>>> GetDevicesGroupedByPolicyAsync(List<DeviceViewModel> devices)
        {
            var result = new Dictionary<string, List<DeviceViewModel>>(StringComparer.OrdinalIgnoreCase);
            var semaphore = new SemaphoreSlim(5); // control parallelism

            var tasks = devices.Select(async device =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var policyStates = await _graphClient.DeviceManagement.ManagedDevices[device.DeviceId]
                        .DeviceCompliancePolicyStates
                        .GetAsync();

                    if (policyStates?.Value != null)
                    {
                        foreach (var policyState in policyStates.Value)
                        {
                            if (policyState.State != Microsoft.Graph.Models.ComplianceStatus.NonCompliant)
                                continue;

                            var policyName = policyState.DisplayName ?? "Unknown Policy";

                            lock (result)
                            {
                                if (!result.TryGetValue(policyName, out var list))
                                {
                                    list = new List<DeviceViewModel>();
                                    result[policyName] = list;
                                }

                                list.Add(device);
                            }

                            // Update device compliance issues inline
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                device.ComplianceIssues.Add(policyName);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Policy fetch error for {device.DeviceName}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return result;
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
                        ComplianceIssues = new List<string> { "Uncompliant" },
                        UserId = device.UserId
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

        // Extract country from office location field
        private string ExtractCountryFromOfficeLocation(string officeLocation)
        {
            if (string.IsNullOrWhiteSpace(officeLocation))
                return null;

            // Common country names that might appear in office location
            var countries = new[] {
        "United Kingdom", "UK", "England", "Scotland", "Wales",
        "United States", "USA", "US", "America",
        "Australia", "Canada", "Germany", "France", "India",
        "Japan", "Singapore", "Ireland", "Spain", "China",
        "Brazil", "Mexico", "Netherlands", "Italy", "Romania",
        "Israel", "New Zealand"
    };

            // Check if any country name appears in the office location
            foreach (var country in countries)
            {
                if (officeLocation.Contains(country, StringComparison.OrdinalIgnoreCase))
                    return country;
            }

            return null;
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

        // Try to infer country from city
        private string InferCountryFromCity(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return null;

            var cityToCountry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "London", "United Kingdom" },
        { "Manchester", "United Kingdom" },
        { "Birmingham", "United Kingdom" },
        { "Liverpool", "United Kingdom" },
        { "Edinburgh", "United Kingdom" },
        { "Glasgow", "United Kingdom" },
        { "New York", "United States" },
        { "Los Angeles", "United States" },
        { "Chicago", "United States" },
        { "Houston", "United States" },
        { "Phoenix", "United States" },
        { "Philadelphia", "United States" },
        { "San Antonio", "United States" },
        { "San Diego", "United States" },
        { "Dallas", "United States" },
        { "San Jose", "United States" },
        { "Sydney", "Australia" },
        { "Melbourne", "Australia" },
        { "Brisbane", "Australia" },
        { "Toronto", "Canada" },
        { "Montreal", "Canada" },
        { "Vancouver", "Canada" },
        { "Berlin", "Germany" },
        { "Munich", "Germany" },
        { "Hamburg", "Germany" },
        { "Paris", "France" },
        { "Marseille", "France" },
        { "Lyon", "France" },
        { "Singapore", "Singapore" },
        { "Dublin", "Ireland" },
        { "Cork", "Ireland" },
        { "Galway", "Ireland" },
        { "Madrid", "Spain" },
        { "Barcelona", "Spain" },
        { "Mumbai", "India" },
        { "Delhi", "India" },
        { "Bangalore", "India" },
        { "Tokyo", "Japan" },
        { "Osaka", "Japan" },
        { "Amsterdam", "Netherlands" },
        { "Rotterdam", "Netherlands" },
        { "Rome", "Italy" },
        { "Milan", "Italy" },
        { "Bucharest", "Romania" },
        { "Cluj", "Romania" },
        { "Timisoara", "Romania" },
        { "Tel Aviv", "Israel" },
        { "Jerusalem", "Israel" },
        { "Auckland", "New Zealand" },
        { "Wellington", "New Zealand" },
        { "Christchurch", "New Zealand" }
    };

            if (cityToCountry.TryGetValue(city, out var country))
                return country;

            return null;
        }

        // Try to infer country from device name
        private string InferCountryFromDeviceName(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return null;

            // Check for common prefixes/suffixes that indicate location
            if (deviceName.Contains("AKL", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("Rangitoto", StringComparison.OrdinalIgnoreCase))
                return "New Zealand";

            if (deviceName.Contains("UK-", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("UKS", StringComparison.OrdinalIgnoreCase))
                return "United Kingdom";

            if (deviceName.Contains("US-", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("USA-", StringComparison.OrdinalIgnoreCase))
                return "United States";

            if (deviceName.Contains("SG-", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("SGP-", StringComparison.OrdinalIgnoreCase))
                return "Singapore";

            if (deviceName.Contains("AU-", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("AUS-", StringComparison.OrdinalIgnoreCase))
                return "Australia";

            // Check for language indicators
            if (deviceName.Contains("de Vincent", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Contains("de Noor", StringComparison.OrdinalIgnoreCase))
                return "France";

            return null;
        }

        // Try to infer country from state/province
        private string InferCountryFromState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            // US States
            var usStates = new[] {
        "Alabama", "Alaska", "Arizona", "Arkansas", "California",
        "Colorado", "Connecticut", "Delaware", "Florida", "Georgia",
        "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa",
        "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland",
        "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri",
        "Montana", "Nebraska", "Nevada", "New Hampshire", "New Jersey",
        "New Mexico", "New York", "North Carolina", "North Dakota", "Ohio",
        "Oklahoma", "Oregon", "Pennsylvania", "Rhode Island", "South Carolina",
        "South Dakota", "Tennessee", "Texas", "Utah", "Vermont",
        "Virginia", "Washington", "West Virginia", "Wisconsin", "Wyoming",
        "DC", "D.C.", "District of Columbia"
    };

            // Canadian Provinces
            var canadaProvinces = new[] {
        "Alberta", "British Columbia", "Manitoba", "New Brunswick",
        "Newfoundland and Labrador", "Northwest Territories", "Nova Scotia",
        "Nunavut", "Ontario", "Prince Edward Island", "Quebec",
        "Saskatchewan", "Yukon"
    };

            // Australian States
            var australiaStates = new[] {
        "New South Wales", "Queensland", "South Australia",
        "Tasmania", "Victoria", "Western Australia",
        "Australian Capital Territory", "Northern Territory"
    };

            // UK Countries/Regions
            var ukRegions = new[] {
        "England", "Scotland", "Wales", "Northern Ireland"
    };

            foreach (var usState in usStates)
            {
                if (state.Equals(usState, StringComparison.OrdinalIgnoreCase))
                    return "United States";
            }

            foreach (var province in canadaProvinces)
            {
                if (state.Equals(province, StringComparison.OrdinalIgnoreCase))
                    return "Canada";
            }

            foreach (var ausState in australiaStates)
            {
                if (state.Equals(ausState, StringComparison.OrdinalIgnoreCase))
                    return "Australia";
            }

            foreach (var region in ukRegions)
            {
                if (state.Equals(region, StringComparison.OrdinalIgnoreCase))
                    return "United Kingdom";
            }

            return null;
        }

        // Helper method to infer location from device info before making Graph API calls
        private void InferLocationFromDeviceInfo(DeviceViewModel device)
        {
            string name = device.DeviceName?.ToLowerInvariant() ?? "";
            string owner = device.Owner?.ToLowerInvariant() ?? "";

            // Check for country codes in device name
            if (name.Contains("-uk") || name.Contains("_uk") || name.EndsWith("-uk") || name.EndsWith("_uk"))
            {
                device.Country = "United Kingdom";
                return;
            }

            if (name.Contains("-us") || name.Contains("_us") || name.EndsWith("-us") || name.EndsWith("_us"))
            {
                device.Country = "United States";
                return;
            }

            if (name.Contains("-au") || name.Contains("_au") || name.EndsWith("-au") || name.EndsWith("_au"))
            {
                device.Country = "Australia";
                return;
            }

            if (name.Contains("-ca") || name.Contains("_ca") || name.EndsWith("-ca") || name.EndsWith("_ca"))
            {
                device.Country = "Canada";
                return;
            }

            if (name.Contains("-de") || name.Contains("_de") || name.EndsWith("-de") || name.EndsWith("_de"))
            {
                device.Country = "Germany";
                return;
            }

            if (name.Contains("-fr") || name.Contains("_fr") || name.EndsWith("-fr") || name.EndsWith("_fr"))
            {
                device.Country = "France";
                return;
            }

            // Look for country names in the device name or owner name
            var countries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "london", "United Kingdom" },
        { "uk", "United Kingdom" },
        { "britain", "United Kingdom" },
        { "england", "United Kingdom" },
        { "scotland", "United Kingdom" },
        { "wales", "United Kingdom" },
        { "usa", "United States" },
        { "america", "United States" },
        { "us", "United States" },
        { "australia", "Australia" },
        { "aus", "Australia" },
        { "canada", "Canada" },
        { "can", "Canada" },
        { "germany", "Germany" },
        { "ger", "Germany" },
        { "france", "France" },
        { "fra", "France" },
        { "india", "India" },
        { "ind", "India" },
        { "japan", "Japan" },
        { "jpn", "Japan" },
        { "singapore", "Singapore" },
        { "sgp", "Singapore" },
        { "ireland", "Ireland" },
        { "irl", "Ireland" },
        { "spain", "Spain" },
        { "esp", "Spain" },
        { "china", "China" },
        { "chn", "China" },
        { "brazil", "Brazil" },
        { "bra", "Brazil" },
        { "mexico", "Mexico" },
        { "mex", "Mexico" },
        { "netherlands", "Netherlands" },
        { "nld", "Netherlands" },
        { "holland", "Netherlands" },
        { "italy", "Italy" },
        { "ita", "Italy" },
        { "romania", "Romania" },
        { "rom", "Romania" },
        { "israel", "Israel" },
        { "isr", "Israel" },
        { "new zealand", "New Zealand" },
        { "nzl", "New Zealand" },
        { "nz", "New Zealand" },
    };

            // Check device name against country list
            foreach (var country in countries)
            {
                if (name.Contains(country.Key) || owner.Contains(country.Key))
                {
                    device.Country = country.Value;
                    return;
                }
            }

            // Check for city names that can identify a country
            var cities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "london", "United Kingdom" },
        { "manchester", "United Kingdom" },
        { "birmingham", "United Kingdom" },
        { "liverpool", "United Kingdom" },
        { "edinburgh", "United Kingdom" },
        { "glasgow", "United Kingdom" },
        { "new york", "United States" },
        { "los angeles", "United States" },
        { "chicago", "United States" },
        { "houston", "United States" },
        { "phoenix", "United States" },
        { "philadelphia", "United States" },
        { "san antonio", "United States" },
        { "san diego", "United States" },
        { "dallas", "United States" },
        { "san jose", "United States" },
        { "sydney", "Australia" },
        { "melbourne", "Australia" },
        { "brisbane", "Australia" },
        { "toronto", "Canada" },
        { "montreal", "Canada" },
        { "vancouver", "Canada" },
        { "berlin", "Germany" },
        { "munich", "Germany" },
        { "hamburg", "Germany" },
        { "paris", "France" },
        { "marseille", "France" },
        { "lyon", "France" },
        { "singapore", "Singapore" },
        { "dublin", "Ireland" },
        { "cork", "Ireland" },
        { "galway", "Ireland" },
        { "madrid", "Spain" },
        { "barcelona", "Spain" },
        { "mumbai", "India" },
        { "delhi", "India" },
        { "bangalore", "India" },
        { "tokyo", "Japan" },
        { "osaka", "Japan" },
        { "amsterdam", "Netherlands" },
        { "rotterdam", "Netherlands" },
        { "rome", "Italy" },
        { "milan", "Italy" },
        { "bucharest", "Romania" },
        { "cluj", "Romania" },
        { "timisoara", "Romania" },
        { "tel aviv", "Israel" },
        { "jerusalem", "Israel" },
        { "auckland", "New Zealand" },
        { "wellington", "New Zealand" },
        { "christchurch", "New Zealand" }
    };

            // Check device name and owner against city list
            foreach (var city in cities)
            {
                if (name.Contains(city.Key) || owner.Contains(city.Key))
                {
                    device.Country = city.Value;
                    device.City = city.Key;
                    return;
                }
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

        #endregion Methods
    }
}