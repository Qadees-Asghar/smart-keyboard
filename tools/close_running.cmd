@echo off
rem Closes a running SmartKeyboard, if there is one, and waits until it has
rem gone. Used by run.cmd, install.cmd and uninstall.cmd.
rem
rem It asks politely first, because a copy that is asked to close saves what it
rem learned on the way out. Only if it has not gone after about ten seconds is
rem it closed the hard way.
rem
rem Asking politely means setting the ExitRequested event the app listens for
rem (see InstanceSignal.cs). taskkill without /F is sent as well, for a copy
rem older than that, but it never really worked: SmartKeyboard's only window is
rem hidden, so taskkill reported success and nothing closed.
rem
rem It has to be gone, not just asked. Windows locks SmartKeyboard.App.exe while
rem it runs, so building or copying on top of it would fail, and two copies at
rem once both watch the keyboard and fix the same word twice.

setlocal
set "EXE=SmartKeyboard.App.exe"

tasklist /FI "IMAGENAME eq %EXE%" 2>nul | find /I "%EXE%" >nul
if errorlevel 1 exit /b 0

echo Closing the copy that is already running...
powershell -NoProfile -Command "try { [void][System.Threading.EventWaitHandle]::OpenExisting('SmartKeyboard.ExitRequested.v1').Set() } catch { }" >nul 2>&1
taskkill /IM "%EXE%" >nul 2>&1

set TRIES=0
:wait
tasklist /FI "IMAGENAME eq %EXE%" 2>nul | find /I "%EXE%" >nul
if errorlevel 1 exit /b 0
set /a TRIES+=1
if %TRIES% geq 10 goto force
ping -n 2 127.0.0.1 >nul
goto wait

:force
echo   Taking longer than expected, closing it the hard way...
taskkill /F /IM "%EXE%" >nul 2>&1
ping -n 3 127.0.0.1 >nul
exit /b 0
