using IntuneComplianceMonitor.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace IntuneComplianceMonitor.ViewModels
{
    public class CompliancePolicyViewModel : INotifyPropertyChanged
    {
        private Dictionary<string, List<DeviceViewModel>> _groupedData;
        private string _selectedPolicy;
        private string _searchText;
        private bool _isLoading;
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

        public CompliancePolicyViewModel()
        {
            Policies = new ObservableCollection<string>();
            Devices = new ObservableCollection<DeviceViewModel>();
            LoadDataCommand = new RelayCommand(_ => LoadData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
            LoadData(); // 👈 THIS is what was missing
        }

        public ObservableCollection<string> Policies { get; }
        public ObservableCollection<DeviceViewModel> Devices { get; }
        public ICommand LoadDataCommand { get; }
        public ICommand ApplyFiltersCommand { get; }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public async Task LoadData()
        {
            IsLoading = true;

            try
            {
                // Try cache first
                var cached = await ServiceManager.Instance.DataCacheService.GetCompliancePoliciesFromCacheAsync();
                if (cached != null)
                {
                    _groupedData = cached;
                    foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                        Policies.Add(policy);
                    SelectedPolicy = Policies.FirstOrDefault();
                    return;
                }

                // Else fetch and cache
                var (allDevices, _) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();
                var grouped = await ServiceManager.Instance.IntuneService.GetDevicesGroupedByPolicyAsync(allDevices);

                _groupedData = grouped;
                await ServiceManager.Instance.DataCacheService.SaveCompliancePoliciesToCacheAsync(grouped);

                foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                    Policies.Add(policy);
                SelectedPolicy = Policies.FirstOrDefault();
            }
            finally
            {
                IsLoading = false;
            }
        }





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
        }
    }
}
