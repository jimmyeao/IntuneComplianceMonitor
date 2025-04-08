using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using IntuneComplianceMonitor.Models;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsFilePath;
    private ApplicationSettings _currentSettings;

    public SettingsService()
    {
        System.Diagnostics.Debug.WriteLine("SettingsService constructor starting");
        try
        {
            // Store settings in the same directory as the application
            _settingsFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                SettingsFileName);

            System.Diagnostics.Debug.WriteLine($"Settings file path: {_settingsFilePath}");

            // Initialize with empty settings
            _currentSettings = new ApplicationSettings();

            // Load settings immediately
            LoadSettingsSync();

            // Validate that required settings exist
            ValidateRequiredSettings();

            System.Diagnostics.Debug.WriteLine("SettingsService constructor completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SettingsService constructor: {ex.Message}");
            throw; // Re-throw to let the application handle it
        }
    }

    public ApplicationSettings CurrentSettings => _currentSettings;

    private void LoadSettingsSync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                System.Diagnostics.Debug.WriteLine("Settings file exists, reading content");
                string json = File.ReadAllText(_settingsFilePath);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine("Deserializing settings");
                    var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);

                    if (settings != null)
                    {
                        _currentSettings = settings;
                        System.Diagnostics.Debug.WriteLine("Settings loaded successfully");
                        return;
                    }
                }
            }

            // If we get here, either the file doesn't exist or couldn't be loaded
            System.Diagnostics.Debug.WriteLine("Settings file not found or invalid");

            // Create a new settings file with prompts for required values
            PromptForRequiredSettings();
            SaveSettingsSync(_currentSettings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void ValidateRequiredSettings()
    {
        if (string.IsNullOrEmpty(_currentSettings.IntuneClientId) ||
            string.IsNullOrEmpty(_currentSettings.IntuneTenantId))
        {
            PromptForRequiredSettings();
            SaveSettingsSync(_currentSettings);
        }
    }

    private void PromptForRequiredSettings()
    {
        // Create a simple input dialog
        var dialog = new Window
        {
            Title = "Intune API Settings Required",
            Width = 450,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Margin = new Thickness(20);

        var titleText = new TextBlock
        {
            Text = "Please enter the required Intune API credentials:",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        Grid.SetRow(titleText, 0);
        Grid.SetColumn(titleText, 0);
        Grid.SetColumnSpan(titleText, 2);  // Correctly set column span

        var clientIdLabel = new TextBlock
        {
            Text = "Client ID:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetRow(clientIdLabel, 1);
        Grid.SetColumn(clientIdLabel, 0);

        var clientIdTextBox = new TextBox
        {
            Margin = new Thickness(0, 5, 0, 5),
            Padding = new Thickness(5),
            Text = _currentSettings.IntuneClientId
        };
        Grid.SetRow(clientIdTextBox, 1);
        Grid.SetColumn(clientIdTextBox, 1);

        var tenantIdLabel = new TextBlock
        {
            Text = "Tenant ID:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetRow(tenantIdLabel, 2);
        Grid.SetColumn(tenantIdLabel, 0);

        var tenantIdTextBox = new TextBox
        {
            Margin = new Thickness(0, 5, 0, 5),
            Padding = new Thickness(5),
            Text = _currentSettings.IntuneTenantId
        };
        Grid.SetRow(tenantIdTextBox, 2);
        Grid.SetColumn(tenantIdTextBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(buttonPanel, 3);
        Grid.SetColumn(buttonPanel, 0);
        Grid.SetColumnSpan(buttonPanel, 2);  // Correctly set column span

        var saveButton = new Button
        {
            Content = "Save",
            Padding = new Thickness(20, 5, 20, 5),
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true
        };

        saveButton.Click += (s, e) =>
        {
            _currentSettings.IntuneClientId = clientIdTextBox.Text;
            _currentSettings.IntuneTenantId = tenantIdTextBox.Text;
            dialog.DialogResult = true;
        };

        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(titleText);
        grid.Children.Add(clientIdLabel);
        grid.Children.Add(clientIdTextBox);
        grid.Children.Add(tenantIdLabel);
        grid.Children.Add(tenantIdTextBox);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;

        // Show the dialog
        bool? result = dialog.ShowDialog();

        if (result != true)
        {
            // User canceled, exit the application
            MessageBox.Show("API credentials are required to use this application.",
                "Required Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }
    private void SaveSettingsSync(ApplicationSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsFilePath, json);
            System.Diagnostics.Debug.WriteLine("Settings saved successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    public async Task SaveSettings(ApplicationSettings settings)
    {
        try
        {
            _currentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            System.Diagnostics.Debug.WriteLine("Settings saved successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }
}