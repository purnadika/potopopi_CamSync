using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class AnalysisResultModel
    {
        public double BlurScore { get; set; }
        public ulong ImageHash { get; set; }
        public bool IsPotentiallyBlurry { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
