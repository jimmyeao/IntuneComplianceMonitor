using System.Windows.Controls;
using System.Windows;
using Microsoft.Graph.Models;
using IntuneComplianceMonitor.ViewModels;

namespace IntuneComplianceMonitor.Views
{
    public partial class CompliancePolicyPage : Page
    {
        public CompliancePolicyPage()
        {
            InitializeComponent();
            Loaded += CompliancePolicyPage_Loaded;
        }

        private async void CompliancePolicyPage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = new CompliancePolicyViewModel();
            DataContext = viewModel;

            var loadingWindow = new LoadingWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            loadingWindow.Show();

            try
            {
                await viewModel.LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load compliance data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingWindow.Close();
            }
        }
    }

}
