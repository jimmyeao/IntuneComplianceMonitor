using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.ViewModels
{
    public class CompliancePolicyStateViewModel
    {
        public string DisplayName { get; set; }
        public string State { get; set; }
        public string UserPrincipalName { get; set; }
        public DateTimeOffset? LastReportedDateTime { get; set; }
        public string PolicyId { get; set; }
        public List<string> ErrorDetails { get; set; } = new();
    }



}
