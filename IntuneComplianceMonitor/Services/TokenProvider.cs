using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.Services
{
    // Custom token provider implementation for Microsoft Graph
    public class TokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _msalClient;
        private readonly string[] _scopes;
        private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
        private string _cachedToken = null;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public TokenProvider(IPublicClientApplication msalClient, string[] scopes)
        {
            _msalClient = msalClient;
            _scopes = scopes;
            AllowedHostsValidator = new AllowedHostsValidator();
        }

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            // Synchronize token acquisition to prevent multiple simultaneous token requests
            await _tokenSemaphore.WaitAsync(cancellationToken);

            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting GetAuthorizationTokenAsync: {DateTime.Now}");

                // Check if we have a valid cached token (with some buffer time)
                if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiration > DateTime.Now.AddMinutes(5))
                {
                    System.Diagnostics.Debug.WriteLine("Using cached token");
                    return _cachedToken;
                }

                var accounts = await _msalClient.GetAccountsAsync();
                System.Diagnostics.Debug.WriteLine($"Found {accounts.Count()} accounts");

                AuthenticationResult result = null;
                int retryCount = 0;
                const int maxRetries = 3;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        // Try to acquire token silently first
                        if (accounts.Any())
                        {
                            System.Diagnostics.Debug.WriteLine("Attempting to acquire token silently");
                            result = await _msalClient
                                .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                                .ExecuteAsync(cancellationToken);
                            System.Diagnostics.Debug.WriteLine("Successfully acquired token silently");
                        }
                        else
                        {
                            // If no accounts, we need to authenticate interactively
                            throw new MsalUiRequiredException("no_accounts", "No accounts found");
                        }

                        break; // Success, exit the retry loop
                    }
                    catch (MsalUiRequiredException)
                    {
                        System.Diagnostics.Debug.WriteLine("Silent token acquisition failed, trying interactive");
                        // If silent acquisition fails, acquire token interactively
                        result = await _msalClient
                            .AcquireTokenInteractive(_scopes)
                            .ExecuteAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine("Successfully acquired token interactively");
                        break; // Success, exit the retry loop
                    }
                    catch (Exception ex) when (
                        ex is MsalServiceException ||
                        ex is MsalClientException ||
                        ex is TaskCanceledException)
                    {
                        retryCount++;
                        System.Diagnostics.Debug.WriteLine($"Token acquisition attempt {retryCount} failed: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine("Max retries reached, throwing exception");
                            throw;
                        }

                        // Exponential backoff
                        int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }

                if (result != null)
                {
                    // Cache the token and set expiration
                    _cachedToken = result.AccessToken;
                    _tokenExpiration = result.ExpiresOn.DateTime;
                    System.Diagnostics.Debug.WriteLine($"Token expires on: {_tokenExpiration}");

                    System.Diagnostics.Debug.WriteLine($"Completed GetAuthorizationTokenAsync: {DateTime.Now}");
                    return _cachedToken;
                }
                else
                {
                    throw new InvalidOperationException("Failed to acquire token after multiple attempts");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAuthorizationTokenAsync: {ex.Message}");
                throw;
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }
    }
}
