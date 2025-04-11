using IntuneComplianceMonitor.Services;
using System.Windows;

public class ServiceManager
{
    #region Fields

    private static readonly object _lock = new object();
    private static ServiceManager _instance;
    private Lazy<DataCacheService> _dataCacheService;

    // Make these lazy-initialized with thread-safety
    private Lazy<IntuneService> _intuneService;

    private Lazy<SampleDataService> _sampleDataService;
    private Lazy<SettingsService> _settingsService;

    #endregion Fields

    #region Constructors

    private ServiceManager()
    {
        InitializeServices();
    }

    #endregion Constructors

    #region Properties

    public static ServiceManager Instance
    {
        get
        {
            // Double-checked locking pattern
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Creating ServiceManager instance");
                        _instance = new ServiceManager();
                    }
                }
            }
            return _instance;
        }
    }

    public DataCacheService DataCacheService => _dataCacheService.Value;

    // Expose services with lazy initialization
    public IntuneService IntuneService => _intuneService.Value;

    public SampleDataService SampleDataService => _sampleDataService.Value;
    public SettingsService SettingsService => _settingsService.Value;
    public bool UseRealData { get; private set; }

    #endregion Properties

    #region Methods

    private void InitializeServices()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("ServiceManager InitializeServices starting");

            // Lazy initialization with thread-safe singleton pattern
            _settingsService = new Lazy<SettingsService>(() =>
            {
                System.Diagnostics.Debug.WriteLine("Initializing SettingsService");
                return new SettingsService();
            }, true);

            _sampleDataService = new Lazy<SampleDataService>(() =>
            {
                System.Diagnostics.Debug.WriteLine("Initializing SampleDataService");
                return new SampleDataService();
            }, true);

            _dataCacheService = new Lazy<DataCacheService>(() =>
            {
                System.Diagnostics.Debug.WriteLine("Initializing DataCacheService");
                return new DataCacheService();
            }, true);

            _intuneService = new Lazy<IntuneService>(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Initializing IntuneService");
                    var service = new IntuneService(SettingsService);
                    UseRealData = true;
                    return service;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing IntuneService: {ex.Message}");
                    UseRealData = false;
                    MessageBox.Show($"Could not connect to Intune: {ex.Message}\n\nThe application will use sample data instead.",
                        "Service Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }, true);

            System.Diagnostics.Debug.WriteLine("ServiceManager InitializeServices completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in ServiceManager constructor: {ex.Message}");
            throw;
        }
    }

    #endregion Methods
}