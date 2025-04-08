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
    private const string TOKEN_CACHE_FILE = "token_cache.json";

    // Extend token cache duration (e.g., 14 days)
    private const int TOKEN_CACHE_DAYS = 14;

    public TokenProvider(IPublicClientApplication msalClient, string[] scopes)
    {
        _msalClient = msalClient;
        _scopes = scopes;
        AllowedHostsValidator = new AllowedHostsValidator();

        // Load persisted token on initialization
        LoadPersistedToken();
    }

    private void LoadPersistedToken()
    {
        try
        {
            string cacheFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                TOKEN_CACHE_FILE);

            if (File.Exists(cacheFilePath))
            {
                var tokenData = JsonSerializer.Deserialize<TokenCacheData>(
                    File.ReadAllText(cacheFilePath));

                // Check if the cached token is still valid and not too old
                if (tokenData != null &&
                    tokenData.Expiration > DateTime.UtcNow &&
                    DateTime.UtcNow < tokenData.CacheCreated.AddDays(TOKEN_CACHE_DAYS))
                {
                    _cachedToken = tokenData.Token;
                    _tokenExpiration = tokenData.Expiration;
                    System.Diagnostics.Debug.WriteLine($"Loaded persisted token, valid until {_tokenExpiration}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading persisted token: {ex.Message}");
        }
    }

    private void PersistToken(string token, DateTime expiration)
    {
        try
        {
            string cacheFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                TOKEN_CACHE_FILE);

            var tokenData = new TokenCacheData
            {
                Token = token,
                Expiration = expiration,
                CacheCreated = DateTime.UtcNow
            };

            File.WriteAllText(cacheFilePath,
                JsonSerializer.Serialize(tokenData,
                    new JsonSerializerOptions { WriteIndented = true }));

            System.Diagnostics.Debug.WriteLine($"Persisted token, expires at {expiration}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error persisting token: {ex.Message}");
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
            // Check for valid cached token
            if (!string.IsNullOrEmpty(_cachedToken) &&
                _tokenExpiration > DateTime.Now.AddMinutes(5) &&
                DateTime.UtcNow < _tokenExpiration.AddDays(TOKEN_CACHE_DAYS))
            {
                System.Diagnostics.Debug.WriteLine($"Using cached token, valid until {_tokenExpiration}");
                return _cachedToken;
            }

            var accounts = await _msalClient.GetAccountsAsync();

            AuthenticationResult result = null;
            try
            {
                // Try silent authentication first
                if (accounts.Any())
                {
                    result = await _msalClient
                        .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                        .ExecuteAsync(cancellationToken);
                }
                else
                {
                    throw new MsalUiRequiredException("no_accounts", "No accounts found");
                }
            }
            catch (MsalUiRequiredException)
            {
                // Fallback to interactive authentication
                result = await _msalClient
                    .AcquireTokenInteractive(_scopes)
                    .WithUseEmbeddedWebView(true)
                    .WithParentActivityOrWindow(new WindowInteropHelper(Application.Current.MainWindow).Handle)
                    .ExecuteAsync(cancellationToken);
            }

            // Cache and persist the token
            _cachedToken = result.AccessToken;
            _tokenExpiration = result.ExpiresOn.DateTime;
            PersistToken(_cachedToken, _tokenExpiration);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Token acquisition error: {ex.Message}");
            throw;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    public AllowedHostsValidator AllowedHostsValidator { get; }
}

// Updated to include cache creation time
public class TokenCacheData
{
    public string Token { get; set; }
    public DateTime Expiration { get; set; }
    public DateTime CacheCreated { get; set; }
}