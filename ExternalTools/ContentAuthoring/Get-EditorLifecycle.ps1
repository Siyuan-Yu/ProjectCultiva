param([Parameter(Mandatory)][string]$EditorName)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Import-Module (Join-Path $root "EditorManifest.psm1") -Force
$manifest = Get-ContentAuthoringEditorManifest -Root $root
$editor = @($manifest.editors | Where-Object { $_.EditorName -ceq $EditorName })
if ($editor.Count -ne 1) { throw "Editor is missing or duplicated in manifest: $EditorName" }
if ($editor[0].LifecycleStatus -eq "Legacy Compatibility") {
    $replacementText = if ([string]::IsNullOrWhiteSpace([string]$editor[0].Replacement)) { "No replacement is locked yet." } else { "Future replacement: $($editor[0].Replacement)." }
    Write-Host "[Legacy Compatibility] $EditorName. $($editor[0].Notes) $replacementText"
}
