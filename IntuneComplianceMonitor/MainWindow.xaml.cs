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
                        // Set days not checked in to 7 days by default
                        notCheckedInViewModel.DaysNotCheckedIn = 7;
                        notCheckedInViewModel.FilterByNotCheckedIn = true;
                    }
                    break;
                case "Settings":
                    // Not implemented yet
                    MessageBox.Show("Settings page is not implemented yet.", "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                default:
                    page = new DashboardPage();
                    break;
            }

            MainFrame.Navigate(page);
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            // Show loading window or indicator
            var loadingWindow = new SyncProgressWindow();
            loadingWindow.Owner = this;
            loadingWindow.Show();

            // Use Task to avoid freezing the UI
            Task.Run(async () => {
                try
                {
                    await Task.Delay(500); // Brief delay to ensure loading window is shown

                    // Refresh the current page on the UI thread
                    Dispatcher.Invoke(() => {
                        if (MainFrame.Content is DashboardPage dashboardPage)
                        {
                            // Refresh dashboard
                            if (dashboardPage.DataContext is ViewModels.DashboardViewModel viewModel)
                            {
                                viewModel.LoadData();
                            }
                        }
                        else if (MainFrame.Content is DevicesPage devicesPage)
                        {
                            // Refresh devices
                            if (devicesPage.DataContext is ViewModels.DashboardViewModel viewModel)
                            {
                                viewModel.LoadData();
                            }
                        }

                        // Close the loading window and show success message
                        loadingWindow.Dispatcher.Invoke(() => {
                            loadingWindow.Close();
                            MessageBox.Show("Successfully synchronized with Intune.", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    });
                }
                catch (Exception ex)
                {
                    // Handle any errors
                    Dispatcher.Invoke(() => {
                        loadingWindow.Close();
                        MessageBox.Show($"Error syncing with Intune: {ex.Message}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }
    }
}