@echo off
echo ============================================
echo  Ruler Overlay - Build Script
echo ============================================
echo.

:: Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found.
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/3] Restoring NuGet packages...
dotnet restore RulerOverlay\RulerOverlay.csproj
if errorlevel 1 (
    echo ERROR: Package restore failed.
    pause
    exit /b 1
)

echo.
echo [2/3] Publishing self-contained single-file EXE...
dotnet publish RulerOverlay\RulerOverlay.csproj -c Release -o publish
if errorlevel 1 (
    echo ERROR: Build failed.
    pause
    exit /b 1
)

echo.
echo [3/3] Done!
echo.
echo ============================================
echo  Output: publish\RulerOverlay.exe
echo ============================================
echo.
echo The EXE is self-contained and portable.
echo No .NET runtime installation needed to run it.
echo.
pause
