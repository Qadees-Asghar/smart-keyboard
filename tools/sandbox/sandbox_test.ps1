# Runs INSIDE Windows Sandbox: a clean Windows that has never seen .NET or
# this project, thrown away when the window closes. It is the closest thing to
# trying SmartKeyboard on somebody else's PC.
#
# Started by run_sandbox_test.ps1 at sign in. Reads the release zip from
# C:\sk-dist (read only) and writes what happened to C:\sk-results\results.txt,
# then done.txt, both of which the host can see.
#
# Typing is done with SendInput and real key codes, never SendKeys, which
# toggles Caps Lock and sends bursts that Notepad drops (see CLAUDE.md).

$ErrorActionPreference = "Continue"
$out = "C:\sk-results"
$results = Join-Path $out "results.txt"
Remove-Item (Join-Path $out "*") -Force -ErrorAction SilentlyContinue

function Log([string]$message) {
    Add-Content -Path $results -Value $message
}

function Check([string]$name, [bool]$ok, [string]$detail = "") {
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Log ("{0}  {1}{2}" -f $mark, $name, $(if ($detail) { "  ($detail)" } else { "" }))
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Kb {
  [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk; public ushort scan; public uint flags; public uint time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit, Size=40)] public struct IN { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KI ki; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, IN[] i, int size);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint c, uint t);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  static IN K(ushort vk, bool up) { var i = new IN(); i.type = 1; i.ki.vk = vk; i.ki.scan = (ushort)MapVirtualKey(vk, 0); i.ki.flags = up ? 2u : 0u; return i; }
  public static void Press(ushort vk) { SendInput(2, new[] { K(vk, false), K(vk, true) }, Marshal.SizeOf(typeof(IN))); }
  public static void Chord(ushort mod, ushort vk) { SendInput(4, new[] { K(mod, false), K(vk, false), K(vk, true), K(mod, true) }, Marshal.SizeOf(typeof(IN))); }
  public static void Type(string s, int gap) { foreach (char c in s) { ushort vk = c == ' ' ? (ushort)0x20 : (ushort)char.ToUpperInvariant(c); Press(vk); System.Threading.Thread.Sleep(gap); } }
}
"@

# The whole text of whatever has keyboard focus, or null.
function Read-FocusedText {
    try {
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        $pattern = $null
        if ($focused.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$pattern)) {
            return $pattern.DocumentRange.GetText(-1)
        }
        if ($focused.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
            return $pattern.Current.Value
        }
    } catch { }
    return $null
}

# Top level window titles belonging to a process.
function Get-WindowNames([int]$processId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
    $found = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $condition)
    return @($found | ForEach-Object { $_.Current.Name })
}

# Runs a .cmd file, waits for it, and returns its exit code, writing what it
# printed into the results.
#
# Two traps here, both of which failed this test before. Windows PowerShell
# 5.1 mangles quotes passed to cmd.exe, so a path with a space in it was split
# and never ran. And the installer starts SmartKeyboard, which inherits the
# installer's output: through a pipe, that would keep the pipe open for as
# long as SmartKeyboard runs. Start-Process with the output sent to a file
# avoids both.
function Invoke-Cmd([string]$path) {
    $output = Join-Path $env:TEMP ("sk-cmd-{0}.txt" -f [guid]::NewGuid())
    $process = Start-Process cmd.exe -ArgumentList "/c `"`"$path`"`"" `
        -RedirectStandardOutput $output -NoNewWindow -PassThru
    $null = $process.Handle   # without this, ExitCode comes back empty
    $process.WaitForExit()
    Get-Content $output -ErrorAction SilentlyContinue | ForEach-Object { Log "    $_" }
    return $process.ExitCode
}

function Stop-SmartKeyboard {
    try { [void][System.Threading.EventWaitHandle]::OpenExisting('SmartKeyboard.ExitRequested.v1').Set() } catch { }
    for ($i = 0; $i -lt 30 -and (Get-Process SmartKeyboard.App -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
}

try {
    # The desktop is still settling when sign in scripts start.
    Start-Sleep -Seconds 10

    $os = Get-CimInstance Win32_OperatingSystem
    Log ("Windows: {0} build {1}" -f $os.Caption, $os.BuildNumber)
    Log ("Started: {0}" -f (Get-Date))
    Log ""

    # 1. A clean PC: no .NET anywhere.
    $dotnetOnPath = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
    $dotnetFolder = Test-Path "C:\Program Files\dotnet"
    Check "no .NET on this PC" (-not $dotnetOnPath -and -not $dotnetFolder) "dotnet on PATH: $dotnetOnPath, Program Files\dotnet: $dotnetFolder"

    # 2. Extract the zip and install, the way a person would.
    $zip = Get-ChildItem "C:\sk-dist\SmartKeyboard-*-win-x64.zip" | Select-Object -First 1
    Check "release zip found" ($null -ne $zip) "$($zip.Name)"
    $extract = Join-Path $env:USERPROFILE "Downloads\sk"
    Expand-Archive -LiteralPath $zip.FullName -DestinationPath $extract -Force
    $installer = Get-ChildItem $extract -Recurse -Filter "Install SmartKeyboard.cmd" | Select-Object -First 1

    $env:SK_NO_PAUSE = "1"
    $exitCode = Invoke-Cmd $installer.FullName
    Check "installer finished" ($exitCode -eq 0) "exit $exitCode"

    # 3. Everything the installer promises.
    $installDir = Join-Path $env:LOCALAPPDATA "Programs\SmartKeyboard"
    $exe = Join-Path $installDir "SmartKeyboard.App.exe"
    Check "program installed" (Test-Path $exe) $exe
    Check "dictionary installed" (Test-Path (Join-Path $installDir "Data\words.txt"))
    Check "fonts installed" ((Get-ChildItem (Join-Path $installDir "Assets\Fonts\*.ttf")).Count -eq 4)

    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
        $lnk = Join-Path $folder 'SmartKeyboard.lnk'
        Check "shortcut in $folder" ((Test-Path $lnk) -and $shell.CreateShortcut($lnk).TargetPath -eq $exe)
    }

    $run = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue).SmartKeyboard
    Check "starts with Windows" ($run -eq "`"$exe`" --background") "$run"

    Start-Sleep -Seconds 5
    $process = Get-CimInstance Win32_Process -Filter "Name='SmartKeyboard.App.exe'"
    Check "running after install" ($null -ne $process -and $process.CommandLine -like "*--background*") "$($process.CommandLine)"

    # 4. Turn on fixing in other apps (it starts off) and mixup fixing, and
    #    turn learning off so the test leaves nothing behind.
    Stop-SmartKeyboard
    $settingsDir = Join-Path $env:APPDATA "SmartKeyboard"
    New-Item -ItemType Directory -Force $settingsDir | Out-Null
    Set-Content (Join-Path $settingsDir "settings.txt") @(
        "autocorrect=True", "fixMessyWords=True", "fixRealWords=True",
        "systemWideAutocorrect=True", "learning=False", "showPredictions=True",
        "spellCheck=True", "suggestionCount=5", "diagnostics=True")
    Start-Process $exe -ArgumentList "--background"

    # A fresh VM is slow to read a 5 MB word list and build the typo tree.
    Start-Sleep -Seconds 30
    $process = Get-Process SmartKeyboard.App -ErrorAction SilentlyContinue
    Check "running after restart" ($null -ne $process) ("memory {0} MB" -f [int]($process.WorkingSet64 / 1MB))

    # 5. Type into a real app and read back what it holds.
    #
    # Windows Sandbox has no Notepad: on Windows 11 it is a Store app, and the
    # Sandbox has no Store apps. So a tiny WPF app with a text box stands in.
    # It is compiled here with the .NET Framework that ships inside every
    # Windows, which has nothing to do with SmartKeyboard's own .NET, checked
    # absent above.
    #
    # WPF rather than WinForms on purpose: a .NET Framework WinForms text box
    # shows up to UI Automation as a bare pane with no text in it, so neither
    # SmartKeyboard's first-word check nor this test could read it. WPF, like
    # Notepad and browsers, reports its text properly.
    #
    # It has to be a program of its own. Run as a PowerShell script, the
    # window belonged to powershell.exe, and SmartKeyboard rightly kept quiet,
    # because it treats terminals as code windows; PowerShell's console also
    # took the first keys.
    #
    # Every case gets a fresh window, so each one is also the first word after
    # a change of window, the case CaretTextCheck exists for.
    $padExe = Join-Path $env:TEMP "TestPad.exe"
    Add-Type -OutputAssembly $padExe -OutputType WindowsApplication `
        -ReferencedAssemblies PresentationFramework, PresentationCore, WindowsBase, System.Xaml -TypeDefinition @"
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
public static class TestPad {
    [STAThread]
    public static void Main() {
        var box = new TextBox { AcceptsReturn = true, FontSize = 18, TextWrapping = TextWrapping.Wrap };
        var window = new Window { Title = "TestPad", Width = 700, Height = 300, Content = box };
        window.Loaded += (s, e) => { window.Activate(); box.Focus(); Keyboard.Focus(box); };
        new Application().Run(window);
    }
}
"@
    Check "test text app built" (Test-Path $padExe) "TestPad.exe, a WPF text box"

    $cases = @(
        @{ Typed = "helo world "; Want = "hello world " },
        @{ Typed = "email form the office "; Want = "email from the office " },
        @{ Typed = "i want too go "; Want = "i want to go " }
    )

    foreach ($case in $cases) {
        $pad = Start-Process $padExe -PassThru
        for ($i = 0; $i -lt 20 -and $pad.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 500; $pad.Refresh() }
        [void][Kb]::SetForegroundWindow($pad.MainWindowHandle)
        Start-Sleep -Seconds 1

        [Kb]::Type($case.Typed, 110)
        Start-Sleep -Milliseconds 1500
        $got = Read-FocusedText
        Check ("typed '{0}'" -f $case.Typed.Trim()) ($got -eq $case.Want) ("got '{0}'" -f $got)

        Stop-Process -Id $pad.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    # 6. The desktop icon opens the window, and never a second copy.
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SmartKeyboard.lnk'
    Start-Process $desktopLink
    Start-Sleep -Seconds 4
    $copies = @(Get-Process SmartKeyboard.App -ErrorAction SilentlyContinue)
    $names = if ($copies.Count -gt 0) { Get-WindowNames $copies[0].Id } else { @() }
    Check "desktop icon opens the window" ($names -contains "SmartKeyboard") ("windows: " + ($names -join ', '))
    Check "still one copy running" ($copies.Count -eq 1) ("copies: " + $copies.Count)

    # 7. The uninstaller cleans up after itself.
    $uninstaller = Get-ChildItem $extract -Recurse -Filter "Uninstall SmartKeyboard.cmd" | Select-Object -First 1
    $exitCode = Invoke-Cmd $uninstaller.FullName
    Check "uninstaller finished" ($exitCode -eq 0) "exit $exitCode"
    $runAfter = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue).SmartKeyboard
    Check "uninstall removed the program" (-not (Test-Path $installDir))
    Check "uninstall removed the icon" (-not (Test-Path $desktopLink))
    Check "uninstall removed start with Windows" ($null -eq $runAfter)
    Check "uninstall kept the user's data" (Test-Path (Join-Path $settingsDir "settings.txt"))

    $log = Join-Path $settingsDir "inject.log"
    if (Test-Path $log) { Copy-Item $log (Join-Path $out "inject.log") }
}
catch {
    Log ("ERROR  " + $_.Exception.Message)
}
finally {
    Log ""
    Log ("Finished: {0}" -f (Get-Date))
    New-Item -ItemType File -Force (Join-Path $out "done.txt") | Out-Null
}
