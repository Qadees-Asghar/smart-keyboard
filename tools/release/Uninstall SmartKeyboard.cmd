@echo off
rem Removes SmartKeyboard from this PC.
rem
rem   Uninstall SmartKeyboard.cmd                 removes it from your programs folder
rem   Uninstall SmartKeyboard.cmd "E:\Apps\SK"    removes it from somewhere else
rem
rem Takes away the program, its desktop and Start menu icons, and starting
rem with Windows. Your words, settings and everything it learned are kept in
rem %APPDATA%\SmartKeyboard, so installing again picks up where you left off.

setlocal
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=%LOCALAPPDATA%\Programs\SmartKeyboard"

rem Only a folder that really holds SmartKeyboard is ever deleted, so a
rem mistyped path cannot take something else with it.
if not exist "%TARGET%\SmartKeyboard.App.exe" (
    echo SmartKeyboard is not installed in %TARGET%. Nothing was removed.
    if not defined SK_NO_PAUSE pause
    exit /b 1
)

tasklist /FI "IMAGENAME eq SmartKeyboard.App.exe" 2>nul | find /I "SmartKeyboard.App.exe" >nul
if errorlevel 1 goto closed
echo Closing SmartKeyboard...
powershell -NoProfile -Command "try { [void][System.Threading.EventWaitHandle]::OpenExisting('SmartKeyboard.ExitRequested.v1').Set() } catch { }" >nul 2>&1
set TRIES=0
:wait
tasklist /FI "IMAGENAME eq SmartKeyboard.App.exe" 2>nul | find /I "SmartKeyboard.App.exe" >nul
if errorlevel 1 goto closed
set /a TRIES+=1
if %TRIES% geq 10 (
    taskkill /F /IM SmartKeyboard.App.exe >nul 2>&1
    ping -n 3 127.0.0.1 >nul
    goto closed
)
ping -n 2 127.0.0.1 >nul
goto wait
:closed

echo Removing the desktop and Start menu icons...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {" ^
  "  Remove-Item -LiteralPath (Join-Path $folder 'SmartKeyboard.lnk') -ErrorAction SilentlyContinue }"

echo Stopping it from starting with Windows...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v SmartKeyboard /f >nul 2>&1

echo Removing %TARGET% ...
rmdir /s /q "%TARGET%"

echo.
echo SmartKeyboard is removed. Your words and settings are still in
echo %APPDATA%\SmartKeyboard, in case you install it again.
if not defined SK_NO_PAUSE pause
exit /b 0
