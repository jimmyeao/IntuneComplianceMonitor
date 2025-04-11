using IntuneComplianceMonitor.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace IntuneComplianceMonitor.Views
{
    /// <summary>
    /// Interaction logic for DeviceDetailsWindow.xaml
    /// </summary>
    public partial class DeviceDetailsWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private readonly DeviceViewModel _device;
        private bool _isLoading = true;
        private bool _isLoadingProfiles;

        #endregion Fields

        #region Constructors

        public DeviceDetailsWindow(DeviceViewModel device)
        {
            InitializeComponent();
            _device = device;

            // Set DataContext to this to bind properties like PolicyStates
            DataContext = this;

            // Start with loading state
            IsLoading = true;
            IsLoadingProfiles = true;

            Loaded += DeviceDetailsWindow_Loaded;
        }

        #endregion Constructors

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Properties

        public ObservableCollection<ConfigurationProfileViewModel> ConfigurationProfiles { get; set; } = new();
        public DeviceViewModel Device => _device;
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

        public bool IsLoadingProfiles
        {
            get => _isLoadingProfiles;
            set
            {
                if (_isLoadingProfiles != value)
                {
                    _isLoadingProfiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<CompliancePolicyStateViewModel> PolicyStates { get; set; } = new();

        #endregion Properties

        #region Methods

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ComplianceDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ComplianceDataGrid.SelectedItem is CompliancePolicyStateViewModel selectedPolicy)
            {
                var detailsWindow = new ComplianceDetailsWindow(selectedPolicy, _device);

                detailsWindow.Owner = this;
                detailsWindow.ShowDialog();
            }
        }

        private async void DeviceDetailsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load data in parallel
                await Task.WhenAll(
                    LoadComplianceDetailsAsync(),
                    LoadConfigurationProfilesAsync()
                );

                // Refresh the UI
                ComplianceDataGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading device details: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading overlay when done
                IsLoading = false;
                IsLoadingProfiles = false;
            }
        }

        private async Task LoadComplianceDetailsAsync()
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading compliance details: {ex.Message}");
                throw;
            }
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
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration profiles: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoadingProfiles = false;
            }
        }

        #endregion Methods
    }
}