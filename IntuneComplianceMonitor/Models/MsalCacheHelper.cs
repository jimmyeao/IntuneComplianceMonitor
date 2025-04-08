using Microsoft.Identity.Client;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IntuneComplianceMonitor.Models
{
    public static class MsalCacheHelper
    {
        private static readonly string CacheFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "msal_token_cache.bin");

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }

        private static readonly object FileLock = new object();

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
    }
}
