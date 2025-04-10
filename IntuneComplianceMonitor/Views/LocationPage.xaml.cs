using IntuneComplianceMonitor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace IntuneComplianceMonitor.Views
{
    public partial class LocationPage : Page
    {
        private readonly LocationViewModel _viewModel;

        public LocationPage()
        {
            InitializeComponent();

            // Create the view model and set as data context
            _viewModel = new LocationViewModel();
            DataContext = _viewModel;

            // Update status when property changes
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "StatusMessage" || e.PropertyName == "IsLoading")
                {
                    UpdateMainWindowStatus();
                }
            };

            // Load data when the page is loaded
            Loaded += LocationPage_Loaded;
        }

        private void UpdateMainWindowStatus()
        {
            // Find the MainWindow and update its status
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
            }
        }

        private async void LocationPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Show a loading window while data is being loaded
            var loadingWindow = new LoadingWindow
            {
                Owner = Application.Current.MainWindow
            };

            try
            {
                loadingWindow.Show();

                // Set status message to loading
                _viewModel.StatusMessage = "Loading location data...";
                _viewModel.IsLoading = true;

                // Load the location data
                await _viewModel.LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading location data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                _viewModel.StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _viewModel.IsLoading = false;
                loadingWindow.Close();
            }
        }

        private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Show a loading window during refresh
            var loadingWindow = new LoadingWindow
            {
                Owner = Application.Current.MainWindow
            };

            try
            {
                loadingWindow.Show();

                // Set status message to refreshing
                _viewModel.StatusMessage = "Refreshing location data...";
                _viewModel.IsLoading = true;

                // Clear the device location cache first
                ServiceManager.Instance.DataCacheService.ClearDeviceLocationCache();

                // Force refresh of location data
                await _viewModel.LoadDataAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing location data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                _viewModel.StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _viewModel.IsLoading = false;
                loadingWindow.Close();
            }
        }
    }
}