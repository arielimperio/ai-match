@echo off
setlocal EnableDelayedExpansion

echo ==========================================
echo Nacka Traff Matchmaking - Setup Script
echo ==========================================

echo.
echo 1. Checking Prerequisites...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK is not installed. Please install .NET 8 SDK.
    pause
    exit /b 1
) else (
    echo [OK] .NET SDK found.
)

node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Node.js is not installed. Please install Node.js LTS.
    pause
    exit /b 1
) else (
    echo [OK] Node.js found.
)

echo.
echo 2. Setting up Backend...
cd api\NackaMatchmaking.API
if %errorlevel% neq 0 (
    echo [ERROR] Could not find API directory.
    pause
    exit /b 1
)

echo Restoring .NET dependencies...
call dotnet restore
if %errorlevel% neq 0 (
    echo [ERROR] Failed to restore .NET dependencies.
    pause
    exit /b 1
)

echo.
echo [IMPORTANT] Please ensure your SQL Server connection string in:
echo '%CD%\appsettings.json'
echo is correct for your local SQL Server instance.
echo.
echo Press any key to confirm you have checked appsettings.json...
pause >nul

echo Updating Database...
call dotnet ef database update
if %errorlevel% neq 0 (
    echo [WARNING] Failed to update database. Trying to install dotnet-ef tool...
    call dotnet tool install --global dotnet-ef
    call dotnet ef database update
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to update database even after installing dotnet-ef.
        echo Please check your connection string and SQL Server status.
        pause
    )
)

cd ..\..

echo.
echo 3. Setting up Frontend...
echo Installing npm dependencies...
call npm install
if %errorlevel% neq 0 (
    echo [ERROR] Failed to install npm dependencies.
    pause
    exit /b 1
)

echo.
echo ==========================================
echo Setup Complete!
echo ==========================================
echo.
echo To run the project:
echo 1. Backend: Open a terminal in 'api\NackaMatchmaking.API' and run 'dotnet run'
echo 2. Frontend: Open a terminal in root and run 'npm start'
echo.
pause
