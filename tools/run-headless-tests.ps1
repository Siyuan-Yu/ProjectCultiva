# Headless EditMode test runner (Mono) for environments where the Unity Test Runner cannot run
# (project open in the interactive Editor, or Unity licensing IPC unavailable in batchmode).
#
# Compiles XianXia.Tests.dll with tools/offline-compile.ps1 -DropUnityEditor (so the tests use the
# XIANXIA_BASEGAME / HeadlessContentRoot injection instead of UnityEngine.Application.dataPath),
# builds tools/headless-tests/Program.cs with Mono's mcs and executes the selected test classes.
#
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/run-headless-tests.ps1 [-Filter A,B]

param(
  [string]$Project = "",
  [string[]]$Filter = @()
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Project)) {
  $Project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$mono = "D:\UnityEditor\2022.3.6f1\Editor\Data\MonoBleedingEdge"
$mcs = Join-Path $mono "lib\mono\4.5\mcs.exe"
$monoExe = Join-Path $mono "bin\mono.exe"
$sa = Join-Path $Project "Library\ScriptAssemblies"
$nunit = Join-Path $Project "Library\PackageCache\com.unity.ext.nunit@1.0.6\net35\unity-custom\nunit.framework.dll"
$scratch = Join-Path $Project "Temp\offline-compile"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$runner = Join-Path $scratch "testrunner.exe"

if (-not (Test-Path $mcs)) { Write-Output "MCS_NOT_FOUND=$mcs"; exit 1 }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "offline-compile.ps1") `
  -Project $Project -Only XianXia.Tests -DropUnityEditor | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Output "TEST_ASSEMBLY_COMPILE_FAILED"; exit 1 }

$compileArgs = @(
  "-target:exe", "-out:$runner", "-langversion:latest",
  "-r:System.Core.dll", "-r:System.dll",
  "-r:$nunit",
  "-r:$sa\XianXia.Tests.dll", "-r:$sa\XianXia.Core.dll", "-r:$sa\XianXia.Data.dll", "-r:$sa\XianXia.Unity.dll",
  (Join-Path $PSScriptRoot "headless-tests\Program.cs")
)
& $monoExe $mcs @compileArgs
if (-not (Test-Path $runner)) { Write-Output "RUNNER_COMPILE_FAILED"; exit 1 }

$env:XIANXIA_BASEGAME = Join-Path $Project "Content\BaseGame"
$runArgs = @($runner)
foreach ($entry in $Filter) {
  foreach ($part in ($entry -split ',')) {
    if (-not [string]::IsNullOrWhiteSpace($part)) { $runArgs += $part.Trim() }
  }
}
& $monoExe @runArgs
exit $LASTEXITCODE
