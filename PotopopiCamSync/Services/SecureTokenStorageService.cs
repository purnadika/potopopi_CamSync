using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using PotopopiCamSync.Utilities;

namespace PotopopiCamSync.Services
{
    public class SecureTokenStorageService : ITokenStorageService
    {
        private readonly string _storagePath;

        public SecureTokenStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storagePath = Path.Combine(appData, AppConstants.General.InternalName, "secure_vault");
            Directory.CreateDirectory(_storagePath);
        }

        public void StoreToken(string key, string token)
        {
            var data = Encoding.UTF8.GetBytes(token);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(GetFilePath(key), encrypted);
        }

        public string? GetToken(string key)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path)) return null;

            try
            {
                var encrypted = File.ReadAllBytes(path);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        public void RemoveToken(string key)
        {
            var path = GetFilePath(key);
            if (File.Exists(path)) File.Delete(path);
        }

        private string GetFilePath(string key)
        {
            // Simple hash of the key to avoid illegal characters in filename
            return Path.Combine(_storagePath, $"{key}.bin");
        }
    }
}
