using IntuneComplianceMonitor.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

// Updated to include cache creation time
public class TokenCacheData
{
    #region Properties

    public DateTime CacheCreated { get; set; }
    public DateTime Expiration { get; set; }
    public string Token { get; set; }

    #endregion Properties
}

public class TokenProvider : IAccessTokenProvider
{
    #region Fields

    // Extend token cache duration (e.g., 14 days)
    private const int TOKEN_CACHE_DAYS = 14;

    private const string TOKEN_CACHE_FILE = "token_cache.json";
    private readonly IPublicClientApplication _msalClient;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
    private string _cachedToken = null;
    private DateTime _tokenExpiration = DateTime.MinValue;

    #endregion Fields

    #region Constructors

    public TokenProvider(IPublicClientApplication msalClient, string[] scopes)
    {
        _msalClient = msalClient;

        // Hook MSAL cache persistence
        MsalCacheHelper.EnableSerialization(_msalClient.UserTokenCache);

        _scopes = scopes;
        AllowedHostsValidator = new AllowedHostsValidator();

        // Load persisted token on initialization
        LoadPersistedToken();
    }

    #endregion Constructors

    #region Properties

    public AllowedHostsValidator AllowedHostsValidator { get; }
    public string CurrentUserPrincipalName { get; set; }
    public TimeSpan TimeUntilExpiry => _tokenExpiration - DateTime.Now;
    public DateTime TokenExpires => _tokenExpiration;

    #endregion Properties

    #region Methods
    public async Task AuthenticateRequestAsync(RequestInformation request)
    {
        var token = await GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"), null);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
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
            CurrentUserPrincipalName = result.Account?.Username ?? "Unknown";
            System.Diagnostics.Debug.WriteLine($"Signed in as: {CurrentUserPrincipalName}");

            PersistToken(_cachedToken, _tokenExpiration);
            CurrentUserPrincipalName = result.Account?.Username ?? "Unknown";
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

    public async Task LoadAccountInfoAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account != null)
            {
                CurrentUserPrincipalName = account.Username ?? "Unknown";
                System.Diagnostics.Debug.WriteLine($"[MSAL] Loaded account: {CurrentUserPrincipalName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MSAL] No cached account found.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MSAL] Error loading account info: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var acc in accounts)
            {
                await _msalClient.RemoveAsync(acc);
            }

            // Clear cache file
            var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "msal_token_cache.bin");
            if (File.Exists(cachePath))
                File.Delete(cachePath);

            _cachedToken = null;
            _tokenExpiration = DateTime.MinValue;
            CurrentUserPrincipalName = null;

            System.Diagnostics.Debug.WriteLine("User logged out and token cache cleared.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
        }
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

    #endregion Methods
}