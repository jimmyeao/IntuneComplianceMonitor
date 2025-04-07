using IntuneComplianceMonitor.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace IntuneComplianceMonitor.Views
{
    public partial class DashboardPage : Page
    {
        private readonly DashboardViewModel _viewModel;

        public DashboardPage()
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

            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
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