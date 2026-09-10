# 209 — Continuous Outdoor Opening Population Bootstrap（NewGame 荒村人口 + 退休 W1C 入口）

日期：2026-09-10
状态：待制作人验收（未提交）

## 制作人复验发现的 blocker

- Normal NewGame 已能直进 Main Continuous Outdoor（上轮修复）；
- 荒村 Surface／buildings／places／movement 正常，但**初始荒村没有其它 NPC，同伴甲／乙也没出现**；
- 结论：不是 Surface migration failure，而是 NewGame opening population bootstrap。

## 本轮范围（只做这一块）

Startup Correctness（opening population）+ 退休 W1C Acceptance teleport 作为正常入口。
不进入下一阶段，不改 Gameplay 语义。

## 定位与修复

### 1. SpawnZone NPC 缺 macro presence（确定的 ordering bug）

`PlayableDayBootstrap` 顺序：`OpeningSpawnWorldPresenceApplier` → **`SpawnZoneApplier`**。
`SpawnZoneApplier.ApplyZone` 只写 `EntityLocation.PresentationOverride`，**从不写 `WorldPresence`**。
Continuous Outdoor 的人口真源是 `WorldPresence`（`StrategicWorldSitePopulationService.IsUngroupedResidentAtSite`
只认 `AtSite` + `SiteId`），因此这些 NPC 永远无法 materialize —— 旧 LocalMap 时代靠 Active WorldRegion
掩盖了这个缺口。

修复：`SpawnZoneApplier` 在 spawn 后立即经新 resolver 建立 macro presence：

- **新增** `ContinuousOutdoorSpawnPresenceResolver`（`Assets/Scripts/Data/Bootstrap/`）
  - `TryResolveContinuousOutdoorSiteForSourceMap`：source MapLayoutId → 唯一 Continuous Outdoor WorldSite
    （多个 Site 共用同一 layout → 拒绝解析，不猜）；
  - `TryResolveCanonicalAnchor`：legacy Site LocalMap presentation 坐标 → canonical Outdoor WorldPosition
    （与 `OpeningSpawnWorldPresenceApplier` 同一套 geometry：source bounds + `WorldSiteSpatialMapping`）。
- 只处理 `WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site)` 的 source map；
  Interior／Cave／Dungeon／Encounter／legacy-only map 保持旧语义。

### 2. Opening population presence 归一化（安全网）

**新增** `ContinuousOutdoorOpeningPopulationBootstrap`：在**所有会 spawn entity 的 opening bootstrap 之后**
（`SpawnZoneApplier` → `ContentRuntimeBootstrap` → 这里）跑一次。

- 只补「完全没有 `WorldPresence`」的实体，**绝不覆盖**已有 AtSite／AtWorldPosition／AtHex／InEncounter
  以及 FormalArmy member 战略位置；
- location → Site 必须**唯一可消解**：`ContinuousOutdoorSitePlaceIndex` 显式禁止 first-wins，
  多候选只能用 source LocalMap 消解，否则记 ambiguity（= Content validation error）；
- opening character（未写 worldSiteId）无 presence 时补 DefaultStartSite（同伴仍是 Background AtSite，
  绝不进 `PlayerPartyTravel.TravelingMembers`）；
- 顺便产出 census：`ContinuousOutdoorOpeningCensus`（OpeningCharacter／OpeningNpc／PresenceAtSite／
  ArmyAtSite／ExpectedPopulation）。

**新增** sitePlace 歧义 Content validation：
`ContentReferenceValidator.ValidateOutdoorSurfaceSitePlaceIdentities`（同名 locationId 绑多 Site 且缺
source localMapId → `DuplicateDefinitionId` error，加载即失败，不再运行期静默选第一个）。

### 3. FINAL OPENING POPULATION BARRIER（Host）

`PlayableHostBootstrap.FinalizeContinuousOutdoorOpeningPopulation()`：所有 Host binding 完成后**唯一一次**
显式 `ReconcileOutdoorEntityMaterializationForScopeChange()` + `RefreshViewableEntityIds` +
`PruneHiddenViews` + `SpawnMissingVisibleViews`，然后才 frame camera / postcondition。
不恢复 per-tick reconcile。

### 4. Startup population invariant 变严格（§13）

旧：`expected population 中至少有一个 visibleNpc` → 只有主控在场也能通过。
新：`ContinuousOutdoorStartupPlanner.TryCheckPopulationComplete`（Host 与测试共用同一实现）——
expected 中**每一个**都必须 materialized **且** 有 EntityView；否则 startup invariant 失败并逐实体输出
原因（`Entity/Name/Tags/LocationId/WorldPresence/ExpectedSite/ContinuousSiteLoaded/Materialized/EntityView`）。
诊断面板新增 `ExpectedOpeningPopulation=N Materialized=N Views=N Missing=[]`。

### 5. 退休 W1C Acceptance teleport 作为正常入口

- 诊断页顶部只读显示真实 authority：
  `Authority=ContinuousOutdoorSurface SurfaceId=… AcceptanceOnly=false CurrentChunk=… LoadedChunks=… CurrentOutdoorWorldSiteId=…`
  （只有真的 acceptance surface 才显示 `W1CAcceptanceSurface`）；
- W1C 传送按钮移入默认收起的 **Regression / Legacy Acceptance** 折叠区，不再是制作人入口；
- acceptance content 保留供历史 regression。

## 验证（本环境可执行部分）

- Host 全链（Core+Data+Unity，真实 Unity 2022.3.6f1 dll）：**0 error / 0 warning**；
- EditMode 测试程序集编译：**0 error**；
- 新增 `ContinuousOutdoorOpeningPopulationTests`（A–F）经 headless NUnit runner 实际执行：**6/6 PASS**：
  - A opening character／同伴 → AtSite(荒村) 且在期望 population；同伴不在 traveling members；
  - B 合成 spawnZone on continuous source map → 立即 AtSite + canonical anchor；真实 content 的
    `base:map_ch01_reference` 唯一解析为 `base:site_huangcun`；
  - C 有 LocationId 无 presence → normalize 补 AtSite(荒村)；
  - D cave／独立空间 NPC 不被 normalize 拉进 Outdoor，cave location 不解析为 Outdoor Site；
  - E 4/5 materialized 必须 FAIL（得到 Missing id）；
  - F 60 个正常 tick 后 `EntityReconcileRevision` 不变（无 per-tick reconcile）；
- real BaseGame 在新增 sitePlace 歧义校验下仍加载成功（0 歧义）。

## 未验证／风险

- EntityView 实际 materialize 与相机取景只能由制作人在 Unity 内 Play 验证（本环境无 Unity Test Runner）；
- 若荒村仍缺 NPC，startup invariant 现在会打印 `Missing=[…]` 与逐实体原因，可直接定位是
  presence／materialize／view 哪一层；
- 已知数据事实：`ch01_reference` 的荒村 source map **没有 spawnZone**（唯一 spawnZone 在
  `base:map_ch01_cave`，属独立空间）—— 因此荒村失踪人口是 18 名 scenario 居民／同伴，不是 SpawnZone NPC；
  §1 的 ordering bug 仍是真实缺陷（其它 Site／后续内容会踩），已一并修复。
