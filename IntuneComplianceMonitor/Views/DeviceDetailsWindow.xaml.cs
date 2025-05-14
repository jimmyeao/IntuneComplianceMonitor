using IntuneComplianceMonitor.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        private bool _isRemediationInProgress;

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
            IsRemediationInProgress = false;

            // Initialize remediation commands
            SyncDeviceCommand = new RelayCommand(_ => SyncDevice(), _ => !IsRemediationInProgress);
            SendNotificationCommand = new RelayCommand(_ => SendNotification(), _ => !IsRemediationInProgress);
            RebootDeviceCommand = new RelayCommand(_ => RebootDevice(), _ => !IsRemediationInProgress);
            WipeDeviceCommand = new RelayCommand(_ => WipeDevice(), _ => !IsRemediationInProgress);

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

        public bool IsRemediationInProgress
        {
            get => _isRemediationInProgress;
            set
            {
                if (_isRemediationInProgress != value)
                {
                    _isRemediationInProgress = value;
                    OnPropertyChanged();
                    // Force command CanExecute to be reevaluated
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<CompliancePolicyStateViewModel> PolicyStates { get; set; } = new();

        // Remediation Commands
        public ICommand RebootDeviceCommand { get; }
        public ICommand SendNotificationCommand { get; }
        public ICommand SyncDeviceCommand { get; }
        public ICommand WipeDeviceCommand { get; }

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

        #region Remediation Methods

        private async void RebootDevice()
        {
            if (string.IsNullOrEmpty(_device.DeviceId))
            {
                MessageBox.Show("No device ID available for this operation.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsRemediationInProgress = true;

                var remediationService = ServiceManager.Instance.RemediationService;
                if (remediationService == null)
                {
                    MessageBox.Show("Remediation service is not available.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show a progress overlay or indicate activity
                this.Cursor = Cursors.Wait;

                var success = await remediationService.RebootDeviceAsync(_device.DeviceId);

                if (success)
                {
                    MessageBox.Show(
                        $"Reboot command sent to device {_device.DeviceName}.\n\nThe device will restart after the user acknowledges the notification.",
                        "Reboot Initiated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initiating device reboot: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                IsRemediationInProgress = false;
            }
        }

        private async void SendNotification()
        {
            if (string.IsNullOrEmpty(_device.DeviceId))
            {
                MessageBox.Show("No device ID available for this operation.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create a custom dialog to get the notification message
            var dialog = new NotificationDialog("Send Notification", "Please update your device", this)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            var title = dialog.NotificationTitle;
            var message = dialog.NotificationMessage;

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Notification message cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


           

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Notification message cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsRemediationInProgress = true;

                var remediationService = ServiceManager.Instance.RemediationService;
                if (remediationService == null)
                {
                    MessageBox.Show("Remediation service is not available.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show a progress indicator
                this.Cursor = Cursors.Wait;

                var success = await remediationService.SendNotificationAsync(_device.DeviceId, message, title);

                if (success)
                {
                    MessageBox.Show(
                        $"Notification sent to device {_device.DeviceName}.\n\nIt will appear in the Company Portal app.",
                        "Notification Sent", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending notification: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                IsRemediationInProgress = false;
            }
        }

        private async void SyncDevice()
        {
            if (string.IsNullOrEmpty(_device.DeviceId))
            {
                MessageBox.Show("No device ID available for this operation.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsRemediationInProgress = true;

                var remediationService = ServiceManager.Instance.RemediationService;
                if (remediationService == null)
                {
                    MessageBox.Show("Remediation service is not available.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show a progress indicator
                this.Cursor = Cursors.Wait;

                var success = await remediationService.SyncDeviceAsync(_device.DeviceId);

                if (success)
                {
                    MessageBox.Show(
                        $"Sync command sent to device {_device.DeviceName}.\n\nIt may take a few minutes for the device to complete the sync process.",
                        "Sync Initiated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initiating device sync: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                IsRemediationInProgress = false;
            }
        }

        private async void WipeDevice()
        {
            if (string.IsNullOrEmpty(_device.DeviceId))
            {
                MessageBox.Show("No device ID available for this operation.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsRemediationInProgress = true;

                var remediationService = ServiceManager.Instance.RemediationService;
                if (remediationService == null)
                {
                    MessageBox.Show("Remediation service is not available.", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show a progress indicator
                this.Cursor = Cursors.Wait;

                var success = await remediationService.WipeDeviceAsync(_device.DeviceId);

                if (success)
                {
                    MessageBox.Show(
                        $"Factory reset command sent to device {_device.DeviceName}.\n\nThe device will be reset to factory settings and all data will be erased.",
                        "Factory Reset Initiated", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initiating device wipe: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                IsRemediationInProgress = false;
            }
        }

        #endregion Remediation Methods

        #endregion Methods
    }
}