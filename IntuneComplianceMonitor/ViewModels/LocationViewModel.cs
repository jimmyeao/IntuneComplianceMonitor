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

namespace IntuneComplianceMonitor.ViewModels
{
    public class LocationViewModel : INotifyPropertyChanged
    {
        private HeatLandSeries[] _series;
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
            var devices = await ServiceManager.Instance.DataCacheService.GetDeviceLocationCacheAsync();
            if (forceRefresh || devices == null)
            {
                var (nonCompliantDevices, _) = await ServiceManager.Instance.IntuneService.GetNonCompliantDevicesAsync();
                await ServiceManager.Instance.IntuneService.EnrichDevicesWithUserLocationAsync(nonCompliantDevices);
                devices = nonCompliantDevices;
                await ServiceManager.Instance.DataCacheService.SaveDeviceLocationCacheAsync(devices);
            }

            var isoCodeMap = CountryNameToISO3();

            // Try to infer countries if missing
            foreach (var device in devices)
            {
                if (string.IsNullOrWhiteSpace(device.Country))
                {
                    if (device.DeviceName?.Contains("-UK", StringComparison.OrdinalIgnoreCase) == true)
                        device.Country = "united kingdom";
                    else if (device.DeviceName?.Contains("-US", StringComparison.OrdinalIgnoreCase) == true)
                        device.Country = "united states";
                    else
                        device.Country = "unknown";
                }
            }

            // Debug all country assignments
            foreach (var d in devices)
                System.Diagnostics.Debug.WriteLine($"{d.DeviceName} → '{d.Country}'");

            var grouped = devices
                .Where(d => !string.IsNullOrWhiteSpace(d.Country))
                .GroupBy(d => d.Country.ToLowerInvariant())
                .Select(g => new
                {
                    Country = g.Key,
                    Count = g.Count()
                })
                .Where(g => isoCodeMap.ContainsKey(g.Country))
                .Select(g => new HeatLand
                {
                    Name = isoCodeMap[g.Country],
                    Value = g.Count
                })
                .ToArray();

            System.Diagnostics.Debug.WriteLine($"Mapped countries: {grouped.Length}, Total devices: {devices.Count}");

            Series = new[]
            {
                new HeatLandSeries
                {
                    Lands = grouped,
                   
                }
            };
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
                ["zimbabwe"] = "zwe"
            };
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
