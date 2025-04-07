using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

            // Initialize with default settings first
            _currentSettings = new ApplicationSettings();
            System.Diagnostics.Debug.WriteLine("Initialized with default settings");

            // Load settings asynchronously without blocking
            // This will update the current settings later
            LoadSettingsAsync();

            System.Diagnostics.Debug.WriteLine("SettingsService constructor completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SettingsService constructor: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

            // Create default settings instead of throwing
            _currentSettings = new ApplicationSettings();
            System.Diagnostics.Debug.WriteLine("Created default settings due to error");
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
            throw; // Re-throw to let the UI handle the error
        }
    }

    public ApplicationSettings CurrentSettings => _currentSettings;
    private async void LoadSettingsAsync()
    {
        try
        {
            var settings = await LoadSettings();
            // Update current settings with loaded values
            _currentSettings = settings;
            System.Diagnostics.Debug.WriteLine("Settings loaded and applied asynchronously");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LoadSettingsAsync: {ex.Message}");
        }
    }
    private async Task<ApplicationSettings> LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                System.Diagnostics.Debug.WriteLine("Settings file exists, reading content");

                try
                {
                    string json = await File.ReadAllTextAsync(_settingsFilePath);

                    // Check if the file is empty or contains invalid JSON
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        System.Diagnostics.Debug.WriteLine("Settings file is empty, using defaults");
                        return new ApplicationSettings();
                    }

                    System.Diagnostics.Debug.WriteLine("Deserializing settings");
                    var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);

                    if (settings != null)
                    {
                        System.Diagnostics.Debug.WriteLine("Settings deserialized successfully");
                        return settings;
                    }

                    System.Diagnostics.Debug.WriteLine("Settings deserialized to null, using defaults");
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON error in settings file: {jsonEx.Message}");

                    // Create a backup of the corrupted file
                    string backupPath = _settingsFilePath + ".bak";
                    File.Copy(_settingsFilePath, backupPath, true);
                    System.Diagnostics.Debug.WriteLine($"Created backup of corrupted settings at {backupPath}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Settings file does not exist");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        // Return default settings if loading fails or file doesn't exist
        System.Diagnostics.Debug.WriteLine("Returning default settings");
        return new ApplicationSettings();
    }

    // Rest of the service...
}