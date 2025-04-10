﻿using IntuneComplianceMonitor.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IntuneComplianceMonitor.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly PasswordBox _clientSecretBox;
        private int _activeDevicesTimeframe;
        private bool _autoRefreshEnabled;
        private int _autoRefreshInterval;
        private int _daysNotCheckedIn;
        private string _intuneClientId;
        private string _intuneClientSecret;
        private string _intuneTenantId;
        private bool _isLoading;
        private DateTime _lastSyncTime;
        private string _statusMessage;

        #endregion Fields

        #region Constructors

        public SettingsViewModel(PasswordBox clientSecretBox)
        {
            _clientSecretBox = clientSecretBox;

            // Initialize with current settings
            var settings = ServiceManager.Instance.SettingsService.CurrentSettings;
            DaysNotCheckedIn = settings.DaysNotCheckedIn;
            ActiveDevicesTimeframe = settings.ActiveDevicesTimeframeInDays;
            AutoRefreshEnabled = settings.AutoRefreshEnabled;
            AutoRefreshInterval = settings.AutoRefreshIntervalMinutes;
            LastSyncTime = settings.LastSyncTime;
            IntuneClientId = settings.IntuneClientId;
            IntuneTenantId = settings.IntuneTenantId;

            // Don't display client secret directly for security reasons
            // We'll handle it separately with the password box
            if (!string.IsNullOrEmpty(settings.IntuneClientSecret))
            {
                _clientSecretBox.Password = settings.IntuneClientSecret;
            }

            SaveCommand = new RelayCommand(_ => SaveSettings());
            ResetToDefaultsCommand = new RelayCommand(_ => ResetToDefaults());

            StatusMessage = "Settings loaded";
        }

        #endregion Constructors

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Properties

        public int ActiveDevicesTimeframe
        {
            get => _activeDevicesTimeframe;
            set
            {
                if (_activeDevicesTimeframe != value)
                {
                    _activeDevicesTimeframe = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoRefreshEnabled
        {
            get => _autoRefreshEnabled;
            set
            {
                if (_autoRefreshEnabled != value)
                {
                    _autoRefreshEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public int AutoRefreshInterval
        {
            get => _autoRefreshInterval;
            set
            {
                if (_autoRefreshInterval != value)
                {
                    _autoRefreshInterval = value;
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

        public string IntuneClientId
        {
            get => _intuneClientId;
            set
            {
                if (_intuneClientId != value)
                {
                    _intuneClientId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IntuneClientSecret
        {
            get => _intuneClientSecret;
            set
            {
                if (_intuneClientSecret != value)
                {
                    _intuneClientSecret = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IntuneTenantId
        {
            get => _intuneTenantId;
            set
            {
                if (_intuneTenantId != value)
                {
                    _intuneTenantId = value;
                    OnPropertyChanged();
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

        public DateTime LastSyncTime
        {
            get => _lastSyncTime;
            set
            {
                if (_lastSyncTime != value)
                {
                    _lastSyncTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LastSyncTimeDisplay => LastSyncTime > DateTime.MinValue
            ? LastSyncTime.ToString("g")
            : "Never";

        public ICommand ResetToDefaultsCommand { get; }
        public ICommand SaveCommand { get; }
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ResetToDefaults()
        {
            var defaultSettings = new ApplicationSettings();
            DaysNotCheckedIn = defaultSettings.DaysNotCheckedIn;
            ActiveDevicesTimeframe = defaultSettings.ActiveDevicesTimeframeInDays;
            AutoRefreshEnabled = defaultSettings.AutoRefreshEnabled;
            AutoRefreshInterval = defaultSettings.AutoRefreshIntervalMinutes;

            StatusMessage = "Default settings loaded";
        }

        private async void SaveSettings()
        {
            IsLoading = true;
            StatusMessage = "Saving settings...";

            try
            {
                var settings = new ApplicationSettings
                {
                    DaysNotCheckedIn = DaysNotCheckedIn,
                    ActiveDevicesTimeframeInDays = ActiveDevicesTimeframe,
                    AutoRefreshEnabled = AutoRefreshEnabled,
                    AutoRefreshIntervalMinutes = AutoRefreshInterval,
                    LastSyncTime = LastSyncTime,
                    IntuneClientId = IntuneClientId,
                    IntuneTenantId = IntuneTenantId,
                    IntuneClientSecret = _clientSecretBox.Password
                };

                await ServiceManager.Instance.SettingsService.SaveSettings(settings);

                // Clear any cached data to ensure new settings take effect
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.MainFrame.Content is Page page &&
                        page.DataContext is DashboardViewModel dashboardViewModel)
                    {
                        dashboardViewModel.ClearCache();
                    }
                }

                StatusMessage = "Settings saved successfully";
                MessageBox.Show("Settings saved successfully. The changes will take effect the next time data is loaded.",
                    "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error saving settings: {ex.Message}",
                    "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion Methods
    }
}