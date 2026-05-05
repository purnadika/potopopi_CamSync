namespace PotopopiCamSync.Services
{
    public interface ITokenStorageService
    {
        void StoreToken(string key, string token);
        string? GetToken(string key);
        void RemoveToken(string key);
    }
}
