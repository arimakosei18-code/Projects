@echo off
echo Building Network Monitor System...
echo ==================================
echo.

set ROOT_DIR=%~dp0..

echo 1. Building Shared library...
cd /d "%ROOT_DIR%\Shared"
dotnet restore --force
dotnet build --configuration Release --force

echo.
echo 2. Building Windows Service...
cd /d "%ROOT_DIR%\NetworkMonitorService"
dotnet restore --force
dotnet build --configuration Release --force

echo.
echo 3. Building GUI Application...
cd /d "%ROOT_DIR%\NetworkMonitorGUI"
dotnet restore --force
dotnet build --configuration Release --force

echo.
if %errorLevel% equ 0 (
    echo ==================================
    echo BUILD COMPLETE!
    echo ==================================
    echo.
    echo Outputs:
    echo   Service: %ROOT_DIR%\NetworkMonitorService\bin\Release\net48\
    echo   GUI: %ROOT_DIR%\NetworkMonitorGUI\bin\Release\net48\
) else (
    echo ==================================
    echo BUILD FAILED!
    echo ==================================
)

echo.
pause