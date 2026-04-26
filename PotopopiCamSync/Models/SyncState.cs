using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class SyncState
    {
        // Key: DeviceId, Value: HashSet of file identifiers (hash or relative path_size)
        public Dictionary<string, HashSet<string>> SyncedFiles { get; set; } = new Dictionary<string, HashSet<string>>();
    }
}
