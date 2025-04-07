using IntuneComplianceMonitor.Services;
using IntuneComplianceMonitor.ViewModels;
using IntuneComplianceMonitor.Views;
using System;
using System.Windows;
using System.Windows.Controls;

namespace IntuneComplianceMonitor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Load the dashboard page by default
            NavigateToPage("Dashboard");
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string pageName = button.Tag.ToString();
                NavigateToPage(pageName);
            }
        }
        // Add this improved UpdateStatus method to MainWindow.xaml.cs

        public void UpdateStatus(string message, bool showProgress = false)
        {
            // Make sure we update on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(message, showProgress));
                return;
            }

            // Update the status UI elements
            StatusText.Text = message;
            StatusProgress.Visibility = showProgress ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            StatusProgress.IsIndeterminate = showProgress;

            // Force layout update to ensure changes are rendered immediately
            StatusText.UpdateLayout();
            StatusProgress.UpdateLayout();

            // Log status updates to debug output as well
            System.Diagnostics.Debug.WriteLine($"Status: {message} (Progress: {showProgress})");
        }
        private void NavigateToPage(string pageName)
        {
            Page page = null;
            bool forceRefresh = false;

            // If we're already on a page of the same type, force a refresh
            if (MainFrame.Content is Page currentPage &&
                ((pageName == "Dashboard" && currentPage is DashboardPage) ||
                 (pageName == "Devices" && currentPage is DevicesPage) ||
                 (pageName == "NonCompliant" && currentPage is DevicesPage) ||
                 (pageName == "NotCheckedIn" && currentPage is DevicesPage) ||
                 (pageName == "Settings" && currentPage is SettingsPage)))
            {
                // We're navigating to the same page type, so use existing instance but force refresh
                page = currentPage;
                forceRefresh = true;
            }
            else
            {
                // Create a new page instance
                switch (pageName)
                {
                    case "Dashboard":
                        page = new DashboardPage();
                        break;
                    case "Devices":
                        page = new DevicesPage();
                        break;
                    case "NonCompliant":
                        // Create DevicesPage with non-compliant filter
                        page = new DevicesPage();
                        // Check if the DataContext is already set and is a DashboardViewModel
                        if (page.DataContext is DashboardViewModel nonCompliantViewModel)
                        {
                            // Set a flag to indicate we want only non-compliant devices
                            nonCompliantViewModel.ShowOnlyNonCompliant = true;
                        }
                        break;
                    case "NotCheckedIn":
                        // Create DevicesPage with not checked in filter
                        page = new DevicesPage();
                        // Check if the DataContext is already set and is a DashboardViewModel
                        if (page.DataContext is DashboardViewModel notCheckedInViewModel)
                        {
                            // Set days not checked in from settings
                            var settings = ServiceManager.Instance.SettingsService.CurrentSettings;
                            notCheckedInViewModel.DaysNotCheckedIn = settings.DaysNotCheckedIn;
                            notCheckedInViewModel.FilterByNotCheckedIn = true;
                        }
                        break;
                    case "Settings":
                        page = new SettingsPage();
                        break;
                    default:
                        page = new DashboardPage();
                        break;
                }
            }

            // If we got a new or existing page, use it
            if (page != null)
            {
                // If reusing the same page, just refresh its data if it's a DashboardViewModel
                if (forceRefresh && page.DataContext is DashboardViewModel viewModel)
                {
                    viewModel.LoadData(forceRefresh: false);
                }

                // Navigate to the page
                if (!forceRefresh)
                {
                    MainFrame.Navigate(page);
                }
            }
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            var loadingWindow = new SyncProgressWindow();
            loadingWindow.Owner = this;
            loadingWindow.Show();

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500); // Allow spinner to show

                    Dispatcher.Invoke(() =>
                    {
                        if (MainFrame.Content is Page page)
                        {
                            if (page.DataContext is ViewModels.DashboardViewModel vm)
                            {
                                // Clear both memory cache and disk cache
                                vm.ClearCache();
                                // Load fresh data, forcing a refresh
                                vm.LoadData(forceRefresh: true);
                            }
                        }

                        loadingWindow.Dispatcher.Invoke(() =>
                        {
                            loadingWindow.Close();
                            MessageBox.Show("Successfully synchronized with Intune.", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        loadingWindow.Close();
                        MessageBox.Show($"Error syncing with Intune: {ex.Message}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        // Add this method to MainWindow.xaml.cs
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content is Page page)
            {
                if (page.DataContext is ViewModels.DashboardViewModel vm)
                {
                    // No need to clear cache, just reload with current data
                    vm.LoadData(forceRefresh: false);
                }
            }
        }
    }
}