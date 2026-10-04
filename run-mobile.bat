@echo off
title FaceStone Mobile Application Runner
cd /d "%~dp0"

echo =========================================================================
echo             FACESTONE MOBILE APPLICATION (PWA STANDALONE)
echo =========================================================================
echo.

set "LOCAL_IP=192.168.1.7"
for /f "tokens=4" %%a in ('route print 0.0.0.0 ^| findstr /v "Persistent" ^| findstr "0.0.0.0"') do (
    set "LOCAL_IP=%%a"
)

:: Check if server is already running on port 5124
netstat -ano | findstr ":5124" | findstr "LISTENING" >nul 2>nul
if %errorlevel% neq 0 (
    echo [*] FaceStone backend is not running. Starting server...
    where dotnet >nul 2>nul
    if %errorlevel% equ 0 (
        set "DOTNET_CMD=dotnet"
    ) else if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
        set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET SDK was not found!
        pause
        exit /b 1
    )
    start "FaceStone Backend Server" /min "%DOTNET_CMD%" run --project FaceStone.Web.csproj --urls "http://0.0.0.0:5124"
    timeout /t 5 /nobreak >nul
) else (
    echo [OK] FaceStone backend is already active on http://0.0.0.0:5124
)

echo.
echo =========================================================================
echo  [1] Desktop Mobile Preview: Standalone mobile window opening...
echo  [2] Real Mobile Phone (LAN): Open in Chrome on your phone:
echo      --^> http://%LOCAL_IP%:5124
echo.
echo  Demo Credentials:
echo    - CEO:               kareem_ceo        / Password123!
echo    - Transportation:    transport_manager / Password123!
echo    - Storage Warehouse: storage_manager   / Storage123!
echo    - Finance:           sarah_concrete    / Password123!
echo =========================================================================
echo.

:: Detect browser and launch in standalone app mode with mobile dimensions
if exist "C:\Program Files\Google\Chrome\Application\chrome.exe" (
    start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --app=http://localhost:5124/ --window-size=412,892 --window-position=120,60
) else if exist "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" (
    start "" "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" --app=http://localhost:5124/ --window-size=412,892 --window-position=120,60
) else if exist "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" (
    start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --app=http://localhost:5124/ --window-size=412,892 --window-position=120,60
) else (
    start "" "http://localhost:5124/"
)

echo [OK] FaceStone Mobile Application is running!
