<#
.SYNOPSIS
    Leg #2 conversion: cooked UE output -> glTF (CUE4Parse) -> FBX (Blender).

.DESCRIPTION
    Consumes loose cooked output from Start-Cook.ps1 and produces an output
    folder with the SAME shape as Run-Export.ps1 (leg #1), so the Unity
    importer treats both identically:
        <OutDir>\<Mesh>.fbx
        <OutDir>\<Texture>.png
        <OutDir>\import_manifest.csv

    Material rows are resolved from cooked material instances. Plain Material
    assets carry no readable texture parameters, so some materials may have no
    texture rows - the Unity importer falls back to convention matching and
    logs what it could not wire.

.PARAMETER CookedContent
    Path to <uproject>\Saved\Cooked\<Platform>\<ProjectName>\Content.

.PARAMETER OutDir
    Output directory (FBX + PNG + manifest).

.PARAMETER Filter
    Substring filter on package paths, e.g. "Building_kit". Required to keep
    scope sane; cook output contains the whole project.

.PARAMETER Game
    CUE4Parse EGame version. Default GAME_UE5_8 - match your engine build.

.EXAMPLE
    Tools\UEImport\cue4parse\Convert-Cooked.ps1 -CookedContent "C:\...\Saved\Cooked\Windows\MyProject\Content" -Filter "Building_kit" -OutDir "C:\...\Exports\cue4parse_fbx"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $CookedContent,
    [Parameter(Mandatory = $true)] [string] $Filter,
    [Parameter(Mandatory = $true)] [string] $OutDir,
    [string] $Game = 'GAME_UE5_8'
)

$ErrorActionPreference = 'Stop'
$CookedContent = (Resolve-Path $CookedContent).Path
$vendor = Join-Path $PSScriptRoot '..\vendor'
$dotnet = Join-Path $vendor 'dotnet\dotnet.exe'
$blender = Join-Path $vendor 'blender\blender.exe'
$cue4parse = Join-Path $vendor 'CUE4Parse\CUE4Parse\CUE4Parse.csproj'

if (-not (Test-Path $dotnet))     { throw "dotnet not found at $dotnet - run: Tools\UEImport\cue4parse\setup-prereqs.ps1" }
if (-not (Test-Path $blender))    { throw "blender not found at $blender - run: Tools\UEImport\cue4parse\setup-prereqs.ps1" }
if (-not (Test-Path $cue4parse))  { throw "CUE4Parse sources not found at $cue4parse - run: Tools\UEImport\cue4parse\setup-prereqs.ps1" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$glbDir = Join-Path $OutDir '_glb'
New-Item -ItemType Directory -Force -Path $glbDir | Out-Null

$env:DOTNET_ROOT = Join-Path $vendor 'dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

Write-Host '[1/3] CUE4Parse: cooked -> glTF ...'
& $dotnet run --project (Join-Path $PSScriptRoot 'Cue4ParseExport') -c Release -- `
    --content "$CookedContent" --filter "$Filter" --out "$glbDir" --game "$Game"
if ($LASTEXITCODE -ne 0) { throw "CUE4Parse export failed (exit $LASTEXITCODE)" }
# the converter writes the manifest next to the glbs; the consumer expects it
# at the output root
Move-Item (Join-Path $glbDir 'import_manifest.csv') $OutDir -Force

Write-Host '[2/3] Blender: glTF -> FBX ...'
$env:GLB_IN = $glbDir
$env:GLB_OUT = $OutDir
$blenderLog = Join-Path $OutDir '_blender_log.txt'
# route through cmd so blender's stderr banners don't kill the script
# (PowerShell 5.1 + $ErrorActionPreference=Stop treats native stderr as errors)
cmd /c "`"$blender`" --background --factory-startup --python `"$(Join-Path $PSScriptRoot 'glb2fbx.py')`" > `"$blenderLog`" 2>&1"
Select-String -Path $blenderLog -Pattern 'UEI CONVERTED|UEI FBXFAIL' | ForEach-Object { Write-Host $_.Line }

Write-Host '[3/3] Copying textures ...'
Get-ChildItem $glbDir -Recurse -Filter *.png | ForEach-Object { Copy-Item $_.FullName $OutDir -Force }

$fbxCount = @(Get-ChildItem $OutDir -Filter *.fbx).Count
$pngCount = @(Get-ChildItem $OutDir -Filter *.png).Count
$manifestOk = Test-Path (Join-Path $OutDir 'import_manifest.csv')
Write-Host ''
Write-Host "Output: $OutDir"
Write-Host "  FBX: $fbxCount  PNG: $pngCount  manifest: $manifestOk"

if ($fbxCount -gt 0 -and $manifestOk) {
    Write-Host 'UEI CONVERT OK' -ForegroundColor Green
    exit 0
}
Write-Host 'UEI CONVERT NOT OK' -ForegroundColor Red
exit 1
