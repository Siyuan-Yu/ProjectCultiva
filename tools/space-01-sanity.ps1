# SPACE-01 offline sanity (no Unity).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools/space-01-sanity.ps1

param(
  [string]$Project = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Project)) {
  $Project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

& powershell -NoProfile -Command "& '$PSScriptRoot\offline-compile.ps1' -Project '$Project' -Only @('XianXia.Core','XianXia.Data')"
if ($LASTEXITCODE -ne 0) { Write-Output "COMPILE_FAILED"; exit 1 }

# Static consumer audit (no Unity).
$auditFailed = 0
$hostRoot = Join-Path $Project "Assets\Scripts\Unity\Host"
if (Test-Path (Join-Path $hostRoot "HostLocalMapEnterPrompt.cs")) {
  Write-Output "FAIL HostLocalMapEnterPrompt.cs still exists"
  $auditFailed++
}
$bridge = Get-Content (Join-Path $hostRoot "HostCommandBridge.cs") -Raw
if ($bridge -notmatch 'IssueEnterSeparateSpace') {
  Write-Output "FAIL IssueEnterSeparateSpace missing"
  $auditFailed++
}
$menu = Get-Content (Join-Path $hostRoot "HostNpcContextMenu.cs") -Raw
if ($menu -match 'HostLocalMapEnterPrompt') {
  Write-Output "FAIL HostNpcContextMenu still references HostLocalMapEnterPrompt"
  $auditFailed++
}
if ($menu -notmatch 'IssueEnterSeparateSpace') {
  Write-Output "FAIL BeginCaveEnter does not call IssueEnterSeparateSpace"
  $auditFailed++
}
$melee = Get-Content (Join-Path $hostRoot "HostNpcMeleeAssault.cs") -Raw
if ($melee -notmatch 'AreBothInActiveSeparateSpace') {
  Write-Output "FAIL HostNpcMeleeAssault missing SeparateSpace gate"
  $auditFailed++
}
$encounter = Get-Content (Join-Path $hostRoot "HostCharacterEncounter.cs") -Raw
if ($encounter -notmatch 'Independent CharacterEncounter bypassed') {
  Write-Output "FAIL HostCharacterEncounter missing SeparateSpace guard"
  $auditFailed++
}
$coreReq = Get-Content (Join-Path $Project "Assets\Scripts\Core\World\Strategic\CharacterEncounter.cs") -Raw
if ($coreReq -notmatch 'AreBothInActiveSeparateSpace') {
  Write-Output "FAIL RequiresEntry missing SeparateSpace early-out"
  $auditFailed++
}
$bootstrap = Get-Content (Join-Path $hostRoot "PlayableHostBootstrap.cs") -Raw
if ($bootstrap -notmatch 'RebuildSeparateSpacePresentationAfterLoad') {
  Write-Output "FAIL RebuildSeparateSpacePresentationAfterLoad missing"
  $auditFailed++
}
if ($bootstrap -match 'ResolvePartyWorldFromActiveControlledCharacter' -and
    $bootstrap -notmatch 'LocalMap\.IsActive') {
  Write-Output "FAIL RebuildPresentationAfterLoad missing SeparateSpace gate before Outdoor resolve"
  $auditFailed++
}
$menuLeave = Get-Content (Join-Path $hostRoot "HostNpcContextMenu.cs") -Raw
if ($menuLeave -match 'TryPickInteriorExitAtMouse|DrawLeaveMenu|BeginLeaveInterior|_leaveInteriorTarget') {
  Write-Output "FAIL HostNpcContextMenu still has right-click leave path"
  $auditFailed++
}
$actionMenu = Get-Content (Join-Path $hostRoot "HostActionMenu.cs") -Raw
if ($actionMenu -match '离开洞窟') {
  Write-Output "FAIL HostActionMenu still has 离开洞窟 product button"
  $auditFailed++
}
if (-not (Test-Path (Join-Path $hostRoot "HostSeparateSpaceExitTrigger.cs"))) {
  Write-Output "FAIL HostSeparateSpaceExitTrigger.cs missing"
  $auditFailed++
}
$resolver = Get-Content (Join-Path $Project "Assets\Scripts\Core\Persistence\SnapshotActiveControlledLocalMapResolver.cs") -Raw
if ($resolver -notmatch 'TryResolveFromActiveSeparateSpace|SeparateSpaceSession') {
  Write-Output "FAIL SnapshotActiveControlledLocalMapResolver missing SeparateSpace priority"
  $auditFailed++
}
$outdoor = Get-Content (Join-Path $hostRoot "ContinuousOutdoorSurfaceRuntime.cs") -Raw
if ($outdoor -notmatch 'LocalMap\.IsActive') {
  Write-Output "FAIL RebuildAfterWorldRestore missing SeparateSpace early-out"
  $auditFailed++
}

# SPACE-01 Final Hardening audits
$transition = Get-Content (Join-Path $Project "Assets\Scripts\Core\Exploration\SeparateSpaceTransitionService.cs") -Raw
if ($transition -match 'IsPlayerPartyCharacter' -and $transition -match 'foreach \(var e in world\.Entities\.All\)') {
  Write-Output "FAIL CollectTransitionMembers/Evacuate still scans all Player characters"
  $auditFailed++
}
if ($transition -notmatch 'No eligible PlayerParty members') {
  Write-Output "FAIL Enter missing explicit empty-membership failure"
  $auditFailed++
}
if ($transition -match 'PartyWorldPresenceMode\.AtHex') {
  Write-Output "FAIL SeparateSpaceTransitionService still writes AtHex"
  $auditFailed++
}
if ($transition -notmatch 'PartyWorldPresenceMode\.InSeparateSpace') {
  Write-Output "FAIL SeparateSpaceTransitionService missing InSeparateSpace"
  $auditFailed++
}
$modeEnum = Get-Content (Join-Path $Project "Assets\Scripts\Core\World\PartyWorldPresenceMode.cs") -Raw
if ($modeEnum -notmatch 'InEncounter\s*=\s*0' -or
    $modeEnum -notmatch 'DepartingLocalMap\s*=\s*1' -or
    $modeEnum -notmatch 'AtHex\s*=\s*2' -or
    $modeEnum -notmatch 'AtSite\s*=\s*3' -or
    $modeEnum -notmatch 'AtWorldPosition\s*=\s*4' -or
    $modeEnum -notmatch 'InSeparateSpace\s*=\s*5') {
  Write-Output "FAIL PartyWorldPresenceMode numeric values changed or InSeparateSpace missing"
  $auditFailed++
}
$placement = Get-Content (Join-Path $Project "Assets\Scripts\Core\Persistence\LoadedLocalMapPlacementSnapshotRestore.cs") -Raw
if ($placement -match 'ContainsOccupant\(entity\.Id\)\s*\n\s*continue') {
  Write-Output "FAIL Capture still occupant-only skip path"
  $auditFailed++
}
if ($placement -notmatch 'BelongsToActiveSeparateSpaceMap') {
  Write-Output "FAIL Capture missing BelongsToActiveSeparateSpaceMap"
  $auditFailed++
}
if ($placement -match 'AddOccupant') {
  Write-Output "FAIL ApplySavedPlacementsToDomain still AddOccupant (membership pollution)"
  $auditFailed++
}
if ($bridge -match 'IssueEnterLocalMapWithParty') {
  Write-Output "FAIL IssueEnterLocalMapWithParty wrapper still present"
  $auditFailed++
}

if ($auditFailed -gt 0) {
  Write-Output "STATIC_AUDIT_FAILED=$auditFailed"
  exit 1
}
Write-Output "STATIC_AUDIT_OK"

$csc = "D:\UnityEditor\2022.3.6f1\Editor\Data\DotNetSdkRoslyn\csc.dll"
$sa = Join-Path $Project "Library\ScriptAssemblies"
$src = Join-Path $Project "Temp\space-01-sanity\Program.cs"
$out = Join-Path $Project "Temp\space-01-sanity\space01-sanity.exe"
$scratch = Join-Path $Project "Temp\space-01-sanity"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

# Reuse Core rsp references (defines + framework refs) then add Core/Data + our Program.
$coreRsp = Join-Path $Project "Temp\offline-compile\XianXia.Core.rsp"
$refLines = Get-Content $coreRsp | Where-Object { $_ -match '^-r:' -or $_ -match '^-nostdlib' -or $_ -match '^-langversion' -or $_ -match '^-define:' }
$exeRsp = Join-Path $scratch "sanity.rsp"
$body = @()
$body += "-target:exe"
$body += "-out:`"$out`""
$body += "-nologo"
$body += $refLines
$body += "-r:`"$sa\XianXia.Core.dll`""
$body += "-r:`"$sa\XianXia.Data.dll`""
$body += "`"$src`""
[System.IO.File]::WriteAllLines($exeRsp, $body)

& dotnet exec $csc "@$exeRsp" 2>&1 | ForEach-Object { $_ } | Where-Object { $_ -match 'error' } |
  Select-Object -First 40 | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0) { Write-Output "SANITY_COMPILE_FAILED"; exit 1 }

# Prefer mono host: copy fresh Core/Data next to the exe for probing.
Copy-Item (Join-Path $sa "XianXia.Core.dll") $scratch -Force
Copy-Item (Join-Path $sa "XianXia.Data.dll") $scratch -Force
$monoExe = "D:\UnityEditor\2022.3.6f1\Editor\Data\MonoBleedingEdge\bin\mono.exe"
$monoFacades = "D:\UnityEditor\2022.3.6f1\Editor\Data\MonoBleedingEdge\lib\mono\4.5\Facades"
if (Test-Path $monoExe) {
  $prevMonoPath = $env:MONO_PATH
  $env:MONO_PATH = "$scratch;$sa;$monoFacades"
  try {
    & $monoExe --runtime=v4.0.30319 $out $Project
    exit $LASTEXITCODE
  } finally {
    if ($null -eq $prevMonoPath) { Remove-Item Env:MONO_PATH -ErrorAction SilentlyContinue }
    else { $env:MONO_PATH = $prevMonoPath }
  }
}

Write-Output "MONO_NOT_FOUND"
exit 1
