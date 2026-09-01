<#
.SYNOPSIS
    Install the leg-#2 prerequisites into Tools\UEImport\vendor (gitignored).

.DESCRIPTION
    Downloads/installs, skipping anything already present:
      1. Portable .NET 10 SDK            -> vendor\dotnet
      2. Portable Blender 4.2 LTS        -> vendor\blender
      3. CUE4Parse sources (recursive)   -> vendor\CUE4Parse

    Nothing is installed machine-wide; deleting the vendor folder removes
    everything.
#>
[CmdletBinding()]
param(
    [string] $VendorDir
)

$ErrorActionPreference = 'Stop'
if (-not $VendorDir) { $VendorDir = Join-Path $PSScriptRoot '..\vendor' }
$VendorDir = [IO.Path]::GetFullPath($VendorDir)
New-Item -ItemType Directory -Force -Path $VendorDir | Out-Null
Write-Host "Vendor dir: $VendorDir"

# ---------------------------------------------------------------- 1. dotnet 10
$dotnetExe = Join-Path $VendorDir 'dotnet\dotnet.exe'
if (Test-Path $dotnetExe) {
    Write-Host "[skip] dotnet already present: $dotnetExe"
} else {
    Write-Host '[get ] portable .NET 10 SDK (~300 MB) ...'
    $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    & powershell -ExecutionPolicy Bypass -File $installer -Channel 10.0 -InstallDir (Join-Path $VendorDir 'dotnet') -NoPath
    if ($LASTEXITCODE -ne 0) { throw 'dotnet install failed' }
}

# ---------------------------------------------------------------- 2. blender
$blenderExe = Join-Path $VendorDir 'blender\blender.exe'
if (Test-Path $blenderExe) {
    Write-Host "[skip] blender already present: $blenderExe"
} else {
    $ver = '4.2.9'
    $zipUrl = "https://mirrors.dotsrc.org/blender/release/Blender4.2/blender-$ver-windows-x64.zip"
    $zip = Join-Path $VendorDir 'blender.zip'
    Write-Host "[get ] blender $ver (~370 MB, dotsrc mirror) ..."
    & curl.exe -L --retry 3 -o $zip $zipUrl
    if ($LASTEXITCODE -ne 0) { throw 'blender download failed' }
    Write-Host '[get ] extracting blender ...'
    & "$env:SystemRoot\System32\tar.exe" -xf $zip -C $VendorDir
    if ($LASTEXITCODE -ne 0) { throw 'blender extraction failed' }
    Move-Item (Join-Path $VendorDir "blender-$ver-windows-x64") (Join-Path $VendorDir 'blender') -Force
    Remove-Item $zip -Force
    if (-not (Test-Path $blenderExe)) { throw "blender.exe not found after extraction ($blenderExe)" }
}

# ---------------------------------------------------------------- 3. CUE4Parse
$cue4parseCore = Join-Path $VendorDir 'CUE4Parse\CUE4Parse\CUE4Parse.csproj'
if (Test-Path $cue4parseCore) {
    Write-Host "[skip] CUE4Parse already present: $cue4parseCore"
} else {
    Write-Host '[get ] cloning CUE4Parse (recursive) ...'
    & git clone --recursive --depth 1 https://github.com/FabianFG/CUE4Parse.git (Join-Path $VendorDir 'CUE4Parse')
    if ($LASTEXITCODE -ne 0) { throw 'CUE4Parse clone failed' }
}

Write-Host ''
Write-Host 'UEI PREREQS OK' -ForegroundColor Green
Write-Host "  dotnet    : $dotnetExe"
Write-Host "  blender   : $blenderExe"
Write-Host "  CUE4Parse : $cue4parseCore"
