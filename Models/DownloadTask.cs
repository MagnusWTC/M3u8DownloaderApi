namespace M3u8DownloaderApi.Models;

public class DownloadTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime? StartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
}

public enum DownloadStatus
{
    Pending,
    Downloading,
    Converting,
    Completed,
    Failed
} 