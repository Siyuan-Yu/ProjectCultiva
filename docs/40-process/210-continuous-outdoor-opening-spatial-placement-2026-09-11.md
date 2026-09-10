# 210 — Continuous Outdoor Opening Entity Spatial Placement（Opening Anchor Authority V1）

日期：2026-09-11
状态：已实现，等待制作人验收（未提交）

## 症状（制作人复验）

- Normal NewGame 已直接进入 Main Continuous Outdoor，Surface / buildings / places 正常；
- 诊断**全部成立**：`Expected=17 Materialized=17 Views=17 Missing=[] Barrier=applied`；
- 但画面上只看到主角 + 荒村主管，**其余荒村 opening NPC、同伴甲、同伴乙都看不到**；
- 到青石镇后当地 NPC 反而正常出现；
- 伴随 `MovingNpc=0`、`NpcPathReq/sec≈9`、`NpcRepath/sec≈9`、`PathBuildMs≈14ms`，Event 出现 `Move retry budget exhausted`。

结论：不是 presence／population／EntityView 数量问题，而是 **opening entity 的 Continuous spatial placement 错误**。

## 根因（用真实 BaseGame 内容实测，非推断）

写了一个无头探针：`ContentPackageLoader.Load(Content/BaseGame)` → `PlayableDayBootstrap.Start(scenario_ch01_reference)`
→ 对荒村 opening population 复刻 materialize 的落点计算并打印。结果：

```
=== PLACEMENT COLLISIONS (same world position) ===
  x12: 主角 | 同伴甲 | 同伴乙 | 巡卫乙 | 巡卫丙 | 农夫乙 | 农夫丙 |
       药农乙 | 药农丙 | 阿枝 | 村内可招募 | 将老
```

即 **12 名 opening entity 的落点完全相同**：world `(5.20698, 10.165)`，presentation `(186.0, 363.0)`。

机制（三个事实叠加）：

1. `WorldRegionBootstrap.PlaceOpeningSpawns` 对「找不到 resident place」的 opening spawn 一律退回
   `WorldRegion.StartLocationId`（= `base:loc_ref_labor_yard`），所以这 12 人的 `EntityLocationComponent.LocationId`
   全部相同；
2. `ContinuousOutdoorSurfaceRuntime.ReconcileOutdoorEntityMaterialization` 用
   `sitePlaces[(siteId, locationId)]` 的中心作为落点 → 12 人落到**同一坐标**；
3. materialize 还给每个人写入**相同**的 `PresentationOverride`，而
   `EntityViewSpawner.ResolvePresentationPosition` 对 `HasPresentationOverride` 优先、
   **绕过** `location.PresentationX + stack 偏移** 的分散逻辑** → 12 个精灵精确重叠。

叠加 2+3 后：17 个 EntityView 全部存在（所以 census 通过），但 12 个叠成一团 → 视觉上「只剩主管」。
NPC 同点互相挤压、目标一致 → 寻路反复失败 → `Move retry budget exhausted`（`MovingNpc=0`）。

## 修复（Opening Anchor Authority V1）

### 1. checked-in Opening Entity baked anchors（§6/§7）

- `OutdoorWorldSurfaceDefinition` 新增 `openingEntityAnchors`：
  `{ siteId, spawnKey, definitionId, sourceLocationId, worldX, worldY }`；
  `spawnKey` 是稳定 authored key（同 Site 同 definitionId 多次 spawn 时 `definitionId#n`），不用随机／时间戳。
- `ContentPackageLoader` 解析 + schema 白名单 + 校验（siteId 必须属于本 surface 的 siteRegions；
  同 Site 内 spawnKey 不得重复）。真实 BaseGame 写入 18 条。
- 生成方式（同一坐标空间、与 `sitePlaces` 同源）：取该 `LocationId` 绑定的全部 `sitePlacements`
  中心点包围盒，按 `cellSize × 3`（= 3 presentation 单位）步长做确定性网格，slot index 由
  `spawnKey` ordinal 排序后给出 —— 纯函数，无随机。

### 2. 落点优先级重定义（§9）+ 防御性校验（§11/§12）

新增 `Data/Content/ContinuousOutdoorOpeningAnchorResolver.cs`（纯 Data 层、可无头测试）：

```
RuntimePreciseAnchor      // NPC 真实移动过 / snapshot / dematerialize capture
  ↓ 仅当落在该 Site 的 baked physical envelope 内（否则拒绝并记录）
BakedOpeningEntityAnchor  // checked-in openingEntityAnchors
  ↓
BakedSitePlace            // 该 LocationId 的 baked 地点中心
  ↓
SiteArrivalFallback       // siteRegions arrival + deterministic offset
```

- `TryGetSiteBakedEnvelope`：由 `sitePlacements ∪ sitePlaces ∪ siteRegions` + margin 计算
  **baked physical envelope**（比 Strategic Footprint 小得多，能真正发现坐标系跑偏）；
- 被拒的 runtime anchor 打 `[ContinuousResidentAnchorRejected] Entity=… Site=… Anchor=… ResolvedSite=…`
  （每实体一次），**不删除 Domain entity**。

### 3. Normal NewGame presence 不再携带 legacy 派生位置（§4/§5/§8/§10）

`OpeningSpawnWorldPresenceApplier.ApplyPresence`：Continuous Outdoor Site 只写 `SetAtSite`（Site membership），
**不再**把 `spawn.LocalPosition` 经 `WorldSiteSpatialMapping` 转出来的 legacy 位置写进
`HasContinuousWorldPosition`。legacy 映射保留给 old save / legacy LocalMap / migration tooling。
`WorldPresence.HasContinuousWorldPosition` 从此刻起只表示「当前的 runtime 物理位置」。

### 4. 同一 materialize pass 内禁止两点重合（安全网）

`SeparateMaterializePoint`：量化 presentation 点去重，按 EntityId 确定性推开（≤32 次），
保证即使未来 content 漏了 anchor 也不会再出现「N 人压成一点」。

### 5. 非法起点不得启动 AI 寻路（§16）

- materialize 落点若不在 `CompositeWalkGrid`：先按确定性同心环在附近找可行格；
- 仍找不到 → 打 `[ContinuousMaterializationInvalidSpawn]` 并记入 invalid 集合，
  `HostNpcScheduleMover` 对该实体跳过日程寻路（不再无限 retry A*）。

### 6. Startup spatial invariant + census（§2/§3/§13/§18）

- `ContinuousOutdoorStartupPlanner.TryCheckPopulationSpatiallyValid`（与 Host 共用同一份逻辑）；
- opening expected entity 的 spatial validity =
  有 EntityView + View 落在 loaded chunks + 落在 CompositeWalkGrid +
  该位置解析回期望 Site（或落在该 Site 的 baked envelope 内）+ 起点未被判非法；
- 失败 → `Opening Site population spatially invalid: Expected=… SpatialValid=… SpatialInvalid=[…]`；
- 诊断显示 `OpeningPopulation: Expected=… Materialized=… Views=… SpatialValid=… SpatialInvalid=[…]`
  + `InvalidSpawn=n`；`SpatialInvalid` 非空时才展开逐实体行（Name / AnchorSource / PresenceWorld /
  ViewWorld / ViewChunk / InLoaded / InWalkGrid / ResolvedSite）。

### 7. 未改动（§13/§19/§20）

Surface streaming、Site placements、ControlCore、FactionFlag、Interior、AutoTravel、Save schema、
Schedule 规则、pathfinder 性能、Gateway retirement 全部保持原样；普通（非 opening）Site 的
`AtSite → SitePlace → fallback` population 路径未重写；同伴甲乙仍为背景 resident，不进 `PlayerParty`。

## 验证

| 项 | 结果 |
|---|---|
| 无头探针（真实 BaseGame + 真实 bootstrap） | 18 条 anchor；18 个落点**零重合**；全部 `BakedOpeningEntityAnchor`；全部落在荒村 baked envelope 内；chunk 落在玩家 loaded 邻域 |
| `ContinuousOutdoorOpeningSpatialTests`（A–G，headless NUnit 实际执行） | **7/7 PASS** |
| 既有 `ContinuousOutdoorOpeningPopulationTests`（A–G） | **7/7 PASS** |
| 鉴别力验证：用去掉 `openingEntityAnchors` 的临时内容包重跑 | **B / C / E 失败**（3 失败）→ 测试确实咬住新行为 |
| Host/Unity + Core + Data 编译（真实 2022.3.6f1 dll） | 0 error |
| 整个 EditMode 测试程序集编译 | 0 error（5 个既有无关 warning） |
| `git diff --check` | exit 0 |

## 未验证 / 待人工验收

- 真实 Play 路径（EntityView 相机内可见性、NPC 到点上班、`Move retry budget exhausted` 消失）
  只能由制作人在 Unity 内确认；本环境无 Unity Test Runner，用「真实内容 + 真实解析器」的无头执行作为等价证据。
- `ch01_reference` 的 6 名 chengzhen acceptance NPC 带 legacy `localPosition` 且**没有** `LocationId`
  → 按 §5 不再使用 legacy 坐标，落点走 SiteArrivalFallback（chengzhen arrival + deterministic offset）。
  它们不属于荒村 startup population，不影响本轮验收。
