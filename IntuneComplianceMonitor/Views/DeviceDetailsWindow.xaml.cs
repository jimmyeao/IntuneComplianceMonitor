using IntuneComplianceMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            };
        }

        private async Task LoadConfigurationProfilesAsync()
        {
            IsLoadingProfiles = true;
            try
            {
                var profiles = await ServiceManager.Instance.IntuneService
      .GetAppliedConfigurationProfilesAsync(_device.DeviceId);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConfigurationProfiles.Clear();
                    foreach (var profile in profiles)
                        ConfigurationProfiles.Add(profile);
                });
            }
            finally
            {
                IsLoadingProfiles = false;
            }

            // Notify binding (you can use INotifyPropertyChanged if needed)
            Dispatcher.Invoke(() => BindingOperations.GetBindingExpressionBase(this, DataContextProperty)?.UpdateTarget());
        }

        private async Task LoadComplianceDetailsAsync()
        {
            var list = await ServiceManager.Instance.IntuneService.GetDeviceComplianceStateWithMetadataAsync(_device.DeviceId);

            Application.Current.Dispatcher.Invoke(() =>
            {
                PolicyStates.Clear();
                foreach (var item in list)
                {
                    PolicyStates.Add(item);
                    System.Diagnostics.Debug.WriteLine($"Added: {item.DisplayName} - {item.State}");
                }
            });
        }



     
    }

}
