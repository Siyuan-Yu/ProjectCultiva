param([Parameter(Mandatory)][string]$EditorName)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$exePath = Join-Path $root "Apps/$EditorName.exe"
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    Write-Error "找不到 $exePath；请运行编译-所有编辑器.cmd。"
    exit 1
}

$builtAt = (Get-Item -LiteralPath $exePath).LastWriteTimeUtc
$sourceDirs = Get-ChildItem -LiteralPath $root -Directory | Where-Object {
    $_.Name -notin @("Apps", ".build", "bin", "obj")
}
$sources = @(
    Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Extension -in @(".ps1", ".psm1", ".json") }
    foreach ($dir in $sourceDirs) {
        Get-ChildItem -LiteralPath $dir.FullName -Recurse -File | Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $_.Extension -in @(".cs", ".xaml", ".csproj", ".props", ".targets")
        }
    }
)
$newer = $sources | Where-Object { $_.LastWriteTimeUtc -gt $builtAt } | Select-Object -First 1
if ($newer) {
    Write-Error "$EditorName.exe 早于作者工具源码（$($newer.FullName)）；请先运行编译-所有编辑器.cmd。"
    exit 1
}
