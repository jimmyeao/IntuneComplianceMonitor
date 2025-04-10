using IntuneComplianceMonitor.ViewModels;
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
                loadingWindow.Close();
            }
        }

        private void AddPushpinsToMap(System.Collections.Generic.List<CountryDeviceCount> countryData)
        {
            // Clear existing pushpins
            MyMap.Children.Clear();

            // Add a pushpin for each country
            foreach (var country in countryData)
            {
                // Create location
                var location = new Location(country.Latitude, country.Longitude);

                // Calculate size based on count - make it more proportional
                double baseSize = 40; // Minimum size
                double maxSize = 100; // Maximum size
                double sizeIncrement = Math.Min(country.Count / 2.0, (maxSize - baseSize));
                double size = baseSize + sizeIncrement;

                // Create a Grid to hold the pushpin content for better centering
                Grid pinGrid = new Grid();
                pinGrid.Width = size;
                pinGrid.Height = size;

                // Create an ellipse for the background
                Ellipse ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(country.PushpinColor),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                // Create text with better formatting
                TextBlock countText = new TextBlock
                {
                    Text = country.Count.ToString(),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = Math.Max(12, Math.Min(size / 2.5, 20)), // Adjust font size based on pin size
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                // Add the elements to the grid
                pinGrid.Children.Add(ellipse);
                pinGrid.Children.Add(countText);

                // Create the pushpin with our custom content
                Pushpin pushpin = new Pushpin
                {
                    Location = location,
                    Content = pinGrid,
                    Background = Brushes.Transparent, // Make the pushpin background transparent
                    BorderThickness = new Thickness(0) // No border
                };

                // Add tooltip with country name and count
                ToolTipService.SetToolTip(pushpin, $"{country.CountryName}: {country.Count} devices");
                ToolTipService.SetInitialShowDelay(pushpin, 0); // Show tooltip immediately
                ToolTipService.SetShowDuration(pushpin, 7000); // Show for 7 seconds

                // Add pushpin to map
                MyMap.Children.Add(pushpin);
            }
        }
        private async void Refresh_Click(object sender, RoutedEventArgs e)
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
                loadingWindow.Close();
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
    }
}