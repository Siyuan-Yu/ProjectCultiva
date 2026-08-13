@echo off
setlocal
chcp 65001 >nul
set "ROOT=%~dp0"
set "APP=%~1"
if "%APP%"=="" (
  echo 用法: _launch-editor.cmd ^<AppName^>
  exit /b 1
)
set "EXE=%ROOT%Apps\%APP%\%APP%.exe"
if not exist "%EXE%" (
  echo [ContentAuthoring] 找不到 %APP%.exe
  echo 首次使用需要先打包（约 1 分钟），正在自动执行 publish.ps1 …
  echo.
  powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%publish.ps1"
  if errorlevel 1 (
    echo.
    echo 自动打包失败。可手动双击「发布-所有编辑器.cmd」，或安装 .NET 8 SDK 后重试。
    pause
    exit /b 1
  )
  if not exist "%EXE%" (
    echo 打包完成但仍找不到 %EXE%
    pause
    exit /b 1
  )
)
start "" "%EXE%"
