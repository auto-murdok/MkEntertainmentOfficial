<#
.SYNOPSIS
    Safe C# Script Evaluation in Unity Editor.
.DESCRIPTION
    Executes C# code against a running Unity Editor using 'unity command eval_file'.
    Avoids PowerShell string escaping, quote mangling, and flag collision issues
    by persisting incoming code to a clean temporary file before dispatch.
.PARAMETER Code
    C# code string to evaluate. Must use fully-qualified type names (no 'using' directives).
.PARAMETER ScriptFile
    Path to an existing C# file to evaluate directly.
.PARAMETER ProjectPath
    Target Unity project path (defaults to current working directory).
.PARAMETER TimeoutMs
    Execution timeout in milliseconds (default: 15000).
.EXAMPLE
    ./Tools/eval-cs.ps1 -Code 'return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'
.EXAMPLE
    "return UnityEngine.Time.frameCount;" | ./Tools/eval-cs.ps1
#>
[CmdletBinding(DefaultParameterSetName = "StringCode")]
param(
    [Parameter(ParameterSetName = "StringCode", Position = 0, ValueFromPipeline = $true)]
    [string]$Code,

    [Parameter(ParameterSetName = "FilePath")]
    [string]$ScriptFile,

    [string]$ProjectPath = $PSScriptRoot + "/..",
    [int]$TimeoutMs = 15000
)

$ErrorActionPreference = "Stop"

$tempFile = $null
try {
    if ($PSCmdlet.ParameterSetName -eq "FilePath") {
        if (-not (Test-Path $ScriptFile)) {
            Write-Error "Script file not found: $ScriptFile"
            exit 2
        }
        $targetFile = (Resolve-Path $ScriptFile).Path
    } else {
        if ([string]::IsNullOrWhiteSpace($Code)) {
            Write-Error "No C# code provided to evaluate."
            exit 2
        }

        $tempFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "unity_eval_" + [System.Guid]::NewGuid().ToString("N") + ".cs")
        [System.IO.File]::WriteAllText($tempFile, $Code, [System.Text.Encoding]::UTF8)
        $targetFile = $tempFile
    }

    Write-Host "Evaluating C# in Unity Editor ($targetFile)..." -ForegroundColor Cyan
    unity command eval_file --file "$targetFile" --project-path "$ProjectPath" -- --timeout $TimeoutMs
    exit $LASTEXITCODE
}
finally {
    if ($tempFile -and (Test-Path $tempFile)) {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
}
