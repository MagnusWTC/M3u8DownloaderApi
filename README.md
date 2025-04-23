# M3u8 Downloader API

一个基于 .NET Core 的 Web API 服务，用于下载和处理 M3U8 视频流。该服务提供了简单的 RESTful API 接口，支持异步下载、任务管理和状态查询功能。

## 功能特点

- 支持 M3U8 视频流下载
- RESTful API 接口
- 异步任务处理
- 实时任务状态查询
- Docker 支持
- Swagger API 文档

## 系统要求

- .NET 6.0 或更高版本
- FFmpeg（用于视频处理）

## 快速开始

### 使用 Docker

1. 克隆仓库：
```bash
git clone [repository-url]
```

2. 使用 Docker Compose 构建和运行：
```bash
docker-compose up --build
```

### 手动安装

1. 克隆仓库：
```bash
git clone [repository-url]
```

2. 安装 FFmpeg：
```powershell
.\download-tools.ps1
```

3. 运行应用：
```bash
dotnet run
```

## API 接口

### 开始下载任务

```http
POST /api/video/download
Content-Type: application/json

{
    "url": "your-m3u8-url",
    "outputFileName": "output.mp4"  // 可选，默认自动生成文件名
}
```

### 查询任务状态

```http
GET /api/video/{taskId}
```

### 获取所有任务

```http
GET /api/video
```

### 清除所有任务

```http
DELETE /api/video/clear
```

## 配置说明

配置文件位于 `appsettings.json`，可以根据需要调整相关设置。

## Docker 支持

项目包含 Dockerfile 和 docker-compose.yml，支持容器化部署。构建镜像时会自动安装所需的 FFmpeg 工具。

## 开发说明

- 项目使用 .NET 6.0 开发
- 使用 Swagger 提供 API 文档
- 支持异步任务处理
- 包含日志记录功能

## 许可证

MIT License

## 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 该仓库
2. 创建您的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开一个 Pull Request 