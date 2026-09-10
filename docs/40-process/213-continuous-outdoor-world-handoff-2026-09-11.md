# Continuous Outdoor World — Current Handoff / Recovery Checkpoint

> 本文档是 Continuous Outdoor / Outdoor WorldSite migration 的 **Canonical Handoff**。
> **下一 GPT / DeepSeek 会话请先完整读本文件，再读 §19 列出的文档。**
> 本文档自包含：不依赖 ChatGPT Memory、不依赖任何聊天记录、不依赖上一会话上下文。
>
> 生成日期：2026-09-11
> 文档性质：Documentation + Checkpoint（**本轮不做任何实现**）

---

# 1. Purpose

- 这是当前 Repository 在 `dev_openworld` 分支上的**权威交接文档**。
- 它回答：现在世界长什么样、哪些已封板、哪些只是"实现了但没被制作人验收"、哪些还没做、哪些是故意保留的 legacy、下一会话第一件事做什么。
- 与本文件冲突时，**以真实代码 + 本文件为准**；历史 stage 文档（204～212）是过程记录，其中 212 描述本轮 A/B 修复细节。
- 本文档**不授权**任何新阶段开工。见 §17。

---

# 2. Repository Checkpoint

| 项 | 值 |
|---|---|
| Repository | `https://github.com/Siyuan-Yu/ProjectCultiva.git` |
| Branch | `dev_openworld` |
| Implementation checkpoint commit | `d34efc0` — `先提交吧`（已包含本轮 A/B 修复、Content 重烘、工具与 process docs） |
| Document commit | 见 §16 最终报告（本文档所在 commit） |
| Date | 2026-09-11 |
| Unity | `2022.3.6f1`（`D:\UnityEditor\2022.3.6f1`） |
| Content package | `Content/BaseGame` |
| Opening scenario | `base:scenario_ch01_reference`（`Content/BaseGame/Data/Scenarios/scenarios.json`） |
| Playable hex world | `base:hex_world_travel_mvp_30x15`（30×15，`hexSize=1`） |
| Main continuous surface | `base:surface_main_wilderness_v1` |
| Acceptance-only surface | `base:surface_w1c_wilderness_acceptance`（`acceptanceOnly=true`） |

> 本文档不把自己所在的 commit SHA 写进正文（否则无法自洽）；`d34efc0` 是**代码**checkpoint，文档 commit 见最终报告。

---

# 3. One-Minute State Summary

- **当前正常世界**：Normal NewGame 直接进入 **Main Continuous Surface**（`base:surface_main_wilderness_v1`），主角 canonical `WorldPosition` 是唯一物理 authority；chunk（1.4×1.4，`cellSize=0.028`）按半径 1 邻域（正常 3×3）流式呈现。
- **W1C 是什么**：`base:surface_w1c_wilderness_acceptance` 只是**历史 regression 资产**（`acceptanceOnly=true`）。normal resolver **永不**选择它；LevelTester 里的传送按钮在默认收起的「Regression / Legacy Acceptance」区。**正常验收不需要、也不应该点它。**
- **WorldSite 现在是什么**：Outdoor WorldSite 是 **Domain 战略身份 + Continuous physical region**，不再是"进一张 LocalMap"的物理 transition。走过村庄/城镇边界是**连续移动**，不是换图。
- **LocalMap 还负责什么**：Interior / Cave / Dungeon / Secret Realm 等**真正独立空间**；以及 old save / legacy compatibility。Wilderness LocalMap 只在外层 coverage 之外作为兼容兜底。
- **当前第一优先事项**：**制作人对最新 A/B（212）做 Unity 人工 Play 验收**（见 §16）。验收通过前不要把 migration 标为 ACCEPTED。
- **不要做的事**：不要用 W1C 传送按钮当入口；不要为了"干净"删 legacy；不要在未授权的下一阶段（Surface Geography）开工。

---

# 4. Architecture Invariants — DO NOT REGRESS

1. **`WorldPosition` = Outdoor physical authority。** 位置只有一份真源（`PlayerPartyWorldMotion.WorldPosition` / `WorldPresence`）；chunk、placement、view transform 都是表现。
2. **`SurfaceChunk ≠ Strategic Hex`。** 两者覆盖同一 `WorldPosition`，但 chunk 决定呈现/加载，Hex 只负责 Territory / FormalArmy / Background Travel / WorldSite footprint / WorldMap。
3. **Chunk 只拥有 presentation；Domain／Stateful object 拥有 identity 与 state。** chunk 卸载不得改变 Domain state，stable id 不含 chunk index。
4. **Outdoor WorldSite = Domain context + Continuous physical region，不是 LocalMap。** 正常 Outdoor 中 `PlayerPartyWorldMotion.LocationKind` 始终 `AtWorldPosition`；`WorldSite.LocalMapId` 只是 legacy authoring 来源。
5. **穿过 Outdoor 城镇/村庄边界不是 transition**，不加载 LocalMap、不 Snap、不用 SurfaceExit/Gateway。
6. **Interior / Cave / Dungeon 仍是独立空间**，仍是 LocalMap + Portal；进出必须保留精确 Outdoor 回归位置。
7. **Continuous World 不得依赖 Scene / LocalMap transition 的清理**（这正是迁移的目的：旧 LocalMap 时代靠 Active WorldRegion 掩盖的问题必须显式解决）。
8. **世界方向**：World **+X = East/right**，World **+Y = North/up**。
9. **Strategic road ≠ passability。** 战略层的 road/passable 不直接等于物理可行走；物理可行性由 Surface WalkGrid / blocker 决定。
10. **Physical Surface 最终拥有真实 terrain truth**（当前尚未做到，见 §14.3）。
11. **Party co-presence**：Continuous Outdoor 不要求 same LocalMap / same Site / same Hex，只要求同一个 active continuous presentation scope；Interior/Cave 保持真正独立空间规则（见 §9）。
12. **Opening authored placement ≠ runtime current position**：authored 位置只在**第一次** materialize 使用；NPC 之后走动/存档/回载后不得被 authored anchor 重置（见 §8）。

---

# 5. Stage History / Seal Status

| Stage | Status | Producer accepted? | Doc | Key outcome |
|---|---|---|---|---|
| W1A Multi-Surface Presentation Foundation | **ACCEPTED / SEALED** | 是 | [204](204-continuous-world-w1a-presentation-foundation-2026-09-09.md) | 多 Surface 呈现基础设施；Zero Gameplay Behavior Change |
| W1B First Seamless Wilderness Pair | **ACCEPTED / SEALED** | 是 | [205](205-continuous-world-w1b-first-seamless-wilderness-pair-2026-09-09.md) | 第一对无缝相邻 Wilderness Hex 连续呈现（仍保留为低优先级 presenter） |
| W1C First Real Surface Chunk Neighborhood | **ACCEPTED / SEALED** | 是 | [206](206-continuous-world-w1c-surface-chunk-grid-2026-09-09.md) | `SurfaceChunkCoord` 网格、radius-1 邻域、Composite WalkGrid、`acceptanceOnly` surface |
| W1D Default Wilderness Cutover / Gateway | **NOT ACCEPTED / SUPERSEDED** | 否 | [207](207-continuous-world-w1d-default-wilderness-cutover-2026-09-10.md) | 被 208 的 WorldSite 迁移取代；Gateway 不再拥有 normal runtime |
| **Outdoor WorldSite Continuous Migration** | **IMPLEMENTED / PENDING PRODUCER ACCEPTANCE** | 否（未明确宣布） | [208](208-continuous-world-outdoor-worldsite-surface-migration-v1-2026-09-10.md) | 7 个 `continuousOutdoor` Site 迁入 Main Surface：PhysicalRegion / SitePlacements / SitePlaces / chunk 分配 / ControlCore / FactionFlag / population / schedule 全链 |
| Opening population bootstrap repairs | **IMPLEMENTED / PENDING PRODUCER ACCEPTANCE** | 部分（生产者看到 NPC 出现并工作） | [209](209-continuous-outdoor-opening-population-bootstrap-2026-09-10.md)、[210](210-continuous-outdoor-opening-spatial-placement-2026-09-11.md)、[211](211-continuous-outdoor-view-placement-realign-2026-09-11.md) | opening presence normalize、baked opening anchors、view realign、spawn invalid safety net |
| **Opening placement fidelity + Party co-presence** | **IMPLEMENTED / PENDING PRODUCER ACCEPTANCE** | 否（最新 Play 仍报过 2 次 invariant 误报，已修，待重验） | [212](212-continuous-outdoor-opening-placement-and-party-copresence-2026-09-11.md) | 单一 bake truth、hybrid (c) authored placement、spawn stable key、bake validation、Continuous co-presence join |

---

# 6. Current Runtime Flow

```
Normal NewGame
  → PlayableDayBootstrap.Start(base:scenario_ch01_reference)
      → OpeningScenario / SpawnZone / WorldRegion / Hex session / population normalize
      → OpeningSpawnWorldPresenceApplier  : AtSite(site) / AtSite+anchor，并登记 SpawnStableKey
  → PlayableHostBootstrap.TryInitialize
      → TryPrepareInitialContinuousOutdoorStartup   (只读：Site → Surface → Chunk → canonical anchor)
      → Preflight（neighborhood 可加载）→ Commit（提交 canonical WorldPosition，清 legacy LocalMap authority）
      → ContinuousOutdoorSurfaceRuntime.ActivateSurface
          → UpdateNeighborhood(center) : radius-1 chunk（正常 3×3）add/remove
          → RecomposeWalkGrid()        : Wilderness grid + Site blocker grid → Composite WalkGrid
          → RefreshLoadedOutdoorPlaces(): Site-scoped place registry（SiteId+LocationId → WorldPosition）
          → ReconcileOutdoorEntityMaterialization()
          → RealignMaterializedViewPlacements()
      → FinalizeContinuousOutdoorOpeningPopulation()（Host 侧唯一一次 population barrier）
```

Outdoor 连续区（全部 Continuous，无 transition）：

```
Wilderness ↔ Village（荒村）↔ Town（青石镇）↔ Pass/Sect outdoor … （7 个 continuousOutdoor Site）
```

独立空间：

```
Outdoor ── Portal/Portal→ Interior / Cave LocalMap ── 退出 ──→ 精确回到该 Outdoor WorldPosition（再进 Main Surface）
```

---

# 7. Current Content / Bake State

真实读数（本文件写作时对 `Content/BaseGame/Data/Worlds/*.json` 静态审计所得）：

| 项 | 值 |
|---|---|
| Main Surface id | `base:surface_main_wilderness_v1` |
| `acceptanceOnly` | 未写（= false） |
| `cellSize` | `0.028` WorldPosition units / source cell（**PROVISIONAL / TUNABLE**） |
| `chunkWidth × chunkHeight` | `1.4 × 1.4` |
| `originWorldX/Y` | `-1.4 / -1.4` |
| Chunks（checked-in） | **646**（38 × 17，X 0..37 / Y 0..16） |
| SiteRegions（PhysicalRegions） | **7** |
| SitePlacements | **75** |
| SitePlaces | **18** |
| OpeningEntityAnchors | **18**（全部属于 `base:site_huangcun`） |
| Acceptance Surface | `base:surface_w1c_wilderness_acceptance`（`acceptanceOnly=true`，25 chunks，无 siteRegions/placements/places/anchors） |

### 7.1 七个 Continuous Outdoor Site（真实表）

| SiteId | Type | `sourceLocalMapId` | ArrivalWorld | SitePlacements | SitePlaces | OpeningAnchors |
|---|---|---|---|---|---|---|
| `base:site_chengzhen` | Town | `base:map_site_chengzhen` | (18.61955, 11.25) | 1 | 1 | 0 |
| `base:site_guanai` | Pass | `base:map_site_guanai` | (21.65064, 16.5) | 1 | 1 | 0 |
| `base:site_huangcun` | Village | `base:map_ch01_reference` | (6.49519, 11.25) | **68** | **12** | **18** |
| `base:site_lingdi` | SpiritLand | `base:map_site_lingdi` | (38.10512, 9.0) | 1 | 1 | 0 |
| `base:site_linjian` | Forest | `base:map_site_linjian` | (12.99038, 7.5) | 1 | 1 | 0 |
| `base:site_zhuangyuan` | Village | `base:map_site_zhuangyuan` | (29.01185, 6.75) | 1 | 1 | 0 |
| `test:site_player_camp` | Camp | `base:map_player_camp` | (6.9282, 6.0) | 2 | 1 | 0 |

`PhysicalRegion status`（全部 7 个）：checked-in `siteRegions[]` 条目存在，几何为**战略 footprint 近似**（见 §14.2）。渲染对象合计 **405**（`singleCentered=1` / `zoneOverlay=1` / `perCell=SourceCellsW×H`）。

### 7.2 为什么历史审计出现「8 Outdoor Sites」，实现报告是「7/7」

**证据（对 `travel_mvp_hex_world_30x15` 的真实审计）**：

- 该地图 `sites[]` **共 8 条**；
- 其中 `continuousOutdoor=true` 的正好 **7 条**（就是 §7.1 的 7 个）；
- 第 8 条是 **`base:site_editor_8`**：`displayName = "新地点"`、`siteType = "Village"`、**`localMapId = ""`（空）**、footprint 只有 1 hex、只有一个 7-hex 的 `base:region_editor_8` territory region，**没有** surface `siteRegion`、**没有**任何 placement / place / opening anchor，也没有任何 scenario / roster / army 引用它。

**结论**：`8` = 该 hex 世界里 `sites[]` 的**未过滤总数**（含 MapEditor 遗留占位点）；`7/7` = 真正标记 `continuousOutdoor=true` 并被迁入 Main Surface 的 Site 数。**不存在漏迁的 Outdoor WorldSite**；`base:site_editor_8` 只是一个无 LocalMap、无内容、无物理几何的编辑器占位（`siteId` 前缀 `editor` 也说明来源），不属于 playable 迁移范围。

**补充证据**：大图 `base:hex_world_ch01`（100×50）有 **30 个 site，`continuousOutdoor=true` 数量为 0** → 大图**未迁移**，也不在当前 playable world（`base:scenario_ch01_reference.openingHexWorldId = base:hex_world_travel_mvp_30x15`）。

---

# 8. Opening Population / NPC Spatial Rules

### 8.1 presence

- Continuous Outdoor Site 的 opening NPC，`WorldPresence` 只表达 **Site membership**（`AtSite(siteId)`）；正常 NewGame **不**把 legacy LocalPosition 映射结果写进 `HasContinuousWorldPosition`（那条路径只服务 old save / legacy tooling）。
- 精确初始位置由 **checked-in `openingEntityAnchors`** 在**第一次 materialize** 时解析。

### 8.2 spawn stable key（`SpawnStableKey ≠ DefinitionId`）

- GameStart 时 `OpeningSpawnWorldPresenceApplier.Apply` 按 authored spawn 顺序把每个 spawn 登记进 `SimulationWorld.OpeningSpawnIdentities`：key = `definitionId`（authored index 0）或 `definitionId#n`。
- **可从 spawned Entity 反查**（`TryGetSpawnKey` / `TryGetEntity`）；绝不用 Unity InstanceId / 随机 / runtime 顺序。
- `ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor` **只按 SpawnKey 精确匹配**，已删除 `DefinitionId == input` 兜底 → 同一 Definition 多次 spawn 不会吃 first-match anchor。

### 8.3 authored placement policy = **Hybrid (c)**（制作人确认）

`ContinuousOutdoorOpeningPlacementResolver.ResolveAuthoredPlace` 的权威顺序：

1. **`AssignedLocalPlace`** — bootstrap 实际分配的 authored resident LocalPlace，且**不是** LocalPlaceSet 的 `startLocationId` 退化值；
2. **`HomeWorkArea`** — 人物 Content 的 `homeWorkAreaId` → 工区 `locationId`（**仅当第 1 条退化为 StartLocation 时**使用）；
3. **`StartLocation`** — 无任何 authored placement 时的兜底。

每条都要求该 location 绑定到本 Site（`surface.sitePlaces`），否则 `Unresolved`（不猜）。

已确认的产品决策（**不要改**）：

| NPC | 出生点 | 依据 |
|---|---|---|
| 阿木 `woodcutter` | **树林** `loc_ref_forest` | resident place（**不被 `homeWorkAreaId=workarea_houses` 覆盖**） |
| 阿柴 `wood_b` | **矿洞** `loc_ref_mine` | resident place |
| 阿青 `mortal_b` | **药田** `loc_ref_herb_field` | resident place |
| 阿石 `mortal_a` | 凡人住房 | resident + home 一致 |
| 阿土/阿禾/阿兰/阿杏/阿枝 | **凡人住房** | LocationId 退化为农田 → 用 `homeWorkAreaId=workarea_houses` |
| 巡卫甲/乙/丙 | **巡卫住房** | resident / `homeWorkAreaId=workarea_guard_quarters` |
| 杂役主管 | **主管住房** | resident |
| 将老 `jiang_lao` | **灵泉** `loc_ref_spring` | `homeWorkAreaId=workarea_spring_check` |
| 主角 / 同伴甲 / 同伴乙 / 村内可招者 | 农田杂役区 | 无 `homeWorkAreaId`，**不要猜住房** |

### 8.4 shared bake transform（单一 truth）

- `WorldSiteOutdoorBakeTransform`（Core，纯函数）：`source MapLayout bounds + Site footprint + hexSize + source local point → canonical WorldPosition`，公式为**纯 AABB 线性归一化**（footprint 全部 hex 角点 AABB）。
- `SitePlacements` / `SitePlaces` / `OpeningEntityAnchors` 以及 legacy `LocalPosition → canonical` 全部只走它。
- 实测：纯 AABB 对 checked-in 内容 **sitePlaces 12/12 + sitePlacements 68/68 命中**；`HexFootprintSpatialMapping`（带内含性投影）只有 9/12 + 29/68；`WorldSiteSpatialMapping`（V2 radial）0/12。**后两者不得再作为 opening entity bake authority。**
- 遗留非 opening 用途（**LEGACY / 不要删**）：`PlayerPartyLocalMapMaterializationService`（LegacyRestoreLocal / BootstrapFromAuthoredLocal / SafeLanding）、`LocalCombatCasualtyHandoffService`（legacy Site LocalMap 内倒下）、`WildernessLocalWorldProjection`（Wilderness 单 hex 投影）、`PlayableHostBootstrap.TryResolveLegacyContinuousStartupAnchor`（仅当没有 baked SitePlace/RegionArrival 时的 C 兜底；当前内容永远走 A 分支）。

### 8.5 anchor 内部布局

- slot 0 恒为该 location 的 authored presentation 点；只有当该点落在自己的 bound placement 矩形**之外**（Content 自相矛盾，例如 `loc_ref_spring`）才改用矩形中心。
- 其余 slot 按固定 8 向环、步长 `3 × source CellSize` 展开，clamp 进 authored placement 矩形与 source bounds。
- **slot index 真源 = checked-in anchors 的 authored 顺序**（`BuildAuthoredSlotRegistry`），与"当前谁在场"无关。

### 8.6 runtime 落点优先级（不要改）

```
已有真实 Runtime current anchor（NPC 确实移动过 / SaveLoad / dematerialize capture）
  > Baked precise OpeningEntityAnchor
  > Baked SitePlace
  > SiteArrivalFallback
```

Normal NewGame 的 authored 初始位置**不得**写成 runtime current anchor。

### 8.7 校验（§5）

- Content 侧 `TryValidateAuthoredAnchors`：逐个 anchor 用（`sourceLocationId` + authored slot）复算 shared bake 并比对（epsilon `1e-3`），**完全不读实体 / population / 运行时板**。
- 实体侧：identity board 登记过的 authored opening spawn 必须能查到自己的 anchor。
- outcome 三态：`Validated` / `Skipped`（无 anchor 或 Content 真源查不到，只在诊断显示）/ `Failed`（进 startup invariant）。
- 诊断行含 `AnchorBake=Validated|Skipped|Failed`。

### 8.8 NPC 走动后的 position commit

- opening authored position **只用于第一次 materialize**；population refresh 是 Add/Keep/Remove diff（Keep 不写 override，Remove 在 prune 前捕获当前位置）。
- `HostNpcScheduleMover` 到达才提交 `AtSite + SiteId + precise WorldPosition`；不伪造 arrival；pathfinding 失败按 repath interval 重试。

---

# 9. PlayerParty / Companion Rules

- **统一判定**：`PlayerPartyLocalCoPresenceQuery`（Core）。
  - **Continuous Outdoor scope** = Partyt 处于 `AtWorldPosition` + 非 Interior + runtime 已建立连续呈现 scope（有 loaded continuous site 或已 materialize 实体）。
    - 是 → 只要 candidate 与 active 属于**同一个 continuous presentation scope**：active 在 scope、candidate 已 materialized（或属 traveling members）、living/can act、非 FormalArmy、不处于独立空间（`InEncounter`/departing）presence。**不要求 same SiteId / same Hex / same LocalMap。**
    - 否（Legacy / Interior / Cave）→ 保留 `PlayerPartyRuntime.IsOnSameLocalMap`。
- `PlayerPartyRuntime.ValidateJoin` 已改用该 query；提示为空间中性「**需要与主控处于同一可交互空间。**」，Normal Continuous Outdoor join **不再**调用 `IsOnSameLocalMap`。后者保留为 legacy compatibility。
- `HostPlayerPartyController.TryFollowActive`：
  - Continuous 分支：**不写** `world.LocalMap.AddOccupant(candidate)`；`BackgroundCharacterTravelService.CancelTravelIfAny` → `PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion`（`AtWorldPosition(WorldPosition, CurrentHex)`）→ `CaptureTravelingMembersForPartyTransition`（新 follower 进 traveling members）；**不 teleport**，view 保持当前位置，随后 `OrderFollowerTowardActive` 走 formation slot 实时跟随。
  - Legacy/Interior 分支：保留原 `AddOccupant`。
- `TryStopFollow`：优先取 follower 实际 view 落点（`ContinuousOutdoorSurfaceRuntime.PresentationToWorld`），否则用已有 precise anchor，经 `SyncIndependentCharacterPresenceFromPosition` 恢复为普通独立角色 presence（Continuous Site 内 → `AtSite + precise anchor`；荒野 → `AtWorldPosition`），并刷新 `EntityLocationComponent.PresentationOverride` = 当前 view 位置（否则下次 reconcile 会把它拉回旧 override），清 LocalMap occupant，刷新 traveling members。

---

# 10. WorldSite Gameplay Compatibility（逐项真实状态）

| 项 | 状态 | 说明（代码依据） |
|---|---|---|
| ControlCore 连续空间路径 | DONE（未 producer 验收） | 连续 Site 分支先走 baked placement / site place registry，不用 `MapLayoutPick`；Host 侧 `HostHousingAreaSelection` 显式走 `ContinuousOutdoorSurfaceRuntime` |
| Capture / Siege | DONE（沿用既有领域服务；未在连续空间重新人工验收） | Domain `ControlCoreBoard` / WorldSite owner + Capture/Siege 服务不变 |
| FactionFlag 连续分支 | DONE（未 producer 验收） | `HostFactionFlagQuery.TryGetContinuousFootprint` 以 canonical world position + `CompositeWalkGrid` 计算 |
| Housing 查询 | DONE | `HostHousingAreaSelection` + `ContinuousOutdoorMaterialization.TryGetAnyPlace(area.LocationId)` |
| Work areas / 工作目标 | DONE | `HostZoneQuery` / `WorldLocationQuery`：Continuous SitePlace 优先、WorldRegion fallback；`HostInteractSpots` 由 tile map owner-scoped 注册 |
| NPC Schedule | DONE（producer 已实际看到能工作） | `HostNpcScheduleMover` + `NpcSchedulePathRequestPolicy`；到达才 commit |
| Site population materialization | DONE | `ContinuousOutdoorMaterialization`（loaded site / place / entity）由 radius-1 neighborhood 驱动 |
| AutoTravel（进入 / 穿越 Site） | PARTIAL / NOT VERIFIED in this round | 不关 Surface、不换 Site LocalMap；但本轮**没有**重新验证 AutoTravel 全链路 |
| Interior / Cave 入口与返回 | DONE（既有链路保留） | `HostCaveEntranceQuery` / `HostLocalMapEnterPrompt` / `HostCommandBridge` 走 `ContinuousOutdoorMaterialization` place；退出恢复精确 Outdoor WorldPosition |
| Save/Load（party / presence / site） | **KNOWN ISSUE** | 见 §11（outdoor stateful 的 JSON 序列化缺失） |
| W1C Acceptance surface | LEGACY / REGRESSION ONLY | normal resolver 排除；见 §13 |

---

# 11. Persistence Status

| 内容 | 状态 | 证据 |
|---|---|---|
| Player `WorldPosition` / `PlayerPartyTravel` | DONE | `StrategicSnapshotHelper.Capture/RestorePlayerPartyTravel` |
| `WorldPresence`（含 AtSite + precise anchor） | DONE | `StrategicSnapshotHelper` AtSite 分支写/读 `HasWorldPosition` + `WorldX/Y` |
| PlayerParty membership / followers | DONE | `PlayerPartySnapshotRestore`（按存档顺序重建，跳过 Join 校验） |
| **Outdoor stateful objects（destructible / farm plot）** | **NOT DONE / KNOWN PERSISTENCE GAP** | `WorldSnapshot.OutdoorDestructibles/OutdoorFarmPlots`（`WorldSnapshot.cs:55-58`）存在；`SnapshotService.CaptureOutdoorStatefulObjects`（line 338/398）与 `RestoreOutdoorStatefulObjects`（line 845/908）存在；但 `JsonSnapshotSerializer` 里 **`grep -i outdoor` = 0 命中** → 正式 JSON save pipeline **不写也不读**这两组数据。 |

**结论（不要写成"DONE"）**：Runtime `WorldSnapshot` 对象层面有 capture/restore 代码，但经**正式 JSON save** 时 Outdoor destructible / farm state **会丢**。修一个 serializer 字段即可，但**本轮不修**（属于新实现，超出 checkpoint 任务范围）。

（附注：这正是 EditMode `MainWildernessSurfaceW1DTests.OutdoorStatefulObjects_AreIncludedInSnapshotJson` 失败的原因；该失败为既有问题，与 A/B 修复无关。）

---

# 12. Legacy Code Still Present — DO NOT DELETE BLINDLY

| Legacy | 现状 | 为什么保留 / 何时仍在用 |
|---|---|---|
| **W1B** `ContinuousWildernessLoadedSet` | 存在，作为较低优先级 Wilderness presenter | Normal authority 已是 Main Surface；W1B 仍需服务 Main Surface 未覆盖时的兼容呈现（与 205 sealed 一致） |
| **Wilderness LocalMap** (`EnterWildernessLocalMap`) | 存在 | ① Main Surface **outer coverage 之外**的兼容兜底；② legacy Wilderness 行走/AutoTravel 路径；③ old save |
| **`AtWorldSite`（location kind / transition）** | enum 仍在（50 个文件引用） | enum 值是普通状态标签，正常 Outdoor 不再写入 transition；`EnterWorldSiteAsParty` 的 LocalMap 分支只服务**未迁 legacy Site**（`UsesContinuousOutdoorSurface == false`），迁过的 Site 走 `AtWorldPosition` 分支 |
| **SurfaceExit 旧 presenter** | `HostSurfaceExitZonePresenter` 保留 | 未迁 legacy Site 边界 + 历史 regression；Normal Continuous 不使用 |
| **Legacy LocalMap mapping** | `PlayerPartyLocalMapMaterializationService`、`LocalCombatCasualtyHandoffService`、`WildernessLocalWorldProjection` 里的 `WorldSiteSpatialMapping` / `HexFootprintSpatialMapping` | Interior/Cave 与 old save/legacy Site；**不是** opening entity bake authority（§8.4） |
| **Old save restore** | `LoadedLocalMapPlacementSnapshotRestore`、`StrategicSnapshotHelper`、`PlayerPartySnapshotRestore`、`SnapshotActiveControlledLocalMapResolver` | 读旧档需要 |
| **Outer coverage handoff** | `ContinuousOutdoorSurfaceRuntime.HandoffToLegacy()` → `WorldTravelService.EnterWildernessLocalMap(...)` | **OUTER COVERAGE COMPATIBILITY**：当 canonical position 跑到**整个 authored Surface coverage 之外**/跨 outer boundary 时兜底。**不是** Outdoor WorldSite transition |
| W1C acceptance surface & teleport | 存在 | 历史 regression asset only（§13） |

**规则**：这些是**故意保留**的兼容层。删除前必须证明「没有 old save、没有 Interior、没有未迁 Site、没有 coverage 外」——**本阶段不要动**。

---

# 13. Diagnostics

正常只读诊断（`HostLevelTesterCheatPanel` + `PlayableHostBootstrap`）：

| 诊断 | 位置 | 应读到的正常值 |
|---|---|---|
| `Authority=ContinuousOutdoorSurface SurfaceId=… AcceptanceOnly=false` | LevelTester 顶部只读显示 | 正常 NewGame 一定是 `AcceptanceOnly=false` |
| `OpeningPopulation: Expected / Materialized / Views / SpatialValid / SpatialInvalid` | `OpeningPopulationDiagnostic` | `SpatialInvalid=[]` |
| `… Barrier=applied InvalidSpawn=0 RealignedViews=n AnchorBake=Validated` | 同上 | `AnchorBake=Validated`；`RealignedViews` 首次启动非 0 属预期 |
| `[OpeningAnchorBakeInvalid]` | 同上（仅非 Validated 展开） | 正常不应出现 |
| `[ContinuousStartupInvariantFailure]` | `Debug.LogError` | 正常不应出现 |
| `LastMovementDiagnostic` / `SurfaceEgressStatus` / `NextOutsideHex` | `ContinuousOutdoorSurfaceRuntime.DescribeDiagnostics()` | 用于判断 outer coverage egress |
| `ContinuousStartupPostconditionDiagnostic` | `PlayableHostBootstrap:275` | 启动后置条件摘要 |

### W1C Acceptance Teleport（务必写清）

- 位置：LevelTester → **「Regression / Legacy Acceptance（非正常入口，默认收起）」** 折叠区（`PlayableHostBootstrap.ShowLegacyAcceptanceTools` 默认 false），按钮文案 `传送：Continuous World W1C Acceptance（legacy regression）`。
- 语义：`ContinuousOutdoorSurfaceRuntime.TryActivateAcceptanceAtCurrentWorldPosition()`，**只**服务于历史 W1C regression。
- **W1C Acceptance Teleport = legacy regression tool ≠ Continuous World enable button ≠ normal gameplay entry。**
- Normal resolver 证据：`OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(...)` 默认 `includeAcceptanceOnly: false`，并在 `!includeAcceptanceOnly` 时跳过 `AcceptanceOnly` surface；`ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite` 同样排除 `AcceptanceOnly`。
- **正常制作人验收：不需要、也不应该点击这个按钮。**

---

# 14. Known Issues / Deferred

### 14.1 Outdoor stateful JSON persistence —— **KNOWN PERSISTENCE GAP**
见 §11。DTO/Capture/Restore 有，JSON serializer 没有 → 正式 save 丢 Outdoor destructible / farm state。**未修（本轮不修）。**

### 14.2 WorldSite PhysicalRegion 仍是 V1（战略 footprint 近似）—— **IMPLEMENTED V1 / FINAL GEOMETRY DEFERRED**
`WorldSitePhysicalRegionQuery` 目前是 `WorldPosition → Hex → Strategic WorldSite footprint`（并过滤 `UsesContinuousOutdoorSurface`）。它不是 hand-authored 的物理村庄/城镇 polygon。最终 geometry 属 Surface Geography 阶段。

### 14.3 Final Surface Geography 未做 —— **NOT DONE**
尚未做：真正大陆 terrain authoring、road 物理烘焙、river、mountain/cliff、biome 分布、最终 forest geography、city physical authored layout workflow、Surface Bake Editor / authoring UX。当前 Surface 主要是 migration/placeholder geography；**Strategic Hex 仍不是最终 physical terrain authority**。

### 14.4 Stale W1C naming —— **NAMING / CLEANUP DEBT（非 blocker）**
"W1C" 字样在 Scripts 中仍有 **39 处**，集中在 `ContinuousOutdoorSurfaceRuntime.cs`（15，含类注释 "W1C presentation owner…" 与 `[W1C]` 日志）、`HostLevelTesterCheatPanel.cs`（6）、`HostWorldMapPanel.cs`（5）、`PlayableHostBootstrap.cs`（3）。**本轮不做大范围 rename。**

### 14.5 Mouse outer acceptance egress（若仍 relevant）
只与 W1C acceptance surface 相关；Normal Main Surface 走 `HandoffToLegacy`（outer coverage compatibility）。

### 14.6 最新 A/B 尚未由制作人 Unity 验收 —— **PENDING PRODUCER ACCEPTANCE**
见 §15/§16。

### 14.7 既有（本轮无关）EditMode 失败
- `MainWildernessSurfaceW1DTests.OutdoorStatefulObjects_AreIncludedInSnapshotJson` → §11 同一 serializer gap。
- 若干 Phase 2B/2C 时代 suite（`FormalArmyPhase3AuthorityTests`、`PlayerPartyContinuousWorldPhase2CTests`、`PlayerPartyWorldTravelPhase2BTests`、`PlayerPartyFollowerLocalMapTransitionTests`、`SnapshotActiveControlledLocalMapResolverTests`、`BackgroundWildernessLocalMapMaterializationTests` 等）在 headless 环境**改动前后逐条相同地失败**，起因是 Phase 5R-B3B「ingress 前必须有 canonical physical position」不变量与陈旧 fixture，以及部分测试直接调用 Unity `Application.dataPath`（Unity 之外是 ECall）。**不是本轮回归；本轮未调查。**

---

# 15. Producer Acceptance History

## Producer-confirmed（制作人实际在 Unity 里报告过 / 验收过）

| 项 | 来源 |
|---|---|
| W1A Unity 人工 sanity acceptance 通过 | devlog 2026-09-09 条目 |
| W1B 无缝 wilderness pair 呈现 | 205（ACCEPTED / SEALED） |
| W1C streaming / chunk 邻域、内部移动、outer WASD handoff、方向映射修复 | 206（ACCEPTED / SEALED）+ devlog |
| Normal NewGame 可**直接**进入 Main Continuous Surface（W1C teleport 不再是入口） | 制作人复验（209/210 记录 + 本轮制作人陈述） |
| Wilderness / Outdoor WorldSite 连续移动正常 | 本轮制作人陈述 |
| 荒村 NPC 全部 materialize，并能正常 Schedule / Work | 本轮制作人陈述 |
| 青石镇等其它 Site population 正常 | 本轮制作人陈述 |
| Outdoor Site 建筑 / NPC / 工作行为在若干轮中实际看到能运行 | 208/209/210/211 记录的复验 |

## Producer-reported failures → repaired（制作人 Play 实测发现，DS 已修）

| 症状 | 修复 | 状态 |
|---|---|---|
| W1C 黑屏 / Editor 卡死（伪网格生成数百万 prefab） | 移除伪网格路径，改 physical rect + `SourceCellsW/H` | 已修，制作人复验通过 |
| NewGame 荒村没有其它 NPC、同伴甲乙不出现 | `OpeningSpawnWorldPresenceApplier` + population normalize + FINAL population barrier | 已修，制作人复验通过 |
| `Expected=17 Materialized=17 Views=17` 但只有 1 人可见 | baked `openingEntityAnchors`（210）+ view realign（211） | 已修，制作人复验通过 |
| NPC 到工作时段被每 tick 拉回 opening anchor | population Add/Keep/Remove diff + arrival-radius commit | 已修 |
| `AnchorBake` 误报 ①：`authored place '…' missing from WorldRegion` | §5 真源改为 checked-in Content（`TryResolveSiteSourcePlaceSet` / `TryResolvePlaceLocalPosition` / `TryResolveContentHexSize`），无真源 → `Skipped` | 已修（212 §2.5.1），**待重验** |
| `AnchorBake` 误报 ②：`village_recruit slot=2 == 主角 anchor` | slot 真源改为 checked-in anchors 的 authored 顺序；§5 校验主体改为 Content 侧复算 | 已修（212 §2.5.2），**待重验** |

## Implemented but producer-unverified（**不要当成 SEALED**）

- Hybrid (c) opening placement 的**屏幕观感**（住房内站位是否符合制作人预期）；
- 同伴甲/乙 在 Continuous Outdoor 加「跟随主控」（不要求 same LocalMap）；
- follow 跨 Site boundary / Hex / Chunk 持续跟随；
- StopFollow 保留精确 Continuous 位置；
- Interior 下不同 LocalMap 仍被拒（回归保护）；
- Outdoor WorldSite migration 整体（未宣布 ACCEPTED）。

## Not re-tested（本轮）

- 全部 runtime 行为（本轮**没有**重新编译/跑测试/跑 Play；见 §18 的 Validation Budget）。
- AutoTravel 进出/穿越 Site 全链路。

---

# 16. NEXT SESSION — FIRST ACTION

**下一 GPT / DeepSeek 不要马上开始新阶段。**

第一件事：请制作人对最新 **212 A/B** 做 Unity 人工 Play 验收：

1. **New Game → 荒村 NPC authored placement 观感正确**
   - 阿石/阿土/阿禾/阿兰/阿杏/阿枝 在**凡人住房**；三名巡卫在**巡卫住房**；主管在**主管住房**；
   - 阿木在**树林**、阿柴在**矿洞**、阿青在**药田**、将老在**灵泉**；主角/同伴/村内可招者在**农田杂役区**；
   - 诊断行 `AnchorBake=Validated`，**没有** `[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid`。
2. **Schedule 正常工作**：时间推进后从住房走到工作区；不出现 `Move retry budget exhausted`。
3. **同伴甲/乙 →「跟随主控」直接成功**（Continuous Outdoor 不要求 same LocalMap）；**不出现** `Must be on the same LocalMap as the active character.`
4. **跟随持续性**：主控移动 → 跨 **Site boundary / Hex / Chunk** 仍持续跟随；同伴从**当前位置**走向 formation slot（不瞬移、不重复）。
5. **StopFollow**：同伴停在当前 Continuous 位置（不回荒村 arrival 点）。
6. **Interior**：洞府内室等独立空间，两人不在同一 LocalMap 时「跟随主控」**仍必须被拒**。

**如果 PASS**：才可以把
- `Outdoor WorldSite Continuous Surface Migration`（208）**以及** 212 标为 **ACCEPTED / SEALED**；
- 然后才讨论下一阶段（§17）。

**如果 FAIL**：把制作人观察到的现象 + 诊断行原文交给 DS，只修该现象（不要顺手扩范围）。

> 每次 Play 前请先确认 Unity 已完成一次脚本重新编译（Console 无 `error CS`），否则会看到旧程序集的日志。

---

# 17. Likely Next Design Topic — NOT YET AUTHORIZED

候选（**只记录，不给实现指令**）：**Surface Geography / Real Outdoor World Authoring** —— 从 migration placeholder Surface 走向真正的大陆地理内容（terrain / road / river / mountain / biome / city physical layout / Surface Bake Editor UX）。

> **下一阶段尚未获制作人确认。不得根据本 Handoff 自动开工。**

---

# 18. Collaboration Rules（工作规则）

- DS（DeepSeek）负责实现；**制作人负责 Unity 人工验收**。DS **不要**代替制作人做 Unity 人工验收，也不要把 unit test 当成 producer acceptance。
- Foundation 类工作**中途不要停**；只有 **hard blocker** 才停（例如 Unity/工具链不可用、GitHub 拒收、架构冲突需要制作人裁决）。
- DeepSeek 额度有限：**不要跑 Full EditMode**，除非制作人明确要求。
- 实现完成后**先等 Producer acceptance**，不要直接进入下一阶段。
- 切换新议题时，assistant 先给建议，由制作人决定。

**Validation Budget（默认）**

- 允许：compile sanity、**2–6 个 targeted tests**、`git diff --check`、静态代码/Content audit。
- 禁止：新建测试基础设施、mutation / 鉴别力测试、大范围 baseline suite、调查与本轮无关的既有失败。
- 若本轮**只改文档**：不需要重新编译/跑测试；历史结果必须标为 "Last reported validation"，**不得**伪装成本次执行结果。

**本环境已知工具事实（省时间）**

- Unity Test Runner 在工程被交互式 Editor 占用时不可用；Unity batchmode 在本沙箱连不上 licensing IPC（返回 199）。
- 因此 project 内有：`tools/offline-compile.ps1`（复用 Unity Bee `.rsp` + Unity 自带 Roslyn 离线编译）与 `tools/run-headless-tests.ps1` + `tools/headless-tests/Program.cs`（Unity Mono `mcs`+`mono` 反射跑 EditMode `[Test]`，`-DropUnityEditor` 让测试走 `XIANXIA_BASEGAME` / `HeadlessContentRoot` 注入）。
- PowerShell 中执行 Unity/batch 命令请用 `cmd /c "…"`（直接 `& Unity.exe` 在本环境会静默失败）。
- ⚠️ 不要用 PowerShell `Get-Content`（默认 ANSI）+ `Set-Content` 改写含中文的 UTF-8 文档 —— 会破坏编码。用编辑器工具或 `-Encoding UTF8` 显式往返。

---

# 19. Recovery Checklist（新会话按顺序）

1. `git pull` / checkout `dev_openworld`（见 §2）。
2. 读 [ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)（Continuous Outdoor World Surface 架构）。
3. 读 W1C sealed 文档 [206](206-continuous-world-w1c-surface-chunk-grid-2026-09-09.md)（+ 204/205）。
4. 读 [208](208-continuous-world-outdoor-worldsite-surface-migration-v1-2026-09-10.md)（Outdoor WorldSite 迁移）。
5. 读 [212](212-continuous-outdoor-opening-placement-and-party-copresence-2026-09-11.md)（Opening placement + Party co-presence，含两次 Play 误报的修法）。
6. 读本文件（CURRENT HANDOFF）。
7. 检查当前 `git status` / `git log --oneline -10`，确认与 §2 一致（若 HEAD 已前进，先读新增 commit）。
8. **不要**使用 W1C teleport 作为入口（§13）。
9. 从 §16「NEXT SESSION — FIRST ACTION」继续。

---

# 20. Copy-Paste Context for a Fresh Model

> **如果你是新接手的 GPT / DeepSeek，请从这里开始。**
>
> 项目：`ProjectCultiva`（Unity 2022.3.6f1），仓库 `https://github.com/Siyuan-Yu/ProjectCultiva.git`，分支 **`dev_openworld`**，代码 checkpoint commit **`d34efc0`**。先读 `docs/40-process/213-continuous-outdoor-world-handoff-2026-09-11.md`（本文件），再按 §19 顺序读 ADR-0031 → 206(W1C sealed) → 208 → 212。
>
> **当前架构（不要回退）**：Normal NewGame 直接进入 **Main Continuous Surface**（`base:surface_main_wilderness_v1`）；**`WorldPosition` 是唯一 Outdoor 物理 authority**；**`SurfaceChunk`（1.4×1.4，`cellSize=0.028`）≠ Strategic Hex**，chunk 只拥有 presentation，Domain/Stateful object 拥有 identity/state；半径 1 邻域（正常 3×3）增量加载；`Composite WalkGrid` = Wilderness grid + Site blocker；世界 **+X = 东/右，+Y = 北/上**；Strategic road ≠ 可行走。**Outdoor WorldSite 是 Domain 战略身份 + Continuous physical region，不再是 LocalMap**；穿过村庄/城镇边界是连续移动，不是换图；**Interior / Cave / Dungeon 仍是独立空间**（LocalMap + Portal + 精确 Outdoor 返回）。W1C `base:surface_w1c_wilderness_acceptance` 只是 `acceptanceOnly` 历史 regression 资产，**normal resolver 永不选它**；LevelTester 的 W1C 传送按钮在默认收起的「Regression / Legacy Acceptance」区，**正常验收不需要也不应该点它**。
>
> **当前状态**：W1A / W1B / W1C = **ACCEPTED / SEALED**；W1D Gateway route = **NOT ACCEPTED / SUPERSEDED**；**Outdoor WorldSite Continuous Migration（208）= IMPLEMENTED / PENDING PRODUCER ACCEPTANCE**；**Opening placement + Party co-presence（212）= IMPLEMENTED / PENDING PRODUCER ACCEPTANCE**。7 个 `continuousOutdoor` Site 已迁入 Main Surface（646 chunks / 7 siteRegions / 75 SitePlacements / 18 SitePlaces / 18 OpeningEntityAnchors）。历史「8 Outdoor Sites」= 该 hex 世界里 `sites[]` 的未过滤总数，第 8 个是 MapEditor 遗留占位 `base:site_editor_8`（`localMapId` 空、1 hex、无 surface region、无任何内容引用），**不存在漏迁**。
>
> **Opening 规则**：authored 初始位置只在**第一次 materialize** 使用（`Initial authored position ≠ runtime current position`）；落点优先级 = runtime current anchor > baked precise `OpeningEntityAnchor` > baked SitePlace > arrival fallback；`SpawnStableKey ≠ DefinitionId`（`SimulationWorld.OpeningSpawnIdentities` 可从 Entity 反查）。authored place policy 是 **Hybrid (c)**：① explicit resident place（阿木→树林、阿柴→矿洞、阿青→药田，**不被 `homeWorkAreaId` 覆盖**）；② 仅当 LocationId 退化为 generic StartLocation 时才用 `homeWorkAreaId`（阿土/阿禾/阿兰/阿杏/阿枝→凡人住房；巡卫乙/丙→巡卫住房）；③ 都没有则保持 authored opening/start 语义；**主角/同伴/无 `homeWorkAreaId` 者不要猜住房**。`WorldSiteOutdoorBakeTransform` 是 SitePlacements / SitePlaces / OpeningEntityAnchors **共同唯一**的 authored bake truth（纯 AABB 线性归一化）；**不得**再用 `WorldSiteSpatialMapping`（V2 radial）或 `HexFootprintSpatialMapping`（带投影）作为 opening entity bake authority。slot index 真源是 checked-in anchors 的 authored 顺序，**与当前在场 population 无关**。
>
> **Party 规则**：`PlayerPartyLocalCoPresenceQuery` —— Continuous Outdoor 只要求**同一个 active continuous presentation scope**（**不要求 same LocalMap / same Site / same Hex**）；Legacy / Interior / Cave 仍要求 same LocalMap（独立空间）。`TryFollowActive` 的 Continuous 分支不写 LocalMap occupant、会同步 `WorldPresence` 与 traveling members、**不 teleport**（follower 从当前位置实时走到 formation slot）；`TryStopFollow` 保留当前实际 Continuous `WorldPosition`（不回旧 Site/起点）。
>
> **未验项（不要当成 SEALED）**：最新 212 A/B 尚未由制作人 Unity 验收 —— hybrid opening placement 的屏幕观感、同伴在 Continuous Outdoor 直接加入跟随、跨 Site/Hex/Chunk 持续跟随、StopFollow 保位、Interior 拒绝。生产者也尚未宣布 Outdoor WorldSite migration 整体 ACCEPTED。
>
> **已知重要债务（未修）**：① **Outdoor stateful JSON persistence gap** —— `JsonSnapshotSerializer` 里 `grep -i outdoor` = 0，正式 save 会丢 `WorldSnapshot.OutdoorDestructibles/OutdoorFarmPlots`（DTO/Capture/Restore 已有）；② `WorldSitePhysicalRegionQuery` 仍是**战略 footprint 近似 V1**，最终村庄/城镇 polygon deferred；③ **Surface Geography 未做**（terrain/road/river/mountain/biome/city layout/Surface Bake Editor）；④ "W1C" 命名债 39 处（非 blocker）；⑤ 若干 Phase 2B/2C 时代 EditMode suite 在 headless 环境既有失败（与本轮无关，未调查）。
>
> **故意保留的 legacy（不要盲删）**：W1B `ContinuousWildernessLoadedSet`、Wilderness LocalMap（`EnterWildernessLocalMap`）、`AtWorldSite`（enum 仍普遍使用）、`HostSurfaceExitZonePresenter`、Legacy LocalMap mapping（`PlayerPartyLocalMapMaterializationService` / `LocalCombatCasualtyHandoffService` / `WildernessLocalWorldProjection`）、old save restore、以及 `ContinuousOutdoorSurfaceRuntime.HandoffToLegacy()` 的 **outer coverage compatibility**（跑出整个 authored Surface coverage 之外时的兜底；**不是** Outdoor WorldSite transition）。
>
> **下一步只做一件事**：请制作人按本文件 §16 做 Unity Play 验收。**PASS 后**才把 208 + 212 标为 ACCEPTED / SEALED，**然后**才讨论下一阶段（大概率是 Surface Geography，**尚未授权**）。不要用 W1C 传送按钮当入口；不要跑 Full EditMode；不要顺手修无关 bug；不要把 unit test 当作 producer acceptance。Validation budget：compile sanity + 2–6 个 targeted tests + `git diff --check`。
