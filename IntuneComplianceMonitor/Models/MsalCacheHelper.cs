using Microsoft.Identity.Client;
using System.IO;

namespace IntuneComplianceMonitor.Models
{
    public static class MsalCacheHelper
    {
        #region Fields

        private static readonly string CacheFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "msal_token_cache.bin");

        private static readonly object FileLock = new object();

        #endregion Fields

        #region Methods

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }

        private static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (args.HasStateChanged)
            {
                lock (FileLock)
                {
                    byte[] data = args.TokenCache.SerializeMsalV3();
                    File.WriteAllBytes(CacheFilePath, data);
                }
            }
        }

        private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                if (File.Exists(CacheFilePath))
                {
                    byte[] data = File.ReadAllBytes(CacheFilePath);
                    args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true);
                }
            }
        }

        #endregion Methods
    }
}