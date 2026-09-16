@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "LOG=%ROOT%build-all.log"
cd /d "%ROOT%"
if errorlevel 1 goto :root_error

> "%LOG%" echo Build All log
call :log Started: %DATE% %TIME%

where powershell.exe >nul 2>nul
if errorlevel 1 goto :powershell_error
call :log Preflight: powershell.exe found.

where dotnet >nul 2>nul
if errorlevel 1 goto :dotnet_error
call :log Preflight: dotnet found.

call :log Starting publish.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%publish.ps1" -LogPath "%LOG%"
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" goto :publish_error

call :log Build All completed successfully. Apps: %ROOT%Apps
echo.
echo Build All completed successfully.
echo Apps: %ROOT%Apps
pause
exit /b 0

:root_error
echo ERROR: Cannot enter the ContentAuthoring directory.
pause
exit /b 1

:powershell_error
call :log ERROR: powershell.exe was not found.
echo ERROR: powershell.exe was not found.
pause
exit /b 1

:dotnet_error
call :log ERROR: dotnet was not found. Install the .NET 8 SDK and retry.
echo ERROR: dotnet was not found. Install the .NET 8 SDK and retry.
pause
exit /b 1

:publish_error
call :log ERROR: publish.ps1 failed with exit code %RESULT%.
echo.
echo ERROR: Build All failed with exit code %RESULT%.
echo Read the diagnostic log: %LOG%
pause
exit /b %RESULT%

:log
echo %*
>> "%LOG%" echo %*
exit /b 0
