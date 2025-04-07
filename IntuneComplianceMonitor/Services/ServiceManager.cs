using IntuneComplianceMonitor.Services;
using System.Windows;

public class ServiceManager
{
    private static ServiceManager _instance;
    private static readonly object _lock = new object();

    public IntuneService IntuneService { get; private set; }
    public SampleDataService SampleDataService { get; private set; }
    public DataCacheService DataCacheService { get; private set; }
    public SettingsService SettingsService { get; private set; }
    public bool UseRealData { get; private set; }

    private ServiceManager()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("ServiceManager constructor starting");

            // Initialize settings service first
            System.Diagnostics.Debug.WriteLine("Initializing SettingsService");
            SettingsService = new SettingsService();
            System.Diagnostics.Debug.WriteLine("SettingsService initialized successfully");

            try
            {
                System.Diagnostics.Debug.WriteLine("Initializing IntuneService");
                IntuneService = new IntuneService(SettingsService);
                UseRealData = true;
                System.Diagnostics.Debug.WriteLine("IntuneService initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing IntuneService: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Could not connect to Intune: {ex.Message}\n\nThe application will use sample data instead.",
                    "Service Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                UseRealData = false;
            }

            // Create the sample data service as a fallback
            System.Diagnostics.Debug.WriteLine("Initializing SampleDataService");
            SampleDataService = new SampleDataService();
            System.Diagnostics.Debug.WriteLine("SampleDataService initialized successfully");

            // Create the data cache service
            System.Diagnostics.Debug.WriteLine("Initializing DataCacheService");
            DataCacheService = new DataCacheService();
            System.Diagnostics.Debug.WriteLine("DataCacheService initialized successfully");

            System.Diagnostics.Debug.WriteLine("ServiceManager constructor completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in ServiceManager constructor: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            MessageBox.Show($"A critical error occurred during application initialization: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Re-throw to allow the application to terminate gracefully
            throw;
        }
    }

    public static ServiceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                System.Diagnostics.Debug.WriteLine("ServiceManager Instance property accessed, instance is null");
                lock (_lock)
                {
                    System.Diagnostics.Debug.WriteLine("Entered lock in ServiceManager Instance property");
                    if (_instance == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Creating new ServiceManager instance");
                        try
                        {
                            _instance = new ServiceManager();
                            System.Diagnostics.Debug.WriteLine("ServiceManager instance created successfully");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error creating ServiceManager instance: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                            // Re-throw the exception to be handled by the application
                            throw;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ServiceManager instance already exists");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ServiceManager Instance property accessed, returning existing instance");
            }
            return _instance;
        }
    }
}