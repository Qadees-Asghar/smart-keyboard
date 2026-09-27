@echo off
rem Builds SmartKeyboard and starts it, leaving exactly one copy running.
rem
rem The copy that is already running is closed FIRST, and not out of tidiness.
rem Windows locks SmartKeyboard.App.exe while it runs, so building on top of a
rem running copy fails and you carry on using the old one without being told.
rem Two copies at once is worse still: both watch the keyboard, so both correct
rem the same word, and "helo" comes out as "hhello".

cd /d "%~dp0"

set "EXE=SmartKeyboard.App.exe"
set "OUT=src\SmartKeyboard.App\bin\Release\net10.0-windows"

tasklist /FI "IMAGENAME eq %EXE%" 2>nul | find /I "%EXE%" >nul
if errorlevel 1 goto build

echo Closing the copy that is already running...
taskkill /IM "%EXE%" >nul 2>&1

set TRIES=0
:wait
tasklist /FI "IMAGENAME eq %EXE%" 2>nul | find /I "%EXE%" >nul
if errorlevel 1 goto build
set /a TRIES+=1
if %TRIES% geq 10 goto force
ping -n 2 127.0.0.1 >nul
goto wait

:force
rem It did not go quietly, so insist. Anything learned was already saved when
rem the close was asked for.
echo   Taking longer than expected, closing it the hard way...
taskkill /F /IM "%EXE%" >nul 2>&1
ping -n 3 127.0.0.1 >nul

:build
echo Building...
dotnet build -c Release -v q --nologo
if errorlevel 1 (
    echo.
    echo Build failed. Nothing was started.
    pause
    exit /b 1
)

echo Starting SmartKeyboard...
start "" "%OUT%\%EXE%"

echo.
echo SmartKeyboard is running in the tray, by the clock.
echo The icon appears straight away. It says "Loading dictionary..." for a
echo moment while it reads the word list, then it is ready.
echo.
echo Right click the tray icon to open the editor, change settings, or exit.
echo Press any key to close this window. SmartKeyboard keeps running.
pause
