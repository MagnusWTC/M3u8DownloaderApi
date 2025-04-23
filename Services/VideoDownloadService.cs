using System.Diagnostics;
using System.Runtime.InteropServices;
using M3u8DownloaderApi.Models;

namespace M3u8DownloaderApi.Services;

public class VideoDownloadService
{
    private readonly ILogger<VideoDownloadService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _downloadPath;
    private readonly Dictionary<string, DownloadTask> _tasks = new();
    private readonly string _m3u8dlPath;
    private readonly string _ffmpegPath;
    private readonly bool _isDocker;

    

    public VideoDownloadService(ILogger<VideoDownloadService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        
        if (_isDocker)
        {
            _downloadPath = configuration["Docker:DownloadPath"] ?? "/app/downloads";
            _m3u8dlPath = configuration["Docker:M3u8DLPath"] ?? "/app/tools/N_m3u8DL-RE";
            _ffmpegPath = configuration["Docker:FFmpegPath"] ?? "/usr/bin/ffmpeg";
        }
        else
        {
            _downloadPath = configuration["DownloadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "downloads");
            _m3u8dlPath = configuration["M3u8DLPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "tools", "N_m3u8DL-RE", "N_m3u8DL-RE.exe");
            _ffmpegPath = configuration["FFmpegPath"] ?? "ffmpeg";
        }

       
        Directory.CreateDirectory(_downloadPath);
      

        if (!File.Exists(_m3u8dlPath))
        {
            throw new Exception($"N_m3u8DL-RE not found at path: {_m3u8dlPath}");
        }

        _logger.LogInformation("Environment: {Env}", _isDocker ? "Docker" : "Local");
        _logger.LogInformation("Download Path: {Path}", _downloadPath);
        _logger.LogInformation("N_m3u8DL-RE Path: {Path}", _m3u8dlPath);
        _logger.LogInformation("FFmpeg Path: {Path}", _ffmpegPath);
    }

    public DownloadTask CreateDownloadTask(string url, string outputFileName)
    {
        if (!outputFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            outputFileName += ".mp4";
        }

        var task = new DownloadTask
        {
            Url = url,
            OutputFileName = outputFileName,
            OutputPath = Path.Combine(_downloadPath, outputFileName)
        };
        _tasks[task.Id] = task;
        return task;
    }

    private async Task ConvertToMp4Async(string inputPath, string outputPath, DownloadTask task)
    {
        _logger.LogInformation("Starting FFmpeg conversion from {Input} to {Output}", inputPath, outputPath);
        task.Status = "Converting";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            // 添加转码参数，这里使用 H.264 编码和 AAC 音频
            Arguments = $"-i \"{inputPath}\" -c copy -movflags +faststart \"{outputPath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var errorBuilder = new System.Text.StringBuilder();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogInformation("FFmpeg: {Output}", e.Data);

                // 从 FFmpeg 输出中解析进度
                if (e.Data.Contains("time="))
                {
                    try
                    {
                        var timeIndex = e.Data.IndexOf("time=");
                        var timeStr = e.Data.Substring(timeIndex + 5, 11).Trim();
                        var time = TimeSpan.Parse(timeStr);
                        
                        // 假设视频总长度为2小时，计算进度
                        var progress = (int)(time.TotalSeconds / (2 * 3600) * 100);
                        task.Progress = Math.Min(progress, 99); // 保持在99%以下，直到完成
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse FFmpeg progress");
                    }
                }
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("FFmpeg Output: {Output}", e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        // 设置转码超时时间为1小时
        using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch { }
            throw new TimeoutException("FFmpeg conversion timed out after 1 hour");
        }

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("FFmpeg conversion failed: {Error}", error);
            throw new Exception($"FFmpeg conversion failed: {error}");
        }

        _logger.LogInformation("FFmpeg conversion completed successfully");
    }

    public async Task StartDownloadAsync(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new ArgumentException("Task not found", nameof(taskId));
        }

       
        try
        {
            task.Status = "Downloading";
            var outputDir = Path.Combine(_downloadPath, task.Id);
            Directory.CreateDirectory(outputDir);
            var tempm3u8Path = Path.Combine(outputDir,$"{Guid.NewGuid()}.m3u8");

            if (!task.Url.EndsWith(".m3u8",StringComparison.OrdinalIgnoreCase))
            {
                using var steam = await _httpClientFactory.CreateClient().GetStreamAsync(task.Url);
              
                 // 创建文件流
                using var streamToWriteTo = System.IO.File.Open(tempm3u8Path, FileMode.Create);
                await steam.CopyToAsync(streamToWriteTo);
                task.Url = tempm3u8Path;
            }


            // 构建 N_m3u8DL-RE 命令行参数
            var startInfo = new ProcessStartInfo
            {
                FileName = _m3u8dlPath,
                Arguments = $"\"{task.Url}\" --save-dir \"{outputDir}\" --save-name \"raw_video\" --binary-merge --auto-select  --ffmpeg-binary-path \"{_ffmpegPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_m3u8dlPath) ?? Directory.GetCurrentDirectory()
            };

            // 记录完整命令行和 FFmpeg 路径
            _logger.LogInformation("FFmpeg path: {FFmpegPath}", _ffmpegPath);
            _logger.LogInformation("Executing command: {Command} {Args}", startInfo.FileName, startInfo.Arguments);
            _logger.LogInformation("Working directory: {WorkingDir}", startInfo.WorkingDirectory);

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogInformation("N_m3u8DL-RE Output: {Output}", e.Data);

                    // 解析进度信息
                    if (e.Data.Contains("%"))
                    {
                        try
                        {
                            var percentStr = e.Data.Split('%')[0].Trim();
                            var lastSpace = percentStr.LastIndexOf(' ');
                            if (lastSpace >= 0)
                            {
                                percentStr = percentStr.Substring(lastSpace).Trim();
                            }
                            if (double.TryParse(percentStr, out var percent))
                            {
                                task.Progress = (int)(percent * 0.5); // 下载进度占总进度的50%
                                _logger.LogInformation("Download progress: {Progress}%", task.Progress);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse progress from output: {Output}", e.Data);
                        }
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogError("N_m3u8DL-RE Error: {Error}", e.Data);
                }
            };

            try
            {
                _logger.LogInformation("Starting N_m3u8DL-RE process...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 设置超时时间为3hr
                using var cts = new CancellationTokenSource(TimeSpan.FromHours(3));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    _logger.LogInformation("Process exit code: {ExitCode}", process.ExitCode);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("Process execution timed out");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            _logger.LogWarning("Process was killed due to timeout");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while killing process");
                    }
                    throw new TimeoutException("Download timed out after 3 hr");
                }

                if (process.ExitCode != 0)
                {
                    var errorMessage = errorBuilder.ToString();
                    _logger.LogError("N_m3u8DL-RE failed with exit code {ExitCode}. Error: {Error}", 
                        process.ExitCode, errorMessage);
                    throw new Exception($"Download failed with exit code {process.ExitCode}: {errorMessage}");
                }

                // 检查输出目录
                _logger.LogInformation("Checking output directory: {Dir}", outputDir);
                var files = Directory.GetFiles(outputDir);
                foreach (var file in files)
                {
                    _logger.LogInformation("Found file: {File}", file);
                }

                // 查找下载的文件
                var downloadedFiles = Directory.GetFiles(outputDir, "raw_video.*");
                _logger.LogInformation("Found {Count} matching files", downloadedFiles.Length);

                if (downloadedFiles.Length == 0)
                {
                    var allFiles = Directory.GetFiles(outputDir);
                    var errorMsg = $"No video file found after download. Files in directory: {string.Join(", ", allFiles)}";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                var downloadedFile = downloadedFiles[0];
                var tempOutputPath = Path.Combine(outputDir, "converted.mp4");

                // 使用 FFmpeg 转码
                await ConvertToMp4Async(downloadedFile, tempOutputPath, task);

                // 移动到最终位置
                File.Move(tempOutputPath, task.OutputPath, true);

                // 清理临时文件
                try
                {
                    Directory.Delete(outputDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp folder: {Path}", outputDir);
                }

                task.Status = "Completed";
                task.Progress = 100;
                _logger.LogInformation("Task completed: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                task.Status = "Failed";
                task.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error processing task {TaskId}", taskId);
                throw;
            }
        }
        catch (Exception ex)
        {
            task.Status = "Failed";
            task.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error processing task {TaskId}", taskId);
            throw;
        }
    }

    public DownloadTask? GetTask(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return task;
    }

    public IEnumerable<DownloadTask> GetAllTasks()
    {
        return _tasks.Values;
    }

    public void ClearTasks()
    {
        _tasks.Clear();
        _logger.LogInformation("All tasks have been cleared");
    }
} 