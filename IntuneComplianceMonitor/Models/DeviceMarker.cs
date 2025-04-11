using Microsoft.Maps.MapControl.WPF;
using System.Windows.Media;

namespace IntuneComplianceMonitor.Models
{
    public class DeviceMarker
    {
        #region Constructors

        public DeviceMarker(string countryName, double latitude, double longitude, int count)
        {
            CountryName = countryName;
            Location = new Location(latitude, longitude);
            Count = count;

            // Calculate size based on count (min 20, max 70)
            Size = 20 + (count / 5.0);
            if (Size > 70) Size = 70;

            // Determine color based on count
            if (count > 100)
                Fill = new SolidColorBrush(Color.FromRgb(232, 17, 35));    // Red
            else if (count > 50)
                Fill = new SolidColorBrush(Color.FromRgb(255, 140, 0));    // Orange
            else if (count > 20)
                Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215));    // Blue
            else if (count > 5)
                Fill = new SolidColorBrush(Color.FromRgb(0, 153, 188));    // Light Blue
            else
                Fill = new SolidColorBrush(Color.FromRgb(16, 137, 62));    // Green
        }

        #endregion Constructors

        #region Properties

        public int Count { get; set; }
        public string CountryName { get; set; }
        public SolidColorBrush Fill { get; set; }
        public Location Location { get; set; }
        public double Size { get; set; }

        #endregion Properties
    }
}