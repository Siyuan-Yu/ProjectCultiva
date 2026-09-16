[CmdletBinding()]
param(
  [switch]$ValidateOnly,
  [string]$LogPath
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$buildRoot = Join-Path $root ".build"
$appsRoot = Join-Path $root "Apps"
$preserveBuildRoot = $false

function Write-BuildLog {
  param([Parameter(Mandatory)][string]$Message)
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message"
  Write-Host $line
  if (-not [string]::IsNullOrWhiteSpace($LogPath)) { Add-Content -LiteralPath $LogPath -Value $line -Encoding utf8 }
}

try {
  Set-Location $root
  Import-Module (Join-Path $root "EditorManifest.psm1") -Force
  $manifest = Get-ContentAuthoringEditorManifest -Root $root
  $editors = @($manifest.editors | Where-Object { $_.PublishByDefault })
  if ($editors.Count -eq 0) { throw "No default editors are configured in the manifest." }
  Write-BuildLog "Manifest validated: $($editors.Count) default editor(s)."
  foreach ($editor in $editors) { Write-BuildLog "Manifest editor: $($editor.EditorName) [$($editor.LifecycleStatus)] -> $($editor.ProjectPath)" }
  if ($ValidateOnly) { Write-BuildLog "Validation completed with exit code 0."; exit 0 }

  New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
  $runId = [guid]::NewGuid().ToString("N")
  $stagingRoot = Join-Path $buildRoot "publish-staging-$runId"
  $candidateApps = Join-Path $stagingRoot "Apps"
  $backupApps = Join-Path $buildRoot "apps-backup-$runId"
  New-Item -ItemType Directory -Path $candidateApps -Force | Out-Null

  foreach ($editor in $editors) {
    $projectPath = Join-Path $root $editor.ProjectPath
    Write-BuildLog "Publishing $($editor.EditorName) to staging."
    & dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $candidateApps
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($editor.EditorName) with exit code $LASTEXITCODE." }
    $expectedExe = Join-Path $candidateApps "$($editor.EditorName).exe"
    if (-not (Test-Path -LiteralPath $expectedExe -PathType Leaf)) { throw "Expected executable is missing: $expectedExe" }
    Write-BuildLog "Published $($editor.EditorName) successfully."
  }

  Get-ChildItem -LiteralPath $candidateApps -File | Where-Object { $_.Extension -ne ".exe" } | Remove-Item -Force
  Set-Content -LiteralPath (Join-Path $candidateApps "README.txt") -Value "Open editors directly from this directory. Run the Build All command for the complete daily build. Do not start an editor from .build or project bin directories." -Encoding utf8

  $oldAppsMoved = $false
  try {
    Write-BuildLog "Switching complete candidate Apps directory into place."
    if (Test-Path -LiteralPath $appsRoot) { Move-Item -LiteralPath $appsRoot -Destination $backupApps -ErrorAction Stop; $oldAppsMoved = $true }
    Move-Item -LiteralPath $candidateApps -Destination $appsRoot -ErrorAction Stop
    Write-BuildLog "Apps switch succeeded."
  }
  catch {
    $switchError = $_
    Write-BuildLog "Apps switch failed: $($switchError.Exception.Message)"
    if ($oldAppsMoved -and -not (Test-Path -LiteralPath $appsRoot) -and (Test-Path -LiteralPath $backupApps)) {
      try { Move-Item -LiteralPath $backupApps -Destination $appsRoot -ErrorAction Stop; Write-BuildLog "Apps rollback succeeded." }
      catch { $preserveBuildRoot = $true; throw "Apps switch and rollback both failed. Previous Apps is preserved at: $backupApps" }
    }
    throw $switchError
  }

  Write-BuildLog "Build All completed with exit code 0."
  exit 0
}
catch {
  Write-BuildLog "ERROR: $($_.Exception.Message)"
  Write-BuildLog "Build All completed with exit code 1."
  exit 1
}
finally {
  if (-not $preserveBuildRoot -and (Test-Path -LiteralPath $buildRoot)) { Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
