namespace IntuneComplianceMonitor.ViewModels
{
    public class ConfigurationProfileViewModel
    {
        #region Properties

        public string Description { get; set; }
        public string DisplayName { get; set; }
        public string Id { get; set; }
        public string Status { get; set; }

        #endregion Properties

        // e.g. Success, Error, NotApplicable
    }
}