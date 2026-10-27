<#
.SYNOPSIS
    Exports Unity Humanoid .anim clips baked into standard FBX files.
.DESCRIPTION
    Uses Game.Editor.Animation.HumanoidAnimToFbxExporter in the live Unity Editor
    via Tools/eval-cs.ps1 to bake and export FBX files headlessly.
.PARAMETER ClipPath
    Asset path to the Humanoid AnimationClip (e.g. Assets/_Game/Animations/Characters/Survivor/Animations/take_bite.anim).
.PARAMETER CharacterPrefabPath
    Optional character prefab path (defaults to FemaleCharacter.prefab).
.PARAMETER OutputPath
    Optional output FBX file path (defaults to Assets/_Game/Animations/ExportedFBX/<clip_name>_baked.fbx).
.PARAMETER IncludeMesh
    Whether to include the skinned mesh and materials (defaults to $false for skeleton+anim only).
.PARAMETER Binary
    Whether to export in binary FBX format (defaults to $true).
.PARAMETER FrameRate
    Optional frame rate override (defaults to 0 for clip's native frame rate).
.EXAMPLE
    ./Tools/export-anim-fbx.ps1 -ClipPath "Assets/_Game/Animations/Characters/Survivor/Animations/take_bite.anim"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ClipPath,

    [string]$CharacterPrefabPath = "Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab",
    [string]$OutputPath = "",
    [switch]$IncludeMesh,
    [bool]$Binary = $true,
    [float]$FrameRate = 0
)

$ErrorActionPreference = "Stop"

$evalScript = Join-Path $PSScriptRoot "eval-cs.ps1"
if (-not (Test-Path $evalScript)) {
    Write-Error "eval-cs.ps1 not found at: $evalScript"
    exit 1
}

$normClipPath = $ClipPath.Replace("\", "/")
$normCharPath = $CharacterPrefabPath.Replace("\", "/")
$normOutPath = $OutputPath.Replace("\", "/")
$incMeshBool = if ($IncludeMesh) { "true" } else { "false" }
$binBool = if ($Binary) { "true" } else { "false" }

$code = @"
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("$normClipPath");
if (clip == null) {
    throw new System.ArgumentException("Animation clip not found at path: $normClipPath");
}

var character = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("$normCharPath");
if (character == null) {
    throw new System.ArgumentException("Character prefab not found at path: $normCharPath");
}

var options = new Game.Editor.Animation.HumanoidAnimToFbxExporter.ExportOptions {
    outputPath = string.IsNullOrEmpty("$normOutPath") ? null : "$normOutPath",
    includeSkinnedMesh = $incMeshBool,
    exportBinary = $binBool,
    frameRateOverride = ${FrameRate}f
};

string exported = Game.Editor.Animation.HumanoidAnimToFbxExporter.ExportClip(clip, character, options);
if (string.IsNullOrEmpty(exported)) {
    throw new System.InvalidOperationException("FBX export failed (returned null or empty).");
}

var fi = new System.IO.FileInfo(exported);
return exported + " (" + fi.Length + " bytes)";
"@

Write-Host "Exporting '$ClipPath' to FBX..." -ForegroundColor Cyan
& $evalScript -Code $code -TimeoutMs 30000
if ($LASTEXITCODE -ne 0) {
    Write-Error "Export failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "Export completed successfully!" -ForegroundColor Green
