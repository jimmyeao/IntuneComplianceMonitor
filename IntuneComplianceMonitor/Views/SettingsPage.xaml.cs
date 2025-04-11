using IntuneComplianceMonitor.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace IntuneComplianceMonitor.Views
{
    public partial class SettingsPage : Page
    {
        #region Fields

        private readonly SettingsViewModel _viewModel;

        #endregion Fields

        #region Constructors

        public SettingsPage()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel(ClientSecretBox);
            DataContext = _viewModel;

            // Set up status message updates
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "StatusMessage" || e.PropertyName == "IsLoading")
                {
                    UpdateMainWindowStatus();
                }
            };
        }

        #endregion Constructors

        #region Methods

        private void UpdateMainWindowStatus()
        {
            // Find the MainWindow and update its status
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
            }
        }

        #endregion Methods
    }
}