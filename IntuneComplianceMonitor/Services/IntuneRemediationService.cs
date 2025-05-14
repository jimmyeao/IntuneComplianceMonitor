using System.Text;
using Microsoft.Graph;
using System.Windows;
using System.Text.Json;
using System.Net.Http;
using System.Windows.Controls;

namespace IntuneComplianceMonitor.Services
{
    public class IntuneRemediationService
    {
        private readonly IntuneService _intuneService;

        public IntuneRemediationService(IntuneService intuneService)
        {
            _intuneService = intuneService;
        }

        /// <summary>
        /// Sends a notification to a device
        /// </summary>
        public async Task<bool> SendNotificationAsync(string deviceId, string message, string title = "Compliance Alert")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to send notification to device: {deviceId}");

                var accessToken = await _intuneService.TokenProvider.GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"));
                if (string.IsNullOrEmpty(accessToken))
                    throw new InvalidOperationException("Failed to acquire access token.");
                // Optional safety check
        

                var requestUri = $"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices/{deviceId}/sendCustomNotificationToCompanyPortal";

                var payload = new
                {
                    notificationTitle = title,
                    notificationBody = message
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.PostAsync(requestUri, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Notification sent successfully.");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {response.StatusCode} - {responseText}");
                    MessageBox.Show($"Failed to send notification:\n\n{responseText}", "Notification Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception while sending notification: {ex.Message}");
                MessageBox.Show($"Error sending notification: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Initiates a sync on the device with Intune
        /// </summary>
        public async Task<bool> SyncDeviceAsync(string deviceId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to sync device: {deviceId}");

                // Get access token for authorization
                string accessToken = await _intuneService.TokenProvider.GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"));
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Failed to get access token");
                }

                // Create a HttpClient for direct REST API call
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    // Create the correct URI for the endpoint
                    string requestUri = $"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices/{deviceId}/syncDevice";

                    // For POST with no body content
                    var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

                    // Execute request
                    System.Diagnostics.Debug.WriteLine($"Sending POST request to: {requestUri}");
                    var response = await client.PostAsync(requestUri, content);

                    // Check the response
                    if (response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sync initiated successfully for device: {deviceId}");
                        return true;
                    }
                    else
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Error syncing device: {response.StatusCode}, {responseContent}");
                        throw new HttpRequestException($"Failed to sync device: {response.StatusCode}. {responseContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing device: {ex.Message}");
                MessageBox.Show($"Error syncing device: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Performs a remote reboot of the device
        /// </summary>
        public async Task<bool> RebootDeviceAsync(string deviceId)
        {
            try
            {
                // Confirm with user before rebooting
                var result = MessageBox.Show(
                    "Are you sure you want to reboot this device? The user will receive a notification before the device reboots.",
                    "Confirm Reboot",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return false;

                // Create a request to the correct endpoint
                var requestInfo = _intuneService.GraphClient.DeviceManagement.ManagedDevices[deviceId]
                    .ToGetRequestInformation();

                // Change it to POST
                requestInfo.HttpMethod = Microsoft.Kiota.Abstractions.Method.POST;

                // Update the URL to the correct action
                requestInfo.UrlTemplate = requestInfo.UrlTemplate.Replace("/managedDevices/{managedDevice-id}",
                    "/managedDevices/{managedDevice-id}/rebootNow");

                // Execute the request manually
                await _intuneService.GraphClient.RequestAdapter.SendNoContentAsync(requestInfo,
                    cancellationToken: default);

                System.Diagnostics.Debug.WriteLine($"Reboot initiated for device: {deviceId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rebooting device: {ex.Message}");
                MessageBox.Show($"Error rebooting device: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Wipes the device (factory reset)
        /// </summary>
        public async Task<bool> WipeDeviceAsync(string deviceId)
        {
            try
            {
                // Double confirm with user before wiping
                var result = MessageBox.Show(
                    "WARNING: This will FACTORY RESET the device and remove all data. This action cannot be undone.\n\nAre you absolutely sure you want to proceed?",
                    "CONFIRM DEVICE WIPE",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return false;

                // Ask for one more confirmation with device ID
                result = MessageBox.Show(
                    $"Final confirmation to wipe device ID:\n{deviceId}\n\nType YES in the next prompt to confirm.",
                    "FINAL WIPE CONFIRMATION",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.OK)
                    return false;

                // Custom input dialog for final confirmation
                var dialog = new Window
                {
                    Title = "Type YES to confirm",
                    Width = 300,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow,
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };
                var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 10, 0, 10) };
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, Margin = new Thickness(5) };
                var confirmButton = new System.Windows.Controls.Button { Content = "Confirm", Width = 80, Margin = new Thickness(5) };

                stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Type YES to confirm device wipe:" });
                stackPanel.Children.Add(textBox);
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(confirmButton);
                stackPanel.Children.Add(buttonPanel);
                dialog.Content = stackPanel;

                confirmButton.Click += (s, e) => { dialog.DialogResult = textBox.Text == "YES"; dialog.Close(); };
                cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

                if (dialog.ShowDialog() != true)
                    return false;

                // Create a request to the correct endpoint
                var requestInfo = _intuneService.GraphClient.DeviceManagement.ManagedDevices[deviceId]
                    .ToGetRequestInformation();

                // Change it to POST
                requestInfo.HttpMethod = Microsoft.Kiota.Abstractions.Method.POST;

                // Update the URL to the correct action
                requestInfo.UrlTemplate = requestInfo.UrlTemplate.Replace("/managedDevices/{managedDevice-id}",
                    "/managedDevices/{managedDevice-id}/wipe");

                // Execute the request manually
                await _intuneService.GraphClient.RequestAdapter.SendNoContentAsync(requestInfo,
                    cancellationToken: default);

                System.Diagnostics.Debug.WriteLine($"Wipe initiated for device: {deviceId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error wiping device: {ex.Message}");
                MessageBox.Show($"Error wiping device: {ex.Message}", "Remediation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}