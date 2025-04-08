using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.ViewModels
{
    public class CompliancePolicyStateViewModel
    {
        public string PolicyId { get; set; }
        public string DisplayName { get; set; }
        public string State { get; set; }
        public string UserPrincipalName { get; set; }
        public DateTimeOffset? LastReportedDateTime { get; set; }
        public List<string> ErrorDetails { get; set; } = new();

        // New property to convert ErrorDetails to a single string for display
        public string ErrorDetailsList =>
            ErrorDetails != null && ErrorDetails.Any()
                ? string.Join("; ", ErrorDetails)
                : "No specific details";

        // Computed property for row coloring
        public bool IsNonCompliant =>
            State?.Equals("Non-Compliant", StringComparison.OrdinalIgnoreCase) == true ||
            State?.Equals("Error", StringComparison.OrdinalIgnoreCase) == true;
    }


}
