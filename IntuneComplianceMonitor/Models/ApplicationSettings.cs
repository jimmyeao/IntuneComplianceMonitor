using System.Text.Json.Serialization;

namespace IntuneComplianceMonitor.Models
{
    public class ApplicationSettings
    {
        #region Properties

        [JsonPropertyName("activeDevicesTimeframeInDays")]
        public int ActiveDevicesTimeframeInDays { get; set; } = 30;

        [JsonPropertyName("autoRefreshEnabled")]
        public bool AutoRefreshEnabled { get; set; } = false;

        [JsonPropertyName("autoRefreshIntervalMinutes")]
        public int AutoRefreshIntervalMinutes { get; set; } = 60;

        // Default values
        [JsonPropertyName("daysNotCheckedIn")]
        public int DaysNotCheckedIn { get; set; } = 7;

        // Intune/Graph API credentials
        [JsonPropertyName("intuneClientId")]
        public string IntuneClientId { get; set; }

        // No default for client secret since it's sensitive
        [JsonPropertyName("intuneClientSecret")]
        public string IntuneClientSecret { get; set; } = "";

        [JsonPropertyName("intuneTenantId")]
        public string IntuneTenantId { get; set; }

        [JsonPropertyName("lastSyncTime")]
        public DateTime LastSyncTime { get; set; } = DateTime.MinValue;

        #endregion Properties
    }
}