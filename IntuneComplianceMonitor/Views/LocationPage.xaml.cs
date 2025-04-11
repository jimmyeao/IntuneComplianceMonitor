using IntuneComplianceMonitor.ViewModels;
using Microsoft.Maps.MapControl.WPF;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IntuneComplianceMonitor.Views
{
    public partial class LocationPage : Page
    {
        #region Fields

        private readonly LocationViewModel _viewModel;

        #endregion Fields

        #region Constructors

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

        #endregion Constructors

        #region Methods

        // Updated method to use the new approach
        private void AddPushpinsToMap(System.Collections.Generic.List<CountryDeviceCount> countryData)
        {
            // Clear existing pushpins
            MyMap.Children.Clear();

            // Add a pushpin for each country using the direct method
            foreach (var country in countryData)
            {
                CreateClickablePushpin(
                    new Location(country.Latitude, country.Longitude),
                    country.CountryName,
                    country.Count,
                    country.PushpinColor
                );
            }

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Added {countryData.Count} clickable pushpins to the map");
        }

        private void CreateClickablePushpin(Location location, string countryName, int deviceCount, Color pinColor)
        {
            // Create a simple, directly clickable pushpin
            Pushpin pushpin = new Pushpin();
            pushpin.Location = location;
            pushpin.Content = deviceCount.ToString();
            pushpin.Background = new SolidColorBrush(pinColor);
            pushpin.Foreground = Brushes.White;
            pushpin.FontWeight = FontWeights.Bold;
            pushpin.Tag = countryName;

            // Use preview events which work more reliably
            pushpin.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // Mark the event as handled to prevent map panning
                e.Handled = true;
            };

            pushpin.PreviewMouseLeftButtonUp += (s, e) =>
            {
                // Navigate to devices view for this country
                NavigateToDevicesWithCountryFilter(countryName);
                e.Handled = true;
            };

            // Add tooltip
            ToolTipService.SetToolTip(pushpin, $"{countryName}: {deviceCount} devices");

            // Add to map
            MyMap.Children.Add(pushpin);
        }

        private async void LocationPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Show a loading window while data is being loaded
            //var loadingWindow = new LoadingWindow
            //{
            //    Owner = Application.Current.MainWindow
            //};

            try
            {
               // loadingWindow.Show();

                // Set status message to loading
                _viewModel.StatusMessage = "Loading location data...";
                _viewModel.IsLoading = true;

                // Load the location data
                var countryData = await _viewModel.LoadDataAsync();

                // Add pushpins to the map
                AddPushpinsToMap(countryData);
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
                //loadingWindow.Close();
            }
        }

        private void MyMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Check if we hit a pushpin
            var hitTestResult = VisualTreeHelper.HitTest(MyMap, e.GetPosition(MyMap));
            if (hitTestResult != null)
            {
                // Walk up the visual tree to find a pushpin or button
                DependencyObject depObj = hitTestResult.VisualHit;
                while (depObj != null)
                {
                    if (depObj is Pushpin pushpin && pushpin.Tag is string countryName)
                    {
                        NavigateToDevicesWithCountryFilter(countryName);
                        e.Handled = true;
                        return;
                    }
                    else if (depObj is Button button && button.Tag is string buttonTag)
                    {
                        NavigateToDevicesWithCountryFilter(buttonTag);
                        e.Handled = true;
                        return;
                    }

                    // Move up the visual tree
                    depObj = VisualTreeHelper.GetParent(depObj);
                }
            }
        }

        private void NavigateToDevicesWithCountryFilter(string countryName)
        {
            try
            {
                // Find the MainWindow
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    // Create a new DevicesPage
                    var devicesPage = new DevicesPage();

                    // Wait for the page to load before applying filter
                    devicesPage.Loaded += (s, e) =>
                    {
                        if (devicesPage.DataContext is DashboardViewModel viewModel)
                        {
                            // Clear any existing filters
                            viewModel.SearchText = "";
                            viewModel.SelectedDeviceType = "";
                            viewModel.SelectedOwnership = "";
                            viewModel.ShowOnlyNonCompliant = true; // Only show non-compliant devices
                            viewModel.FilterByNotCheckedIn = false;

                            // Set the country as search text
                            viewModel.SearchText = countryName;

                            // Apply the filters
                            viewModel.ApplyFilters();

                            // Update status
                            viewModel.StatusMessage = $"Showing non-compliant devices in {countryName}";
                        }
                    };

                    // Navigate to the devices page
                    mainWindow.MainFrame.Navigate(devicesPage);

                    // Highlight the "All Devices" button in the navigation
                    mainWindow.HighlightNavigationButton("Devices");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error navigating to devices view: {ex.Message}",
                    "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Pushpin_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Pushpin pushpin && pushpin.Tag is string countryName)
            {
                // Navigate to the Devices page with country filter
                NavigateToDevicesWithCountryFilter(countryName);

                // Mark the event as handled
                e.Handled = true;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Show a loading window during refresh
            //var loadingWindow = new LoadingWindow
            //{
            //    Owner = Application.Current.MainWindow
            //};

            try
            {
                //loadingWindow.Show();

                // Set status message to refreshing
                _viewModel.StatusMessage = "Refreshing location data...";
                _viewModel.IsLoading = true;

                // Clear the device location cache first
                ServiceManager.Instance.DataCacheService.ClearDeviceLocationCache();

                // Force refresh of location data
                var countryData = await _viewModel.LoadDataAsync(forceRefresh: true);

                // Add pushpins to the map
                AddPushpinsToMap(countryData);
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
                //loadingWindow.Close();
            }
        }

        private void UpdateMainWindowStatus()
        {
            // Find the MainWindow and update its status
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
            }
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ZoomLevel += 0.5;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ZoomLevel -= 0.5;
        }

        #endregion Methods
    }
}