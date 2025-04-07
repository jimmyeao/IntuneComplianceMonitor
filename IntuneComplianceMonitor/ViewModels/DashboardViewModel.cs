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

        public async void LoadData()
        {
            IsLoading = true;
            StatusMessage = "Loading data...";

            try
            {
                // Use the appropriate service from ServiceManager
                if (ServiceManager.Instance.UseRealData)
                {
                    StatusMessage = "Fetching data from Intune...";
                    _allDevicesCache = await ServiceManager.Instance.IntuneService.GetDevicesAsync();
                    StatusMessage = "Data retrieved from Intune";
                }
                else
                {
                    StatusMessage = "Loading sample data...";
                    _allDevicesCache = await ServiceManager.Instance.SampleDataService.GetDevicesAsync();
                    StatusMessage = "Sample data loaded";
                }

                StatusMessage = "Processing device data...";

                // Update metrics
                TotalDevices = _allDevicesCache.Count;

                var nonCompliant = _allDevicesCache.Where(d => d.ComplianceIssues.Any()).ToList();
                NonCompliantDevices = nonCompliant.Count;
                NonCompliantDevicesList = new ObservableCollection<DeviceViewModel>(nonCompliant);

                var cutoffDate = DateTime.Now.AddDays(-DaysNotCheckedIn);
                DevicesNotCheckedInRecently = _allDevicesCache.Count(d => d.LastCheckIn < cutoffDate);

                // Update charts data
                DevicesByType = _allDevicesCache
                    .GroupBy(d => d.DeviceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                DevicesByOwnership = _allDevicesCache
                    .GroupBy(d => d.Ownership)
                    .ToDictionary(g => g.Key, g => g.Count());

                StatusMessage = "Processing compliance data...";
                ComplianceIssuesByType = _allDevicesCache
                    .SelectMany(d => d.ComplianceIssues)
                    .GroupBy(i => i)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Update filter lists
                DeviceTypes = new ObservableCollection<string>(_allDevicesCache.Select(d => d.DeviceType).Distinct());
                OwnershipTypes = new ObservableCollection<string>(_allDevicesCache.Select(d => d.Ownership).Distinct());

                // Apply any existing filters
                StatusMessage = "Applying filters...";
                ApplyFilters();
                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error loading data: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
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