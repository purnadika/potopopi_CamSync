using System;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace PotopopiCamSync.Services
{
    public class SecureDataStore : IDataStore
    {
        private readonly ITokenStorageService _tokenStorage;
        private readonly string _prefix;

        public SecureDataStore(ITokenStorageService tokenStorage, string prefix)
        {
            _tokenStorage = tokenStorage;
            _prefix = prefix;
        }

        public Task ClearAsync()
        {
            // Implementation if needed
            return Task.CompletedTask;
        }

        public Task DeleteAsync<T>(string key)
        {
            _tokenStorage.RemoveToken($"{_prefix}_{key}");
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            var value = _tokenStorage.GetToken($"{_prefix}_{key}");
            if (string.IsNullOrEmpty(value)) return Task.FromResult(default(T));

            try
            {
                return Task.FromResult(JsonConvert.DeserializeObject<T>(value));
            }
            catch
            {
                return Task.FromResult(default(T));
            }
        }

        public Task StoreAsync<T>(string key, T value)
        {
            var json = JsonConvert.SerializeObject(value);
            _tokenStorage.StoreToken($"{_prefix}_{key}", json);
            return Task.CompletedTask;
        }
    }
}
