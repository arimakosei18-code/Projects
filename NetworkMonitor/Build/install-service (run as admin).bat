@echo off
echo Installing Network Monitor Service...
echo =====================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires Administrator privileges!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set SERVICE_NAME=NetworkMonitorService
set SERVICE_DISPLAY_NAME="Network Monitor Service"
set SERVICE_DESCRIPTION="Monitors network devices and sends email alerts"
set EXE_PATH="%~dp0..\NetworkMonitorService\bin\Release\net48\NetworkMonitorService.exe"

echo Service: %SERVICE_NAME%
echo Executable: %EXE_PATH%
echo.

if not exist %EXE_PATH% (
    echo ERROR: Service executable not found!
    echo Please build the service first.
    pause
    exit /b 1
)

echo Stopping existing service (if any)...
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 /nobreak >nul

echo Deleting existing service...
sc delete %SERVICE_NAME% >nul 2>&1
timeout /t 2 /nobreak >nul

echo Creating new service...
sc create %SERVICE_NAME% binPath= %EXE_PATH% DisplayName= %SERVICE_DISPLAY_NAME% start= auto

if %errorLevel% neq 0 (
    echo ERROR: Failed to create service!
    pause
    exit /b 1
)

echo Setting service description...
reg add "HKLM\SYSTEM\CurrentControlSet\Services\%SERVICE_NAME%" /v Description /t REG_SZ /d %SERVICE_DESCRIPTION% /f >nul 2>&1

echo Setting recovery options...
sc failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/120000/run/1000

echo.
echo Starting service...
sc start %SERVICE_NAME%

if %errorLevel% equ 0 (
    echo.
    echo =====================================
    echo SERVICE INSTALLED SUCCESSFULLY!
    echo =====================================
    echo.
    echo Service has been installed and started.
    echo.
    echo Configuration file location:
    echo   C:\ProgramData\NetworkMonitor\config.json
    echo.
    echo Log file location:
    echo   C:\NetworkMonitor\Logs\NetworkMonitor.log
) else (
    echo.
    echo WARNING: Service installed but may have errors starting.
    echo Check Event Viewer for details.
)

echo.
pause