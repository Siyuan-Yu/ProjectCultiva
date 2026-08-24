param(
  [string]$Project = "",
  [string]$Label = "m1",
  [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Project)) {
  $Project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
$unity = "D:\UnityEditor\2022.3.6f1\Editor\Unity.exe"
if (-not (Test-Path $unity)) {
  Write-Output "UNITY_NOT_FOUND=$unity"
  exit 1
}
$logs = Join-Path $Project "Logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$compileLog = Join-Path $logs "$Label-compile.log"
$testLog = Join-Path $logs "$Label-editmode-tests.log"
$testResults = Join-Path $logs "$Label-editmode-results.xml"

Write-Output "PROJECT=$Project"

function Wait-UnityFree {
  for ($i = 0; $i -lt 90; $i++) {
    $u = Get-Process Unity -EA SilentlyContinue
    $lock = Test-Path (Join-Path $Project "Temp\UnityLockfile")
    if (-not $u -and -not $lock) { return }
    Start-Sleep 2
  }
  if (-not (Get-Process Unity -EA SilentlyContinue)) {
    Remove-Item (Join-Path $Project "Temp\UnityLockfile") -Force -EA SilentlyContinue
  }
}

Wait-UnityFree
Remove-Item $compileLog, $testLog, $testResults -Force -EA SilentlyContinue

$p = Start-Process -FilePath $unity -ArgumentList @("-batchmode","-nographics","-projectPath",$Project,"-quit","-logFile",$compileLog) -Wait -PassThru -NoNewWindow
$errors = @()
if (Test-Path $compileLog) { $errors = @(Select-String -Path $compileLog -Pattern "error CS") }
Write-Output "COMPILE_EXIT=$($p.ExitCode) ERROR_CS=$($errors.Count)"
if ($p.ExitCode -ne 0 -or $errors.Count -gt 0) {
  $errors | Select-Object -Last 30 | ForEach-Object { $_.Line }
  exit 1
}

Wait-UnityFree
$testArgs = @("-batchmode","-nographics","-projectPath",$Project,"-runTests","-testPlatform","editmode","-testResults",$testResults,"-logFile",$testLog)
if (-not [string]::IsNullOrWhiteSpace($Filter)) {
  $testArgs += @("-testFilter", $Filter)
}
$p2 = Start-Process -FilePath $unity -ArgumentList $testArgs -Wait -PassThru -NoNewWindow
Write-Output "TEST_EXIT=$($p2.ExitCode)"
if (-not (Test-Path $testResults)) { Write-Output "NO_RESULTS"; exit 1 }
[xml]$xml = Get-Content $testResults
$run = $xml."test-run"
Write-Output "TOTAL=$($run.total) PASSED=$($run.passed) FAILED=$($run.failed) RESULT=$($run.result)"
if ($run.failed -ne "0" -or $run.result -ne "Passed") {
  $xml.SelectNodes("//test-case[@result!='Passed']") | ForEach-Object { "$($_.result) $($_.fullname)" }
  exit 1
}
exit 0
