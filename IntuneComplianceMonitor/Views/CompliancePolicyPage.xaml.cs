using IntuneComplianceMonitor.ViewModels;
using IntuneComplianceMonitor.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace IntuneComplianceMonitor.Views
{
    public partial class CompliancePolicyPage : Page
    {
        #region Fields

        private CompliancePolicyViewModel _viewModel;

        #endregion Fields

        #region Constructors

        public CompliancePolicyPage()
        {
            InitializeComponent();
            Loaded += CompliancePolicyPage_Loaded;
        }

        #endregion Constructors

        #region Methods

        private async void CompliancePolicyPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = new CompliancePolicyViewModel();
            DataContext = _viewModel;

            // Set up property change notification to update loading state in the UI
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == "IsLoading" || args.PropertyName == "StatusMessage")
                {
                    // If the main window has a status bar, update it
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
                    }
                }
            };

            // Wire up refresh handler so ViewModel can trigger reload from UI
            _viewModel.OnRefreshRequested = async () => await LoadComplianceData(_viewModel, forceRefresh: true);

            // Initial data load - use cache by default
            await LoadComplianceData(_viewModel, forceRefresh: false);
        }

        private void DevicesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if the click came from a column header - allow built-in sorting to work
            DependencyObject source = (DependencyObject)e.OriginalSource;
            while (source != null && source is not DataGridRow)
            {
                if (source is DataGridColumnHeader)
                    return; // exit: let sorting happen

                source = VisualTreeHelper.GetParent(source);
            }

            // If a row was double-clicked, open device details
            if (DevicesDataGrid.SelectedItem is DeviceViewModel selectedDevice)
            {
                var detailsWindow = new DeviceDetailsWindow(selectedDevice);
                detailsWindow.Owner = Window.GetWindow(this);
                detailsWindow.ShowDialog();
            }
        }

        private async Task LoadComplianceData(CompliancePolicyViewModel viewModel, bool forceRefresh)
        {
            try
            {
                // Set loading state directly in the ViewModel
                if (!viewModel.IsLoading)
                {
                    viewModel.IsLoading = true;
                    viewModel.StatusMessage = forceRefresh
                        ? "Refreshing compliance data from Intune..."
                        : "Loading compliance data...";
                }

                // Load data with caching behavior
                await viewModel.LoadData(forceRefresh);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading compliance data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                viewModel.StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                // Always make sure to turn off loading state
                viewModel.IsLoading = false;
            }
        }

        #endregion Methods
    }
}