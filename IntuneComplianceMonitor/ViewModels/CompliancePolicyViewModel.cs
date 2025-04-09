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

        public CompliancePolicyViewModel()
        {
            Policies = new ObservableCollection<string>();
            Devices = new ObservableCollection<DeviceViewModel>();
            LoadDataCommand = new RelayCommand(_ => LoadData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
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

        public async void LoadData()
        {
            Devices.Clear();
            Policies.Clear();
            var allDevices = await ServiceManager.Instance.SampleDataService.GetDevicesAsync(); // or IntuneService.GetNonCompliantDevicesAsync
            _groupedData = await ServiceManager.Instance.IntuneService.GetDevicesGroupedByPolicyAsync(allDevices);

            foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                Policies.Add(policy);

            SelectedPolicy = Policies.FirstOrDefault();
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
