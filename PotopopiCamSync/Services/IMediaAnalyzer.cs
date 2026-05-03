using System.Threading;
using System.Threading.Tasks;

namespace PotopopiCamSync.Services
{
    public interface IMediaAnalyzer
    {
        double GetBlurScore(string filePath);
        ulong GetImageHash(string filePath);
        Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct = default);
    }
}
