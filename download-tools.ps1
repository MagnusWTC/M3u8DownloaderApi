# 创建tools目录
$toolsDir = Join-Path $PSScriptRoot "tools"
New-Item -ItemType Directory -Force -Path $toolsDir

# 下载最新版本的N_m3u8DL-RE
$releaseUrl = "https://github.com/nilaoda/N_m3u8DL-RE/releases/latest/download/N_m3u8DL-RE_Beta_windows_x64.zip"
$zipPath = Join-Path $toolsDir "N_m3u8DL-RE.zip"
$extractPath = Join-Path $toolsDir "N_m3u8DL-RE"

Write-Host "Downloading N_m3u8DL-RE..."
Invoke-WebRequest -Uri $releaseUrl -OutFile $zipPath

Write-Host "Extracting N_m3u8DL-RE..."
Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

# 清理zip文件
Remove-Item $zipPath

Write-Host "N_m3u8DL-RE has been downloaded and extracted to: $extractPath" 