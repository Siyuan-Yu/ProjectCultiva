Set-StrictMode -Version Latest

function Get-ContentAuthoringEditorManifest {
    param([Parameter(Mandatory)][string]$Root)

    $manifestPath = Join-Path $Root "EditorManifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Editor manifest is missing: $manifestPath" }
    # PowerShell 5 treats BOM-less UTF-8 as the active ANSI code page unless encoding is explicit.
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw "Editor manifest is not valid JSON: $manifestPath. $($_.Exception.Message)" }
    if ($manifest.schemaVersion -ne 1 -or $null -eq $manifest.editors) { throw "Editor manifest schemaVersion or editors is invalid." }

    $allowedLifecycles = @("Active", "Legacy Compatibility")
    $seenNames = @{}
    $rootFullPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    foreach ($editor in @($manifest.editors)) {
        foreach ($property in @("EditorName", "ProjectPath", "LifecycleStatus", "PublishByDefault", "Replacement", "Notes")) {
            if ($null -eq $editor.PSObject.Properties[$property]) { throw "Editor manifest entry is missing $property." }
        }
        if ([string]::IsNullOrWhiteSpace($editor.EditorName) -or $editor.EditorName -notmatch '^[A-Za-z][A-Za-z0-9]*$') { throw "Invalid EditorName: $($editor.EditorName)" }
        if ($seenNames.ContainsKey($editor.EditorName)) { throw "Duplicate EditorName: $($editor.EditorName)" }
        $seenNames[$editor.EditorName] = $true
        if ([string]::IsNullOrWhiteSpace($editor.ProjectPath) -or [IO.Path]::IsPathRooted($editor.ProjectPath)) { throw "Invalid ProjectPath: $($editor.EditorName)" }
        if ($allowedLifecycles -notcontains $editor.LifecycleStatus) { throw "Invalid LifecycleStatus: $($editor.EditorName)" }
        if ($editor.PublishByDefault -isnot [bool]) { throw "PublishByDefault must be boolean: $($editor.EditorName)" }
        if ($null -eq $editor.Notes -or [string]::IsNullOrWhiteSpace([string]$editor.Notes)) { throw "Notes must not be empty: $($editor.EditorName)" }
        $projectFullPath = [IO.Path]::GetFullPath((Join-Path $Root $editor.ProjectPath))
        if (-not $projectFullPath.StartsWith($rootFullPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "ProjectPath escapes tool root: $($editor.EditorName)" }
        if (-not (Test-Path -LiteralPath $projectFullPath -PathType Leaf)) { throw "Project does not exist: $($editor.EditorName) -> $($editor.ProjectPath)" }
    }
    return $manifest
}

Export-ModuleMember -Function Get-ContentAuthoringEditorManifest
