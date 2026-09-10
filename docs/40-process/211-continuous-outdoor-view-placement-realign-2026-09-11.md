# 211 — Continuous Outdoor View Placement Realign（materialize 权威落点 → 已存在 view 的对齐）

日期：2026-09-11
状态：已实现（等待制作人复验；未提交）

## 症状（制作人复验）

- Normal NewGame 后能进入 Main Continuous Outdoor，Surface / buildings / places 正常。
- 诊断：`Authority=ContinuousOutdoorSurface`、`AcceptanceOnly=false`、
  `Expected=17 Materialized=17 Views=17 SpatialValid=1 SpatialInvalid=[16 人] RealignedViews=0`。
- 画面：只有主角 + 荒村主管可见；其余 opening resident、同伴甲、同伴乙看不到。
- 上一轮的 spatial invariant **正确地**把问题暴露为
  `[ContinuousStartupInvariantFailure] Opening Site population spatially invalid`。

关键读数（制作人日志）：

```
同伴甲 AnchorSource=BakedOpeningEntityAnchor PresenceWorld=- ViewWorld=-0.0098,-0.1680 ViewChunk=(0,0)
       InLoaded=False InWalkGrid=False ResolvedSite= Presence=AtSite(base:site_huangcun)
```

`PresenceWorld=-` 是 §5 的预期结果（Continuous Site 的 opening presence 只表达 Site membership）；
但 `ViewWorld` 落在 presentation 原点附近 → view 的呈现位置与权威落点不一致。

## 根因（决定性证据，非推断）

### 1) 三项数值互相印证

- surface `cellSize=0.028` → `presentationUnitsPerWorldUnit=35.714`；`WorldToPresentation(5.20698,10.165) = (185.96,363.04)`。
- 日志 `ViewWorld=-0.0098,-0.1680` → presentation `(-0.35,-6.0)`。
- legacy `WorldRegion` 中 `base:loc_ref_labor_yard` 的 presentation 是 `(0.5,-6.0)`；
  `EntityViewSpawner.ResolvePresentationPosition` 的 legacy 分支叠加 stack 偏移
  `ox=(stack%3)*0.85-0.85`、`oy=(stack/3)*0.85`：

| 实体 | 日志 ViewWorld | ×(1/0.028) | 该 location 的 stack | legacy 公式 |
|---|---|---|---|---|
| 同伴甲 | `-0.0098,-0.1680` | `(-0.35,-6.0)` | stack0 | `(-0.35,-6.0)` ✓ |
| 同伴乙 | `0.0140,-0.1680` | `(0.5,-6.0)` | stack1 | `(0.5,-6.0)` ✓ |
| 巡卫乙 | `-0.0098,-0.1442` | `(-0.35,-5.15)` | stack3 | `(-0.35,-5.15)` ✓ |

即：16 个 view 全部停在 **legacy 地点回退位置** 的 0.85 堆叠网格上。

### 2) 机制

1. `PlayableHostBootstrap.TryInitialize` 在 Continuous activation **之前** 调用 `entityViewSpawner.Rebuild(_session)`。
   此时尚未 materialize，`ResolvePresentationPosition` 只能走 legacy `WorldRegion` 地点 + stack 偏移。
2. `ActivateSurface → UpdateNeighborhood → ReconcileOutdoorEntityMaterialization` 随后计算出**权威**落点并写入
   `EntityLocationComponent.PresentationOverride`。
3. 但 `SpawnMissingVisibleViews` 只创建「缺失」view，**绝不搬动已存在的 view**；continuous 路径
   也从不调用 `SyncLocations`（只有 legacy LocalMap 分支调用）→ view 永久滞留。
4. 主管（Hex FormalArmy 成员）pre-activation 因可见性门禁没有 view，其 view 是在 materialize **之后**
   创建的 → 位置正确，这正是「只有主管可见」的原因。

### 3) 为什么数量全对却看不见

materialize / census 路径看的是 Domain 集合（materialized 集合、EntityView 存在性），
view 的 transform 位置不在其中 —— 于是出现 `Views=17` 但 16 人在镜头外的组合。
这也是上一轮 spatial invariant 的价值：它把「view 到底在哪」变成可测条件。

## 修复

- `Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs`
  materialize pass 收尾新增 `RealignMaterializedViewPlacements()`：同一 pass 内把已存在的 view
  对齐到 `PresentationOverride`（唯一权威落点）。诊断新增 `RealignedViewCount`。
- `Assets/Scripts/Unity/Host/ContinuousMaterializePlacementSync.cs`（新增）
  纯函数策略 `ShouldRealign(...)`：
  - **跳过** PlayerParty 成员（由 `AlignPartyPresentationToWorld` 负责）；
  - **跳过** 正在被 movement 驱动的实体（§15：不得被 authored anchor 重置，`HostMoveController.IsMoving`）；
  - 无权威落点不猜位置；
  - 容差 `PositionEpsilon=0.01`（presentation 单位）内不产生写入。
- `Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`
  opening population 诊断追加 `RealignedViews=n`。

执行时机仅为 reconcile（startup barrier／chunk 邻域变化／scope change），**不进入 per-tick 路径**
（普通 tick 不增长 `EntityReconcileRevision`）。

## 验证

| 手段 | 结果 |
|---|---|
| 新增 `ContinuousMaterializePlacementSyncTests` H1–H7（真实内容 + 策略纯函数，headless NUnit 实际执行） | **7/7 通过** |
| opening spatial（A–G）+ opening population（A–G）+ performance + startup transaction 一并重跑 | **42/42 通过** |
| 鉴别力验证（把 `PositionEpsilon` 中和成 1e6） | H1 **立即失败** → 测试咬住真实行为 |
| Host 全链编译（Core+Data+Unity，真实 2022.3.6f1 dll） | **0 error**（1 个既有无关 warning：`HostWorldMapPanel.cs:795` CS0162） |
| 整个 EditMode 测试程序集编译 | **0 error** |
| `git diff --check` | exit 0 |

H1 的实测数值（真实内容）：legacy `(-0.35,-6.0)` vs 权威 baked anchor presentation `(185.96,363.04)`，
距离 **413.4 presentation 单位**；H6 断言全部 opening anchor 落在「玩家 chunk ±1」邻域内（保证修复后
`ViewInsideLoadedChunks` 成立）；H7 断言共享同一 `LocationId` 的 12+ 个实体**只能靠 baked anchor 区分**
（即「对齐」这一步是必需而非可选）。

## 未验证 / 残余风险（诚实标注）

- **Play 路径未自测**：本环境无 Unity Test Runner，`EntityView` 的真实 transform 与相机取景只能由制作人
  在 Unity 内确认。headless 证据 = 真实内容 + 真实解析器/策略 + 真实 legacy 坐标公式的数值一致。
- **残余风险**：若某实体的 view 位置由 movement 之外的 per-frame 驱动者写入而 `PresentationOverride`
  已过期，本 pass 会在下次 reconcile 把它对齐到 override（`IsMoving` 之外的驱动者无法从 Host 侧识别）。
  当前内容不触发（16 人全部由 materialize 写 override）；若发生，census 会直接报出该实体而不是静默。
- 未改动：Surface streaming／Site placements／ControlCore／FactionFlag／Interior／AutoTravel／
  Save schema／Schedule 规则／pathfinder 性能／Gateway retirement（严格遵守本轮范围）。

## 验收清单（制作人）

1. 直接 New Game（不点任何 debug 按钮）→ 主角 + 同伴甲乙 + 荒村 17 人应在各自村内位置可见；主管仍在原位。
2. 诊断应显示
   `OpeningPopulation: Expected=17 Materialized=17 Views=17 SpatialValid=17 SpatialInvalid=[] Barrier=applied InvalidSpawn=0 RealignedViews=16`（`RealignedViews` 首次启动非 0 属**预期**：正说明有 16 个 activation 前建好的 view 被搬到了正确位置）。
3. 05:00 起 NPC 能正常从村内位置走去工作；`Move retry budget exhausted` 应消失。
