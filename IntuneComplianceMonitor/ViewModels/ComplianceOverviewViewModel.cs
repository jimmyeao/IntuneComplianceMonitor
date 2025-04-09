using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.ViewModels
{
    public class ComplianceOverviewViewModel
    {
        public string PolicyName { get; set; }
        public int AffectedDeviceCount { get; set; }
    }
}
