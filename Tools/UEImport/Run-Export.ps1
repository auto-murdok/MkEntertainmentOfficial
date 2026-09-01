<#
.SYNOPSIS
    Export static meshes (FBX) + referenced textures + material manifest from an
    Unreal project without opening the editor UI.

.DESCRIPTION
    Leg #1 of the UEI pipeline. Launches the UE pythonscript commandlet detached
    (survives shell timeouts), polls the log until a completion marker appears,
    then verifies the output folder.

    Output folder shape (consumed by the Unity importer, Tools menu:
    Tools > UE Import > Import FBX Folder...):
        <OutDir>\<Mesh>.fbx
        <OutDir>\<Texture>.png
        <OutDir>\import_manifest.csv

.PARAMETER UProject
    Path to the .uproject that owns the assets.

.PARAMETER AssetPath
    /Game/... path to a single StaticMesh asset or a folder containing them.

.PARAMETER OutDir
    Output directory. Default: <uproject dir>\Exports\ue_fbx

.PARAMETER Filter
    Optional case-insensitive name filter applied when AssetPath is a folder.

.PARAMETER TimeoutSec
    Give up waiting for the commandlet after this long. Default 1800.

.EXAMPLE
    Tools\UEImport\Run-Export.ps1 -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit" -Filter roof01
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UProject,
    [Parameter(Mandatory = $true)] [string] $AssetPath,
    [string] $OutDir,
    [string] $Filter,
    [int] $TimeoutSec = 1800
)

$ErrorActionPreference = 'Stop'
$UProject = (Resolve-Path $UProject).Path
# default output: <uproject>\Exports\<AssetPathLeaf> so the folder name doubles
# as the kit name used by the Unity importer (Assets/ImportedContent/<kitName>)
$leaf = $AssetPath.TrimEnd('/').Split('/')[-1]
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $UProject -Parent) ("Exports\" + $leaf) }
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
# UE pythonscript commandlets do not reliably inherit caller environment
# variables, and WMI-detached processes inherit none at all - so the generic
# exporter script is exec'd from a per-run bootstrap that sets the config.
$toolDir = $PSScriptRoot
$genericPy = Join-Path $toolDir 'ue\export_fbx_and_map.py'
$bootstrap = Join-Path $env:TEMP ("uei_runner_{0}.py" -f [guid]::NewGuid().ToString('N'))
@"
import os
os.environ['UEI_ASSET_PATH'] = r'$AssetPath'
os.environ['UEI_OUT_DIR'] = r'$OutDir'
os.environ['UEI_FILTER'] = r'$Filter'
exec(compile(open(r'$genericPy', encoding='utf-8').read(), r'$genericPy', 'exec'))
"@ | Set-Content -Path $bootstrap -Encoding ASCII

# ------------------------------------------------------- detached launch (WMI)
$log = Join-Path $OutDir '_export_log.txt'
$runCmd = Join-Path $env:TEMP ("uei_run_{0}.cmd" -f [guid]::NewGuid().ToString('N'))
@"
@echo off
"$engineExe" "$UProject" -run=pythonscript -script="$bootstrap" -unattended -nop4 -nosplash -stdout > "$log" 2>&1
echo UEI_EXIT_CODE=%ERRORLEVEL% >> "$log"
"@ | Set-Content -Path $runCmd -Encoding ASCII

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

    # UEI markers can land in either the redirected stdout or the project log
    foreach ($candidateLog in @($log, $projectLog)) {
        if (Test-Path $candidateLog) {
            if (Select-String -Path $candidateLog -Pattern 'UEI EXPORT FAILED|Python script executed with errors' -ErrorAction SilentlyContinue) {
                $sawFailed = $true
            }
        }
    }

    # success/failure is decided from OUTPUTS after the runner exits (markers
    # are unreliable: unreal.log may go to either sink)
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
$fbxCount = @(Get-ChildItem $OutDir -Filter *.fbx -ErrorAction SilentlyContinue).Count
$pngCount = @(Get-ChildItem $OutDir -Filter *.png -ErrorAction SilentlyContinue).Count
$manifest = Join-Path $OutDir 'import_manifest.csv'
$manifestOk = Test-Path $manifest
Write-Host ''
Write-Host "Output: $OutDir"
Write-Host "  FBX: $fbxCount  PNG: $pngCount  manifest: $manifestOk"

Remove-Item $bootstrap, $runCmd -Force -ErrorAction SilentlyContinue

if (-not $sawFailed -and $fbxCount -gt 0 -and $manifestOk) {
    Write-Host 'UEI EXPORT OK' -ForegroundColor Green
    exit 0
}
Write-Host 'UEI EXPORT NOT OK' -ForegroundColor Red
exit 1
