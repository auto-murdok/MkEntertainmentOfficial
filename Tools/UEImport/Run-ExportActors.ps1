<#
.SYNOPSIS
    Dump a level's actor inventory (mesh + transform per actor) to JSON without
    opening the editor UI. Feeds the Blender faithful-rebuild pass.

.DESCRIPTION
    Launches the UE pythonscript commandlet detached (survives shell timeouts),
    running ue/export_level_actors.py: loads the map, iterates level actors,
    writes {label, class, kind, mesh, loc_cm, rot_pyr, scale} per actor.

    NOTE on World Partition: actors that are not streamed in are invisible to
    the dump. Compare the dumped total against the map's __ExternalActors__
    file count; if short, rerun with the map open in the editor UI.

.PARAMETER UProject
    Path to the .uproject that owns the level.

.PARAMETER MapPath
    /Game/... path to the level (.umap suffix optional).

.PARAMETER OutJson
    Output JSON path. Default: <uproject dir>\Exports\level_actors\<MapLeaf>_actors.json

.PARAMETER HismCap
    Max instanced-mesh instances dumped per component (0 = all).

.PARAMETER TimeoutSec
    Give up waiting for the commandlet after this long. Default 1800.

.EXAMPLE
    Tools\UEImport\Run-ExportActors.ps1 -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" -MapPath "/Game/Mansion/Levels/LV_Haunted_Mansion"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UProject,
    [Parameter(Mandatory = $true)] [string] $MapPath,
    [string] $OutJson,
    [int] $HismCap = 0,
    [int] $TimeoutSec = 1800
)

$ErrorActionPreference = 'Stop'
$UProject = (Resolve-Path $UProject).Path
$leaf = $MapPath.TrimEnd('/').Split('/')[-1] -replace '\.umap$', ''
if (-not $OutJson) { $OutJson = Join-Path (Split-Path $UProject -Parent) ("Exports\level_actors\" + $leaf + "_actors.json") }
$OutDir = Split-Path $OutJson -Parent
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
$genericPy = Join-Path $toolDir 'ue\export_level_actors.py'
$bootstrap = Join-Path $env:TEMP ("uei_actors_{0}.py" -f [guid]::NewGuid().ToString('N'))
@"
import os
os.environ['UEI_MAP_PATH'] = r'$MapPath'
os.environ['UEI_OUT_JSON'] = r'$OutJson'
os.environ['UEI_HISM_CAP'] = r'$HismCap'
exec(compile(open(r'$genericPy', encoding='utf-8').read(), r'$genericPy', 'exec'))
"@ | Set-Content -Path $bootstrap -Encoding ASCII

# ------------------------------------------------------- detached launch (WMI)
$log = Join-Path $OutDir '_actors_log.txt'
$runCmd = Join-Path $env:TEMP ("uei_actors_run_{0}.cmd" -f [guid]::NewGuid().ToString('N'))
@"
@echo off
"$engineExe" "$UProject" -run=pythonscript -script="$bootstrap" -unattended -nop4 -nosplash -stdout > "$log" 2>&1
echo UEI_EXIT_CODE=%ERRORLEVEL% >> "$log"
"@ | Set-Content -Path $runCmd -Encoding ASCII

Write-Host "Loading map: $MapPath"
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
    Start-Sleep -Seconds 30
    $alive = Get-Process -Id $procId -ErrorAction SilentlyContinue

    foreach ($candidateLog in @($log, $projectLog)) {
        if (Test-Path $candidateLog) {
            if (Select-String -Path $candidateLog -Pattern 'UEI ACTORS FAILED|Python script executed with errors' -ErrorAction SilentlyContinue) {
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

$tail = if (Test-Path $log) { Get-Content $log -Tail 15 } else { @('log file was not created') }
$tail | ForEach-Object { Write-Host $_ }

# ---------------------------------------------------------------- verification
$dumpOk = $false
$total = 0
if (Test-Path $OutJson) {
    try {
        $dump = Get-Content $OutJson -Raw | ConvertFrom-Json
        $total = $dump.actors.Count
        $sm = ($dump.actors | Where-Object { $_.kind -eq 'staticmesh' }).Count
        Write-Host ("Dump: total=$total sm=$sm meshes=$($dump.mesh_names.Count)")
        $dumpOk = $total -gt 0
    } catch {
        Write-Warning "OutJson did not parse: $_"
    }
}

Remove-Item $bootstrap, $runCmd -Force -ErrorAction SilentlyContinue

if (-not $sawFailed -and $dumpOk) {
    Write-Host 'UEI ACTORS OK' -ForegroundColor Green
    exit 0
}
Write-Host 'UEI ACTORS NOT OK' -ForegroundColor Red
exit 1
