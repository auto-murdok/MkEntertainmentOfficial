param(
    [Parameter(Mandatory = $true)][ValidateSet('clean', 'lag', 'cheat')][string]$Scenario,
    [int]$RecordSeconds = 0,
    [string]$OutDir = "C:\Users\ljtinitanao\tp-runs"
)

$ErrorActionPreference = 'Stop'
$exe = "C:\Users\ljtinitanao\tp-build\MkTest.exe"
$runDir = Join-Path $OutDir $Scenario
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern bool SetWindowText(IntPtr hWnd, string text);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
}
"@
# Keep the display + system awake for the whole run: an idle display sleep
# freezes the DWM composition and gdigrab then captures a stale frame while
# the game keeps simulating (seen in the first recording attempt).
$ES_AWAKE = [uint32]2147483651   # 0x80000003 ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED (decimal: PS hex literals are signed)
$ES_CONTINUOUS = [uint32]2147483648   # 0x80000000
[Win32]::SetThreadExecutionState($ES_AWAKE) | Out-Null

$ffmpeg = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) {
    $found = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $ffmpeg = $found.FullName }
}
Write-Host "ffmpeg: $ffmpeg"

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$tileW = [math]::Floor($screen.Width / 2)
$tileH = [math]::Floor($tileW * 9 / 16)
if ($tileH -gt $screen.Height) { $tileH = $screen.Height; $tileW = [math]::Floor($tileH * 16 / 9) }
$tileY = [math]::Floor(($screen.Height - $tileH) / 2)
Write-Host "screen $($screen.Width)x$($screen.Height), tiles ${tileW}x${tileH} at y=$tileY"

Get-Process MkTest -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

function Place-Window($proc, [string]$title, [int]$x) {
    for ($i = 0; $i -lt 30; $i++) {
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 500
    }
    $h = $proc.MainWindowHandle
    [Win32]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
    [Win32]::SetWindowText($h, $title) | Out-Null
    [Win32]::MoveWindow($h, $x, $tileY, $tileW, $tileH, $true) | Out-Null
    [Win32]::SetForegroundWindow($h) | Out-Null
    Write-Host "window '$title' placed at x=$x (hwnd $h)"
}

# --- HOST (target) first: it must be listening before the client joins ---
$hostLog = Join-Path $runDir "host.log"
$hostArgs = @('-force-d3d11', '-testrole', 'target', '-screen-width', $tileW, '-screen-height', $tileH,
              '-screen-fullscreen', '0', '-logFile', $hostLog)
$hostProc = Start-Process $exe -ArgumentList $hostArgs -PassThru
Start-Sleep -Seconds 14
Place-Window $hostProc 'HOST' 0

# --- CLIENT ---
$clientLog = Join-Path $runDir "client.log"
$clientArgs = @('-force-d3d11', '-screen-width', $tileW, '-screen-height', $tileH,
                '-screen-fullscreen', '0', '-logFile', $clientLog)
switch ($Scenario) {
    'clean' { $clientArgs = @('-testrole', 'shooter') + $clientArgs }
    'lag'   { $clientArgs = @('-testrole', 'shooter', '-simdelay', '75', '-simjitter', '5') + $clientArgs }
    'cheat' { $clientArgs = @('-testrole', 'cheater') + $clientArgs }
}
$clientProc = Start-Process $exe -ArgumentList $clientArgs -PassThru
Start-Sleep -Seconds 7
Place-Window $clientProc 'CLIENT' $tileW

# Re-assert placement + visibility right before capture: a window that the
# game repositioned (or that lost presentation) captures as a frozen frame.
Place-Window $hostProc 'HOST' 0
Place-Window $clientProc 'CLIENT' $tileW

# --- RECORD ---
# OBS Studio Game Capture (profile/collection "TPTest"): two game_capture
# sources matched by window title (HOST / CLIENT) on a 1920x540 canvas.
# Every GDI route failed on this machine — gdigrab delivered ~1.5 unique
# fps while the games presented at 60+ fps (D3D11 and D3D12, vsync on and
# off, framed and borderless all alike), and neither ffmpeg build ships
# ddagrab. Game Capture hooks each process's Present directly and is
# immune to all of that. OBS records until stopped below; the file lands
# in tp-runs\obs and is copied to side-by-side.mp4 in the run folder.
$recordProcs = @()
$obsProc = $null
if ($RecordSeconds -gt 0) {
    New-Item -ItemType Directory -Force -Path "C:\Users\ljtinitanao\tp-runs\obs" | Out-Null
    $obsProc = Start-Process "C:\Program Files\obs-studio\bin\64bit\obs64.exe" `
        -ArgumentList @('--multi', '--profile', 'TPTest', '--collection', 'TPTest', '--startrecording', '--disable-auto-update') `
        -WorkingDirectory "C:\Program Files\obs-studio\bin\64bit" -PassThru
    Write-Host "OBS recording started (pid $($obsProc.Id))"
}

# --- WAIT for the harness to finish (it quits both peers itself) ---
$deadline = (Get-Date).AddSeconds(170)
while ((Get-Date) -lt $deadline) {
    $hostProc.Refresh(); $clientProc.Refresh()
    if ($hostProc.HasExited -and $clientProc.HasExited) { break }
    Start-Sleep -Seconds 3
}
Write-Host "host exited: $($hostProc.HasExited), client exited: $($clientProc.HasExited)"
Get-Process MkTest -ErrorAction SilentlyContinue | Stop-Process -Force
foreach ($p in $recordProcs) { if (-not $p.HasExited) { $p.WaitForExit(15000) | Out-Null } }
if ($obsProc) {
    Start-Sleep -Seconds 4   # a tail of the game-over screen
    # Graceful close of OUR instance only (CloseMainWindow) so OBS
    # finalizes the recording file; never taskkill /IM — the user may
    # have their own OBS instance running.
    $obsProc.CloseMainWindow() | Out-Null
    if (-not $obsProc.WaitForExit(25000)) { $obsProc | Stop-Process -Force }
    $newest = Get-ChildItem "C:\Users\ljtinitanao\tp-runs\obs\*.mp4" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($newest) {
        Copy-Item $newest.FullName (Join-Path $runDir "side-by-side.mp4") -Force
        Write-Host "composite saved: $($newest.Name) ($([math]::Round($newest.Length / 1MB, 2)) MB) -> side-by-side.mp4"
    } else {
        Write-Host "WARNING: no OBS output file found"
    }
}
[Win32]::SetThreadExecutionState($ES_CONTINUOUS) | Out-Null
Write-Host "RUN-DONE $Scenario"
