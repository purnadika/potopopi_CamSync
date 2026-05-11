using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class SyncStateModel
    {
        // Key: DeviceId, Value: HashSet of file identifiers (hash or relative path_size)
        public Dictionary<string, HashSet<string>> SyncedFiles { get; set; } = new Dictionary<string, HashSet<string>>();

        // Set of local file paths (or identifiers) that the user has explicitly allowed to sync to Immich
        // even if they were flagged as blurry or duplicate.
        public HashSet<string> AllowedAIRejectedFiles { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        // Persistent logs for UI
        public List<string> PersistentLogs { get; set; } = new List<string>();

        // Persistent flagged files for UI
        public List<FlaggedFileModel> PersistentFlaggedFiles { get; set; } = new List<FlaggedFileModel>();
    }
}
