$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$apps = @("PackageBrowser", "RegionEditor", "QuestEditor", "EventEditor")
foreach ($app in $apps) {
  $out = Join-Path $root "publish\$app"
  Write-Host "Publishing $app -> $out"
  dotnet publish (Join-Path $root "$app\$app.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out
}

Write-Host "Done. Exes under publish\"
Get-ChildItem -Recurse publish -Filter *.exe | Select-Object FullName, Length
