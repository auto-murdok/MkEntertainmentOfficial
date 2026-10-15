<#
.SYNOPSIS
    Batch-export static meshes (FBX) + textures + one manifest from many Unreal
    folders in a single commandlet boot, without opening the editor UI.

.DESCRIPTION
    Same detached-launch/poll pattern as Run-Export.ps1, but loops
    ue/export_fbx_batch.py over AssetPaths so one editor boot covers a whole
    estate (avoids a ~2 min boot per folder). The shared import_manifest.csv
    accumulates across folders.

.PARAMETER UProject
    Path to the .uproject that owns the assets.

.PARAMETER AssetPaths
    /Game/... folder paths holding StaticMesh assets (non-recursive each).

.PARAMETER OutDir
    Output directory. Default: <uproject dir>\Exports\MansionProps

.PARAMETER Filter
    Optional case-insensitive name filter applied to every folder.

.PARAMETER TimeoutSec
    Give up waiting after this long. Default 5400 (90 min).

.EXAMPLE
    Tools\UEImport\Run-ExportBatch.ps1 -UProject "C:\...\MyProject.uproject" -AssetPaths @("/Game/Mansion/Mesh/Assets/Books", "/Game/Mansion/Mesh/Assets/Sconce01")
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UProject,
    [Parameter(Mandatory = $true)] [string[]] $AssetPaths,
    [string] $OutDir,
    [string] $Filter,
    [int] $TimeoutSec = 5400
)

$ErrorActionPreference = 'Stop'
$UProject = (Resolve-Path $UProject).Path
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $UProject -Parent) 'Exports\MansionProps' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# ---------------------------------------------------------------- engine lookup
$assoc = ((Get-Content $UProject -Raw | ConvertFrom-Json).EngineAssociation)
$engineCandidates = @()
$reg = Get-ItemProperty "HKLM:\SOFTWARE\EpicGames\Unreal Engine\$assoc" -ErrorAction SilentlyContinue
if ($reg -and $reg.InstalledDirectory) { $engineCandidates += (Join-Path $reg.InstalledDirectory 'Engine\Binaries\Win64\UnrealEditor-Cmd.exe') }
$engineCandidates += "C:\Program Files\Epic Games\UE_$assoc\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
$engineExe = $engineCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $engineExe) { throw "UnrealEditor-Cmd.exe not found for engine association '$assoc'. Tried: $($engineCandidates -join ', ')" }
Write-Host "Engine: $engineExe"

# ------------------------------------------------- bootstrap runner (env + exec)
$toolDir = $PSScriptRoot
$batchPy = Join-Path $toolDir 'ue\export_fbx_batch.py'
$genericPy = Join-Path $toolDir 'ue\export_fbx_and_map.py'
$joined = (($AssetPaths | ForEach-Object { "$_".Trim() }) -join ';')
$bootstrap = Join-Path $env:TEMP ("uei_batch_{0}.py" -f [guid]::NewGuid().ToString('N'))
@"
import os
os.environ['UEI_ASSET_PATHS'] = r'$joined'
os.environ['UEI_GENERIC_PY'] = r'$genericPy'
os.environ['UEI_OUT_DIR'] = r'$OutDir'
os.environ['UEI_FILTER'] = r'$Filter'
exec(compile(open(r'$batchPy', encoding='utf-8').read(), r'$batchPy', 'exec'))
"@ | Set-Content -Path $bootstrap -Encoding ASCII

# ------------------------------------------------------- detached launch (WMI)
$log = Join-Path $OutDir '_batch_log.txt'
$runCmd = Join-Path $env:TEMP ("uei_batch_run_{0}.cmd" -f [guid]::NewGuid().ToString('N'))
@"
@echo off
"$engineExe" "$UProject" -run=pythonscript -script="$bootstrap" -unattended -nop4 -nosplash -stdout > "$log" 2>&1
echo UEI_EXIT_CODE=%ERRORLEVEL% >> "$log"
"@ | Set-Content -Path $runCmd -Encoding ASCII

Write-Host ("Folders: {0} -> {1}" -f $AssetPaths.Count, $OutDir)
Write-Host "Launching UE commandlet (log: $log) ..."
$wmi = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "cmd.exe /c `"$runCmd`"" }
if ($wmi.ReturnValue -ne 0) { throw "Failed to spawn commandlet (Win32_Process.Create returned $($wmi.ReturnValue))" }
$procId = $wmi.ProcessId
Write-Host "Runner PID: $procId"
$projectLog = Join-Path (Split-Path $UProject -Parent) "Saved\Logs\$([IO.Path]::GetFileNameWithoutExtension($UProject)).log"

# ------------------------------------------------------------------ poll loop
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$sawFailed = $false
while ($true) {
    Start-Sleep -Seconds 60
    $alive = Get-Process -Id $procId -ErrorAction SilentlyContinue

    foreach ($candidateLog in @($log, $projectLog)) {
        if (Test-Path $candidateLog) {
            if (Select-String -Path $candidateLog -Pattern 'Python script executed with errors' -ErrorAction SilentlyContinue) {
                $sawFailed = $true
            }
        }
    }

    if (-not $alive) { break }
    if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
        Write-Warning "Timed out after $TimeoutSec s - killing runner tree (PID $procId)"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        $sawFailed = $true
        break
    }
    Write-Host ("waiting ... {0:mm\:ss} elapsed" -f $sw.Elapsed)
}

$tail = if (Test-Path $log) { Get-Content $log -Tail 8 } else { @('log file was not created') }
$tail | ForEach-Object { Write-Host $_ }

# ---------------------------------------------------------------- verification
$fbxCount = @(Get-ChildItem $OutDir -Filter *.fbx -ErrorAction SilentlyContinue).Count
$manifest = Join-Path $OutDir 'import_manifest.csv'
$rows = 0
if (Test-Path $manifest) {
    $rows = @(Import-Csv $manifest -ErrorAction SilentlyContinue).Count
}
Write-Host ''
Write-Host "Output: $OutDir"
Write-Host ("  FBX: {0}  manifest rows: {1}" -f $fbxCount, $rows)

Remove-Item $bootstrap, $runCmd -Force -ErrorAction SilentlyContinue

if (-not $sawFailed -and $fbxCount -gt 0 -and $rows -gt 0) {
    Write-Host 'UEI BATCH OK' -ForegroundColor Green
    exit 0
}
Write-Host 'UEI BATCH NOT OK' -ForegroundColor Red
exit 1
