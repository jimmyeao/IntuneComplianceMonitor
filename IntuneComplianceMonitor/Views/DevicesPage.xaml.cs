using IntuneComplianceMonitor.ViewModels;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;

namespace IntuneComplianceMonitor.Views
{
    public partial class DevicesPage : Page
    {
        private readonly DashboardViewModel _viewModel;

        public DevicesPage()
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            // Set up status message updates
            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == "StatusMessage" || e.PropertyName == "IsLoading")
                {
                    UpdateMainWindowStatus();
                }
            };

            Loaded += DevicesPage_Loaded;
        }
        private void DevicesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if the click came from a column header — allow built-in sorting to work
            DependencyObject source = (DependencyObject)e.OriginalSource;
            while (source != null && source is not DataGridRow)
            {
                if (source is DataGridColumnHeader)
                    return; // 👈 exit: let sorting happen

                source = VisualTreeHelper.GetParent(source);
            }

            // If a row was double-clicked, open details
            if (DevicesGrid.SelectedItem is DeviceViewModel selectedDevice)
            {
                var detailsWindow = new DeviceDetailsWindow(selectedDevice);
                detailsWindow.Owner = Window.GetWindow(this);
                detailsWindow.ShowDialog();
            }
        }

        private void DevicesPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.LoadData();
        }

        private void UpdateMainWindowStatus()
        {
            // Find the MainWindow and update its status
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
            }
        }
    }
}