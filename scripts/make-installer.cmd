@echo off
setlocal

pushd "%~dp0.." || exit /b 1

echo ========================================
echo   DesktopInk - Build Installer
echo ========================================
echo.

for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "([xml](Get-Content 'src\DesktopInk\DesktopInk.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"`) do set "APP_VERSION=%%v"
if not defined APP_VERSION (
    echo x Could not read ^<Version^> from src\DesktopInk\DesktopInk.csproj
    popd
    exit /b 1
)

echo [1/2] Publishing self-contained exe v%APP_VERSION%...
call scripts\publish.cmd
if errorlevel 1 (
    echo.
    echo x Publish step failed.
    popd
    exit /b 1
)

echo.
echo [2/2] Compiling installer with Inno Setup...

set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
    echo x ISCC.exe not found. Install Inno Setup with:
    echo     winget install JRSoftware.InnoSetup -e
    popd
    exit /b 1
)

"%ISCC%" /Qp /DMyAppVersion=%APP_VERSION% installer\DesktopInk.iss
if errorlevel 1 (
    echo.
    echo x Installer compile failed.
    popd
    exit /b 1
)

echo.
echo + Installer built successfully.
echo   Output: publish\installer\DesktopInkSetup-%APP_VERSION%.exe
echo.

popd
