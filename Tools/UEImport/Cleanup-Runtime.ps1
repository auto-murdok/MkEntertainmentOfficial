<#
.SYNOPSIS
    Kill the headless runtime processes the UEI pipeline leaves behind.

.DESCRIPTION
    Deterministically targets (identified by process snapshots of real
    pipeline runs - see docs/ue_content_import.md):
      1. Unity.exe editors launched with -automated for this project,
         including their full child tree (UnityPackageManager, licensing,
         shader compilers, UnityAutoQuitter, ILPP runner, crash handler)
      2. the unity-cli identity helper (unity.exe --internal-identity-serve)
      3. bun servers, matched precisely: process name starting with "bun"
         (bun.exe, bunServer.exe, ...) or bunServer/bun-server/bun.exe tokens
         in the command line - word-bounded, so paths like
         "C:\snapshot\bundle\..." never match

    Never touches: the current shell ancestry, opencode's node.exe MCP
    servers, Unity Hub, or any interactive Unity editor (no -automated flag).

.PARAMETER ProjectRoot
    Repo root used to match -automated editors by project path.
    Default: resolved from this script's location.

.PARAMETER DryRun
    List what would be killed without killing anything.

.EXAMPLE
    Tools\UEImport\Cleanup-Runtime.ps1
    Tools\UEImport\Cleanup-Runtime.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string] $ProjectRoot,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
if (-not $ProjectRoot) { $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')) }
$procPath = ([IO.Path]::GetFullPath($ProjectRoot)).TrimEnd('\').ToLowerInvariant()

$all = Get-CimInstance Win32_Process

function Get-ChildTree([string] $rootPid) {
    $found = New-Object System.Collections.Generic.List[object]
    foreach ($c in ($all | Where-Object { [string]$_.ParentProcessId -eq $rootPid })) {
        $found.Add($c)
        $kids = Get-ChildTree ([string]$c.ProcessId)
        if ($kids) { $found.AddRange($kids) }
    }
    return ,$found
}

# ------------------------------------------------------------------ select roots
$roots = @{}
foreach ($p in ($all | Where-Object { $_.Name -eq 'Unity.exe' })) {
    $cl = if ($p.CommandLine) { $p.CommandLine.ToLowerInvariant() } else { '' }
    if ($cl -match '-automated' -and $cl.Contains($procPath)) { $roots[[string]$p.ProcessId] = $p }
}
foreach ($p in ($all | Where-Object { $_.Name -eq 'unity.exe' -and $_.CommandLine -match 'internal-identity-serve' })) {
    $roots[[string]$p.ProcessId] = $p
}
foreach ($p in ($all | Where-Object {
        $_.Name -like 'bun*' -or
        ($_.CommandLine -and $_.CommandLine -match '(?i)\bbunServer\b|\bbun-server\b|\bbun\.exe\b')
    })) {
    $roots[[string]$p.ProcessId] = $p
}

# ------------------------------------------------- expand trees + safety guards
$kill = New-Object System.Collections.Generic.List[object]
$seen = @{}
foreach ($rootPid in @($roots.Keys)) {
    if ($seen.ContainsKey($rootPid)) { continue }
    $seen[$rootPid] = $true
    $kill.Add($roots[$rootPid])
    $kids = Get-ChildTree $rootPid
    if ($kids) {
        foreach ($c in $kids) {
            if (-not $seen.ContainsKey([string]$c.ProcessId)) {
                $seen[[string]$c.ProcessId] = $true
                $kill.Add($c)
            }
        }
    }
}

# never kill our own shell ancestry
$ancestry = @{}
$cur = $all | Where-Object { [string]$_.ProcessId -eq [string]$PID }
$depth = 0
while ($cur -and $depth -lt 8) {
    $ancestry[[string]$cur.ProcessId] = $true
    $cur = $all | Where-Object { [string]$_.ProcessId -eq [string]$cur.ParentProcessId }
    $depth++
}
$kill = [System.Collections.Generic.List[object]]@($kill | Where-Object { -not $ancestry.ContainsKey([string]$_.ProcessId) })

# ------------------------------------------------------------------- report/kill
if ($kill.Count -eq 0) {
    Write-Host 'UEI CLEANUP: nothing to kill (runtime already clean)'
    exit 0
}

foreach ($p in $kill) {
    $label = if ($p.CommandLine) { $p.CommandLine } else { $p.Name }
    if ($label.Length -gt 120) { $label = $label.Substring(0, 120) + '...' }
    if ($DryRun) {
        Write-Host "would kill PID $($p.ProcessId) [$($p.Name)] $label"
    } else {
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        Write-Host "killed PID $($p.ProcessId) [$($p.Name)] $label"
    }
}

if ($DryRun) {
    Write-Host "UEI CLEANUP DRYRUN: $($kill.Count) process(es) would be killed"
} else {
    Write-Host "UEI CLEANUP OK: $($kill.Count) process(es) killed"
}
