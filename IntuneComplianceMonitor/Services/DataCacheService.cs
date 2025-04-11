using IntuneComplianceMonitor.ViewModels;
using System.IO;
using System.Text.Json;

namespace IntuneComplianceMonitor.Services
{
    public class DataCacheService
    {
        #region Fields

        private const string CacheDirectory = "Cache";
        private const int CacheExpiryHours = 24;
        private const string CompliancePolicyCacheFile = "compliancePolicies.json";
        private const string DevicesCacheFile = "devices.json";
        private const string LocationCacheFile = "deviceLocations.json";
        private const string StatsCacheFile = "stats.json";

        #endregion Fields

        // Cache expires after 24 hours

        #region Constructors

        public DataCacheService()
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        #endregion Constructors

        #region Methods

        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(CacheDirectory))
                    {
                        File.Delete(file);
                    }
                    System.Diagnostics.Debug.WriteLine("Cache cleared");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing cache: {ex.Message}");
            }
        }

        public void ClearCompliancePolicyCache()
        {
            try
            {
                string path = Path.Combine(CacheDirectory, "compliancePolicies.json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    System.Diagnostics.Debug.WriteLine("Cleared compliance policy cache");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing compliance cache: {ex.Message}");
            }
        }

        public void ClearDeviceLocationCache()
        {
            try
            {
                string path = Path.Combine(CacheDirectory, LocationCacheFile);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    System.Diagnostics.Debug.WriteLine("Cleared device location cache");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing location cache: {ex.Message}");
            }
        }

        public async Task<Dictionary<string, List<DeviceViewModel>>> GetCompliancePoliciesFromCacheAsync()
        {
            try
            {
                string path = Path.Combine(CacheDirectory, CompliancePolicyCacheFile);
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine("Compliance policy cache file does not exist");
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                var data = JsonSerializer.Deserialize<CompliancePolicyCacheData>(json);

                // Check if cache is expired (24 hours)
                if (DateTime.Now - data.CacheTime > TimeSpan.FromHours(CacheExpiryHours))
                {
                    System.Diagnostics.Debug.WriteLine("Compliance policy cache expired");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {data.GroupedData.Count} policy groups from cache");
                System.Diagnostics.Debug.WriteLine($"Cache age: {DateTime.Now - data.CacheTime}");

                return data.GroupedData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading compliance policy cache: {ex.Message}");
                return null;
            }
        }
        public async Task<List<DeviceViewModel>> GetDeviceLocationCacheAsync()
        {
            try
            {
                string path = Path.Combine(CacheDirectory, LocationCacheFile);

                // If cache file doesn't exist, return null
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine("Location cache file does not exist");
                    return null;
                }

                string json = await File.ReadAllTextAsync(path);
                var data = JsonSerializer.Deserialize<DeviceLocationCacheData>(json);

                // Check if cache is expired (24 hours)
                if (DateTime.Now - data.CacheTime > TimeSpan.FromHours(CacheExpiryHours))
                {
                    System.Diagnostics.Debug.WriteLine("Location cache expired");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {data.Devices.Count} devices from location cache");
                return data.Devices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading location cache: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DeviceViewModel>> GetDevicesFromCacheAsync()
        {
            try
            {
                string cachePath = Path.Combine(CacheDirectory, DevicesCacheFile);

                if (!File.Exists(cachePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(cachePath);
                var cacheData = JsonSerializer.Deserialize<DevicesCacheData>(json);

                // Check if cache is expired
                if (DateTime.Now - cacheData.CacheTime > TimeSpan.FromHours(CacheExpiryHours))
                {
                    System.Diagnostics.Debug.WriteLine("Cache is expired");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {cacheData.Devices.Count} devices from cache");
                return cacheData.Devices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading devices from cache: {ex.Message}");
                return null;
            }
        }

        public async Task<StatsCacheData> GetStatsFromCacheAsync()
        {
            try
            {
                string cachePath = Path.Combine(CacheDirectory, StatsCacheFile);

                if (!File.Exists(cachePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(cachePath);
                var cacheData = JsonSerializer.Deserialize<StatsCacheData>(json);

                // Check if cache is expired
                if (DateTime.Now - cacheData.CacheTime > TimeSpan.FromHours(CacheExpiryHours))
                {
                    System.Diagnostics.Debug.WriteLine("Stats cache is expired");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine("Loaded stats from cache");
                return cacheData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading stats from cache: {ex.Message}");
                return null;
            }
        }

        public async Task SaveCompliancePoliciesToCacheAsync(Dictionary<string, List<DeviceViewModel>> groupedData)
        {
            try
            {
                var data = new CompliancePolicyCacheData
                {
                    GroupedData = groupedData,
                    CacheTime = DateTime.Now
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(Path.Combine(CacheDirectory, CompliancePolicyCacheFile), json);
                System.Diagnostics.Debug.WriteLine("Saved compliance policy data to cache");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving compliance policy cache: {ex.Message}");
            }
        }

        public async Task SaveDeviceLocationCacheAsync(List<DeviceViewModel> devices)
        {
            try
            {
                // Create cache directory if it doesn't exist
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                var data = new DeviceLocationCacheData
                {
                    Devices = devices,
                    CacheTime = DateTime.Now
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(Path.Combine(CacheDirectory, LocationCacheFile), json);
                System.Diagnostics.Debug.WriteLine($"Saved {devices.Count} devices to location cache");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving location cache: {ex.Message}");
            }
        }

        public async Task SaveDevicesToCacheAsync(List<DeviceViewModel> devices)
        {
            try
            {
                var cacheData = new DevicesCacheData
                {
                    Devices = devices,
                    CacheTime = DateTime.Now
                };

                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(Path.Combine(CacheDirectory, DevicesCacheFile), json);
                System.Diagnostics.Debug.WriteLine($"Saved {devices.Count} devices to cache");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving devices to cache: {ex.Message}");
            }
        }

        public async Task SaveStatsToCacheAsync(
            int totalDevices,
            Dictionary<string, int> devicesByType,
            Dictionary<string, int> devicesByOwnership)
        {
            try
            {
                var cacheData = new StatsCacheData
                {
                    TotalDevices = totalDevices,
                    DevicesByType = devicesByType,
                    DevicesByOwnership = devicesByOwnership,
                    CacheTime = DateTime.Now
                };

                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(Path.Combine(CacheDirectory, StatsCacheFile), json);
                System.Diagnostics.Debug.WriteLine("Saved stats to cache");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving stats to cache: {ex.Message}");
            }
        }

        #endregion Methods

        #region Classes

        public class CompliancePolicyCacheData
        {
            #region Properties

            public DateTime CacheTime { get; set; }
            public Dictionary<string, List<DeviceViewModel>> GroupedData { get; set; }

            #endregion Properties
        }

        public class DeviceLocationCacheData
        {
            #region Properties

            public DateTime CacheTime { get; set; }
            public List<DeviceViewModel> Devices { get; set; }

            #endregion Properties
        }

        #endregion Classes
    }

    // Classes for serialization
    public class DevicesCacheData
    {
        #region Properties

        public DateTime CacheTime { get; set; }
        public List<DeviceViewModel> Devices { get; set; }

        #endregion Properties
    }

    public class StatsCacheData
    {
        #region Properties

        public DateTime CacheTime { get; set; }
        public Dictionary<string, int> DevicesByOwnership { get; set; }
        public Dictionary<string, int> DevicesByType { get; set; }
        public int TotalDevices { get; set; }

        #endregion Properties
    }
}