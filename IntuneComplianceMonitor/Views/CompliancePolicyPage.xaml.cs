using System.Windows.Controls;
using System.Windows;
using Microsoft.Graph.Models;
using IntuneComplianceMonitor.ViewModels;
using Application = System.Windows.Application;

namespace IntuneComplianceMonitor.Views
{
    public partial class CompliancePolicyPage : Page
    {
        public CompliancePolicyPage()
        {
            InitializeComponent();
            Loaded += CompliancePolicyPage_Loaded;
        }
      
        private async Task RefreshComplianceData(CompliancePolicyViewModel viewModel)
        {
            var loadingWindow = new LoadingWindow
            {
                Owner = Application.Current.MainWindow
            };

            loadingWindow.Show();

            try
            {
                ServiceManager.Instance.DataCacheService.ClearCompliancePolicyCache(); // 🔁 Clear cache
                await viewModel.LoadData(forceRefresh: true);                          // 🔄 Force reload
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading compliance data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingWindow.Close();
            }
        }


        private async void CompliancePolicyPage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = new CompliancePolicyViewModel();
            DataContext = viewModel;

            // Wire up refresh handler so ViewModel can trigger reload from UI
            viewModel.OnRefreshRequested = async () => await RefreshComplianceData(viewModel);

            await RefreshComplianceData(viewModel); // initial load with spinner
        }

    }

}
