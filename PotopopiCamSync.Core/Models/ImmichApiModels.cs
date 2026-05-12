using System.Text.Json.Serialization;

namespace PotopopiCamSync.Models
{
    public class ImmichAssetResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class ImmichAlbumResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("albumName")]
        public string AlbumName { get; set; } = string.Empty;
    }

    public class ImmichAddAssetRequest
    {
        [JsonPropertyName("ids")]
        public string[] Ids { get; set; } = [];
    }

    public class ImmichCreateAlbumRequest
    {
        [JsonPropertyName("albumName")]
        public string AlbumName { get; set; } = string.Empty;
    }
    
    public class ImmichUserMeResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }
}
