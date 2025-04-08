using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

public class TokenProvider : IAccessTokenProvider
{
    private readonly IPublicClientApplication _msalClient;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
    private string _cachedToken = null;
    private DateTime _tokenExpiration = DateTime.MinValue;
    private const string TokenCacheFileName = "token_cache.json";

    public TokenProvider(IPublicClientApplication msalClient, string[] scopes)
    {
        _msalClient = msalClient;
        _scopes = scopes;
        AllowedHostsValidator = new AllowedHostsValidator();

        // Initialize token cache on creation
        LoadTokenCache();
    }

    private void LoadTokenCache()
    {
        try
        {
            string cacheFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                TokenCacheFileName);

            if (File.Exists(cacheFilePath))
            {
                var cacheData = JsonSerializer.Deserialize<TokenCacheData>(
                    File.ReadAllText(cacheFilePath));

                if (cacheData != null && cacheData.Expiration > DateTime.UtcNow)
                {
                    _cachedToken = cacheData.Token;
                    _tokenExpiration = cacheData.Expiration;
                    System.Diagnostics.Debug.WriteLine("Loaded token from persistent cache");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading token cache: {ex.Message}");
        }
    }

    private void SaveTokenCache(string token, DateTime expiration)
    {
        try
        {
            string cacheFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                TokenCacheFileName);

            var cacheData = new TokenCacheData
            {
                Token = token,
                Expiration = expiration
            };

            File.WriteAllText(cacheFilePath,
                JsonSerializer.Serialize(cacheData,
                    new JsonSerializerOptions { WriteIndented = true }));

            System.Diagnostics.Debug.WriteLine("Saved token to persistent cache");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving token cache: {ex.Message}");
        }
    }

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object> additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
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

                    result = await _msalClient
                        .AcquireTokenInteractive(_scopes)
                        .WithUseEmbeddedWebView(true)
                        .WithParentActivityOrWindow(new WindowInteropHelper(Application.Current.MainWindow).Handle)
                        .ExecuteAsync(cancellationToken);
                    System.Diagnostics.Debug.WriteLine("Successfully acquired token interactively");
                    break;
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

                // Save to persistent cache
                SaveTokenCache(_cachedToken, _tokenExpiration);

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

// Separate class for serialization
public class TokenCacheData
{
    public string Token { get; set; }
    public DateTime Expiration { get; set; }
}