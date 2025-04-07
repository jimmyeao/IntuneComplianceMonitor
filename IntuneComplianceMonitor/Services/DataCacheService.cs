using IntuneComplianceMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace IntuneComplianceMonitor.Services
{
    public class DataCacheService
    {
        private const string CacheDirectory = "Cache";
        private const string DevicesCacheFile = "devices.json";
        private const string StatsCacheFile = "stats.json";
        private const int CacheExpiryHours = 24; // Cache expires after 24 hours

        public DataCacheService()
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
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
    }

    // Classes for serialization
    public class DevicesCacheData
    {
        public List<DeviceViewModel> Devices { get; set; }
        public DateTime CacheTime { get; set; }
    }

    public class StatsCacheData
    {
        public int TotalDevices { get; set; }
        public Dictionary<string, int> DevicesByType { get; set; }
        public Dictionary<string, int> DevicesByOwnership { get; set; }
        public DateTime CacheTime { get; set; }
    }
}