@echo off
rem Installs SmartKeyboard on this PC, or updates it if it is already here.
rem
rem Double click it after extracting the whole zip. Nothing else is needed:
rem SmartKeyboard carries its own .NET, and it installs for this Windows user
rem only, so it does not ask for Administrator rights.
rem
rem   Install SmartKeyboard.cmd                 installs to your programs folder
rem   Install SmartKeyboard.cmd "E:\Apps\SK"    installs somewhere else
rem
rem What it does:
rem   1. Closes SmartKeyboard if it is running, so its files can be replaced.
rem   2. Copies it to %LOCALAPPDATA%\Programs\SmartKeyboard.
rem   3. Puts a SmartKeyboard icon on the desktop and in the Start menu.
rem   4. Starts it with Windows. Only on a first install, or when that was
rem      already on: updating never turns back on something you switched off.
rem   5. Starts it, quietly, in the tray by the clock.
rem
rem Your words, settings and everything it learns are kept separately, in
rem %APPDATA%\SmartKeyboard, so updating or uninstalling never touches them.

setlocal
set "SOURCE=%~dp0app"
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=%LOCALAPPDATA%\Programs\SmartKeyboard"
set "EXE=%TARGET%\SmartKeyboard.App.exe"
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"

if not exist "%SOURCE%\SmartKeyboard.App.exe" (
    echo Cannot find the "app" folder next to this file.
    echo Extract the whole zip first, then run this from the extracted folder.
    if not defined SK_NO_PAUSE pause
    exit /b 1
)

rem Worked out before anything changes, so step 4 knows what to respect.
set "FRESH=0"
if not exist "%EXE%" set "FRESH=1"
set "HAD_STARTUP=0"
reg query "%RUNKEY%" /v SmartKeyboard >nul 2>&1 && set "HAD_STARTUP=1"

call :close_running

echo Installing SmartKeyboard to %TARGET% ...
robocopy "%SOURCE%" "%TARGET%" /MIR /NFL /NDL /NJH /NJS /NP >nul
rem robocopy reports success with any code below 8.
if errorlevel 8 goto failed
if not exist "%EXE%" goto failed

echo Adding the desktop and Start menu icons...
set "SK_EXE=%EXE%"
set "SK_DIR=%TARGET%"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Get-ChildItem -LiteralPath $env:SK_DIR -Recurse -File | Unblock-File;" ^
  "$shell = New-Object -ComObject WScript.Shell;" ^
  "foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {" ^
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
echo To fix typos in other apps too, right click the tray icon and tick
echo "Fix typos in other apps".
if not defined SK_NO_PAUSE pause
exit /b 0

:failed
echo.
echo SmartKeyboard could not be copied to %TARGET%. Nothing was changed.
if not defined SK_NO_PAUSE pause
exit /b 1

rem Closes a running SmartKeyboard and waits until it has gone. It is asked
rem politely first, so it saves what it learned; only if it is still there
rem after about ten seconds is it closed the hard way. Windows locks the
rem program file while it runs, so it has to be gone before copying.
:close_running
tasklist /FI "IMAGENAME eq SmartKeyboard.App.exe" 2>nul | find /I "SmartKeyboard.App.exe" >nul
if errorlevel 1 exit /b 0
echo Closing the copy that is already running...
powershell -NoProfile -Command "try { [void][System.Threading.EventWaitHandle]::OpenExisting('SmartKeyboard.ExitRequested.v1').Set() } catch { }" >nul 2>&1
set TRIES=0
:close_wait
tasklist /FI "IMAGENAME eq SmartKeyboard.App.exe" 2>nul | find /I "SmartKeyboard.App.exe" >nul
if errorlevel 1 exit /b 0
set /a TRIES+=1
if %TRIES% geq 10 goto close_force
ping -n 2 127.0.0.1 >nul
goto close_wait
:close_force
taskkill /F /IM SmartKeyboard.App.exe >nul 2>&1
ping -n 3 127.0.0.1 >nul
exit /b 0
