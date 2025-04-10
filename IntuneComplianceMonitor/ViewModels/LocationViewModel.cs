using IntuneComplianceMonitor.Services;
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace IntuneComplianceMonitor.ViewModels
{
    public class LocationViewModel : INotifyPropertyChanged
    {
        private bool _isLoading;
        private string _statusMessage;
        private Location _mapCenter;
        private double _zoomLevel;
        private string _bingMapsKey;

        public LocationViewModel()
        {
            // Default map center (0,0) and zoom level
            MapCenter = new Location(30, 0);
            ZoomLevel = 2;

            // You can get a free Bing Maps key at https://www.bingmapsportal.com/
            // For development and testing purposes only
            BingMapsKey = "AqYfEVmxL6zHsAMqY33Tx4rNQF81oBsSqmWf7pNPl_2tF7W7Lxv2Lkh1DKZWQ7bT";
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public Location MapCenter
        {
            get => _mapCenter;
            set
            {
                _mapCenter = value;
                OnPropertyChanged();
            }
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = value;
                OnPropertyChanged();
            }
        }

        public string BingMapsKey
        {
            get => _bingMapsKey;
            set
            {
                _bingMapsKey = value;
                OnPropertyChanged();
            }
        }

        public async Task<List<CountryDeviceCount>> LoadDataAsync(bool forceRefresh = false)
        {
            IsLoading = true;
            StatusMessage = "Loading location data...";

            List<CountryDeviceCount> countryData = new List<CountryDeviceCount>();

            try
            {
                // Step 1: Try to load from location-specific cache
                var devices = await ServiceManager.Instance.DataCacheService.GetDeviceLocationCacheAsync();

                // Step 2: If cache is empty or we're forcing refresh, load fresh data
                if (forceRefresh || devices == null || !devices.Any())
                {
                    StatusMessage = "Fetching device location data...";

                    // Clear location cache if forcing refresh
                    if (forceRefresh)
                    {
                        ServiceManager.Instance.DataCacheService.ClearDeviceLocationCache();
                    }

                    // Get non-compliant devices
                    var (nonCompliantDevices, _) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();

                    // Enrich with location data
                    StatusMessage = "Enriching with location data...";
                    await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(nonCompliantDevices);

                    // Save to location-specific cache
                    devices = nonCompliantDevices;
                    await ServiceManager.Instance.DataCacheService.SaveDeviceLocationCacheAsync(devices);
                }

                // Process devices into country counts
                var countryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int unknownCount = 0;

                // Normalize and count all countries
                foreach (var device in devices)
                {
                    // Normalize country names
                    var normalizedCountry = NormalizeCountryName(device.Country);
                    device.Country = normalizedCountry;

                    // Count occurrences of each country
                    if (!string.IsNullOrWhiteSpace(normalizedCountry) &&
                        normalizedCountry.ToLowerInvariant() != "unknown")
                    {
                        if (countryCounts.ContainsKey(normalizedCountry))
                            countryCounts[normalizedCountry]++;
                        else
                            countryCounts[normalizedCountry] = 1;
                    }
                    else
                    {
                        unknownCount++;
                    }
                }

                // Debug output of country distribution
                System.Diagnostics.Debug.WriteLine("Country distribution:");
                foreach (var kvp in countryCounts.OrderByDescending(k => k.Value))
                {
                    System.Diagnostics.Debug.WriteLine($"  {kvp.Key}: {kvp.Value} devices");
                }
                System.Diagnostics.Debug.WriteLine($"  Unknown: {unknownCount} devices");

                // Get coordinates for each country
                var countryCoordinates = GetCountryCapitalCoordinates();

                // Create country data objects for map display
                foreach (var countryCount in countryCounts)
                {
                    string countryName = countryCount.Key;
                    int count = countryCount.Value;

                    // Skip if we don't have coordinates for this countrya
                    if (!countryCoordinates.TryGetValue(countryName, out var coordinates))
                        continue;

                    // Create data object
                    var country = new CountryDeviceCount
                    {
                        CountryName = countryName,
                        Count = count,
                        Latitude = coordinates.Latitude,
                        Longitude = coordinates.Longitude,
                        PushpinColor = GetColorForCount(count)
                    };

                    countryData.Add(country);
                }

                StatusMessage = $"Showing {devices.Count} non-compliant devices across {countryData.Count} countries";
                return countryData;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading location data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);

                // Display error message to user
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"Error loading device locations: {ex.Message}",
                        "Map Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });

                return new List<CountryDeviceCount>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Get appropriate color based on device count
        private Color GetColorForCount(int count)
        {
            if (count > 100)
                return Colors.Red;
            else if (count > 50)
                return Colors.Orange;
            else if (count > 20)
                return Colors.Blue;
            else if (count > 5)
                return Colors.LightBlue;
            else
                return Colors.Green;
        }

        // Helper method to normalize country names
        private string NormalizeCountryName(string country)
        {
            if (string.IsNullOrWhiteSpace(country))
                return "Unknown";

            country = country.Trim();

            // Map common variations to standardized names
            var countryMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "UK", "United Kingdom" },
                { "Great Britain", "United Kingdom" },
                { "England", "United Kingdom" },
                { "Scotland", "United Kingdom" },
                { "Wales", "United Kingdom" },
                { "Northern Ireland", "United Kingdom" },
                { "Britain", "United Kingdom" },
                { "GB", "United Kingdom" },

                { "USA", "United States" },
                { "US", "United States" },
                { "U.S.A.", "United States" },
                { "U.S.", "United States" },
                { "America", "United States" },
                { "United States of America", "United States" },

                { "UAE", "United Arab Emirates" },
                { "ROI", "Ireland" },
                { "Republic of Ireland", "Ireland" },
                { "Holland", "Netherlands" },
                { "RSA", "South Africa" },
                { "Brasil", "Brazil" },
                { "Nueva Zealand", "New Zealand" },
                { "Deutschland", "Germany" },
                { "España", "Spain" },
                { "Sverige", "Sweden" },
                { "Norge", "Norway" },
                { "Danmark", "Denmark" },
                { "Suomi", "Finland" }
            };

            if (countryMappings.TryGetValue(country, out var normalizedName))
                return normalizedName;

            return country;
        }

        // This dictionary contains country names and their latitude/longitude coordinates
        private Dictionary<string, (double Latitude, double Longitude)> GetCountryCapitalCoordinates()
        {
            // This is a simplified map of country names to their approximate center coordinates
            // Format is CountryName -> (Latitude, Longitude)
            return new Dictionary<string, (double Latitude, double Longitude)>(StringComparer.OrdinalIgnoreCase)
            {
                // Major regions your data shows
                { "United Kingdom", (51.5074, -0.1278) },    // London
                { "Romania", (44.4268, 26.1025) },           // Bucharest
                { "United States", (38.8951, -77.0364) },    // Washington DC
                { "Malaysia", (3.1390, 101.6869) },          // Kuala Lumpur
                { "Vietnam", (21.0285, 105.8342) },          // Hanoi
                { "Ireland", (53.3498, -6.2603) },           // Dublin
                { "India", (28.6139, 77.2090) },             // New Delhi
                { "Australia", (-35.2809, 149.1300) },       // Canberra
                { "New Zealand", (-41.2865, 174.7762) },     // Wellington
                { "Spain", (40.4168, -3.7038) },             // Madrid
                { "Denmark", (55.6761, 12.5683) },           // Copenhagen
                { "Tunisia", (36.8065, 10.1815) },           // Tunis
                { "Germany", (52.5200, 13.4050) },           // Berlin
                { "Singapore", (1.3521, 103.8198) },         // Singapore
                { "Brazil", (-15.7801, -47.9292) },          // Brasília
                { "Sri Lanka", (6.9271, 79.8612) },          // Colombo
                { "Sweden", (59.3293, 18.0686) },            // Stockholm
                { "France", (48.8566, 2.3522) },             // Paris
                { "Portugal", (38.7223, -9.1393) },          // Lisbon
                { "Poland", (52.2297, 21.0122) },            // Warsaw
                
                // Add more countries as needed
                { "Canada", (45.4215, -75.6972) },           // Ottawa
                { "Mexico", (19.4326, -99.1332) },           // Mexico City
                { "Italy", (41.9028, 12.4964) },             // Rome
                { "China", (39.9042, 116.4074) },            // Beijing
                { "Japan", (35.6895, 139.6917) },            // Tokyo
                { "South Korea", (37.5665, 126.9780) },      // Seoul
                { "Russia", (55.7558, 37.6173) },            // Moscow
                { "South Africa", (-26.2041, 28.0473) },     // Johannesburg
                { "Netherlands", (52.3676, 4.9041) },        // Amsterdam
                { "Belgium", (50.8503, 4.3517) },            // Brussels
                { "Switzerland", (46.9480, 7.4474) },        // Bern
                { "Austria", (48.2082, 16.3738) },           // Vienna
                { "Norway", (59.9139, 10.7522) },            // Oslo
                { "Finland", (60.1699, 24.9384) },           // Helsinki
                { "Greece", (37.9838, 23.7275) },            // Athens
                { "Turkey", (39.9334, 32.8597) },            // Ankara
                { "UAE", (24.4539, 54.3773) },               // Abu Dhabi
                { "Saudi Arabia", (24.6877, 46.7219) },      // Riyadh
                { "Israel", (31.7683, 35.2137) },            // Jerusalem
                { "Argentina", (-34.6037, -58.3816) },       // Buenos Aires
                { "Chile", (-33.4489, -70.6693) },           // Santiago
                { "Colombia", (4.7110, -74.0721) },          // Bogotá
                { "Peru", (-12.0464, -77.0428) },            // Lima
                { "Indonesia", (-6.2088, 106.8456) },        // Jakarta
                { "Thailand", (13.7563, 100.5018) },         // Bangkok
                { "Philippines", (14.5995, 120.9842) },      // Manila
                { "Egypt", (30.0444, 31.2357) },             // Cairo
                { "Nigeria", (9.0765, 7.3986) },             // Abuja
                { "Kenya", (-1.2921, 36.8219) }              // Nairobi
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Class to hold country device count data
    public class CountryDeviceCount
    {
        public string CountryName { get; set; }
        public int Count { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Color PushpinColor { get; set; }
    }
}