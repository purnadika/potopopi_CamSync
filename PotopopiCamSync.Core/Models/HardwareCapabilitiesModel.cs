using System.Collections.Generic;

namespace PotopopiCamSync.Models
{
    public class HardwareCapabilitiesModel
    {
        public bool HasNvidiaGpu { get; set; }
        public bool HasAmdGpu { get; set; }
        public int NvidiaVramMb { get; set; }
        public int TotalVramMb { get; set; }
        public List<string> GpuNames { get; set; } = new();

        public AIAnalysisMode SuggestedMode
        {
            get
            {
                if (HasNvidiaGpu && NvidiaVramMb >= 6000) return AIAnalysisMode.Extreme;
                if (TotalVramMb >= 4000) return AIAnalysisMode.Balanced;
                return AIAnalysisMode.Standard;
            }
        }
    }
}
