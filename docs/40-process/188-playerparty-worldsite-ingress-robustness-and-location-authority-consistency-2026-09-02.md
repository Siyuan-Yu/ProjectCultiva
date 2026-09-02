# 188 — PlayerParty WorldSite Ingress Robustness + Location Authority Consistency（2026-09-02）

> 状态：【暂未验收】。配套 devlog 条目：`42-devlog.md` 2026-09-02「Local Hostility→BattleOffer V1 + Qingshi Acceptance Fixtures + 系列 FIX」第 6 节。

## 症状（Unity 人工验收反馈）

PlayerParty Active 带多个 Followers，在 LocalVisible 状态下从一个 LocalMap 移动到另一个 LocalMap，尤其 WorldSite → … → WorldSite 时：

- 到目标 WorldSite 后容易卡在地图边缘 / 透明 Surface Exit 区；
- Active / Followers 可能挤在一起；
- 有时 WASD 无法从边缘走出来；
- Character strategic roster 中 Active / Followers 的「位置」显示为「?」；
- 用户担心即使 WASD 走出来，数据层仍不知道实际位置。

## Position Authority 不变式（本轮保持）

1. **PlayerParty strategic context authority**：`PlayerPartyWorldMotion.LocationKind / SiteId / CurrentHex`。
2. **continuous physical authority**：`PlayerPartyWorldMotion.WorldPosition`。
3. WorldSite LocalVisible 时 Active 实际 Local transform → `WorldSiteSpatialMapping.LocalToWorld` → `WorldPosition`（`HostPlayerPartyController.TickWorldSiteCanonicalSync` 设计正确，不得改成 WorldPosition 强行拖 transform）。
4. `EntityLocationComponent.LocationId` 只是 LocalMap named place / semantic location，**不是** WorldSite/Hex strategic authority；为空不代表 strategic location unknown。
5. Followers 没有独立 strategic position，随 PlayerParty；不按每个 follower transform 分别推战略 Hex/Site。

## Root Cause 与修复

### RC1：WorldSite materialize 把 Party member 全放同一点
`PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap`：Wilderness fresh 分支对 follower 调 `ApplyFollowerPresentationOffset`，WorldSite fresh 分支走 `LoadedLocalMapPlacementSnapshotRestore.TryResolveWorldSiteSpawnPosition`（无 snapshot 时全部返回同一 default）。

修复：
- `ApplyFollowerPresentationOffset` 泛化改名 **`ApplyPartyFormationOffset`**，Wilderness / WorldSite 通用。
- Snapshot 分支保留 snapshot 各自位置，不加 formation offset；DefaultStart / ProjectCanonicalWorldToLocal fresh placement 的 follower 加 deterministic offset。
- follower candidate 经新纯函数 **`ClampFormationCandidateToSafeInterior`**（复用 `WildernessLocalWorldProjection.IsInSafeInterior` / `SurfaceExitZoneCalculator.ResolveDepthFromSession` / NearEdgeMargin）收敛，防止 offset 把人推回 exit band；不发明新 magic distance。

### RC2：Materialize 后 Rebind 又把 follower 拉到 Active 精确位置
`HostPlayerPartyController.RebindAllFollowers → OrderFollowerTowardActive(id)` 的 goal = `activeView.transform.position`。

修复：`OrderFollowerTowardActive(id, followerIndex)`，goal = Active + `FollowerOffset(followerIndex)`，与 `TickFollowers` 同一 formation convention（137.5° slot）；RebindAllFollowers 按 Party.Members 稳定顺序分配 slot；不新建第二套 offset。

### RC3：outside WalkGrid 无 recovery
`HostMoveController.SnapOntoWalkableIfNeeded` 在 `TryWorldToCell == false` 直接 return → 已出 grid 的角色永远无法自救。

修复：新增 **nearest-safe-walkable resolver**：
1. raw cell index = `floor((pos - Origin)/CellSize)`（超 bounds 也保留）；
2. clamp 到 `0..Width-1 / 0..Height-1` 作搜索起点；
3. ring/BFS 向外找 nearest candidate；
4. candidate 必须 `grid.IsWalkable && cell center ∈ SafeInterior && 不在任何 SurfaceExit slot`；
5. 返回 world center。
`SnapOntoWalkableIfNeeded` OOB 分支与 WASD tick 尾部（当前 cell OOB/blocked 时）都走该 resolver；正常撞墙不受影响。

### RC4：Active repair 需同步 presentation + canonical
repair 将 Active 从 badLocal → safeLocal 时：`EntityLocationComponent.SetPresentationOverride` + `view.transform.position` 同步；`motion.LocationKind == AtWorldSite` 时立即经 `WorldSiteSpatialMapping` / `PlayerPartyWorldSiteLocalVisibleSync` 把 safeLocal → canonical WorldPosition（不等 LateUpdate）；保持 `LocationKind=AtWorldSite / SiteId=当前 Site`，不 `SetAtWorldPosition`。

### RC5：Followers fallback
以 Active safe position + stable `FollowerOffset(index)` 为 preferred candidate，每人独立解析 nearest safe walkable；多人落同一 cell → 继续找下一候选（cell-key occupied set），避免全叠一格。Followers 只改 presentation/local placement，不写 PlayerPartyWorldMotion。

### RC6：Gate / actual transform split-brain
`PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation` 原来在传入点不在 SafeInterior 时内部重算 fake safe 点，只写 `SurfaceEdgeGate.CompleteTransition`，不同步 actual EntityLocation/EntityView。

修复：**不再 invent spawn point**，只用调用方提供的「实际最终落点」完成 Gate（Disarmed）；unsafe → diagnostics + Host safety repair 接管。Host repair 成功后 `gate.NoteLocalPosition(actualSafe)` + `TickRearm`；只有 `IsInSafeInterior` 才能 re-arm，绝不硬开 `EdgeArmed=true`。

### RC7：IngressContext one-shot
`PlayerPartySurfaceEdgeGate` 新增 **`ConsumeIngressContext()`**：清 IngressFootprintHex / IngressFromWildernessHex / IngressDirectionLocalX/Y / IngressBoundaryWorldX/Y / HasIngressContext；保留 EdgeArmed / LastLocal / LastExit*。`MaterializePartyOnResolvedLocalMap` return 前消费，防止下一次 Site→Site 读到旧 ingress direction。

### RC8：direct WorldSite→WorldSite 建立 destination ingress
`PlayerPartyWildernessTransitionService.TryExitWorldSiteByConnection` 原对 external ∈ 另一 site 直接无参 `EnterWorldSiteAsParty`（无 ingress context / 可能用旧 fallback direction）。

修复：external 属 destSite 时用 `WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(world, destSite, external, sourceFootprint, hexSize, out destinationIngress)`：成功 → `motion.SurfaceEdgeGate.SetIngressContext(destinationIngress)` + canonical = `destinationIngress.BoundaryContactWorld` + committed ingress footprint hex = external → `EnterWorldSiteAsParty(world, party, destSite, external)`；无正式 destination ingress → 明确 failure，不 silent enter。`PlayerPartyLocalVisibleAutoTravelService` 的 LocalVisible AutoTravel 直连分支同规则。

### RC9：BoundaryContact 不再 WorldToHex 猜正式 ingress Hex
- `TryExitWorldSiteByConnection`：committed hex 用 `external`（正式 connection hex）。
- formal Wilderness→Site ingress：`derived = HexMath.WorldToHex(boundaryWorld)` → `destinationHex`。
- `PlayerPartyLocalVisibleAutoTravelService` destination Site branch：`ingressDerived = WorldToHex(...)` → `destinationHex`。
- 长期 AtWorldSite 的具体 footprint hex 仍由 `WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(site, WorldPosition)` 即时派生，不把 CurrentHex 变成 Site physical authority。

### RC10：PlayerParty member WorldPresence 单向 consistency guard
新增 `PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(world, party, phase)`：遍历 `Party.Members`，仅 `ShouldMemberTransitionWithParty == true`（FormalArmy member 排除）；`AtWorldSite && SiteId valid → WorldPresence.SetAtSite(member, SiteId)`；`AtWorldPosition → SetAtHex(member, CurrentHex)`。**只允许 motion → member presence**，禁止反向覆盖。实际 repair 时打一次 diagnostics（member id / old / new / context / phase），不每帧刷。

调用点：`EnterWorldSiteAsParty` 成功尾；successful surface LocalMap materialize 后（defensive guard）；final arrival completion 后。

### RC11：「?」UI
`HostStrategicRosterQueries`：PlayerParty row 不再用 `ArmyService.ResolveCharacterFormationLocationId`（individual/FormalArmy query，对 PlayerParty member 为空 →「?」）。`StrategicCharacterRosterRow` 新增 **`LocationLabel`**；PlayerParty member（`partyRuntime.IsMember(id)` 且非 grouped）走 `PlayerPartyWorldLocationQuery`：
- AtWorldSite → `row.SiteId = resolved.SiteId`（可 focus）+ LocationLabel = ResolveSiteLabel；
- AtWorldPosition（Wilderness 正常态）→ `SiteId = ""` + LocationLabel = DescribeHexLabel(DerivedHex)，如「(9,7)」；
- 其它保持 individual/Army query，LocationLabel = SiteLabel。

`HostStrategicCharacterListPanel` 显示 `LocationLabel`；双击 focus 仅当 `row.SiteId` 非空；杜绝 fake id `SiteId="(10,7)"`。

### RC12：不根据 Transform 每帧决定 Site/Hex
不做 `Update(): motion.CurrentHex = WorldToHex(active.transform)` 这类 shortcut（会制造边界抖动）。保持：正式 Surface Transition 决定 Context（Site/Wilderness hex）；Context 内实际 Active transform → 连续 WorldPosition；AtWorldSite 需要 footprint hex 时从 WorldPosition + site footprint 即时派生；AtWilderness 时 CurrentHex 只在正式跨边成功后提交。

## Materialize Assertions（加强）

`TryAssertActiveMaterializedOnce` API 统一接受/解析 resolved local bounds：任何 Surface LocalMap 的 Active materialize 后至少 assert one occupant + PresentationOverride valid + inside actual map bounds + SafeInterior。WalkGrid walkability assert 放 Host（Core 不知 WalkGrid）：Host repair 后仍不合法 → `Debug.LogError`（仅 UNITY_EDITOR || DEVELOPMENT_BUILD），不 crash release。

## 验证

- Core / Data / Unity 三程序集编译 0 error（Roslyn + Unity 2022.3.6f1 官方引用）。
- `git diff --check` 干净。
- headless 回归：BOOTSTRAP_CHECK_PASS / ROSTER_PARITY_PASS / CONTENT_CHECK_PASS / HostSim AtSite+AtHex 场景 PASS。

## 人工验收（未做，待 Unity）

- CASE A：Active 单人 WorldSite A → LocalVisible travel → WorldSite B：SafeInterior、不在透明 Exit slot、cell walkable、立即 WASD 任意移动、roster 位置=WorldSite B、无「?」。
- CASE B：Active + 3 Followers 同路径：4 人都出现、不全叠、deterministic formation、无人在透明框/grid 外、全员 label=B。
- CASE C：A→B→A→B 重复 ≥5 次：无 stale ingress direction / 卡边 / 「?」/ Gate 永久 disarmed。
- CASE D：WorldSite → Wilderness：显示当前 Hex 而非「?」；WASD 在当前 Hex 内移动 continuous WorldPosition 跟随；仅正式跨 Surface Exit 才提交邻格。
- CASE E：Wilderness → WorldSite：入场后 WorldMap marker 对应新 Site / 正确 footprint，不跳回旧 Hex/Site。
- CASE F：Site B 内 WASD 走 5–10s → WorldMap marker 对应更新后 canonical WorldPosition 仍属 Site B；关 WorldMap 不 snap 回 ingress。
- CASE G：到 Site B 后立即再 travel 去 Site C：Gate 进入 SafeInterior 后正常 rearm，下次 exit 正常触发。
- CASE H：Active + 5 Followers 连续跨多个 LocalMaps：无 member missing / stack at edge / follower permanent stuck / presence「?」。

## 本轮不触碰

BattleOffer / BattleParticipantGathering / Manual·Auto Battle / FormalArmy movement / LoadedStrategicPopulation / HostileActionRouting / Content fixtures / Faction diplomacy。
