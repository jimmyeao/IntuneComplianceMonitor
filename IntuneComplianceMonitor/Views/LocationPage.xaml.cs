using IntuneComplianceMonitor.ViewModels;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Windows.Controls;

namespace IntuneComplianceMonitor.Views
{
    public partial class LocationPage : Page
    {
        private readonly LocationViewModel _viewModel;

        public LocationPage()
        {
            InitializeComponent();
            _viewModel = new LocationViewModel();
            DataContext = _viewModel;


            Loaded += async (_, __) => await _viewModel.LoadDataAsync();
        }

        private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await _viewModel.LoadDataAsync(forceRefresh: true);
        }
    }
}
