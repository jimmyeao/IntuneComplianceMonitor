using IntuneComplianceMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IntuneComplianceMonitor.Views
{
    /// <summary>
    /// Interaction logic for DeviceDetailsWindow.xaml
    /// </summary>
    public partial class DeviceDetailsWindow : Window
    {
        private readonly DeviceViewModel _device;
        public ObservableCollection<CompliancePolicyStateViewModel> PolicyStates { get; set; } = new();
        public ObservableCollection<ConfigurationProfileViewModel> ConfigurationProfiles { get; set; } = new();
        public bool IsLoadingProfiles { get; set; }
        public DeviceViewModel Device => _device;

        public event PropertyChangedEventHandler PropertyChanged;

        public DeviceDetailsWindow(DeviceViewModel device)
        {
            InitializeComponent();
            _device = device;

            // Set DataContext to this to bind properties like PolicyStates
            DataContext = this;

            Loaded += async (_, __) =>
            {
                await LoadComplianceDetailsAsync();
                await LoadConfigurationProfilesAsync();
                ComplianceDataGrid.Items.Refresh();

            };
        }

        private async Task LoadConfigurationProfilesAsync()
        {
            IsLoadingProfiles = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting to load configuration profiles for device {_device.DeviceId}");

                var profiles = await ServiceManager.Instance.IntuneService
                    .GetAppliedConfigurationProfilesAsync(_device.DeviceId);

                System.Diagnostics.Debug.WriteLine($"Received {profiles.Count} profiles");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConfigurationProfiles.Clear();
                    foreach (var profile in profiles)
                    {
                        ConfigurationProfiles.Add(profile);
                        System.Diagnostics.Debug.WriteLine($"Added profile: {profile.DisplayName}");
                    }

                    // Make sure the DataGrid or ListView is refreshed
                    // If you have a DataGrid or ListView for displaying profiles, add code to refresh it
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration profiles: {ex.Message}");
                MessageBox.Show($"Error loading configuration profiles: {ex.Message}",
                    "Profile Loading Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsLoadingProfiles = false;
            }
        }
        private async Task LoadComplianceDetailsAsync()
        {
            var list = await ServiceManager.Instance.IntuneService
                .GetDeviceComplianceStateWithMetadataAsync(_device.DeviceId);

            Application.Current.Dispatcher.Invoke(() =>
            {
                PolicyStates.Clear();
                foreach (var item in list)
                {
                    // Highlight items that are non-compliant or in error
                    if (item.State.ToLower() == "noncompliant" ||
                        item.State.ToLower() == "error")
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Non-Compliant Policy: {item.DisplayName}" +
                            $"\nError Details: {string.Join("; ", item.ErrorDetails)}");
                    }

                    PolicyStates.Add(item);
                }
            });
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }



    }

}
