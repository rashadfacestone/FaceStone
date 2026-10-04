@echo off
title FaceStone - Advanced RESTful Web API (Silica Fume, Nano Silica, Metakaolin, Stone)
cd /d "%~dp0"

echo =========================================================================
echo   FACESTONE - ADVANCED MINERALS & STONE FACTORY (RESTful Web API)
echo   Silica Fume * Nano Silica * Calcined Metakaolin * Architectural Stone
echo   Interactive Swagger Docs: http://localhost:5124/swagger
echo =========================================================================
echo Starting Web API on http://localhost:5124 ...

where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    set "DOTNET_CMD=dotnet"
) else if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
) else (
    echo [ERROR] .NET SDK was not found!
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

start /b "" cmd /c "timeout /t 3 /nobreak >nul & start https://localhost:52563/swagger"
"%DOTNET_CMD%" run --project FaceStone.Web.csproj --urls "http://0.0.0.0:5124;https://localhost:52563;http://localhost:5124"
pause
