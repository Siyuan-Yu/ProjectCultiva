$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

# 日常请只打开 Apps\<编辑器>\*.exe；不要用各工程下的 bin\（已改到 .build\）
$appsRoot = Join-Path $root "Apps"
$apps = @("PackageBrowser", "RegionEditor", "WorldGraphEditor", "QuestEditor", "EventEditor", "MapEditor", "WorkAreaEditor", "CharacterNpcEditor")
foreach ($app in $apps) {
  $out = Join-Path $appsRoot $app
  Write-Host "Publishing $app -> $out"
  dotnet publish (Join-Path $root "$app\$app.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out
}

$readme = @"
请从本目录打开各编辑器 exe（例如 MapEditor\MapEditor.exe）。

不要去各工程文件夹或 .build 里找 exe——那些是编译中间产物，容易过期。
重新打包：在上级目录运行 .\publish.ps1
"@
Set-Content -Path (Join-Path $appsRoot "请从这里打开.txt") -Value $readme -Encoding UTF8

Write-Host "Done. Exes under Apps\"
Get-ChildItem -Recurse Apps -Filter *.exe | Select-Object FullName, Length
