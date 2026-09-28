@echo off
rem Removes SmartKeyboard installed by install.cmd.
rem
rem   uninstall.cmd                 removes D:\tools\SmartKeyboard
rem   uninstall.cmd "E:\Apps\SK"    removes it from somewhere else
rem
rem Takes away the program, its desktop and Start menu icons, and starting
rem with Windows. Your words, settings and everything it learned are kept in
rem %APPDATA%\SmartKeyboard, so installing again picks up where you left off.

setlocal
cd /d "%~dp0"

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=D:\tools\SmartKeyboard"
set "EXE=%TARGET%\SmartKeyboard.App.exe"

rem Only a folder that really holds SmartKeyboard is ever deleted, so a
rem mistyped path cannot take something else with it.
if not exist "%EXE%" (
    echo SmartKeyboard is not installed in %TARGET%. Nothing was removed.
    if not defined SK_NO_PAUSE pause
    exit /b 1
)

call "%~dp0tools\close_running.cmd"

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
