@echo off
echo Building Network Monitor GUI...

cd /d "C:\NetworkMonitor\NetworkMonitorGUI"

dotnet restore
dotnet build -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo.
    echo Running application...
    echo.
    start "Network Monitor" "bin\Release\net48\NetworkMonitorGUI.exe"
) else (
    echo.
    echo Build failed!
    pause
)