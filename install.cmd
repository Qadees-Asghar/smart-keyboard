@echo off
rem Installs SmartKeyboard as an app, or updates the one already installed.
rem
rem   install.cmd                 installs to D:\tools\SmartKeyboard
rem   install.cmd "E:\Apps\SK"    installs somewhere else
rem
rem What it does:
rem   1. Closes SmartKeyboard if it is running, so its files can be replaced.
rem   2. Builds it into the install folder.
rem   3. Puts a SmartKeyboard icon on the desktop, in the Start menu, and in
rem      this folder next to run.cmd.
rem   4. Starts it with Windows. Only on a first install, or when that was
rem      already on: updating never turns back on something you switched off.
rem   5. Starts it, quietly, in the tray.
rem
rem Your words, settings and everything it learned live in
rem %APPDATA%\SmartKeyboard and are never touched by this.
rem
rem run.cmd is still there for working on the code. This is for using it.

setlocal
cd /d "%~dp0"

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=D:\tools\SmartKeyboard"
set "EXE=%TARGET%\SmartKeyboard.App.exe"
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"

rem Worked out before anything changes, so step 4 knows what to respect.
set "FRESH=0"
if not exist "%EXE%" set "FRESH=1"
set "HAD_STARTUP=0"
reg query "%RUNKEY%" /v SmartKeyboard >nul 2>&1 && set "HAD_STARTUP=1"

call "%~dp0tools\close_running.cmd"

echo Building SmartKeyboard into %TARGET% ...
dotnet publish src\SmartKeyboard.App\SmartKeyboard.App.csproj -c Release -o "%TARGET%" --nologo -v q
if errorlevel 1 goto failed
if not exist "%EXE%" goto failed
if not exist "%TARGET%\Data\words.txt" goto failed

echo Adding the desktop, Start menu and project folder icons...
set "SK_EXE=%EXE%"
set "SK_DIR=%TARGET%"
set "SK_HERE=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$shell = New-Object -ComObject WScript.Shell;" ^
  "foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'), $env:SK_HERE)) {" ^
  "  $link = $shell.CreateShortcut((Join-Path $folder 'SmartKeyboard.lnk'));" ^
  "  $link.TargetPath = $env:SK_EXE;" ^
  "  $link.WorkingDirectory = $env:SK_DIR;" ^
  "  $link.IconLocation = $env:SK_EXE + ',0';" ^
  "  $link.Description = 'SmartKeyboard: word completion, typo fixing and next word prediction';" ^
  "  $link.Save() }"
if errorlevel 1 echo   Could not add the icons. SmartKeyboard is installed anyway.

if "%FRESH%%HAD_STARTUP%"=="00" (
    echo Start with Windows is off, and stays off. The tray menu can turn it on.
) else (
    reg add "%RUNKEY%" /v SmartKeyboard /t REG_SZ /d "\"%EXE%\" --background" /f >nul
    echo SmartKeyboard will start with Windows. The tray menu can turn that off.
)

start "" "%EXE%" --background

echo.
echo SmartKeyboard is installed and running in the tray, by the clock.
echo Double click the SmartKeyboard icon on the desktop to open it.
echo To update it later, run install.cmd again.
if not defined SK_NO_PAUSE pause
exit /b 0

:failed
echo.
echo The build failed, so nothing was installed. Your data is untouched.
if not defined SK_NO_PAUSE pause
exit /b 1
