# Tests the release zip on a clean Windows, using Windows Sandbox.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\sandbox\run_sandbox_test.ps1
#
# Build the zip first with tools\make_release.cmd. Windows Sandbox must be
# turned on once, from an Administrator PowerShell, followed by a restart:
#
#   Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All
#
# It opens a Sandbox window, which installs, uses and uninstalls SmartKeyboard
# on its own (sandbox_test.ps1), then prints the results here and closes the
# Sandbox. Leave the Sandbox window alone while it runs: the test types into
# Notepad inside it. Takes about two minutes.

param([int]$TimeoutMinutes = 8)

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dist = Join-Path $root "dist"
$results = Join-Path $dist "sandbox-results"
$sandbox = Join-Path $env:WINDIR "System32\WindowsSandbox.exe"

if (-not (Test-Path $sandbox)) {
    Write-Host "Windows Sandbox is not turned on. See the top of this file."
    exit 1
}

if (-not (Get-ChildItem (Join-Path $dist "SmartKeyboard-*-win-x64.zip") -ErrorAction SilentlyContinue)) {
    Write-Host "No release zip in dist. Run tools\make_release.cmd first."
    exit 1
}

New-Item -ItemType Directory -Force $results | Out-Null
Remove-Item (Join-Path $results "*") -Force -ErrorAction SilentlyContinue

# The Sandbox sees three folders: the zip (read only), this test (read only),
# and a results folder it can write to.
$config = @"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$dist</HostFolder>
      <SandboxFolder>C:\sk-dist</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$PSScriptRoot</HostFolder>
      <SandboxFolder>C:\sk-test</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$results</HostFolder>
      <SandboxFolder>C:\sk-results</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\sk-test\sandbox_test.ps1</Command>
  </LogonCommand>
</Configuration>
"@

$wsb = Join-Path $results "..\SmartKeyboard-test.wsb"
Set-Content -Path $wsb -Value $config -Encoding UTF8

Write-Host "Starting Windows Sandbox. Leave its window alone until this finishes..."
Start-Process $wsb

$done = Join-Path $results "done.txt"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while (-not (Test-Path $done) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
}

if (Test-Path (Join-Path $results "results.txt")) {
    Get-Content (Join-Path $results "results.txt")
} else {
    Write-Host "No results came back within $TimeoutMinutes minutes."
}

# Closing the Sandbox throws it away, with everything installed in it.
Get-Process WindowsSandbox, WindowsSandboxClient, WindowsSandboxRemoteSession -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

if (-not (Test-Path $done)) { exit 1 }
$failed = Select-String -Path (Join-Path $results "results.txt") -Pattern "^(FAIL|ERROR)" -Quiet
if ($failed) { exit 1 } else { exit 0 }
