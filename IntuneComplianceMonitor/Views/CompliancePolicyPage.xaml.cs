﻿using System.Windows.Controls;
using System.Windows;
using Microsoft.Graph.Models;
using IntuneComplianceMonitor.ViewModels;
using Application = System.Windows.Application;
using System;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.Views
{
    public partial class CompliancePolicyPage : Page
    {
        private CompliancePolicyViewModel _viewModel;

        public CompliancePolicyPage()
        {
            InitializeComponent();
            Loaded += CompliancePolicyPage_Loaded;
        }

        private async void CompliancePolicyPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = new CompliancePolicyViewModel();
            DataContext = _viewModel;

            // Set up property change notification to update loading state in the UI
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == "IsLoading")
                {
                    // If the main window has a status bar, update it
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateStatus(_viewModel.StatusMessage, _viewModel.IsLoading);
                    }
                }
            };

            // Wire up refresh handler so ViewModel can trigger reload from UI
            _viewModel.OnRefreshRequested = async () => await RefreshComplianceData(_viewModel);

            // Initial data load
            await RefreshComplianceData(_viewModel);
        }

        private async Task RefreshComplianceData(CompliancePolicyViewModel viewModel)
        {
            // Set loading state directly in the ViewModel
            viewModel.IsLoading = true;
            viewModel.StatusMessage = "Loading compliance data...";

            try
            {
                // Clear cache and force reload
                ServiceManager.Instance.DataCacheService.ClearCompliancePolicyCache();
                await viewModel.LoadData(forceRefresh: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading compliance data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                viewModel.StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                // Always make sure to turn off loading state
                viewModel.IsLoading = false;
            }
        }
    }
}