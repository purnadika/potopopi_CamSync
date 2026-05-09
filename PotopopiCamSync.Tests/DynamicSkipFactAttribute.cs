using System;
using Xunit;

namespace PotopopiCamSync.Tests
{
    /// <summary>
    /// A custom Fact attribute that automatically skips tests when running in GitHub Actions CI,
    /// but allows them to run normally on local development machines.
    /// </summary>
    public class DynamicSkipFactAttribute : FactAttribute
    {
        public DynamicSkipFactAttribute(string? reason = "CI Stall")
        {
            // GitHub Actions sets this environment variable to 'true'
            bool isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            
            if (isCI)
            {
                Skip = $"[Skipped in CI] {reason}";
            }
        }
    }
}
