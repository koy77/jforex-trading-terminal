@echo off
setlocal enabledelayedexpansion

echo ========================================
echo JForex Trading Terminal - Release Build
echo ========================================
echo.

:: Check if version parameter is provided
if "%~1"=="" (
    set VERSION=1.1.0.0
    echo Version not specified, using default: !VERSION!
) else (
    set VERSION=%~1
    echo Using version: !VERSION!
)

echo.
echo Step 1: Updating version in project file...
echo ----------------------------------------

:: Update AssemblyVersion and FileVersion in csproj
powershell -Command "(Get-Content ScreenCaptureApp.csproj) -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>!VERSION!</AssemblyVersion>' | Set-Content ScreenCaptureApp.csproj"
powershell -Command "(Get-Content ScreenCaptureApp.csproj) -replace '<FileVersion>.*</FileVersion>', '<FileVersion>!VERSION!</FileVersion>' | Set-Content ScreenCaptureApp.csproj"

if errorlevel 1 (
    echo ERROR: Failed to update version
    exit /b 1
)
echo Version updated to !VERSION!
echo.

echo Step 2: Cleaning previous build...
echo ----------------------------------
if exist release rmdir /s /q release
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo Clean complete
echo.

echo Step 3: Building release version...
echo ------------------------------------
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./release

if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)
echo Build successful
echo.

echo Step 4: Creating README...
echo ---------------------------
echo # JForex Trading Terminal - Release v!VERSION! > release\README.md
echo. >> release\README.md
echo ## Installation >> release\README.md
echo. >> release\README.md
echo 1. Extract all files to a folder on your computer >> release\README.md
echo 2. Run `ScreenCaptureApp.exe` >> release\README.md
echo. >> release\README.md
echo ## Requirements >> release\README.md
echo. >> release\README.md
echo - Windows 10 or later (64-bit) >> release\README.md
echo - No additional dependencies required (self-contained) >> release\README.md
echo. >> release\README.md
echo ## Version Information >> release\README.md
echo. >> release\README.md
echo - **Version**: !VERSION! >> release\README.md
echo - **Platform**: Windows x64 >> release\README.md
echo. >> release\README.md
echo ## Features >> release\README.md
echo. >> release\README.md
echo - Screen capture with hotkey support >> release\README.md
echo - Multiple overlay windows for trading analysis >> release\README.md
echo - JForex strategy integration >> release\README.md
echo - HTTP server for external control >> release\README.md
echo - Pattern tracking and visualization >> release\README.md
echo README created
echo.

echo Step 5: Release summary...
echo --------------------------
echo Release v!VERSION! created successfully!
echo.
echo Files in release folder:
dir release /b
echo.

echo ========================================
echo RELEASE COMPLETE
echo ========================================
echo.
echo Release location: %CD%\release
echo.
