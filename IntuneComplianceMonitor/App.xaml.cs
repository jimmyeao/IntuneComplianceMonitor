using System.Windows;
using System.Windows.Threading;

namespace IntuneComplianceMonitor
{
    public partial class App : Application
    {
        public App()
        {
            // Add global exception handler
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("App.OnStartup starting");
                base.OnStartup(e);

                try
                {
                    // Initialize the ServiceManager early
                    System.Diagnostics.Debug.WriteLine("About to initialize ServiceManager");
                    var serviceManager = ServiceManager.Instance;
                    System.Diagnostics.Debug.WriteLine("ServiceManager initialized successfully in App.xaml.cs");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing ServiceManager: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    MessageBox.Show($"Error initializing services: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Continue execution anyway - the application might still be usable
                }

                System.Diagnostics.Debug.WriteLine("App.OnStartup completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception in OnStartup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Critical error during application startup: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                    "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Shut down the application
                Current.Shutdown();
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception.StackTrace}");

            MessageBox.Show($"An unhandled exception occurred: {e.Exception.Message}\n\nStack trace: {e.Exception.StackTrace}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Mark as handled to prevent application crash
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"Unhandled application exception: {exception?.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {exception?.StackTrace}");

            MessageBox.Show($"A fatal error occurred: {exception?.Message}\n\nStack trace: {exception?.StackTrace}",
                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}