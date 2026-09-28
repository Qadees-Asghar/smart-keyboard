@echo off
rem Builds the SmartKeyboard zip for other Windows PCs.
rem
rem   tools\make_release.cmd
rem
rem Produces dist\SmartKeyboard-<version>-win-x64.zip, where the version is
rem <Version> in src\SmartKeyboard.App\SmartKeyboard.App.csproj. Inside:
rem
rem   SmartKeyboard-<version>\
rem     Install SmartKeyboard.cmd     installs it for the current user
rem     Uninstall SmartKeyboard.cmd
rem     README.txt
rem     app\                          SmartKeyboard.App.exe, Data\, Assets\
rem
rem The exe is self-contained and a single file: it carries its own .NET, so
rem the PC it goes to needs nothing installed. The tests run first, and a
rem failing test stops the release.

setlocal
cd /d "%~dp0.."

for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "([xml](Get-Content 'src\SmartKeyboard.App\SmartKeyboard.App.csproj')).Project.PropertyGroup.Version | Where-Object { $_ }"`) do set "VERSION=%%v"
if "%VERSION%"=="" (
    echo No ^<Version^> found in SmartKeyboard.App.csproj.
    exit /b 1
)

set "NAME=SmartKeyboard-%VERSION%"
set "STAGE=dist\%NAME%"
set "ZIP=dist\%NAME%-win-x64.zip"

echo Running the tests...
dotnet test SmartKeyboard.sln --nologo -v q
if errorlevel 1 goto failed

if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del "%ZIP%"

echo Building SmartKeyboard %VERSION% for x64 Windows...
dotnet publish src\SmartKeyboard.App\SmartKeyboard.App.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o "%STAGE%\app" --nologo -v q
if errorlevel 1 goto failed
if not exist "%STAGE%\app\SmartKeyboard.App.exe" goto failed
if not exist "%STAGE%\app\Data\words.txt" goto failed

copy /y "tools\release\Install SmartKeyboard.cmd" "%STAGE%\" >nul
copy /y "tools\release\Uninstall SmartKeyboard.cmd" "%STAGE%\" >nul
copy /y "tools\release\README.txt" "%STAGE%\" >nul

echo Zipping...
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%' -DestinationPath '%ZIP%' -CompressionLevel Optimal"
if errorlevel 1 goto failed

echo.
echo Built %ZIP%
exit /b 0

:failed
echo.
echo The release was not built.
exit /b 1
