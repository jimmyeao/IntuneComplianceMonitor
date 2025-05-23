﻿using IntuneComplianceMonitor.ViewModels;
using System.Windows;

namespace IntuneComplianceMonitor.Views;

public partial class ComplianceDetailsWindow : Window
{
    #region Constructors

    public ComplianceDetailsWindow(CompliancePolicyStateViewModel policy, DeviceViewModel device)
    {
        InitializeComponent();

        // Wrap both into a single DataContext
        this.DataContext = new
        {
            Policy = policy,
            Device = device
        };
    }

    #endregion Constructors
}