using LiveChartsCore.Geo;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using IntuneComplianceMonitor.Services;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using System.Windows;

namespace IntuneComplianceMonitor.ViewModels
{
    public class LocationViewModel : INotifyPropertyChanged
    {
        private HeatLandSeries[] _series;
        private bool _isLoading;
        private string _statusMessage;

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

        public HeatLandSeries[] Series
        {
            get => _series;
            set
            {
                _series = value;
                OnPropertyChanged();
            }
        }

        public async Task LoadDataAsync(bool forceRefresh = false)
        {
            IsLoading = true;
            StatusMessage = "Loading location data...";

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

                    StatusMessage = "Location data ready";
                }

                CreateMap(devices);

                // Make sure to set loading to false when done
                IsLoading = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading location data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                IsLoading = false;
            }
        }

        private void CreateMap(List<DeviceViewModel> devices)
        {
            try
            {
                var isoCodeMap = CountryNameToISO3();
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
                System.Diagnostics.Debug.WriteLine($"Country distribution:");
                foreach (var kvp in countryCounts.OrderByDescending(k => k.Value))
                {
                    System.Diagnostics.Debug.WriteLine($"  {kvp.Key}: {kvp.Value} devices");
                }
                System.Diagnostics.Debug.WriteLine($"  Unknown: {unknownCount} devices");

                // Create heat map lands
                var heatLands = new List<HeatLand>();
                foreach (var countryCount in countryCounts)
                {
                    string country = countryCount.Key.ToLowerInvariant();

                    // Try to map to ISO3 code
                    if (isoCodeMap.TryGetValue(country, out string iso3Code))
                    {
                        heatLands.Add(new HeatLand
                        {
                            Name = iso3Code,
                            Value = countryCount.Value
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not find ISO3 code for country: {country}");

                        // Try some additional normalization
                        if (country.Contains("united kingdom") || country.Contains("uk") ||
                            country.Contains("britain") || country.Contains("england"))
                        {
                            heatLands.Add(new HeatLand
                            {
                                Name = "gbr",
                                Value = countryCount.Value
                            });
                        }
                        else if (country.Contains("united states") || country.Contains("usa") ||
                                 country.Contains("us") || country.Contains("america"))
                        {
                            heatLands.Add(new HeatLand
                            {
                                Name = "usa",
                                Value = countryCount.Value
                            });
                        }
                        else if (country.Contains("spain") || country.Contains("españa"))
                        {
                            heatLands.Add(new HeatLand
                            {
                                Name = "esp",
                                Value = countryCount.Value
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Created {heatLands.Count} heat map lands from {devices.Count} devices");

                // Create the series with the heat lands
                Series = new[]
                {
                    new HeatLandSeries
                    {
                        Lands = heatLands.ToArray()
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating map: {ex.Message}");
                StatusMessage = $"Error creating map: {ex.Message}";

                // Display error message to user
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"Error creating map: {ex.Message}", "Map Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
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

        private Dictionary<string, string> CountryNameToISO3()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["afghanistan"] = "afg",
                ["albania"] = "alb",
                ["algeria"] = "dza",
                ["american samoa"] = "asm",
                ["andorra"] = "and",
                ["angola"] = "ago",
                ["anguilla"] = "aia",
                ["antarctica"] = "ata",
                ["antigua and barbuda"] = "atg",
                ["argentina"] = "arg",
                ["armenia"] = "arm",
                ["aruba"] = "abw",
                ["australia"] = "aus",
                ["austria"] = "aut",
                ["azerbaijan"] = "aze",
                ["bahamas"] = "bhs",
                ["bahrain"] = "bhr",
                ["bangladesh"] = "bgd",
                ["barbados"] = "brb",
                ["belarus"] = "blr",
                ["belgium"] = "bel",
                ["belize"] = "blz",
                ["benin"] = "ben",
                ["bermuda"] = "bmu",
                ["bhutan"] = "btn",
                ["bolivia"] = "bol",
                ["bosnia and herzegovina"] = "bih",
                ["botswana"] = "bwa",
                ["brazil"] = "bra",
                ["brunei"] = "brn",
                ["bulgaria"] = "bgr",
                ["burkina faso"] = "bfa",
                ["burundi"] = "bdi",
                ["cabo verde"] = "cpv",
                ["cambodia"] = "khm",
                ["cameroon"] = "cmr",
                ["canada"] = "can",
                ["cape verde"] = "cpv",
                ["central african republic"] = "caf",
                ["chad"] = "tcd",
                ["chile"] = "chl",
                ["china"] = "chn",
                ["colombia"] = "col",
                ["comoros"] = "com",
                ["congo"] = "cod",
                ["republic of the congo"] = "cog",
                ["costa rica"] = "cri",
                ["croatia"] = "hrv",
                ["cuba"] = "cub",
                ["cyprus"] = "cyp",
                ["czech republic"] = "cze",
                ["denmark"] = "dnk",
                ["djibouti"] = "dji",
                ["dominica"] = "dma",
                ["dominican republic"] = "dom",
                ["ecuador"] = "ecu",
                ["egypt"] = "egy",
                ["el salvador"] = "slv",
                ["equatorial guinea"] = "gnq",
                ["eritrea"] = "eri",
                ["estonia"] = "est",
                ["eswatini"] = "swz",
                ["ethiopia"] = "eth",
                ["fiji"] = "fji",
                ["finland"] = "fin",
                ["france"] = "fra",
                ["gabon"] = "gab",
                ["gambia"] = "gmb",
                ["georgia"] = "geo",
                ["germany"] = "deu",
                ["ghana"] = "gha",
                ["greece"] = "grc",
                ["greenland"] = "grl",
                ["guatemala"] = "gtm",
                ["guinea"] = "gin",
                ["guinea-bissau"] = "gnb",
                ["guyana"] = "guy",
                ["haiti"] = "hti",
                ["honduras"] = "hnd",
                ["hungary"] = "hun",
                ["iceland"] = "isl",
                ["india"] = "ind",
                ["indonesia"] = "idn",
                ["iran"] = "irn",
                ["iraq"] = "irq",
                ["ireland"] = "irl",
                ["israel"] = "isr",
                ["italy"] = "ita",
                ["ivory coast"] = "civ",
                ["jamaica"] = "jam",
                ["japan"] = "jpn",
                ["jordan"] = "jor",
                ["kazakhstan"] = "kaz",
                ["kenya"] = "ken",
                ["korea"] = "kor",
                ["south korea"] = "kor",
                ["north korea"] = "prk",
                ["kosovo"] = "xkx",
                ["kuwait"] = "kwt",
                ["kyrgyzstan"] = "kgz",
                ["laos"] = "lao",
                ["latvia"] = "lva",
                ["lebanon"] = "lbn",
                ["lesotho"] = "lso",
                ["liberia"] = "lbr",
                ["libya"] = "lby",
                ["liechtenstein"] = "lie",
                ["lithuania"] = "ltu",
                ["luxembourg"] = "lux",
                ["madagascar"] = "mdg",
                ["malawi"] = "mwi",
                ["malaysia"] = "mys",
                ["maldives"] = "mdv",
                ["mali"] = "mli",
                ["malta"] = "mlt",
                ["mauritania"] = "mrt",
                ["mauritius"] = "mus",
                ["mexico"] = "mex",
                ["moldova"] = "mda",
                ["monaco"] = "mco",
                ["mongolia"] = "mng",
                ["montenegro"] = "mne",
                ["morocco"] = "mar",
                ["mozambique"] = "moz",
                ["myanmar"] = "mmr",
                ["namibia"] = "nam",
                ["nepal"] = "npl",
                ["netherlands"] = "nld",
                ["new zealand"] = "nzl",
                ["nicaragua"] = "nic",
                ["niger"] = "ner",
                ["nigeria"] = "nga",
                ["north macedonia"] = "mkd",
                ["norway"] = "nor",
                ["oman"] = "omn",
                ["pakistan"] = "pak",
                ["palestine"] = "pse",
                ["panama"] = "pan",
                ["papua new guinea"] = "png",
                ["paraguay"] = "pry",
                ["peru"] = "per",
                ["philippines"] = "phl",
                ["poland"] = "pol",
                ["portugal"] = "prt",
                ["qatar"] = "qat",
                ["romania"] = "rou",
                ["russia"] = "rus",
                ["rwanda"] = "rwa",
                ["saudi arabia"] = "sau",
                ["senegal"] = "sen",
                ["serbia"] = "srb",
                ["singapore"] = "sgp",
                ["slovakia"] = "svk",
                ["slovenia"] = "svn",
                ["somalia"] = "som",
                ["south africa"] = "zaf",
                ["south sudan"] = "ssd",
                ["sri lanka"] = "lka",
                ["sudan"] = "sdn",
                ["suriname"] = "sur",
                ["sweden"] = "swe",
                ["switzerland"] = "che",
                ["syria"] = "syr",
                ["taiwan"] = "twn",
                ["tajikistan"] = "tjk",
                ["tanzania"] = "tza",
                ["thailand"] = "tha",
                ["timor-leste"] = "tls",
                ["togo"] = "tgo",
                ["trinidad and tobago"] = "tto",
                ["tunisia"] = "tun",
                ["turkey"] = "tur",
                ["turkmenistan"] = "tkm",
                ["uganda"] = "uga",
                ["ukraine"] = "ukr",
                ["united arab emirates"] = "are",
                ["united kingdom"] = "gbr",
                ["united states"] = "usa",
                ["uruguay"] = "ury",
                ["uzbekistan"] = "uzb",
                ["venezuela"] = "ven",
                ["vietnam"] = "vnm",
                ["yemen"] = "yem",
                ["zambia"] = "zmb",
                ["zimbabwe"] = "zwe",

                // Add missing countries from the error
                ["spain"] = "esp",
                ["españa"] = "esp",
                ["gb"] = "gbr" // ISO3 code for United Kingdom
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}