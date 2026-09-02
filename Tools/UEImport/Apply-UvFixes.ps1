<#
.SYNOPSIS
    Apply configured UV fixes (rotated mesh UVs) to exported FBX files using
    headless Blender.

.DESCRIPTION
    Reads Tools\UEImport\uvfixes.json and, for every entry, patches the FBX in
    <ExportDir> by rotating UV0 on the faces of the named material slot. The
    first run backs up each FBX to <file>.orig - that backup is the rollback
    anchor and is never overwritten.

    Run AFTER Run-Export.ps1 whenever the config touches newly exported meshes.
    The Unity re-import picks up the patched FBX automatically (materials and
    prefab GUIDs are unaffected - only UVs change).

.PARAMETER ExportDir
    Folder containing the exported FBX files (Run-Export.ps1 output).

.PARAMETER Config
    Path to uvfixes.json. Default: Tools\UEImport\uvfixes.json

.PARAMETER DryRun
    List the configured fixes without running Blender.

.EXAMPLE
    Tools\UEImport\Apply-UvFixes.ps1
#>
[CmdletBinding()]
param(
    [string] $ExportDir = 'C:\Users\ljtinitanao\Documents\Unreal Projects\MyProject\Exports\Building_kit',
    [string] $Config,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$toolDir = $PSScriptRoot
if (-not $Config) { $Config = Join-Path $toolDir 'uvfixes.json' }
$blender = Join-Path $toolDir 'vendor\blender\blender.exe'
$uvfixPy = Join-Path $toolDir 'ue\uvfix.py'

if (-not (Test-Path $Config)) { throw "config not found: $Config" }
if (-not (Test-Path $blender)) { throw "blender not found: $blender - run: Tools\UEImport\cue4parse\setup-prereqs.ps1" }
if (-not (Test-Path $ExportDir)) { throw "export dir not found: $ExportDir" }

$fixes = Get-Content $Config -Raw | ConvertFrom-Json
foreach ($fix in $fixes) {
    $fbx = if ([IO.Path]::IsPathRooted($fix.fbx)) { $fix.fbx } else { Join-Path $ExportDir $fix.fbx }
    if (-not (Test-Path $fbx)) { Write-Warning "FBX missing, skipping: $fbx"; continue }
    if (-not (Test-Path "$fbx.orig")) {
        Copy-Item $fbx "$fbx.orig"
        Write-Host "backup: $fbx.orig"
    }
    Write-Host ("fix: {0} material~'{1}' rotate {2} deg" -f (Split-Path $fbx -Leaf), $fix.material, $fix.degrees)
}
if ($DryRun) { Write-Host 'UEI UVFIX DRYRUN'; exit 0 }

$env:UEI_FIXES = $Config
$env:UEI_EXPORT_DIR = $ExportDir
# route through cmd so blender's stderr banners don't kill the script
# (PowerShell 5.1 + $ErrorActionPreference=Stop treats native stderr as errors)
$blenderLog = Join-Path $ExportDir '_uvfix_log.txt'
cmd /c "`"$blender`" --background --factory-startup --python `"$uvfixPy`" > `"$blenderLog`" 2>&1"
Select-String -Path $blenderLog -Pattern 'UVFIX' | ForEach-Object { Write-Host $_.Line }

if ($LASTEXITCODE -ne 0) { throw "blender uvfix failed (exit $LASTEXITCODE)" }
Write-Host 'UEI UVFIX OK' -ForegroundColor Green
