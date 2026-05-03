using System;
using System.Linq;
using System.Management;
using Microsoft.Extensions.Logging;

namespace PotopopiCamSync.Services
{
    public class HardwareDetectionService
    {
        private readonly ILogger<HardwareDetectionService> _logger;

        public HardwareDetectionService(ILogger<HardwareDetectionService> logger)
        {
            _logger = logger;
        }

        public HardwareCapabilities GetCapabilities()
        {
            var caps = new HardwareCapabilities();
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "Unknown";
                    var vramBytes = Convert.ToInt64(obj["AdapterRAM"]);
                    var vramMb = vramBytes / (1024 * 1024);

                    _logger.LogInformation("Detected GPU: {Name}, VRAM: {Vram} MB", name, vramMb);

                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        caps.HasNvidiaGpu = true;
                        caps.NvidiaVramMb = Math.Max(caps.NvidiaVramMb, (int)vramMb);
                    }
                    else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        caps.HasAmdGpu = true;
                    }
                    
                    caps.TotalVramMb += (int)vramMb;
                    caps.GpuNames.Add(name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect hardware capabilities via WMI.");
            }

            _logger.LogInformation("Hardware detection complete. Suggested AI Mode: {Mode}", caps.SuggestedMode);
            return caps;
        }
    }

    public class HardwareCapabilities
    {
        public bool HasNvidiaGpu { get; set; }
        public bool HasAmdGpu { get; set; }
        public int NvidiaVramMb { get; set; }
        public int TotalVramMb { get; set; }
        public System.Collections.Generic.List<string> GpuNames { get; set; } = new();

        public Models.AIAnalysisMode SuggestedMode
        {
            get
            {
                if (HasNvidiaGpu && NvidiaVramMb >= 6000) return Models.AIAnalysisMode.Extreme;
                if (TotalVramMb >= 4000) return Models.AIAnalysisMode.Balanced;
                return Models.AIAnalysisMode.Standard;
            }
        }
    }
}
