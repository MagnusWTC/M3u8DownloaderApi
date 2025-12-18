using Microsoft.AspNetCore.Mvc;
using M3u8DownloaderApi.Models;
using M3u8DownloaderApi.Services;

namespace M3u8DownloaderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly VideoDownloadService _downloadService;
    private readonly ILogger<VideoController> _logger;

    public VideoController(VideoDownloadService downloadService, ILogger<VideoController> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }


    [HttpPost("upload")]
    public async Task<ActionResult<DownloadTask>> UploadM3U8File(IFormFile file, [FromForm] string outputFileName)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            // 验证文件类型
            if (!file.FileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only .m3u8 and .m3u files are allowed" });
            }

            // 处理输出文件名
            if (string.IsNullOrEmpty(outputFileName))
            {
                outputFileName = $"video_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4";
            }
            else if (!outputFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                outputFileName += ".mp4";
            }

            // 保存上传的文件到临时目录
            var tempDir = Path.Combine(Path.GetTempPath(), "m3u8_uploads");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid()}.m3u8");
            
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 创建下载任务
            var task = _downloadService.CreateDownloadTask(tempFilePath, outputFileName);
            
            // 异步启动下载任务
                _ = _downloadService.StartDownloadAsync(task.Id)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            var exception = t.Exception?.InnerException ?? t.Exception;
                            _logger.LogError(exception, "Download task {TaskId} failed", task.Id);
                        }
                    }, TaskScheduler.Default);

                return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing uploaded M3U8 file");
            return StatusCode(500, new { error = "Failed to process uploaded file", details = ex.Message });
        }
    }


    [HttpPost("download")]
    public ActionResult<DownloadTask> StartDownload([FromBody] DownloadRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Url))
            {
                return BadRequest(new { error = "URL is required" });
            }

            if (string.IsNullOrEmpty(request.OutputFileName))
            {
                request.OutputFileName = $"video_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4";
            }
            else if (!request.OutputFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                request.OutputFileName += ".mp4";
            }

            var task = _downloadService.CreateDownloadTask(request.Url, request.OutputFileName);
            
            // 异步启动下载任务
            _ = _downloadService.StartDownloadAsync(task.Id)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var exception = t.Exception?.InnerException ?? t.Exception;
                        _logger.LogError(exception, "Download task {TaskId} failed", task.Id);
                    }
                }, TaskScheduler.Default);

            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting download");
            return StatusCode(500, new { error = "Failed to start download", details = ex.Message });
        }
    }

    [HttpGet("{taskId}")]
    public ActionResult<DownloadTask> GetTask(string taskId)
    {
        try
        {
            var task = _downloadService.GetTask(taskId);
            if (task == null)
            {
                return NotFound(new { error = $"Task {taskId} not found" });
            }
            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {TaskId}", taskId);
            return StatusCode(500, new { error = "Failed to get task status", details = ex.Message });
        }
    }

    [HttpGet]
    public ActionResult<IEnumerable<DownloadTask>> GetAllTasks()
    {
        try
        {
            return Ok(_downloadService.GetAllTasks());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tasks");
            return StatusCode(500, new { error = "Failed to get tasks", details = ex.Message });
        }
    }

    [HttpDelete("clear")]
    public ActionResult ClearTasks()
    {
        try
        {
            _downloadService.ClearTasks();
            return Ok(new { message = "所有任务已清除" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tasks");
            return StatusCode(500, new { error = "Failed to clear tasks", details = ex.Message });
        }
    }

    [HttpPost("{taskId}/retry")]
    public async Task<ActionResult<DownloadTask>> RetryDownload(string taskId)
    {
        try
        {
            var task = _downloadService.GetTask(taskId);
            if (task == null)
            {
                return NotFound(new { error = $"Task {taskId} not found" });
            }

            if (task.Status != DownloadStatus.Failed)
            {
                return BadRequest(new { error = "Only failed tasks can be retried" });
            }

            // 重置任务状态并重新开始下载
            task = _downloadService.ResetTask(taskId);
            
            // 异步启动下载任务
            _ = _downloadService.StartDownloadAsync(task.Id)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var exception = t.Exception?.InnerException ?? t.Exception;
                        _logger.LogError(exception, "Retry download task {TaskId} failed", task.Id);
                    }
                }, TaskScheduler.Default);

            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying download for task {TaskId}", taskId);
            return StatusCode(500, new { error = "Failed to retry download", details = ex.Message });
        }
    }
}

public class DownloadRequest
{
    public string Url { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
} 