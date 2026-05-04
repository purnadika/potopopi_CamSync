using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace PotopopiCamSync.Models
{
    public partial class FlaggedFileModel : ObservableObject
    {
        public string Path { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(Path);
        public string Reason { get; set; } = string.Empty;
        public bool IsBlurry { get; set; }

        [ObservableProperty]
        private bool _isAllowed;

        [ObservableProperty]
        [JsonIgnore]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isPendingDeletion;
    }
}
