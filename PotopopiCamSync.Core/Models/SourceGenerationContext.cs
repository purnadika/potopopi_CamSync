using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PotopopiCamSync.Models
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AppConfigModel))]
    [JsonSerializable(typeof(SyncStateModel))]
    [JsonSerializable(typeof(SyncFileModel))]
    [JsonSerializable(typeof(FlaggedFileModel))]
    [JsonSerializable(typeof(ImmichAccountModel))]
    [JsonSerializable(typeof(DeviceSignatureModel))]
    [JsonSerializable(typeof(ImmichAssetResponse))]
    [JsonSerializable(typeof(ImmichAlbumResponse))]
    [JsonSerializable(typeof(ImmichAlbumResponse[]))]
    [JsonSerializable(typeof(ImmichAddAssetRequest))]
    [JsonSerializable(typeof(ImmichCreateAlbumRequest))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<FlaggedFileModel>))]
    [JsonSerializable(typeof(List<ImmichAccountModel>))]
    [JsonSerializable(typeof(List<DeviceSignatureModel>))]
    [JsonSerializable(typeof(List<SyncFileModel>))]
    [JsonSerializable(typeof(ImmichUserMeResponse))]
    [JsonSerializable(typeof(Dictionary<string, HashSet<string>>))]
    [JsonSerializable(typeof(HashSet<string>))]
    public partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
