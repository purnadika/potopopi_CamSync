namespace PotopopiCamSync.Models
{
    public class SyncMetrics
    {
        public int TotalFiles { get; set; }
        public int DownloadedFiles { get; set; }
        public int UploadedFiles { get; set; }
        public int FailedFiles { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdateTime { get; set; }

        public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

        public double DownloadSpeed => BytesDownloaded > 0 && ElapsedTime.TotalSeconds > 0
            ? BytesDownloaded / ElapsedTime.TotalSeconds
            : 0;

        public double UploadSpeed => BytesUploaded > 0 && ElapsedTime.TotalSeconds > 0
            ? BytesUploaded / ElapsedTime.TotalSeconds
            : 0;

        public int RemainingFiles => TotalFiles - DownloadedFiles;

        public string FormattedDownloadSpeed => FormatSpeed(DownloadSpeed);
        public string FormattedUploadSpeed => FormatSpeed(UploadSpeed);

        private static string FormatSpeed(double bytesPerSecond)
        {
            return bytesPerSecond switch
            {
                < 1024 => $"{bytesPerSecond:0.0} B/s",
                < 1024 * 1024 => $"{bytesPerSecond / 1024:0.0} KB/s",
                _ => $"{bytesPerSecond / (1024 * 1024):0.0} MB/s"
            };
        }

        public string Summary => $"↓ {DownloadedFiles}/{TotalFiles} | ↑ {UploadedFiles}/{TotalFiles} | " +
                                 $"DL: {FormattedDownloadSpeed} | UL: {FormattedUploadSpeed}";
    }
}
