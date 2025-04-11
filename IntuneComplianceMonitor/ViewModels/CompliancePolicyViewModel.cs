using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace IntuneComplianceMonitor.ViewModels
{
    public class CompliancePolicyViewModel : INotifyPropertyChanged
    {
        #region Fields

        private Dictionary<string, List<DeviceViewModel>> _groupedData;
        private bool _isLoading;
        private string _searchText;
        private string _selectedPolicy;
        private string _statusMessage;

        #endregion Fields

        #region Constructors

        public CompliancePolicyViewModel()
        {
            Policies = new ObservableCollection<string>();
            Devices = new ObservableCollection<DeviceViewModel>();
            LoadDataCommand = new RelayCommand(_ => LoadData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
            RefreshCommand = new RelayCommand(async _ => await LoadData(forceRefresh: true));
        }

        #endregion Constructors

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Properties

        public ICommand ApplyFiltersCommand { get; }

        public ObservableCollection<DeviceViewModel> Devices { get; }

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

        public ICommand LoadDataCommand { get; }

        public Action OnRefreshRequested { get; set; }

        public ObservableCollection<string> Policies { get; }

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

        public string SelectedPolicy
        {
            get => _selectedPolicy;
            set
            {
                if (_selectedPolicy != value)
                {
                    _selectedPolicy = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

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

        #endregion Properties

        #region Methods

        public async Task LoadData(bool forceRefresh = false)
        {
            IsLoading = true;
            StatusMessage = "Loading compliance data...";

            try
            {
                // Try to load from cache if not forcing refresh
                if (!forceRefresh)
                {
                    StatusMessage = "Checking cache...";
                    var cached = await ServiceManager.Instance.DataCacheService.GetCompliancePoliciesFromCacheAsync();
                    if (cached != null && cached.Count > 0)
                    {
                        _groupedData = cached;
                        Policies.Clear();
                        foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                            Policies.Add(policy);
                        SelectedPolicy = Policies.FirstOrDefault();
                        StatusMessage = "Loaded from cache";
                        IsLoading = false;
                        return;
                    }
                    StatusMessage = "No cache found or cache expired. Fetching from Intune...";
                }
                else
                {
                    StatusMessage = "Refreshing data from Intune...";
                }

                // If forceRefresh or no cache available, fetch from API
                var (allDevices, _) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();

                StatusMessage = "Enriching device data with location information...";
                await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(allDevices);

                StatusMessage = "Grouping devices by policy...";
                var grouped = await ServiceManager.Instance.IntuneService.GetDevicesGroupedByPolicyAsync(allDevices);
                _groupedData = grouped;

                // Save to cache for future use
                StatusMessage = "Saving data to cache...";
                await ServiceManager.Instance.DataCacheService.SaveCompliancePoliciesToCacheAsync(grouped);

                // Update UI
                Policies.Clear();
                foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                    Policies.Add(policy);

                SelectedPolicy = Policies.FirstOrDefault();
                StatusMessage = "Compliance data loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading compliance data: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propName = null) =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private void ApplyFilters()
        {
            Devices.Clear();
            if (_groupedData == null || string.IsNullOrEmpty(SelectedPolicy)) return;

            var filtered = _groupedData[SelectedPolicy];

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(d =>
                    (d.DeviceName?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (d.Owner?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            foreach (var device in filtered)
                Devices.Add(device);

            StatusMessage = $"Showing {Devices.Count} devices for policy '{SelectedPolicy}'";
        }

        #endregion Methods
    }
}