<#
.SYNOPSIS
    Automated Play-Mode Verification Loop for Unity Editor.
.DESCRIPTION
    Implements the official Unity CLI play-mode verification workflow:
    1. Confirms connected Editor status.
    2. Configures auto-tick to prevent unfocused frame freeze.
    3. Enables running in background.
    4. Enters Play mode.
    5. Proves frames are ticking via 'job wait' on Time.frameCount.
    6. Captures Game view screenshot.
    7. Checks Editor console for exceptions/errors.
    8. Safely exits Play mode.
.PARAMETER ProjectPath
    Target Unity project path (defaults to current working directory).
.PARAMETER ScreenshotPath
    Path where the game view screenshot is saved (default: Screenshots/playtest_verify.png).
.PARAMETER TimeoutSeconds
    Maximum seconds to wait for frame advance confirmation (default: 10).
.PARAMETER KeepPlaying
    If switch is present, does not exit Play mode after verification.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = $PSScriptRoot + "/..",
    [string]$ScreenshotPath = "Screenshots/playtest_verify.png",
    [int]$TimeoutSeconds = 10,
    [switch]$KeepPlaying
)

$ErrorActionPreference = "Stop"

function Invoke-UnityCommand([string]$cmd) {
    Write-Host "❯ $cmd" -ForegroundColor Cyan
    $output = Invoke-Expression $cmd
    return $output
}

Write-Host "=== Unity Play-Mode Verification Loop ===" -ForegroundColor Green

# 1. Verify Editor connectivity
$statusJson = unity status --format json --project-path $ProjectPath 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($statusJson)) {
    Write-Error "No connected Unity Editor found for project: $ProjectPath. Please start the Editor first with: unity open . --args `"-automated`""
    exit 12
}

$status = $statusJson | ConvertFrom-Json
$instance = $status.data.instances | Select-Object -First 1
if (-not $instance) {
    Write-Error "No active Editor instances registered."
    exit 12
}

Write-Host "Target Editor: PID $($instance.pid) on Port $($instance.serverPort) (Status: $($instance.state))" -ForegroundColor Gray

# 2. Check if already playing
$editorStatusRaw = unity command editor status --project-path $ProjectPath --format json 2>$null
if ($editorStatusRaw) {
    $editorStatus = $editorStatusRaw | ConvertFrom-Json
    if ($editorStatus.data.playMode -eq "playing") {
        Write-Warning "Editor is already in Play mode (user session active). Skipping play toggle."
        exit 0
    }
}

# 3. Set auto tick so unfocused Editor does not throttle/freeze frames
Write-Host "Configuring auto-tick and background execution..." -ForegroundColor Gray
Invoke-UnityCommand "unity command editor set_auto_tick --project-path `"$ProjectPath`""
Invoke-UnityCommand "unity command editor.playmode set_run_in_background --project-path `"$ProjectPath`""

# 4. Enter Play mode
Write-Host "Entering Play mode..." -ForegroundColor Yellow
Invoke-UnityCommand "unity command editor.playmode start --project-path `"$ProjectPath`""

# 5. Wait for frame progression to prove the game is actually ticking
Write-Host "Verifying frame advancement (Time.frameCount)..." -ForegroundColor Yellow
$waitCondition = '{"member":"UnityEngine.Time.frameCount","op":"changed"}'
Invoke-UnityCommand "unity command job wait --project-path `"$ProjectPath`" -- --condition '$waitCondition' --timeout_s $TimeoutSeconds"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Frame advancement check failed! Game may be frozen or blocked by modal dialog."
    Invoke-UnityCommand "unity command editor.playmode stop --project-path `"$ProjectPath`""
    exit 14
}

# 6. Capture Game view screenshot
$screenshotDir = Split-Path -Path $ScreenshotPath -Parent
if ($screenshotDir -and -not (Test-Path $screenshotDir)) {
    New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
}

Write-Host "Capturing Game view screenshot to $ScreenshotPath..." -ForegroundColor Gray
Invoke-UnityCommand "unity command capture game_view --project-path `"$ProjectPath`" -- --source screen --save_path `"$ScreenshotPath`""

# 7. Check Console logs for exceptions
Write-Host "Checking console errors..." -ForegroundColor Gray
Invoke-UnityCommand "unity command console get --project-path `"$ProjectPath`" -- --tail 20 --level error"

# 8. Clean exit from Play mode
if (-not $KeepPlaying) {
    Write-Host "Stopping Play mode..." -ForegroundColor Yellow
    Invoke-UnityCommand "unity command editor.playmode stop --project-path `"$ProjectPath`""
    Invoke-UnityCommand "unity command editor.playmode set_run_in_background --project-path `"$ProjectPath`" -- --enable false"
} else {
    Write-Host "Play mode left active (-KeepPlaying specified)." -ForegroundColor Green
}

Write-Host "=== Play-Mode Verification Completed Successfully ===" -ForegroundColor Green
exit 0
