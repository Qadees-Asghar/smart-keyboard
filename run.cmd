@echo off
rem Builds SmartKeyboard if needed, then starts it and lets go of it, so this
rem window can be closed without taking the program down with it.

cd /d "%~dp0"

echo Building...
dotnet build -v q --nologo
if errorlevel 1 (
    echo.
    echo Build failed. Nothing was started.
    pause
    exit /b 1
)

echo Starting SmartKeyboard...
start "" "src\SmartKeyboard.App\bin\Debug\net10.0-windows\SmartKeyboard.App.exe"

echo.
echo SmartKeyboard is running in the tray, by the clock.
echo Right click the tray icon to open the editor, change settings, or exit.
echo Press any key to close this window. SmartKeyboard keeps running.
pause
