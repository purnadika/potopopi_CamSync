using System.Threading;
using System.Threading.Tasks;
using PotopopiCamSync.Models;

namespace PotopopiCamSync.Services
{
    public interface IMediaAnalyzer
    {
        double GetBlurScore(string filePath);
        ulong GetImageHash(string filePath);
        Task<AnalysisResultModel> AnalyzeAsync(string filePath, CancellationToken ct = default);
    }
}
