using IntuneComplianceMonitor.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace IntuneComplianceMonitor.Views
{
    public partial class SettingsPage : Page
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsPage()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel(ClientSecretBox);
            DataContext = _viewModel;

            // Set up status message updates
            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == "StatusMessage" || e.PropertyName == "IsLoading")
                {
                    UpdateMainWindowStatus();
                }
            };
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