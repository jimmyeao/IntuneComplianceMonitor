using System;
using System.Text.Json.Serialization;

namespace IntuneComplianceMonitor.Models
{
    public class ApplicationSettings
    {
        // Default values
        [JsonPropertyName("daysNotCheckedIn")]
        public int DaysNotCheckedIn { get; set; } = 7;

        [JsonPropertyName("activeDevicesTimeframeInDays")]
        public int ActiveDevicesTimeframeInDays { get; set; } = 30;

        [JsonPropertyName("autoRefreshEnabled")]
        public bool AutoRefreshEnabled { get; set; } = false;

        [JsonPropertyName("autoRefreshIntervalMinutes")]
        public int AutoRefreshIntervalMinutes { get; set; } = 60;

        [JsonPropertyName("lastSyncTime")]
        public DateTime LastSyncTime { get; set; } = DateTime.MinValue;

        // Intune/Graph API credentials
        [JsonPropertyName("intuneClientId")]
        public string IntuneClientId { get; set; }

        [JsonPropertyName("intuneTenantId")]
        public string IntuneTenantId { get; set; }

        // No default for client secret since it's sensitive
        [JsonPropertyName("intuneClientSecret")]
        public string IntuneClientSecret { get; set; } = "";
    }
}