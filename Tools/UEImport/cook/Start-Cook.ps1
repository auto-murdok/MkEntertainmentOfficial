<#
.SYNOPSIS
    Cook an Unreal project to loose files (no Zen store), required before the
    CUE4Parse leg can read anything.

.DESCRIPTION
    Leg #2 step 1. UE 5.6+ cooks into Zen storage by default which external
    tools cannot read; -skipzenstore forces classic loose .uasset/.uexp/.ubulk
    output under <uproject>\Saved\Cooked\<Platform>\<ProjectName>\Content.

.PARAMETER UProject
    Path to the .uproject.

.PARAMETER Platform
    Cook target platform. Default: Windows.

.PARAMETER TimeoutSec
    Give up waiting after this long. Default 7200.

.EXAMPLE
    Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject\MyProject.uproject"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UProject,
    [string] $Platform = 'Windows',
    [int] $TimeoutSec = 7200
)

$ErrorActionPreference = 'Stop'
$UProject = (Resolve-Path $UProject).Path
$projectName = [IO.Path]::GetFileNameWithoutExtension($UProject)

$assoc = ((Get-Content $UProject -Raw | ConvertFrom-Json).EngineAssociation)
$engineCandidates = @()
$reg = Get-ItemProperty "HKLM:\SOFTWARE\EpicGames\Unreal Engine\$assoc" -ErrorAction SilentlyContinue
if ($reg -and $reg.InstalledDirectory) { $engineCandidates += (Join-Path $reg.InstalledDirectory 'Engine\Binaries\Win64\UnrealEditor-Cmd.exe') }
$engineCandidates += "C:\Program Files\Epic Games\UE_$assoc\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
$engineExe = $engineCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $engineExe) { throw "UnrealEditor-Cmd.exe not found for engine association '$assoc'." }

$log = Join-Path $env:TEMP ("uei_cook_{0}.log" -f [guid]::NewGuid().ToString('N'))
$runCmd = Join-Path $env:TEMP ("uei_cook_{0}.cmd" -f [guid]::NewGuid().ToString('N'))
@"
@echo off
"$engineExe" "$UProject" -run=cook -targetplatform=$Platform -CookAll -skipzenstore -unattended -nop4 -nosplash -stdout > "$log" 2>&1
echo UEI_COOK_EXIT_CODE=%ERRORLEVEL% >> "$log"
"@ | Set-Content -Path $runCmd -Encoding ASCII

Write-Host "Cooking $projectName ($Platform) - log: $log"
$wmi = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "cmd.exe /c `"$runCmd`"" }
if ($wmi.ReturnValue -ne 0) { throw "Failed to spawn cook (Win32_Process.Create returned $($wmi.ReturnValue))" }
$procId = $wmi.ProcessId

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$lastCooked = -1
while ($true) {
    Start-Sleep -Seconds 45
    $alive = Get-Process -Id $procId -ErrorAction SilentlyContinue
    $exitLine = $null
    if (Test-Path $log) {
        $exitLine = Select-String -Path $log -Pattern 'UEI_COOK_EXIT_CODE' | Select-Object -Last 1 -ExpandProperty Line -ErrorAction SilentlyContinue
        $cookedLine = Select-String -Path $log -Pattern 'Cooked packages (\d+) Packages Remain (\d+) Total' |
                      Select-Object -Last 1
        if ($cookedLine -and $cookedLine.Matches[0].Groups[1].Value -ne $lastCooked) {
            $lastCooked = $cookedLine.Matches[0].Groups[1].Value
            $remain = $cookedLine.Matches[0].Groups[2].Value
            Write-Host ("[{0:mm\:ss}] cooked {1}, remaining {2}" -f $sw.Elapsed, $lastCooked, $remain)
        }
    }
    if ($exitLine) { break }
    if (-not $alive) { break }
    if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
        Write-Warning "Cook timed out after $TimeoutSec s - killing runner (PID $procId)"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        break
    }
}

$cookedRoot = Join-Path (Split-Path $UProject -Parent) "Saved\Cooked\$Platform\$projectName\Content"
$uassetCount = @(Get-ChildItem $cookedRoot -Recurse -Filter *.uasset -ErrorAction SilentlyContinue).Count
Get-Content $log -Tail 8 | ForEach-Object { Write-Host $_ }
Write-Host ''
Write-Host "Cooked content root: $cookedRoot"
Write-Host "Loose .uasset files: $uassetCount"
Remove-Item $runCmd -Force -ErrorAction SilentlyContinue

if ($uassetCount -gt 0) {
    Write-Host 'UEI COOK OK' -ForegroundColor Green
    exit 0
}
Write-Host 'UEI COOK NOT OK' -ForegroundColor Red
exit 1
