# Offline Roslyn compile using the Unity Bee response files.
#
# Unity batchmode is unusable while the project is open in the interactive Editor, and Unity's
# licensing IPC channel is unavailable in some sandboxes.  This script reuses the exact compiler
# response files Unity generated (same defines, same references, same analyzers) and only
# regenerates the source list, so the offline result matches what Unity would compile.
#
# Usage:  pwsh -File tools/offline-compile.ps1 [-Only XianXia.Core,XianXia.Data]

param(
  [string]$Project = "",
  [string[]]$Only = @(),
  [switch]$DropUnityEditor
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Project)) {
  $Project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$csc = "D:\UnityEditor\2022.3.6f1\Editor\Data\DotNetSdkRoslyn\csc.dll"
$bee = Join-Path $Project "Library\Bee\artifacts\1900b0aEDbg.dag"
$outDir = Join-Path $Project "Library\ScriptAssemblies"

if (-not (Test-Path $csc)) { Write-Output "CSC_NOT_FOUND=$csc"; exit 1 }
if (-not (Test-Path $bee)) { Write-Output "BEE_RSP_DIR_NOT_FOUND=$bee"; exit 1 }

# assembly -> source folders (relative to the project root)
$sourceMap = [ordered]@{
  "XianXia.Core"          = @("Assets/Scripts/Core")
  "XianXia.Data"          = @("Assets/Scripts/Data")
  "XianXia.Unity"         = @("Assets/Scripts/Unity")
  "XianXia.Unity.Editor"  = @("Assets/Scripts/Unity/Editor")
  "XianXia.Tests"         = @("Assets/Tests/EditMode")
  "XianXia.PlayModeTests" = @("Assets/Tests/PlayMode")
  "Assembly-CSharp"       = @("Assets/Scripts/Runtime")
  "Assembly-CSharp-Editor"= @("Assets/Editor")
}

$scratch = Join-Path $Project "Temp\offline-compile"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

$failed = @()
foreach ($name in $sourceMap.Keys) {
  if ($Only.Count -gt 0 -and ($Only -notcontains $name)) { continue }
  $rsp = Join-Path $bee "$name.rsp"
  if (-not (Test-Path $rsp)) { Write-Output "$name : RSP_MISSING"; $failed += $name; continue }

  $lines = Get-Content $rsp
  $options = $lines | Where-Object { $_ -notmatch '^"' }
  if ($DropUnityEditor -and $name -eq "XianXia.Tests") {
    # Headless NUnit runs outside Unity: the UNITY_EDITOR branch reaches
    # UnityEngine.Application.dataPath (an internal call) and the #else branch reads
    # XIANXIA_BASEGAME instead.
    $options = $options | Where-Object { $_ -notmatch '^-define:UNITY_EDITOR' }
  }
  $excluded = @()
  if ($name -eq "XianXia.Unity") { $excluded = @("Assets/Scripts/Unity/Editor") }

  $sources = @()
  foreach ($folder in $sourceMap[$name]) {
    $full = Join-Path $Project $folder
    if (-not (Test-Path $full)) { continue }
    $sources += Get-ChildItem $full -Recurse -File -Filter *.cs |
      ForEach-Object { $_.FullName.Substring($Project.Length + 1).Replace('\', '/') }
  }
  if ($excluded.Count -gt 0) {
    $sources = $sources | Where-Object {
      $rel = $_
      -not ($excluded | Where-Object { $rel.StartsWith($_, [System.StringComparison]::Ordinal) })
    }
  }
  $sources = $sources | Sort-Object -Unique

  $newRsp = Join-Path $scratch "$name.rsp"
  $body = @()
  $body += $options
  $body += ($sources | ForEach-Object { '"' + $_ + '"' })
  [System.IO.File]::WriteAllLines($newRsp, $body)

  Write-Output "=== $name : $($sources.Count) sources ==="
  & dotnet exec $csc "@$newRsp" 2>&1 | ForEach-Object { $_ } | Where-Object { $_ -match 'error|warning CS' } |
    Select-Object -First 60 | ForEach-Object { Write-Output $_ }
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { Write-Output "$name : COMPILE_FAILED exit=$exit"; $failed += $name; continue }

  $built = Join-Path $bee "$name.dll"
  if (Test-Path $built) { Copy-Item $built (Join-Path $outDir "$name.dll") -Force }
  Write-Output "$name : OK"
}

if ($failed.Count -gt 0) { Write-Output "FAILED=$($failed -join ',')"; exit 1 }
Write-Output "ALL_OK"
exit 0
