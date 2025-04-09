using System.Windows.Controls;
using System.Windows;
using Microsoft.Graph.Models;
using IntuneComplianceMonitor.ViewModels;

namespace IntuneComplianceMonitor.Views
{
    public partial class CompliancePolicyPage : Page
    {
        public CompliancePolicyPage()
        {
            InitializeComponent();
            DataContext = new ViewModels.CompliancePolicyViewModel();
        }
    }
}
