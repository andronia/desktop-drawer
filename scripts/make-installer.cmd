@echo off
setlocal

pushd "%~dp0.." || exit /b 1

echo ========================================
echo   DesktopInk - Build Installer
echo ========================================
echo.

echo [1/2] Publishing self-contained exe...
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

"%ISCC%" /Qp installer\DesktopInk.iss
if errorlevel 1 (
    echo.
    echo x Installer compile failed.
    popd
    exit /b 1
)

echo.
echo + Installer built successfully.
echo   Output: publish\installer\DesktopInkSetup-1.5.0.exe
echo.

popd
