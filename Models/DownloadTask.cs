namespace M3u8DownloaderApi.Models;

public class DownloadTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Progress { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string OutputPath { get; set; } = string.Empty;
} 