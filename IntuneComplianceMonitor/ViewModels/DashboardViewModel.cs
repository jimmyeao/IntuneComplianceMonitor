using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace IntuneComplianceMonitor.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region Fields

        private int _totalDevices;
        private int _nonCompliantDevices;
        private int _devicesNotCheckedInRecently;
        private ObservableCollection<DeviceViewModel> _devices;
        private ObservableCollection<DeviceViewModel> _nonCompliantDevicesList;
        private Dictionary<string, int> _devicesByType;
        private Dictionary<string, int> _devicesByOwnership;
        private Dictionary<string, int> _complianceIssuesByType;
        private bool _isLoading;
        private string _searchText;
        private string _selectedDeviceType;
        private string _selectedOwnership;
        private int _daysNotCheckedIn;
        private ObservableCollection<string> _deviceTypes;
        private ObservableCollection<string> _ownershipTypes;
        private bool _showOnlyNonCompliant;
        private bool _filterByNotCheckedIn;
        private string _statusMessage;
        private List<DeviceViewModel> _allDevicesCache;
        private Dictionary<string, int> _cachedDeviceTypeCounts;
        private Dictionary<string, int> _cachedOwnershipCounts;
        private int? _cachedTotalDeviceCount;
        private string _title = "Intune Compliance Monitor";

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion Fields

        #region Constructors

        public DashboardViewModel()
        {
            // Get settings
            var settings = ServiceManager.Instance.SettingsService.CurrentSettings;

            Devices = new ObservableCollection<DeviceViewModel>();
            NonCompliantDevicesList = new ObservableCollection<DeviceViewModel>();
            DevicesByType = new Dictionary<string, int>();
            DevicesByOwnership = new Dictionary<string, int>();
            ComplianceIssuesByType = new Dictionary<string, int>();
            DeviceTypes = new ObservableCollection<string>();
            OwnershipTypes = new ObservableCollection<string>();

            // Use settings for default values
            DaysNotCheckedIn = settings.DaysNotCheckedIn;
            StatusMessage = "Ready";

            RefreshCommand = new RelayCommand(_ => LoadData());
            ExportCommand = new RelayCommand(_ => ExportData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());

            // Apply initial filter if needed
            if (ShowOnlyNonCompliant || FilterByNotCheckedIn)
            {
                // We'll load data when the view is loaded
            }
        }

        #endregion Constructors

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Properties

        public ISeries[] DeviceTypesSeries { get; set; }
        public ISeries[] OwnershipSeries { get; set; }

        public ICommand ApplyFiltersCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, int> ComplianceIssuesByType
        {
            get => _complianceIssuesByType;
            set
            {
                if (_complianceIssuesByType != value)
                {
                    _complianceIssuesByType = value;
                    OnPropertyChanged();
                }
            }
        }

        public int DaysNotCheckedIn
        {
            get => _daysNotCheckedIn;
            set
            {
                if (_daysNotCheckedIn != value)
                {
                    _daysNotCheckedIn = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DeviceViewModel> Devices
        {
            get => _devices;
            set
            {
                if (_devices != value)
                {
                    _devices = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, int> DevicesByOwnership
        {
            get => _devicesByOwnership;
            set
            {
                if (_devicesByOwnership != value)
                {
                    _devicesByOwnership = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, int> DevicesByType
        {
            get => _devicesByType;
            set
            {
                if (_devicesByType != value)
                {
                    _devicesByType = value;
                    OnPropertyChanged();
                }
            }
        }

        public int DevicesNotCheckedInRecently
        {
            get => _devicesNotCheckedInRecently;
            set
            {
                if (_devicesNotCheckedInRecently != value)
                {
                    _devicesNotCheckedInRecently = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> DeviceTypes
        {
            get => _deviceTypes;
            set
            {
                if (_deviceTypes != value)
                {
                    _deviceTypes = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ExportCommand { get; }

        public bool FilterByNotCheckedIn
        {
            get => _filterByNotCheckedIn;
            set
            {
                if (_filterByNotCheckedIn != value)
                {
                    _filterByNotCheckedIn = value;
                    OnPropertyChanged();

                    // Don't apply filters here if we're just initializing
                    // ApplyFilters will be called after LoadData
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public int NonCompliantDevices
        {
            get => _nonCompliantDevices;
            set
            {
                if (_nonCompliantDevices != value)
                {
                    _nonCompliantDevices = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DeviceViewModel> NonCompliantDevicesList
        {
            get => _nonCompliantDevicesList;
            set
            {
                if (_nonCompliantDevicesList != value)
                {
                    _nonCompliantDevicesList = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> OwnershipTypes
        {
            get => _ownershipTypes;
            set
            {
                if (_ownershipTypes != value)
                {
                    _ownershipTypes = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public string SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                if (_selectedDeviceType != value)
                {
                    _selectedDeviceType = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public string SelectedOwnership
        {
            get => _selectedOwnership;
            set
            {
                if (_selectedOwnership != value)
                {
                    _selectedOwnership = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public bool ShowOnlyNonCompliant
        {
            get => _showOnlyNonCompliant;
            set
            {
                if (_showOnlyNonCompliant != value)
                {
                    _showOnlyNonCompliant = value;
                    OnPropertyChanged();

                    // Don't apply filters here if we're just initializing
                    // ApplyFilters will be called after LoadData
                }
            }
        }

        public int TotalDevices
        {
            get => _totalDevices;
            set
            {
                if (_totalDevices != value)
                {
                    _totalDevices = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion Properties

        #region Methods

        public async void LoadData(bool forceRefresh = false)
        {
            IsLoading = true;
            StatusMessage = "Loading data...";
            await ServiceManager.Instance.IntuneService.GetTotalDeviceCountAsync(); // forces token acquisition

            var user = ServiceManager.Instance.IntuneService.TokenProvider.CurrentUserPrincipalName;
            if (!string.IsNullOrWhiteSpace(user))
            {
                Title = $"Intune Compliance Monitor — {user}";
            }
            System.Diagnostics.Debug.WriteLine($"[TOKEN DEBUG] UPN: {user}");

            try
            {
                // Always load quick stats first
                await LoadQuickStatsAsync();

                // Clear previous data to ensure fresh load
                _allDevicesCache = null;

                // Try disk cache if not forcing refresh
                if (!forceRefresh)
                {
                    var cachedDevices = await ServiceManager.Instance.DataCacheService.GetDevicesFromCacheAsync();
                    if (cachedDevices != null && cachedDevices.Any())
                    {
                        _allDevicesCache = cachedDevices;

                        // Check if we need to enrich with location data
                        int devicesWithLocation = cachedDevices.Count(d => !string.IsNullOrEmpty(d.Country));
                        System.Diagnostics.Debug.WriteLine($"Cached devices with location data: {devicesWithLocation} of {cachedDevices.Count}");

                        if (devicesWithLocation < cachedDevices.Count * 0.5) // If less than half have location data
                        {
                            StatusMessage = "Enhancing device data with location information...";
                            await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(_allDevicesCache);
                            await ServiceManager.Instance.DataCacheService.SaveDevicesToCacheAsync(_allDevicesCache);
                            StatusMessage = "Location data added to devices";
                        }
                        else
                        {
                            StatusMessage = "Loaded from disk cache";
                        }

                        ApplyDeviceData(_allDevicesCache);
                        IsLoading = false;
                        return;
                    }
                }

                // If we get here, we need to fetch fresh data
                if (ServiceManager.Instance.UseRealData)
                {
                    StatusMessage = "Fetching from Intune...";
                    var (devices, deviceTypeCounts) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();
                    _allDevicesCache = devices;

                    // Enrich with location data
                    StatusMessage = "Enhancing with location data...";
                    await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(_allDevicesCache);

                    // Save to cache with location data included
                    await ServiceManager.Instance.DataCacheService.SaveDevicesToCacheAsync(_allDevicesCache);

                    StatusMessage = "Data loaded from Intune with location information";
                }
                else
                {
                    StatusMessage = "Loading sample data...";
                    _allDevicesCache = await ServiceManager.Instance.SampleDataService.GetDevicesAsync();

                    // Enrich sample data with location information
                    StatusMessage = "Adding location information to sample data...";
                    await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(_allDevicesCache);

                    StatusMessage = "Sample data loaded with location information";
                }

                ApplyDeviceData(_allDevicesCache);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error loading data: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadQuickStatsAsync()
        {
            try
            {
                StatusMessage = "Fetching quick stats...";

                // Get the device counts
                var totalCountTask = ServiceManager.Instance.IntuneService.GetTotalDeviceCountAsync();
                var typeCountsTask = ServiceManager.Instance.IntuneService.GetDeviceCountsByTypeAsync();
                var ownershipCountsTask = ServiceManager.Instance.IntuneService.GetDeviceCountsByOwnershipAsync();

                await Task.WhenAll(totalCountTask, typeCountsTask, ownershipCountsTask);

                TotalDevices = totalCountTask.Result; // This is critical - make sure it's actually assigning the value
                DevicesByType = typeCountsTask.Result;
                DevicesByOwnership = ownershipCountsTask.Result;

                // Now create the charts using OxyPlot
                CreateCharts();

                StatusMessage = "Quick stats loaded";
                if (string.IsNullOrWhiteSpace(this.Title) || this.Title.StartsWith("Intune Compliance Monitor"))
                {
                    var user = await ServiceManager.Instance.IntuneService.EnsureUserPrincipalNameAsync();
                    if (!string.IsNullOrWhiteSpace(user))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var mainWindow = Application.Current.MainWindow as MainWindow;
                            if (mainWindow != null)
                            {
                                mainWindow.Title = this.Title;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading quick stats: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading quick stats: {ex.Message}");
            }
        }

        // Separate method for chart creation
        private void CreateCharts()
        {
            try
            {
                // Device types pie chart
                var deviceTypesSeries = new List<ISeries>();

                // Define consistent colors for device types
                var deviceTypeColors = new Dictionary<string, SKColor>
        {
            { "windows", new SKColor(30, 144, 255) },  // DodgerBlue
            { "macos", new SKColor(0, 200, 81) },      // Green
            { "ios", new SKColor(255, 167, 38) },      // Orange
            { "android", new SKColor(76, 175, 80) }    // Material Green
        };

                // For simplified data display, collect values
                var deviceTypesData = new Dictionary<string, double>();

                foreach (var kvp in DevicesByType)
                {
                    string key = kvp.Key.ToLower();
                    if (deviceTypesData.ContainsKey(key))
                        deviceTypesData[key] += kvp.Value;
                    else
                        deviceTypesData[key] = kvp.Value;
                }

                // Add slices for each device type
                foreach (var kvp in deviceTypesData)
                {
                    SKColor sliceColor;
                    if (deviceTypeColors.TryGetValue(kvp.Key, out sliceColor))
                    {
                        // Use the custom color
                    }
                    else
                    {
                        // Use a default color if not found
                        sliceColor = new SKColor(100, 100, 100);
                    }

                    deviceTypesSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { kvp.Value },
                        Name = kvp.Key,
                        Fill = new SolidColorPaint(sliceColor),
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                        DataLabelsSize = 14,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        InnerRadius = 50
                    });
                }

                DeviceTypesSeries = deviceTypesSeries.ToArray();
                OnPropertyChanged(nameof(DeviceTypesSeries));

                // Ownership pie chart
                var ownershipSeries = new List<ISeries>();

                // Define colors for ownership types
                var ownershipColors = new Dictionary<string, SKColor>
        {
            { "company", new SKColor(76, 175, 80) },   // Material Green
            { "personal", new SKColor(255, 167, 38) }  // Material Orange
        };

                // For simplified data display, collect values
                var ownershipData = new Dictionary<string, double>();

                foreach (var kvp in DevicesByOwnership)
                {
                    string key = kvp.Key.ToLower();
                    if (ownershipData.ContainsKey(key))
                        ownershipData[key] += kvp.Value;
                    else
                        ownershipData[key] = kvp.Value;
                }

                foreach (var kvp in ownershipData)
                {
                    SKColor sliceColor;
                    if (ownershipColors.TryGetValue(kvp.Key, out sliceColor))
                    {
                        // Use the custom color
                    }
                    else
                    {
                        // Use a default color if not found
                        sliceColor = new SKColor(100, 100, 100);
                    }

                    ownershipSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { kvp.Value },
                        Name = kvp.Key,
                        Fill = new SolidColorPaint(sliceColor),
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                        DataLabelsSize = 14,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        InnerRadius = 50
                    });
                }

                OwnershipSeries = ownershipSeries.ToArray();
                OnPropertyChanged(nameof(OwnershipSeries));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating charts: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private void UpdateCharts()
        {
            try
            {
                // Device types pie chart
                var deviceTypesSeries = new List<ISeries>();

                // Only proceed if we have data
                if (_cachedDeviceTypeCounts == null || !_cachedDeviceTypeCounts.Any())
                {
                    DeviceTypesSeries = Array.Empty<ISeries>();
                    OwnershipSeries = Array.Empty<ISeries>();
                    OnPropertyChanged(nameof(DeviceTypesSeries));
                    OnPropertyChanged(nameof(OwnershipSeries));
                    return;
                }

                // Define colors
                var deviceTypeColors = new List<SKColor>
        {
            new SKColor(30, 144, 255),  // Windows - DodgerBlue
            new SKColor(0, 200, 81),    // MacOS - Green
            new SKColor(255, 69, 0),    // iOS - OrangeRed
            new SKColor(76, 175, 80)    // Android - Material Green
        };

                int colorIndex = 0;
                foreach (var item in _cachedDeviceTypeCounts)
                {
                    var currentColor = deviceTypeColors[colorIndex % deviceTypeColors.Count];

                    deviceTypesSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { item.Value },
                        Name = $"{item.Key}: {item.Value}",
                        Fill = new SolidColorPaint(currentColor),
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                        DataLabelsSize = 14,
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        InnerRadius = 50
                    });

                    colorIndex++;
                }

                DeviceTypesSeries = deviceTypesSeries.ToArray();
                OnPropertyChanged(nameof(DeviceTypesSeries));

                // Ownership pie chart
                var ownershipSeries = new List<ISeries>();
                var ownershipColors = new List<SKColor>
        {
            new SKColor(76, 175, 80),   // Company - Material Green
            new SKColor(255, 167, 38)   // Personal - Material Orange
        };

                colorIndex = 0;
                foreach (var item in _cachedOwnershipCounts)
                {
                    var currentColor = ownershipColors[colorIndex % ownershipColors.Count];

                    ownershipSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { item.Value },
                        Name = $"{item.Key}: {item.Value}",
                        Fill = new SolidColorPaint(currentColor),
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                        DataLabelsSize = 14,
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        InnerRadius = 50
                    });

                    colorIndex++;
                }

                OwnershipSeries = ownershipSeries.ToArray();
                OnPropertyChanged(nameof(OwnershipSeries));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating charts: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private void CreateLiveCharts()
        {
            // Device types pie chart
            var deviceTypesSeries = new List<ISeries>();
            var deviceTypeColors = new SKColor[]
            {
        new SKColor(30, 144, 255),  // Windows - DodgerBlue
        new SKColor(0, 200, 81),    // MacOS - Green
        new SKColor(255, 69, 0),    // iOS - OrangeRed
        new SKColor(76, 175, 80)    // Android - Material Green
            };

            int colorIndex = 0;
            foreach (var item in _cachedDeviceTypeCounts)
            {
                deviceTypesSeries.Add(new PieSeries<int>
                {
                    Values = new[] { item.Value },
                    Name = $"{item.Key}: {item.Value}",
                    Fill = new SolidColorPaint(deviceTypeColors[colorIndex % deviceTypeColors.Length]),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                    DataLabelsFormatter = point => $"{point.Context.Series.Name}",
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 14,
                    InnerRadius = 50
                });
                colorIndex++;
            }

            DeviceTypesSeries = deviceTypesSeries.ToArray();
            OnPropertyChanged(nameof(DeviceTypesSeries));

            // Ownership pie chart
            var ownershipSeries = new List<ISeries>();
            var ownershipColors = new SKColor[]
            {
        new SKColor(76, 175, 80),   // Company - Material Green
        new SKColor(255, 167, 38)   // Personal - Material Orange
            };

            colorIndex = 0;
            foreach (var item in _cachedOwnershipCounts)
            {
                ownershipSeries.Add(new PieSeries<int>
                {
                    Values = new[] { item.Value },
                    Name = $"{item.Key}: {item.Value}",
                    Fill = new SolidColorPaint(ownershipColors[colorIndex % ownershipColors.Length]),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                    DataLabelsFormatter = point => $"{point.Context.Series.Name}",
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 14,
                    InnerRadius = 50
                });
                colorIndex++;
            }

            OwnershipSeries = ownershipSeries.ToArray();
            OnPropertyChanged(nameof(OwnershipSeries));
        }

        // Update ClearCache method to also clear disk cache
        public void ClearCache()
        {
            _allDevicesCache = null;
            ServiceManager.Instance.DataCacheService.ClearCache();
        }

        // In DashboardViewModel.cs, modify the ApplyDeviceData method
        private void ApplyDeviceData(List<DeviceViewModel> devices)
        {
            _allDevicesCache = devices;

            // Make sure these lines are properly setting the counts
            NonCompliantDevices = devices.Count;
            NonCompliantDevicesList = new ObservableCollection<DeviceViewModel>(devices);

            var cutoffDate = DateTime.Now.AddDays(-DaysNotCheckedIn);
            DevicesNotCheckedInRecently = devices.Count(d => d.LastCheckIn < cutoffDate);

            if (_cachedOwnershipCounts != null)
            {
                DevicesByOwnership = new Dictionary<string, int>(_cachedOwnershipCounts);
            }

            ComplianceIssuesByType = devices
                .SelectMany(d => d.ComplianceIssues)
                .GroupBy(i => i)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            DeviceTypes = new ObservableCollection<string>(devices.Select(d => d.DeviceType).Distinct());
            OwnershipTypes = new ObservableCollection<string>(devices.Select(d => d.Ownership).Distinct());

            ApplyFilters();
            StatusMessage = $"Loaded {devices.Count} devices";
            System.Diagnostics.Debug.WriteLine($"Total devices: {TotalDevices}");
            System.Diagnostics.Debug.WriteLine($"Non-compliant devices: {NonCompliantDevices}");
            System.Diagnostics.Debug.WriteLine($"Not checked in recently: {DevicesNotCheckedInRecently}");

            // REMOVE this call since we don't need lazy loading anymore
            // StartComplianceReasonFetch(_allDevicesCache);
        }

        // You can also remove the StartComplianceReasonFetch method entirely if it's not used elsewhere
        private void StartComplianceReasonFetch(List<DeviceViewModel> devices)
        {
            Task.Run(async () =>
            {
                var semaphore = new SemaphoreSlim(5); // limit concurrency

                var tasks = devices
                    .Where(d => d.ComplianceIssues.Count == 1 && d.ComplianceIssues[0] == "Loading...") // ✅ Only if not fetched
                    .Select(async device =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var tempList = new List<string>();
                            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            await ServiceManager.Instance.IntuneService.GetComplianceIssuesAsync(device.DeviceId, tempList, timeout.Token);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                device.ComplianceIssues = tempList.Any()
                            ? tempList
                            : new List<string> { "No specific policy issues reported" };
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                device.ComplianceIssues = new List<string> { $"Error: {ex.Message}" };
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                await Task.WhenAll(tasks);

                // ✅ Save updated cache with compliance issues
                await ServiceManager.Instance.DataCacheService.SaveDevicesToCacheAsync(devices);
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void ApplyFilters()
        {
            if (_allDevicesCache == null) return;

            StatusMessage = "Filtering data...";

            try
            {
                var filtered = _allDevicesCache.AsEnumerable();

                // Apply search filter (modified to check country field)
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(d =>
                        (d.DeviceName != null && d.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Owner != null && d.Owner.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (d.SerialNumber != null && d.SerialNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Country != null && d.Country.Contains(SearchText, StringComparison.OrdinalIgnoreCase))); // Added country check
                }

                // Apply device type filter
                if (!string.IsNullOrWhiteSpace(SelectedDeviceType))
                {
                    filtered = filtered.Where(d => d.DeviceType == SelectedDeviceType);
                }

                // Apply ownership filter
                if (!string.IsNullOrWhiteSpace(SelectedOwnership))
                {
                    filtered = filtered.Where(d => d.Ownership == SelectedOwnership);
                }

                // Apply check-in date filter
                if (FilterByNotCheckedIn)
                {
                    var cutoffDate = DateTime.Now.AddDays(-DaysNotCheckedIn);
                    filtered = filtered.Where(d => d.LastCheckIn < cutoffDate);
                }

                // Apply non-compliant filter
                if (ShowOnlyNonCompliant)
                {
                    filtered = filtered.Where(d => d.ComplianceIssues.Any());
                }

                Devices = new ObservableCollection<DeviceViewModel>(filtered);
                StatusMessage = $"Showing {Devices.Count} of {_allDevicesCache.Count} devices";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error applying filters: {ex.Message}", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportData()
        {
            // In a real app, this would export the data to CSV or Excel
            // Implementation depends on your requirements
            System.Windows.MessageBox.Show("Export functionality is not implemented yet.", "Information", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion Methods
    }
}