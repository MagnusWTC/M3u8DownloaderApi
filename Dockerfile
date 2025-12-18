FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# 安装 FFmpeg
RUN apt-get update && apt-get install -y ffmpeg

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["M3u8DownloaderApi.csproj", "./"]
RUN dotnet restore "M3u8DownloaderApi.csproj"
COPY . .
RUN dotnet build "M3u8DownloaderApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "M3u8DownloaderApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 创建必要的目录
RUN mkdir -p /app/downloads /app/tools

# 下载并解压 N_m3u8DL-RE
RUN apt-get update && apt-get install -y wget && \
    wget https://github.com/nilaoda/N_m3u8DL-RE/releases/download/v0.3.0-beta/N_m3u8DL-RE_v0.3.0-beta_linux-x64_20241203.tar.gz -O /app/tools/n_m3u8dl.tar.gz && \
    cd /app/tools && \
    tar xzf n_m3u8dl.tar.gz && \
    rm n_m3u8dl.tar.gz && \
    chmod +x N_m3u8DL-RE

RUN apt-get update && apt-get install -y ca-certificates

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:80
ENV FFmpegPath=/usr/bin/ffmpeg
ENV DownloadPath=/app/downloads
ENV M3u8DLPath=/app/tools/N_m3u8DL-RE

ENTRYPOINT ["dotnet", "M3u8DownloaderApi.dll"]