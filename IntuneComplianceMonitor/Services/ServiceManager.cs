using System;
using System.Windows;

namespace IntuneComplianceMonitor.Services
{
    public class ServiceManager
    {
        private static ServiceManager _instance;
        private static readonly object _lock = new object();

        public IntuneService IntuneService { get; private set; }
        public SampleDataService SampleDataService { get; private set; }
        public DataCacheService DataCacheService { get; private set; }
        public bool UseRealData { get; private set; }

        private ServiceManager()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Initializing IntuneService in ServiceManager");
                IntuneService = new IntuneService();
                UseRealData = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing IntuneService: {ex.Message}");
                MessageBox.Show($"Could not connect to Intune: {ex.Message}\n\nThe application will use sample data instead.",
                    "Service Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                UseRealData = false;
            }

            // Always create the sample data service as a fallback
            SampleDataService = new SampleDataService();

            // Create the data cache service
            DataCacheService = new DataCacheService();
        }

        public static ServiceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ServiceManager();
                        }
                    }
                }
                return _instance;
            }
        }
    }
}