# 212 — Opening NPC Authored Placement Fidelity + PlayerParty Follow 移除 LocalMap gate

日期：2026-09-11
状态：已实现，等待制作人验收（未提交）
范围：**只修制作人点名的两项 migration regression**（A opening placement fidelity／B 同伴 Follow 的 LocalMap gate）。不进入下一阶段。
前置：[208](208-continuous-world-outdoor-worldsite-surface-migration-v1-2026-09-10.md)（site/placement 迁移）、[209](209-continuous-outdoor-opening-population-bootstrap-2026-09-10.md)（opening population）、[210](210-continuous-outdoor-opening-spatial-placement-2026-09-11.md)（anchor V1）、[211](211-continuous-outdoor-view-placement-realign-2026-09-11.md)（view realign）

---

## 0. 制作人复验输入

已成立（本轮不动）：Normal NewGame 直进 Main Continuous Outdoor；Wilderness／Outdoor WorldSite 连续移动正常；荒村 NPC 全部 materialize 且能 Schedule／Work；青石镇等其它 Site population 正常；Continuous World 不再依赖 W1C Acceptance 入口。

剩余两项明确 regression：

- **A** 荒村 Opening NPC 初始位置与旧 authored placement 不一致 —— 人都出现、AI 也能工作，但部分 NPC 没有出生在原来安排的住房／房间位置。
- **B** 同伴甲／乙加入「跟随主控」仍要求与主控处于同一个 LocalMap（`"Must be on the same LocalMap as the active character."`）；Continuous Outdoor 已经没有 Outdoor LocalMap，这是 legacy gate。

---

## 1. Part A — 实测数据对照（先量，不猜）

用真实 BaseGame + 真实 bootstrap 跑无头探针（`ContentPackageLoader.Load` → `PlayableDayBootstrap.Start(base:scenario_ch01_reference)`），对 `base:site_huangcun` 的 18 名 opening population 逐个复算落点。**修复前**的关键读数：

| DefinitionId | LocalLocationId（bootstrap 实际分配） | Authoring 事实 | 修复前 anchor | 问题 |
|---|---|---|---|---|
| 阿石 `mortal_a` | `loc_ref_houses` | resident + `homeWorkAreaId=workarea_houses` | `(4.96882,11.04000)` | 正确（住房内） |
| 阿土 `farmer_b` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_houses` | `(5.03898,10.08100)` | **落在农田，不在凡人住房** |
| 阿禾 `farmer_c` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_houses` | `(5.12298,10.08100)` | 同上 |
| 阿兰 `herb_b` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_houses` | `(5.37498,10.08100)` | 同上 |
| 阿杏 `herb_c` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_houses` | `(5.03898,10.16500)` | 同上 |
| 阿枝 `wood_c` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_houses` | `(5.12298,10.16500)` | 同上 |
| 巡卫乙 `guard_b` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_guard_quarters` | `(5.20698,10.08100)` | **落在农田，不在巡卫住房** |
| 巡卫丙 `guard_c` | **`loc_ref_labor_yard`（退化）** | `homeWorkAreaId=workarea_guard_quarters` | `(5.29098,10.08100)` | 同上 |
| 阿木 `woodcutter` | `loc_ref_forest` | resident | `(4.49381,9.71000)` | 与 authored forest 点相差 0.48 world |
| 阿柴 `wood_b` | `loc_ref_mine` | resident | `(4.48298,10.95600)` | 与 authored mine 点相差 0.085 |
| 阿青 `mortal_b` | `loc_ref_herb_field` | resident | `(5.71837,10.04600)` | 与 authored herb 点相差 0.119 |
| 巡卫甲／主管 | `loc_ref_guard_housing`／`loc_ref_supervisor_housing` | resident | 与 SitePlace 中心重合 | 数值正确但**与 Location center 不可区分** |

结论（两点，都可复现）：

1. **12 人的 `EntityLocationComponent.LocationId` 退化成 `StartLocationId = base:loc_ref_labor_yard`**，另 6 人各有 authored resident place —— 与 [210](210-continuous-outdoor-opening-spatial-placement-2026-09-11.md) 记的同一根因一致；因此「原来安排的住房」这条 authored 事实（人物 Content 的 `homeWorkAreaId`）在迁移后**完全丢失**。
2. `openingEntityAnchors` 是上一轮用**另一套规则**烘的（该 LocationId 绑定 `sitePlacements` 中心包围盒内的确定性网格），与 `sitePlacements`／`sitePlaces` 实际使用的公式**不是同一个**。实测：18 条 checked-in anchor 里有 **15 条**通不过「source local point → 共享 transform」校验（例：阿木 `delta=0.484`、阿土 `delta=0.962`）。

### 1.1 真正的 bake transform（决定性测量）

对 12 个 `sitePlaces` 与 68 个 huangcun `sitePlacements` 逐个比对三种候选公式：

| 候选 | sitePlaces 命中 | sitePlacements 命中（68 个） |
|---|---|---|
| 纯 AABB 线性归一化（local → footprint world AABB） | **12/12** | **68/68（0 mismatch）** |
| `HexFootprintSpatialMapping.TryLocalToWorldSurface`（线性 + footprint 内含性投影） | 9/12 | 29/68（**39 mismatch**） |
| `WorldSiteSpatialMapping`（V2 radial kernel） | 0/12 | 0/68 |

即 checked-in Content 的 authored bake truth 是**纯 AABB 线性归一化**，不含投影。此前项目里同时存在三套「Site → Continuous」公式，正是 §2 所指的「两套 transform」问题的具体形态。

---

## 2. Part A — 修复

### 2.1 单一 bake truth：`WorldSiteOutdoorBakeTransform`（新增，Core 纯函数）

`Assets/Scripts/Core/World/Strategic/WorldSiteOutdoorBakeTransform.cs`

```
domain = footprint 全部 hex 角点的 world AABB（复用 HexFootprintSpatialMapping.TryComputeWorldDomain）
u = Clamp01((localX - sourceMinX) / sourceSpanX)；v 同理
canonical = (domainMinX + u*domainSpanX, domainMinY + v*domainSpanY)
```

输入：source MapLayout bounds ＋ Site physical footprint ＋ hexSize ＋ source local point；输出 canonical WorldPosition。
**SitePlacements／SitePlaces／OpeningEntityAnchors／legacy LocalPosition→canonical 全部只走这一个公式**（并附 `TryUnbake` 供诊断）。类注释明确写清它与 `HexFootprintSpatialMapping`（带投影）／`WorldSiteSpatialMapping`（V2 radial）的边界，避免再混用。

已改用的调用点：`OpeningSpawnWorldPresenceApplier.TryResolveAuthoredAnchor`、`ContinuousOutdoorSpawnPresenceResolver.TryResolveCanonicalAnchor`、opening anchor bake／校验。

### 2.2 authored placement 权威顺序（制作人确认的 (c) 混合）

`Assets/Scripts/Data/Bootstrap/ContinuousOutdoorOpeningPlacementResolver.cs`（新增）：

1. **`AssignedLocalPlace`** —— bootstrap 实际分配的 LocalPlace，且**不是** LocalPlaceSet 的 `startLocationId` 退化值（= authored `residentNpcDefinitionId`）。
2. **`HomeWorkArea`** —— 人物 Content 的 `homeWorkAreaId` → 工区 `locationId`（**只有退化到 StartLocationId 的人**走这条）。
3. **`StartLocation`** —— 无任何 authored placement 时的兜底。

每个候选都要求该 location 绑定到本 Site（`surface.sitePlaces`），否则 `Unresolved`（不猜）。

于是 18 人恢复为：

| 出生点 | NPC |
|---|---|
| 凡人住房 `loc_ref_houses` | 阿石、阿土、阿禾、阿兰、阿杏、阿枝 |
| 巡卫住房 `loc_ref_guard_housing` | 巡卫甲、巡卫乙、巡卫丙 |
| 主管住房 `loc_ref_supervisor_housing` | 杂役主管 |
| 树林 `loc_ref_forest` | 阿木（尊重 resident place） |
| 矿洞 `loc_ref_mine` | 阿柴 |
| 药田 `loc_ref_herb_field` | 阿青 |
| 灵泉 `loc_ref_spring` | 将老（`homeWorkAreaId=workarea_spring_check`） |
| 农田杂役区 `loc_ref_labor_yard` | 主角、同伴甲、同伴乙、村内可招者 |

### 2.3 anchor 从 authored source local point 烘，不再用 Location center

`Assets/Scripts/Data/Content/WorldSiteOutdoorOpeningAnchorBake.cs`（新增）：

- slot 0 **恒为**该 location 的 authored presentation 点（§3：不允许用 Location center 覆盖 precise LocalPosition）；只有当该 authored 点落在自己的 bound placement 矩形**之外**（Content 自相矛盾，例如 `loc_ref_spring` 的 presentation 不在 `zone_spring` 内）才改用该矩形中心，保证 §6「anchor 位于对应 bound location 的合理内部区域」。
- 其余 slot 按固定 8 向环、步长 `3 × source CellSize` 展开，clamp 进 authored placement 矩形（有）与 source MapLayout bounds（始终），并保证互不重合。
- 全部 slot 点再经 §2.1 的共享 transform 烘成 canonical anchor。纯函数、无随机、无时间。

checked-in `openingEntityAnchors` 已按此重烘（`spawnKey` 形态不变，`sourceLocationId` 改为实际 authored place）。18 条 anchor 现在**全部**通过 §5 校验。

### 2.4 spawn identity：SpawnStableKey ≠ DefinitionId（§4）

`Assets/Scripts/Core/World/OpeningSpawnIdentityBoard.cs`（新增）＋ `SimulationWorld.OpeningSpawnIdentities`（session-only，不进 Snapshot）：

- GameStart（`OpeningSpawnWorldPresenceApplier.Apply`）按 authored spawn 顺序为每个 spawn 注册稳定 key：`definitionId`（authored index 0）或 `definitionId#n`；**可从 spawned Entity 反查**，绝不用 Unity InstanceId／随机／runtime order。
- `ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor` **删掉** `DefinitionId == input` 兜底：只按 `SpawnKey` 精确匹配，同一 Definition 多次 spawn 不再吃 first-match anchor。
- `ContinuousOutdoorSurfaceRuntime` 落点解析改用 `ResolveOpeningSpawnKey(entity)`（identity board → 缺 key 时退回 authored index 0）。

### 2.5 bake validation（§5，不能只验证「anchor 存在」）

`ContinuousOutdoorOpeningPlacementResolver.ValidateBakedAnchors`：

- 每个 opening spawn 必须存在**唯一** checked-in `openingEntityAnchors`（同 Site 内 spawnKey 不得重复）；
- `shared bake(source local point) ≈ checked-in anchor`（epsilon `1e-3` world）；
- 失败逐条输出 `spawnKey / definitionId / checked-in / shared bake / source local / place / slot / delta`；
- 三种 outcome：`Validated`／`Skipped`（该 Site 没有 checked-in anchor，或 Content 查不到 authored 真源 → **只在诊断显示，不打断启动**）／`Failed`（真的 anchor 缺陷 → 进 startup invariant）。

该校验接进 `ContinuousOutdoorSurfaceRuntime.TryValidateStartupPostconditions`（`Failed` → `[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid`）与制作人诊断行（`AnchorBake=Validated|Skipped|Failed`，非 Validated 时展开 `[OpeningAnchorBakeInvalid]`）。

#### 2.5.1 制作人 Play 实测发现并修掉的一处缺陷（真源必须是 Content）

首次 Play 时出现：

```
[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid:
  spawn base:character_ch01_ref_supervisor ... has no authored source placement:
  authored place 'base:loc_ref_supervisor_housing' missing from WorldRegion
  （17 条同类）
```

根因：`TryBuildPlan` 当时把**运行时 `WorldRegion` 板**当成 authored placement 真源。Continuous Outdoor 正常运行时**不会加载** legacy WorldRegion place set（这正是迁移的目的），于是启动 invariant 在 `ActivateSurface`／`TryInitialize` 时刻必然误报。这既会让 `world.WorldRegion.StartLocationId` 为空（退化哨兵失效 → 9 名 NPC 会被误判成 `AssignedLocalPlace(农田)`），也让 bake 校验拿不到 place 点。

修复（真源改为 checked-in Content）：

- 新增 `ContinuousOutdoorStartupPlanner.TryResolveSiteSourcePlaceSet`（source MapLayout → 唯一 LocalPlaceSet）与 `TryResolvePlaceLocalPosition`（LocalPlaceSet 的 `presentationX/Z`）；
- `ResolveAuthoredPlace` 的 `startLocationId` 哨兵：运行时 WorldRegion 为空时取 Content LocalPlaceSet 的 `startLocationId`（两者同源）；
- bake 的 place 点：**Content 优先**，WorldRegion 仅作为兜底；
- 无 authored 真源 → `Skipped`（不再误报为 invalid）；只有该 Site 真的声明了 `openingEntityAnchors` 才校验（青石镇等普通 Site 不会被误判）；
- 新增回归测试 `A2_AnchorValidationDoesNotDependOnRuntimeWorldRegion`（刻意 `ClearLocations()` 后要求 plan／bake 校验／materialize 侧 `ResolveInitialPlacement` 结果逐条与已加载时相同）与 `A3_SiteWithoutCheckedInAnchorsIsNotReportedAsInvalid`。

#### 2.5.2 制作人第二次 Play 实测：slot 不得由「当前在场 population」决定

第二次 Play 出现（单条）：

```
[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid:
  spawn base:character_village_recruit anchor (5.206980,10.270000)
  != shared bake (5.252905,10.239250) from source local (2.6213,-3.8787)
  place=base:loc_ref_labor_yard slot=2 delta=0.055271
```

`(5.252905,10.239250)` 正是**主角**的 anchor —— 即 `village_recruit` 被算成了 slot 2，把主角的槽位占了。

根因：`TryBuildPlan` 的 slot index 当时按「**当前在场**的 population 在该 place 内的 spawnKey 排序」给出。但 startup invariant 运行时，Party 成员（主角、已入队的同伴）已经被 startup transaction 写成 `AtWorldPosition`，因此**不属于** `site population`（`IsUngroupedResidentAtSite` 只认 `AtSite`）→ 主角缺席 → 后面每个人的 slot 全部前移一格 → 与 checked-in anchor 错位。

注意：**NPC 实际落点是对的**（materialize 只按 spawnKey 查 checked-in anchor，与 slot 无关），错的只是这层校验。

修复（slot 真源改为 Content，与在场者／实体状态完全无关）：

- 新增 `BuildAuthoredSlotRegistry(surface, siteId)`：`sourceLocationId → 该 place 内按 spawnKey ordinal 排序的 spawnKey 列表`，直接由 checked-in `openingEntityAnchors` 推导；anchor 里声明过但当前不在场的 spawn（如主角）**仍占据它自己的 slot**。
- `TryBuildPlan` 的 slot index 改为「查该登记表」；未登记的新 spawn 才按 spawnKey ordinal 依次补位（bake 工具首次生成时走这条）。
- **§5 校验主体改为 Content 侧**：新增 `TryValidateAuthoredAnchors` —— 逐个 checked-in anchor 用（`sourceLocationId` ＋ 它在登记表里的 slot）复算 shared bake 并比对，**完全不读实体／population**；hexSize 也从 Content 解析（`TryResolveContentHexSize`，Site 所属 HexWorld，多图含同一 Site 则拒绝）。
- 实体侧只保留「GameStart 建过 stable spawn key 的 opening spawn 必须能查到自己的 anchor」，并且**只认 identity board 里登记过的 authored spawn**（普通 background 居民不要求 anchor；也避免了「NPC 走动后 LocationId 变了」造成误判）。
- 新增回归测试 `A4_AnchorValidationIsIndependentOfPopulationMembership`：把主角改成 `AtWorldPosition` 后用真实 `CollectCharacterIdsPresentAtWorldSite` 重新收集 population（数量确实减少），要求校验仍通过且在场者 slot／落点与完整 population 时逐条一致。

### 2.6 walkability 语义（§6，不混淆「在 grid 内」与「可走」）

新增 targeted 测试断言：housing anchor 必须落在对应住房 migrated placement 矩形内部、**不得**落在 source MapLayout 的 `BlocksMovement` placement 上、也**不得**落在 `sitePlacements` 中 `blocksMovement=true` 的 rect（= runtime CompositeWalkGrid 的 Site blocker）上。实测荒村 18 条 anchor 全部满足 —— **不需要把任何 NPC 吸到建筑外，也没有需要修的 blocker bake**。

### 2.7 §7 未被破坏

opening authored position 仍只用于**第一次** materialize（`ContinuousOutdoorOpeningPopulationBootstrap` 的 Add/Keep/Remove diff 未改）；`RealignMaterializedViewPlacements` 继续跳过 PlayerParty 成员与正在移动的实体。Schedule position commit 逻辑未动。

---

## 3. Part B — 移除 Continuous Outdoor 的 LocalMap gate

### 3.1 统一 co-presence：`PlayerPartyLocalCoPresenceQuery`（新增，Core）

```
Continuous Outdoor presentation scope（V1）
  = PlayerParty 处于 AtWorldPosition
    AND 不在 Interior
    AND runtime 已建立连续呈现 scope（有 loaded continuous site 或已 materialize 实体）
```

- 是 → candidate 与 active 只需属于**同一个 continuous presentation scope**：
  active 在 scope 内、candidate 已 materialized（或属 traveling members）、candidate living／can act、
  candidate 不属 FormalArmy、candidate 不处于真正独立空间（`InEncounter`／departing）presence。
  **不要求** same SiteId／same Hex／same LocalMap（Site/Hex 是 context，不是 co-presence 边界）。
- 否（Legacy／Interior／Cave）→ 保留 `PlayerPartyRuntime.IsOnSameLocalMap`（同一 LocalMap occupant）。

### 3.2 `PlayerPartyRuntime.ValidateJoin` 改用该 query

错误提示改为空间中性：`需要与主控处于同一可交互空间。`（`PlayerPartyLocalCoPresenceQuery.DeniedPlayerMessage`），Normal Continuous Outdoor join **不再**调用 `IsOnSameLocalMap`。后者保留为 legacy compatibility（Interior／Cave／old save／既有调用方），并补了「不得用于 Normal Continuous Outdoor join」的注释。

### 3.3 `HostPlayerPartyController.TryFollowActive`

- Continuous Outdoor：**不写** `world.LocalMap.AddOccupant(candidate)`；
- 加入后立即 `BackgroundCharacterTravelService.CancelTravelIfAny` → `PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion`（LocationKind=AtWorldPosition → `AtWorldPosition(WorldPosition, CurrentHex)`；AtWorldSite → `AtSite(SiteId)`）→ `CaptureTravelingMembersForPartyTransition`（新 follower 进 traveling members）；
- **不 teleport**：不写 view transform，仍走 `OrderFollowerTowardActive` → formation slot → realtime follow；
- Legacy／Interior：保留原 `AddOccupant` 逻辑。

### 3.4 `HostPlayerPartyController.TryStopFollow`

Continuous：优先用 follower 实际 view 落点（`ContinuousOutdoorSurfaceRuntime.PresentationToWorld`），否则用已有 precise anchor，经 `PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition` 恢复为普通独立角色 presence（落在 Continuous Site 内 → `AtSite + 精确 anchor`；荒野 → `AtWorldPosition`），并刷新 traveling members、清 LocalMap occupant。**不丢位置、不回 Site arrival、不由 LocalMap occupant 决定位置。**

同时把 `EntityLocationComponent.PresentationOverride` 刷新为**当前 view 位置**：否则下一次 materialize reconcile 的 `RealignMaterializedViewPlacements` 会把 view 拉回 follow 之前的过期 override（= 位置被回退）。写入值与 view 相同，因此是 no-op 对齐。

---

## 4. 验证（本环境实际执行）

Unity Test Runner 在本环境不可用（工程正被交互式 Editor 持有；Unity batchmode 无法连上 licensing IPC，返回码 199）。因此：

- `tools/offline-compile.ps1`：复用 Unity Bee 的 `.rsp`（同 defines／同引用／同 analyzer），只重扫 source 列表，用 Unity 自带 Roslyn（`Editor/Data/DotNetSdkRoslyn/csc.dll`）离线编译。
- `tools/run-headless-tests.ps1` + `tools/headless-tests/Program.cs`：用 Unity Mono（`mcs` + `mono`）编译并反射执行 `XianXia.Tests.dll` 的 `[Test]`（`-DropUnityEditor` 让测试走 `XIANXIA_BASEGAME`／`HeadlessContentRoot` 注入，避开 `Application.dataPath` 这个 ECall）。

| 手段 | 结果 |
|---|---|
| Core / Data / Host(Unity) / EditMode Tests 离线编译 | **0 error**（仅既有无关 warning：`HostWorldMapPanel.cs:795` CS0162、`HostPlayerPartyController.cs:53` CS0414、`ControlCoreState.PlayerControlled` obsolete 等） |
| **targeted A** `ContinuousOutdoorOpeningPlacementBakeTests.A` | PASS（18/18 spawn 唯一 anchor 且 `shared bake ≈ checked-in`） |
| **targeted A2/A3** 同文件 | PASS（WorldRegion 为空时 plan／bake 校验／materialize 侧 `ResolveInitialPlacement` 逐条相同；无 anchor 的 Site 不误判） |
| **targeted A4** 同文件 | PASS（Party 成员变 `AtWorldPosition` 后 population 减少，slot／落点仍与完整 population 逐条一致） |
| **targeted B** 同文件 `.B` | PASS（住房 NPC 落在对应住房矩形内、不在 source blocker／Site blocker 上） |
| **targeted C** 同文件 `.C` | PASS（全部命中 `BakedOpeningEntityAnchor`，零 `BakedSitePlace`／arrival；同 place 多人 anchor 互不重合且非 slot 0 不等于 Location center） |
| **targeted D** 同文件 `.D` | PASS（同 DefinitionId、不同 spawnKey → 各自 anchor；key 缺失不得用 DefinitionId 兜底） |
| **targeted E/F/G** `PlayerPartyLocalCoPresenceTests` | PASS（Continuous 无 ActiveMapLayoutId 可 join；SiteId 不同仍可 join；Interior 不同 LocalMap 必须拒且提示不含 LocalMap；Legacy 同图 join 仍可用） |
| **targeted H/J** 同文件 | PASS（加入后 ∈ Members、∈ TravelingMembers、presence == motion authority、不写 LocalMap occupant；Stop Follow 保留 precise 位置） |
| 回归 `ContinuousOutdoorOpeningSpatialTests`（含重写的 G） | 7/7 PASS |
| 回归 `ContinuousOutdoorOpeningPopulationTests` | 7/7 PASS |
| 回归 `ContinuousMaterializePlacementSyncTests` | 7/7 PASS |
| 回归 `ContinuousOutdoorStartupTransactionTests` | 9/9 PASS |
| 回归 `ContinuousRuntimePerformanceTests` | 11/11 PASS |
| 回归 `MainWildernessSurfaceW1DTests` | 10/11 PASS（唯一失败见 §5，与本轮两项目标无关） |
| 回归 `PlayerPartyRuntimeTests` | 12/12 PASS |
| 合计 | **78 个 case：77 PASSED / 1 FAILED** |
| `git diff --check` | exit 0 |

### 4.1 鉴别力验证（baseline 对照）

把 `PlayerPartyRuntime.ValidateJoin` 临时改回改动前的 `IsOnSameLocalMap` gate，重编译后跑同一批测试：

- 7 个「相邻历史 suite」（FormalArmyPhase3Authority / PlayerPartyContinuousWorldPhase2C / SnapshotActiveControlledLocalMapResolver / BackgroundWildernessLocalMapMaterialization / PlayerPartyWorldTravelPhase2B / PlayerPartyFollowerLocalMapTransition）+ 本轮 2 个新 suite：`TOTAL=152 PASSED=91 FAILED=61`。
- 其中**改动前就失败的 56 项**与**改动后完全一致**（同一批 test name 逐条相同）→ 这些失败与本轮两项无关，属于更早的 5R-B3B「ingress 必须有 canonical physical position」不变量与 `Application.dataPath` ECall 造成的**既有**陈旧 fixture／环境失败。
- 同时新增的 `PlayerPartyLocalCoPresenceTests` E/F/H/J **在 baseline 下全部失败**（`Must be on the same LocalMap as the active character.`），换回新实现后全部通过 → **测试确实咬住本轮行为**，不是空跑。
- 扩到 9 个相邻 suite（再加 Adr0023BattlePhases / CharacterWorldPresencePhase2A / StartupBootstrapAuthority）后为 `184 / 120 PASSED / 64 FAILED`；64 − 8（这 3 个额外 suite 的既有失败）= 56，与 baseline 一致。

---

## 5. 未验证／残余风险（诚实标注）

- **Play 路径未自测**：EntityView 实际 transform、相机取景、follow 的实走过程只能由制作人在 Unity 内确认；本轮 headless 证据 = 真实内容 + 真实 resolver + 真实 Domain 路径。§2.5.1（WorldRegion 真源）与 §2.5.2（slot 依赖在场 population）**两处缺陷都是制作人 Play 实测发现的** —— 说明 §5 校验这一层仍需 Play 复验；两处现已各有专门回归测试，且校验主体已改为**不读实体／不读运行时板**的 Content 侧复算。
- **上一版日志里 `authored place '…' missing from WorldRegion` 那条**：该字符串在当前源码里已不存在（`grep` 只剩注释与测试注释），只可能来自 §2.5.1 修复前的程序集 —— 即那次 Play 跑的是旧 DLL（或旧日志）。修复后该失败路径在代码上不可能再产生：Content 真源查不到时一律返回 `Skipped`（只在诊断显示），只有真正的 anchor 缺陷才会进 startup invariant。
- **`MainWildernessSurfaceW1DTests.OutdoorStatefulObjects_AreIncludedInSnapshotJson` FAIL（既有、与本轮无关）**：`JsonSnapshotSerializer` 里**完全没有** outdoor 字段处理（`grep -i outdoor` 于该文件 0 命中；该序列化器是显式 key 映射而非反射），而 `SnapshotService.CaptureOutdoorStatefulObjects` 会写 `WorldSnapshot.OutdoorDestructibles/FarmPlots` → JSON roundtrip 丢这两组数据。该测试不加载 Content、不触碰本轮任何改动；[208](208-continuous-world-outdoor-worldsite-surface-migration-v1-2026-09-10.md) 也记明这些 destructible/farm Snapshot case 当时**只编译未执行**。**本轮按「只修两项」的范围未修**，建议下一轮单独处理。
- **另有 56 项既有失败（相邻历史 suite）**：见 §4.1 的 baseline 对照 —— 改动前后逐条相同，起因是 Phase 5R-B3B 之后要求 ingress 前必须有 canonical physical position（`EnterWorldSiteAsParty` 明确返回 `"…no canonical physical position for context-preserving ingress (5R-B3B gap)."`），而 Phase 2B/2C 时代的 fixture 没有 `SetAtWorldPosition`；以及部分测试直接调用 `UnityEngine.Application.dataPath`（Unity 之外是 ECall）。本轮**未修**（超出「只修两项」范围）。
- **`loc_ref_spring` 的 Content 自相矛盾**：其 `presentationX/Z = (20,-6)` 不在自己的 `zone_spring` 矩形内（cells 78..90 / 26..38）。本轮按 §6 让将老的 anchor 落在该矩形**中心**，而不是把它放到矩形外；若制作人希望改 Content 的 place presentation，改完 anchor 会随之重烘。
- **`MainWildernessSurfaceW1DTests` 增加了 `HeadlessContentRoot` 注入点**（与 `ContinuousOutdoorStartupTransactionTests` 同一模式）；Unity Test Runner 下该字段为 null，行为与改动前完全一致。
- 未改：Surface streaming／SitePlacements 烘焙值／ControlCore／FactionFlag／Interior／AutoTravel／Save schema／Schedule 规则／pathfinder 性能／Gateway retirement／FormalArmy／PlayerParty 非 Follow 语义。

---

## 6. 制作人验收清单

1. 直接 New Game（不点任何 debug 按钮）→ 荒村：**阿石／阿土／阿禾／阿兰／阿杏／阿枝在凡人住房内**，**三名巡卫在巡卫住房内**，主管在主管住房内；阿木在树林、阿柴在矿洞、阿青在药田、将老在灵泉附近；主角／同伴甲乙／村内可招者在农田杂役区。
2. 诊断行应为 `... InvalidSpawn=0 RealignedViews=16 AnchorBake=Validated`，且**没有** `[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid`（上一版曾误报 `authored place ... missing from WorldRegion`，已修）。
3. 时间推进：他们能从住房正常走到工作区，Schedule 继续工作（`Move retry budget exhausted` 不应出现）。
4. 同伴甲／乙：Continuous Outdoor 下点「跟随主控」应**直接成功**（同一 loaded physical scope 即可），**不再**出现 `Must be on the same LocalMap as the active character.`（新提示为「需要与主控处于同一可交互空间。」且只在真的不同空间时出现）。
5. 加入后主控移动 → 同伴正常跟随；跨 Site boundary／Hex boundary／Chunk boundary 持续跟随；同伴**原地开始走**向 formation slot（不瞬移、不重复）。
6. 停止跟随后同伴停在当前 Continuous 位置（不回荒村 arrival 点）。
7. Interior（洞府内室／内景）：两人不在同一 LocalMap 时「跟随主控」仍必须被拒（旧独立空间规则不变）。
8. 到青石镇等普通 Site：**不应**出现 `Opening Site baked anchors invalid`（那些 Site 没有 checked-in opening anchor，校验自动 Skipped）。
