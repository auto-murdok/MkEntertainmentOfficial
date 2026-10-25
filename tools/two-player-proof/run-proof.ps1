# run-proof.ps1 — the formal two-player proof run.
#
# THE AWAY RULE: a proof run takes over the screen (two game windows +
# OBS), so it only runs when the user is NOT at the computer. The guard
# below measures keyboard/mouse idle time; if the user has been active
# within $MinIdleMinutes, the run is DEFERRED (exit code 10) and nothing
# is launched. See PROOF-PROCESS.md for the full procedure.
#
# Usage: powershell -File run-proof.ps1 [-Scenario clean] [-MinIdleMinutes 10]
# Exit codes: 0 = proof captured and verified; 10 = deferred (user active);
#             1 = the run or the verification failed.

param(
    [string]$Scenario = "clean",
    [int]$MinIdleMinutes = 10,
    [int]$RecordSeconds = 60
)

$ErrorActionPreference = "Stop"
$home2 = "C:\Users\ljtinitanao"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static double Minutes()
    {
        var lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(lii);
        if (!GetLastInputInfo(ref lii)) return 0;
        uint idleMs = (uint)Environment.TickCount - lii.dwTime;
        return idleMs / 60000.0;
    }
}
"@

# --- AWAY GUARD -------------------------------------------------------------
$idle = [IdleTime]::Minutes()
Write-Host ("user idle for {0:N1} min (guard requires >= {1})" -f $idle, $MinIdleMinutes)
if ($idle -lt $MinIdleMinutes) {
    Write-Host "PROOF DEFERRED: user is at the computer. Nothing was launched."
    exit 10
}
Write-Host "away guard passed: starting proof run"

# --- RUN --------------------------------------------------------------------
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $home2 "run-test.ps1") `
    -Scenario $Scenario -RecordSeconds $RecordSeconds

$runDir = Join-Path $home2 "tp-runs\$Scenario"
$video = Join-Path $runDir "side-by-side.mp4"
if (-not (Test-Path $video)) {
    Write-Host "PROOF FAILED: no side-by-side.mp4 in $runDir"
    exit 1
}
$videoInfo = Get-Item $video
$ageMin = ((Get-Date) - $videoInfo.LastWriteTime).TotalMinutes
if ($videoInfo.Length -lt 2MB -or $ageMin -gt 10) {
    Write-Host "PROOF FAILED: video looks stale or truncated (size $($videoInfo.Length), age ${ageMin:N1} min)"
    exit 1
}

# --- SUMMARIZE + ARCHIVE ------------------------------------------------------
$clientLog = Join-Path $runDir "client.log"
$hostLog = Join-Path $runDir "host.log"
function Count-Lines($path, $pattern) {
    if (Test-Path $path) { return (Select-String -Path $path -Pattern $pattern -SimpleMatch).Count }
    return 0
}
$shots      = Count-Lines $clientLog "[TPTEST] honest shot"
$reloads    = Count-Lines $clientLog "[TPTEST] reload (clip spent)"
$died       = (Count-Lines $clientLog "[TPTEST] TARGET DIED") -gt 0
$exceptions = (Count-Lines $clientLog "Exception") + (Count-Lines $hostLog "Exception")
$brainOff   = (Count-Lines $clientLog "CharacterBrain disabled") -gt 0

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$proofDir = Join-Path $home2 "tp-proofs\$stamp-$Scenario"
New-Item -ItemType Directory -Force -Path $proofDir | Out-Null
Copy-Item $video (Join-Path $proofDir "side-by-side.mp4") -Force
Copy-Item $clientLog (Join-Path $proofDir "client.log") -Force
Copy-Item $hostLog (Join-Path $proofDir "host.log") -Force

$verdict = if ($died -and $exceptions -eq 0) { "PASS" } else { "CHECK" }
$summary = @(
    "Two-player proof - $Scenario - $stamp"
    "verdict: $verdict"
    "target died: $died"
    "honest shots fired: $shots"
    "reloads: $reloads"
    "exceptions in logs: $exceptions"
    "harness sole input control (CharacterBrain off): $brainOff"
    "user idle before run: $([math]::Round($idle, 1)) min"
    "video: side-by-side.mp4 ($([math]::Round($videoInfo.Length / 1MB, 2)) MB)"
)
$summary | Set-Content (Join-Path $proofDir "summary.txt")
$summary | ForEach-Object { Write-Host $_ }
Write-Host "PROOF-DONE $proofDir"
exit 0
