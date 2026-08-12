@echo off
set EXE=%~dp0Apps\PackageBrowser\PackageBrowser.exe
if not exist "%EXE%" (
  echo 找不到 %EXE%
  echo 请先在本目录运行: powershell -File publish.ps1
  pause
  exit /b 1
)
start "" "%EXE%"
