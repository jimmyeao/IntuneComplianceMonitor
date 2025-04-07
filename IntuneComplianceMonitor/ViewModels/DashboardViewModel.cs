using IntuneComplianceMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Legends;





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

        #endregion Fields

        #region Constructors

        public DashboardViewModel()
        {
            // No need to create services here, we'll use the ServiceManager

            Devices = new ObservableCollection<DeviceViewModel>();
            NonCompliantDevicesList = new ObservableCollection<DeviceViewModel>();
            DevicesByType = new Dictionary<string, int>();
            DevicesByOwnership = new Dictionary<string, int>();
            ComplianceIssuesByType = new Dictionary<string, int>();
            DeviceTypes = new ObservableCollection<string>();
            OwnershipTypes = new ObservableCollection<string>();
            DaysNotCheckedIn = 7; // Default value
            StatusMessage = "Ready";

            RefreshCommand = new RelayCommand(_ => LoadData());
            ExportCommand = new RelayCommand(_ => ExportData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());

            // Apply initial filter if needed
            if (ShowOnlyNonCompliant || FilterByNotCheckedIn)
            {
                // We'll load data when the view is loaded, so we don't need to call it here
            }
        }

        #endregion Constructors

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Properties
        public PlotModel DeviceTypePlotModel { get; private set; }
        public PlotModel OwnershipPlotModel { get; private set; }

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

            try
            {
                // Always load quick stats first to ensure we have device count numbers
                await LoadQuickStatsAsync();

                // Check if already cached and not forced to refresh
                if (!forceRefresh && _allDevicesCache != null && _allDevicesCache.Any())
                {
                    StatusMessage = "Using previously loaded device data";
                    ApplyDeviceData(_allDevicesCache);
                    return;
                }

                // If we get here, we need to load fresh data
                if (ServiceManager.Instance.UseRealData)
                {
                    StatusMessage = "Fetching non-compliant devices from Intune...";
                    var (devices, deviceTypeCounts) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();

                    _allDevicesCache = devices;
                    StatusMessage = "Data retrieved from Intune";
                }
                else
                {
                    StatusMessage = "Loading sample data...";
                    _allDevicesCache = await ServiceManager.Instance.SampleDataService.GetDevicesAsync();
                    StatusMessage = "Sample data loaded";
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
                StartComplianceReasonFetch(_allDevicesCache);
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
            // Device Type Donut Chart
            DeviceTypePlotModel = new PlotModel
            {
                Title = string.Empty,
                PlotAreaBorderThickness = new OxyThickness(0),
                Background = OxyColors.Transparent
            };

            var typePieSeries = new PieSeries
            {
                InnerDiameter = 0.55,
                StrokeThickness = 1.5,
                OutsideLabelFormat = "{1}: {0}",
                InsideLabelFormat = null,
                TickHorizontalLength = 12,
                TickRadialLength = 8,
                AngleSpan = 360,
                StartAngle = 90,
                TextColor = OxyColors.Black,
                FontSize = 14,
                ExplodedDistance = 0
            };

            // Define custom colors for device types
            var deviceTypeColors = new Dictionary<string, OxyColor>
    {
        { "windows", OxyColor.Parse("#1E90FF") },  // DodgerBlue
        { "macos", OxyColor.Parse("#00C851") },    // Green
        { "ios", OxyColor.Parse("#FF4500") },      // OrangeRed
        { "android", OxyColor.Parse("#4CAF50") }   // Material Green
    };

            // Add slices for each device type
            foreach (var kvp in DevicesByType)
            {
                var slice = new PieSlice(kvp.Key, kvp.Value);

                // Set custom colors based on device type
                if (deviceTypeColors.TryGetValue(kvp.Key.ToLower(), out var color))
                {
                    slice.Fill = color;
                }

                typePieSeries.Slices.Add(slice);
            }

            DeviceTypePlotModel.Series.Clear();
            DeviceTypePlotModel.Series.Add(typePieSeries);

            // Add legend for device type chart
            DeviceTypePlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Inside,
                LegendMargin = 10,
                LegendPadding = 8,
                LegendBackground = OxyColor.FromArgb(220, 255, 255, 255),
                LegendBorder = OxyColors.Gray,
                LegendBorderThickness = 1,
                LegendTextColor = OxyColors.Black
            });

            OnPropertyChanged(nameof(DeviceTypePlotModel));

            // Ownership Donut Chart with similar improvements
            OwnershipPlotModel = new PlotModel
            {
                Title = string.Empty,
                PlotAreaBorderThickness = new OxyThickness(0),
                Background = OxyColors.Transparent
            };

            var ownershipPieSeries = new PieSeries
            {
                InnerDiameter = 0.55,
                StrokeThickness = 1.5,
                OutsideLabelFormat = "{1}: {0}",
                InsideLabelFormat = null,
                TickHorizontalLength = 12,
                TickRadialLength = 8,
                AngleSpan = 360,
                StartAngle = 90,
                TextColor = OxyColors.Black,
                FontSize = 14,
                ExplodedDistance = 0
            };

            // Define custom colors for ownership types
            var ownershipColors = new Dictionary<string, OxyColor>
    {
        { "company", OxyColor.Parse("#4CAF50") },  // Material Green
        { "personal", OxyColor.Parse("#FFA726") }  // Material Orange
    };

            foreach (var kvp in DevicesByOwnership)
            {
                var slice = new PieSlice(kvp.Key, kvp.Value);
                if (ownershipColors.TryGetValue(kvp.Key.ToLower(), out var color))
                {
                    slice.Fill = color;
                }
                ownershipPieSeries.Slices.Add(slice);
            }

            OwnershipPlotModel.Series.Clear();
            OwnershipPlotModel.Series.Add(ownershipPieSeries);

            // Add legend for ownership chart
            OwnershipPlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Inside,
                LegendMargin = 10,
                LegendPadding = 8,
                LegendBackground = OxyColor.FromArgb(220, 255, 255, 255),
                LegendBorder = OxyColors.Gray,
                LegendBorderThickness = 1,
                LegendTextColor = OxyColors.Black
            });

            OnPropertyChanged(nameof(OwnershipPlotModel));
        }


        private void UpdateCharts()
        {
            // Device Type Donut Chart
            DeviceTypePlotModel = new PlotModel
            {
                Title = string.Empty,
                PlotAreaBorderThickness = new OxyThickness(0),
                Background = OxyColors.Transparent
            };

            var typePieSeries = new PieSeries
            {
                InnerDiameter = 0.55,             // Slightly larger hole
                StrokeThickness = 1.5,
                OutsideLabelFormat = "{1}: {0}",
                InsideLabelFormat = null,         // No inside labels
                TickHorizontalLength = 12,
                TickRadialLength = 8,
                AngleSpan = 360,
                StartAngle = 90,
                TextColor = OxyColors.Black,
                FontSize = 14,
                ExplodedDistance = 0             // No explosion effect
            };

            // Define custom colors for device types with better contrast
            var deviceTypeColors = new Dictionary<string, OxyColor>
    {
        { "windows", OxyColor.Parse("#1E90FF") },  // DodgerBlue
        { "macos", OxyColor.Parse("#00C851") },    // Green
        { "ios", OxyColor.Parse("#FF4500") },      // OrangeRed
        { "android", OxyColor.Parse("#4CAF50") }   // Material Green
    };

            // Add slices for each device type
            foreach (var kvp in _cachedDeviceTypeCounts)
            {
                var slice = new PieSlice(kvp.Key, kvp.Value);

                // Set custom colors based on device type
                if (deviceTypeColors.TryGetValue(kvp.Key.ToLower(), out var color))
                {
                    slice.Fill = color;
                }

                typePieSeries.Slices.Add(slice);
            }

            DeviceTypePlotModel.Series.Clear();
            DeviceTypePlotModel.Series.Add(typePieSeries);

            // Add legend items manually for better control
            DeviceTypePlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Inside,
                LegendMargin = 10,
                LegendPadding = 8,
                LegendBackground = OxyColor.FromArgb(220, 255, 255, 255),
                LegendBorder = OxyColors.Gray,
                LegendBorderThickness = 1,
                LegendTextColor = OxyColors.Black
            });

            // Update the model
            OnPropertyChanged(nameof(DeviceTypePlotModel));

            // Ownership Donut Chart with similar improvements
            OwnershipPlotModel = new PlotModel
            {
                Title = string.Empty,
                PlotAreaBorderThickness = new OxyThickness(0),
                Background = OxyColors.Transparent
            };

            var ownershipPieSeries = new PieSeries
            {
                InnerDiameter = 0.55,
                StrokeThickness = 1.5,
                OutsideLabelFormat = "{1}: {0}",
                InsideLabelFormat = null,
                TickHorizontalLength = 12,
                TickRadialLength = 8,
                AngleSpan = 360,
                StartAngle = 90,
                TextColor = OxyColors.Black,
                FontSize = 14,
                ExplodedDistance = 0
            };

            // Define custom colors for ownership types with better contrast
            var ownershipColors = new Dictionary<string, OxyColor>
    {
        { "company", OxyColor.Parse("#4CAF50") },  // Material Green
        { "personal", OxyColor.Parse("#FFA726") }  // Material Orange
    };

            foreach (var kvp in _cachedOwnershipCounts)
            {
                var slice = new PieSlice(kvp.Key, kvp.Value);
                if (ownershipColors.TryGetValue(kvp.Key.ToLower(), out var color))
                {
                    slice.Fill = color;
                }
                ownershipPieSeries.Slices.Add(slice);
            }

            OwnershipPlotModel.Series.Clear();
            OwnershipPlotModel.Series.Add(ownershipPieSeries);

            // Add legend for ownership chart
            OwnershipPlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Inside,
                LegendMargin = 10,
                LegendPadding = 8,
                LegendBackground = OxyColor.FromArgb(220, 255, 255, 255),
                LegendBorder = OxyColors.Gray,
                LegendBorderThickness = 1,
                LegendTextColor = OxyColors.Black
            });

            OnPropertyChanged(nameof(OwnershipPlotModel));
        }

        // Update ClearCache method to also clear disk cache
        public void ClearCache()
        {
            _allDevicesCache = null;
            ServiceManager.Instance.DataCacheService.ClearCache();
        }
        private void ApplyDeviceData(List<DeviceViewModel> devices)
        {
            _allDevicesCache = devices;

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
        }

        private void StartComplianceReasonFetch(List<DeviceViewModel> devices)
        {
            Task.Run(async () =>
            {
                var semaphore = new SemaphoreSlim(5); // limit concurrency

                var tasks = devices.Select(async device =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var tempList = new List<string>();
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await ServiceManager.Instance.IntuneService.GetComplianceIssuesAsync(device.DeviceId, tempList, timeout.Token);

                        // Replace the placeholder with real data (on UI thread)
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
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ApplyFilters()
        {
            if (_allDevicesCache == null) return;

            StatusMessage = "Filtering data...";

            try
            {
                var filtered = _allDevicesCache.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(d =>
                        (d.DeviceName != null && d.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Owner != null && d.Owner.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (d.SerialNumber != null && d.SerialNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
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
                if (FilterByNotCheckedIn || DaysNotCheckedIn > 0)
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