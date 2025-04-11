using IntuneComplianceMonitor.Services;
using Microsoft.Graph.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace IntuneComplianceMonitor.ViewModels
{
    public class CompliancePolicyViewModel : INotifyPropertyChanged
    {
        private Dictionary<string, List<DeviceViewModel>> _groupedData;
        private string _selectedPolicy;
        private string _searchText;
        private bool _isLoading;
        private string _statusMessage;

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

        public CompliancePolicyViewModel()
        {
            Policies = new ObservableCollection<string>();
            Devices = new ObservableCollection<DeviceViewModel>();
            LoadDataCommand = new RelayCommand(_ => LoadData());
            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
            RefreshCommand = new RelayCommand(async _ => await LoadData(forceRefresh: true));
        }

        public ObservableCollection<string> Policies { get; }
        public ObservableCollection<DeviceViewModel> Devices { get; }
        public ICommand LoadDataCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand RefreshCommand { get; }

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

        public Action OnRefreshRequested { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public async Task LoadData(bool forceRefresh = false)
        {
            IsLoading = true;
            StatusMessage = "Loading compliance data...";

            try
            {
                if (!forceRefresh)
                {
                    var cached = await ServiceManager.Instance.DataCacheService.GetCompliancePoliciesFromCacheAsync();
                    if (cached != null)
                    {
                        _groupedData = cached;
                        Policies.Clear();
                        foreach (var policy in _groupedData.Keys.OrderBy(k => k))
                            Policies.Add(policy);
                        SelectedPolicy = Policies.FirstOrDefault();
                        StatusMessage = "Loaded from cache";
                        return;
                    }
                }

                var (allDevices, _) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();

                await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(allDevices);

                var grouped = await ServiceManager.Instance.IntuneService.GetDevicesGroupedByPolicyAsync(allDevices);
                _groupedData = grouped;

                await ServiceManager.Instance.DataCacheService.SaveCompliancePoliciesToCacheAsync(grouped);

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
    }
}