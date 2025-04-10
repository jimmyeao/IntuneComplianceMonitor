using IntuneComplianceMonitor.Services;
using IntuneComplianceMonitor.ViewModels;
using IntuneComplianceMonitor.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;



namespace IntuneComplianceMonitor
{
    public partial class MainWindow : Window
    {
        public string TokenStatus => $"Token expires in: {ServiceManager.Instance.IntuneService.TokenProvider.TimeUntilExpiry:mm\\:ss}";
        public string CurrentUser => $"Signed in as: {ServiceManager.Instance.IntuneService.TokenProvider.CurrentUserPrincipalName ?? "Not signed in"}";
        public ICommand LogoutCommand => new RelayCommand(async _ => await Logout());

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                Loaded += async (_, __) =>
                {
                    NavigateToPage("Dashboard");

                    await Task.Delay(500); // let the DashboardPage and its DataContext load

                    if (MainFrame.Content is DashboardPage page &&
                        page.DataContext is DashboardViewModel vm)
                    {
                        var user = await ServiceManager.Instance.IntuneService.EnsureUserPrincipalNameAsync();
                        vm.Title = $"Intune Compliance Monitor — {user}";
                    }
                };


            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to set title with user info: {ex.Message}");
            }

            // Load the dashboard page by default
            NavigateToPage("Dashboard");
        }
        private async Task Logout()
        {
            await ServiceManager.Instance.IntuneService.TokenProvider.LogoutAsync();
            MessageBox.Show("Logged out. Please restart the app to reauthenticate.", "Logged Out", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown(); // Optional: force restart
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
            StatusText.Text = message + " "+ ($"Token valid until: {ServiceManager.Instance.IntuneService.TokenExpires:g}");
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
                    if (page.DataContext is DashboardViewModel deviceViewModel)
                    {
                        // Completely reset all filters for All Devices view
                        deviceViewModel.SearchText = "";
                        deviceViewModel.SelectedDeviceType = "";
                        deviceViewModel.SelectedOwnership = "";
                        deviceViewModel.ShowOnlyNonCompliant = false;
                        deviceViewModel.FilterByNotCheckedIn = false;
                        deviceViewModel.DaysNotCheckedIn = 0; // Reset days not checked in

                        // Ensure filters are applied with reset conditions
                        deviceViewModel.ApplyFilters();
                    }
                    break;
                case "NotCheckedIn":
                    page = new DevicesPage();
                    if (page.DataContext is DashboardViewModel notCheckedInViewModel)
                    {
                        // Clear other filters first
                        notCheckedInViewModel.SearchText = "";
                        notCheckedInViewModel.SelectedDeviceType = "";
                        notCheckedInViewModel.SelectedOwnership = "";
                        notCheckedInViewModel.ShowOnlyNonCompliant = false;

                        // Set days not checked in from settings
                        var settings = ServiceManager.Instance.SettingsService.CurrentSettings;
                        notCheckedInViewModel.DaysNotCheckedIn = settings.DaysNotCheckedIn;

                        // Automatically set and apply the filter
                        notCheckedInViewModel.FilterByNotCheckedIn = true;

                        // Trigger immediate filter application
                        notCheckedInViewModel.ApplyFilters();
                    }
                    break;
                case "Settings":
                    page = new SettingsPage();
                    break;
                default:
                    page = new DashboardPage();
                    break;
                case "Compliance":
                    page = new CompliancePolicyPage();
                    break;
                case "Location":
                    page = new LocationPage();
                    break;

            }

            // Always navigate to the new page
            MainFrame.Navigate(page);
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }
    }
}