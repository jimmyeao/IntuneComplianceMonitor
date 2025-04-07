using IntuneComplianceMonitor.Services;
using System;
using System.Windows;

namespace IntuneComplianceMonitor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Initialize the ServiceManager early
                var serviceManager = ServiceManager.Instance;
                System.Diagnostics.Debug.WriteLine("ServiceManager initialized in App.xaml.cs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ServiceManager: {ex.Message}");
                MessageBox.Show($"Error initializing services: {ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}