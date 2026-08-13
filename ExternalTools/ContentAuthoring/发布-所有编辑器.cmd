@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo [ContentAuthoring] 正在打包所有编辑器，首次约需 1 分钟…
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
if errorlevel 1 (
  echo.
  echo 打包失败。请确认已安装 .NET 8 SDK：https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
echo.
echo 完成。可双击「启动-*.cmd」或直接运行 Apps\ 下的 exe。
pause
