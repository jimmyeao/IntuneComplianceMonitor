using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.ViewModels
{
    public class ConfigurationProfileViewModel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; } // e.g. Success, Error, NotApplicable
        public string Description { get; set; }
    }

}
