@echo off
setlocal
chcp 65001 >nul
set "ROOT=%~dp0"
set "APP=%~1"
if "%APP%"=="" (
  echo 用法: _launch-editor.cmd ^<AppName^>
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Get-EditorLifecycle.ps1" -EditorName "%APP%"
if errorlevel 1 (
  echo [ContentAuthoring] Editor manifest 校验失败，未启动 %APP%。
  pause
  exit /b 1
)
set "EXE=%ROOT%Apps\%APP%.exe"
if not exist "%EXE%" (
  echo [ContentAuthoring] 找不到 %APP%.exe
  echo 请先双击「编译-所有编辑器.cmd」完成全部编辑器编译。
  pause
  exit /b 1
)
start "" "%EXE%"
