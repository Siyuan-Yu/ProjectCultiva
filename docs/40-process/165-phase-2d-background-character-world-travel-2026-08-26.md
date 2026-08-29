# Phase 2D：Background Character World Travel Core（2026-08-26 → 封板 2026-08-27）

> **⚠️ 2026-08-30 · 「WorldMap 投影 = PresenceHex」表述被 [ADR-0027](43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED（改 CanonicalWorldSurfacePosition 派生）。本页 Background Travel 契约保持。**

> 状态：**人工验收封板**｜最后更新：2026-08-27  
> 产品契约真源：[2K §5 Background Character](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`（复用 `PlayableHostBootstrap`）

---

## 0. 内部实施顺序（Phase 2D 子阶段）

| 子阶段 | 内容 | 状态 |
|--------|------|------|
| **2D-A** Background Simulation Foundation | `BackgroundSimulationScheduler`：centralized low-frequency tick、bucket stagger、`elapsedWorldTime` 距离预算 | ✅ |
| **2D-B** Background Character Travel | Route planning（一次性 A*）+ 连续 WorldPosition 推进 + Arrival | ✅ |
| **2D-C** Save / Load + Authority | `WorldPresence` 真源、Authority 互斥、中途旅行快照 | ✅ |
| **2D-D** Debug / Acceptance | F12 DEBUG 面板 + EditMode 自动测试 + 500 角色结构基准 | ✅ |
| **2D-E** Loaded LocalMap Materialization | 玩家已 Loaded 的目标 LocalMap 内 Runtime Arrival Materialize（Wilderness + Site Ingress） | ✅ |
| **2D-F** Site Departure 语义 | WorldSite 出发必须有真实 Travel 距离；禁止 BeginTravel 同步 instant arrival | ✅ |
| **2D-G** Destination Canonicalization | Travel To Hex 命中 Footprint → canonicalize 为 `AtWorldSite`；Travel To Site 真源 `WorldSiteId` | ✅ |

**架构约束（2D-A）**

- Continuous World Time ≠ 全角色 Full Realtime Simulation
- Loaded LocalMap → Full Realtime；Background Traveling → centralized scheduler
- 禁止每 Character `MonoBehaviour.Update()` / 每帧全量遍历 + 每帧 A*
- Pause → `elapsedWorldTime = 0`（Simulation tick 不推进）
- 2x / 5x → Host 自动步进更多 world tick（增大 elapsed world time），非单次 tick 内乘倍率
- Staggered bucket（16）+ `currentWorldTick - lastProcessedWorldTick` 保证低频处理不降低实际移动速度

---

## 1. 范围

| 项 | 状态 |
|----|------|
| Background Travel Intent → Route → Execution → Arrival | ✅ |
| WorldLocation（WorldPresence）与 TravelState 分离 | ✅ |
| 复用 HexPathfinder + 连续推进距离预算 | ✅ |
| WorldSite Full-Footprint 边界出口（非 PresenceHex 作唯一入口） | ✅ |
| WorldSite 出发：FootprintCenter → BoundaryEntry → ExitHex 真实 Travel | ✅ |
| Loaded LocalMap Runtime Arrival Materialize | ✅ |
| Destination Canonicalization（Hex-in-Footprint → AtWorldSite） | ✅ |
| Save/Load 中途旅行恢复 | ✅ |
| F12 DEBUG 面板 | ✅ |
| Development-only BGTRAVEL TRACE | ✅ |
| Autonomous AI / Encounter / Activity | Deferred |

---

## 2. 核心类型

| 类型 | 职责 |
|------|------|
| `BackgroundSimulationScheduler` | Phase 2D-A 统一低频后台调度（Travel 首个消费者） |
| `BackgroundCharacterTravelService` | 开始/取消/推进/到达；Site Departure；Destination Canonicalization |
| `BackgroundCharacterTravelBoard` / `BackgroundCharacterTravelMotion` | 每角色 route 状态；SiteDeparturePending 虚拟段 |
| `BackgroundCharacterSiteDepartureResolver` | Footprint 合法 Boundary Connection 解析 |
| `LoadedDestinationArrivalMaterializer` | Initial Load + Runtime Arrival 共用 Materialize 核心 |
| `BackgroundCharacterWildernessLocalMapMaterialization` | 进入 Wilderness Hex 时 Notify + Materialize 触发 |
| `BackgroundTravelArrivalContext` | Site Arrival Ingress 上下文（非 WorldLocation 真源） |
| `LoadedLocalMapBelongingQuery` | 角色 Hex 是否属于当前 Loaded LocalMap |
| `WorldAgentPresence` / `WorldPresenceBoard` | `AtWorldSite` / `AtWorldPosition` 连续真源 |
| `CharacterWorldMovementAuthorityQuery` | Party / Army / Local / Background 互斥 |
| `BackgroundBgTravelFullTrace` + `BackgroundTravelTraceSink` | Development-only 统一 Trace（Core 不引用 UnityEngine） |
| `HostBackgroundTravelDebugPanel` | F12 验收面板 |

---

## 3. Destination Canonicalization（正式规则 · 2026-08-27 审计封板）

### 3.1 Travel To WorldSite

- **Destination 真源：** `WorldSiteId`（`BeginTravelToWorldSite` 不再经 `AnchorHex` 取入口）
- **路线规划：** 从起点到目标 Site 时，使用 **整个 WorldSite Footprint 的合法 Boundary Connection**；`ResolveDeterministicSiteApproachHex` 在 Footprint 全格上选最近可达 approach hex
- **禁止：** 把 `PresenceHex` 当作 Site 唯一物理入口
- **Arrival 后：** `WorldLocation = AtWorldSite(siteId)`
- **需要世界 Hex 时：** 统一投影为 `site.PresenceHex`（≠ `AnchorHex`）

### 3.2 Travel To Hex（Wilderness）

- 目标 Hex **不属于**任何 WorldSite Footprint
- 正常 `TargetHex` → 最终 `AtWorldPosition(canonical Hex center)`

### 3.3 Travel To Hex（命中 WorldSite Footprint）

- **必须** canonicalize 为 `TargetWorldSite(siteId)`
- **不允许**最终保持 `AtWorldPosition` 在 WorldSite Footprint 内
- **Arrival 后：** `AtWorldSite(siteId)`；CurrentHex / WorldMap 投影 = `PresenceHex`
- 实现：`TryCanonicalizeFootprintHexDestination`（`BeginTravel` + `FinishArrival` + `BackgroundTravelArrivalContext.TryFromMotion`）

### 3.4 AnchorHex

- **不参与**上述位置代理；继续只负责 Site Presentation
- Character world projection 一律 `PresenceHex`

### 3.5 Single-Hex / Multi-Hex WorldSite

- 完全相同规则（均走 `EnumerateFootprintHexes()`）

### 3.6 Debug Travel To Hex

- 输入 WorldSite Footprint Hex 时，F12 面板显示：  
  `Requested Hex → Resolved WorldSite(显示名)`  
- Trace 记录：`RequestedHex→ResolvedWorldSite`

### 3.7 已知例外（未纳入 2D 范围）

- **PlayerParty** 若绕过 `WorldMapPartyTravelCommand` 直调 `PlayerPartyHexTravelService.BeginTravel(hex)` 且 hex 在 Footprint 内，仍可能落到 `AtWorldPosition`（UI 点击路径已通过 Command canonicalize）

---

## 4. WorldSite 出发语义（2D-F）

**问题（已修复）：** 旧代码在 `BeginTravel` 同步 `SetAtWorldPosition(boundaryEntry)` + 即时 Materialize，等于 **instant arrival**，Adjacent 目标无真实 Travel 距离。

**正式行为：**

1. `BeginSiteDepartureTravel`：开局保持 `AtWorldSite`；虚拟坐标从 **FootprintHex.Center → BoundaryEntry**
2. Scheduler 跨过边界时才 `CommitSiteDepartureBoundaryCrossing` → `AtWorldPosition` + `NotifyEnteredWorldHex`
3. 路径始终 `[departureFootprintHex, exitHex, ...]`（`TryBuildPathLeavingSite`）
4. **禁止** BeginTravel 同步 Materialize

**验收变化：** Site 出发到相邻 Wilderness Hex 需 **Advance Ticks**，不再一键 instant complete。

---

## 5. Loaded LocalMap Materialization（2D-E）

当玩家（Active Party）已 Loaded 某 LocalMap，Background Character 旅行到达该 Map 所属 Hex / Site 时：

| 场景 | 行为 |
|------|------|
| **Wilderness Hex** | `NotifyEnteredWorldHex` → `TryMaterializeCharacterIntoLoadedLocalMap`（Ingress 投影） |
| **WorldSite Arrival** | `FinishArrival` → `SetAtSite` + Site Ingress Materialize（`BackgroundTravelArrivalContext` 匹配 Boundary Connection） |
| **Initial Load** | `MaterializeEligibleWildernessCharactersOnLocalMap(InitialLoad)` — 已在 Loaded Map 内且符合 Belonging 的 Background 角色 |

**Belonging：** `LoadedLocalMapBelongingQuery` + `LoadedLocalMapBelongingExplain`（Development Trace 带 Reason）

**Host 集成：** `PlayableHostBootstrap.FlushLoadedDestinationArrivals()` — Travel Advance / Debug 操作后刷新 Presentation

---

## 6. 开局与同伴默认位置（Bootstrap 修复）

**问题：** `HexStrategicSessionBootstrap.CaptureTravelingMembers` 曾捕获全部 character spawn；Active 到荒野 Hex 时 `ApplyTravelingMembersAtHex` 把同伴也拉到 Active 的 Hex。

**修复：**

- 所有开局角色仍 `SetAtSite(荒村)`
- `CaptureTravelingMembers` **仅**首个 character spawn（主控）
- Content：`travel_mvp_hex_world_30x15.json` 荒村 `PresenceHex = (4,7)`

---

## 7. F12 DEBUG 面板

- 选中 Background Character → Travel To Site / Travel To Hex / Cancel / Advance Ticks
- `AtWorldSite` 时显示 `PresenceHex`（非误导性 residual `CurrentHex`）
- Travel To Hex 命中 Footprint 显示 canonicalize 解析结果
- `debugOverrideLocalOccupant: true` 用于验收 LocalMap 占用冲突

---

## 8. Development Trace

- 统一前缀：`BGTRAVEL TRACE #N`
- 段：Intent / Route / LocationCommit / SiteDeparture / RuntimeMap / Materialize
- Core 经 `BackgroundTravelTraceSink` 注入 Host `Debug.Log`（`XianXia.Core` 无 UnityEngine 引用）

---

## 9. EditMode 测试

| 测试文件 | 覆盖 |
|----------|------|
| `BackgroundCharacterWorldTravelPhase2DTests` | Travel 核心、Authority、Save/Load、Site Departure、Footprint Canonicalization |
| `BackgroundLoadedDestinationArrivalTests` | Site Ingress Materialize、Connection 匹配 |
| `BackgroundWildernessLocalMapMaterializationTests` | Wilderness Runtime Arrival、Site Departure + AdvanceUntilArrival |

---

## 10. 人工验收清单（已通过 · 2026-08-27）

1. Active + 同伴 → Stop Follow → F12 → **Travel To Site**（青石镇）→ Advance Ticks → 确认 `AtWorldSite` + `PresenceHex`
2. F12 **Travel To Hex** 输入 Footprint 内格 → 日志 `→ Resolved WorldSite` → 到达 `AtWorldSite`（非 Footprint 内 `AtWorldPosition`）
3. F12 **Travel To Hex** 输入纯 Wilderness → 到达 `AtWorldPosition(canonical center)`
4. 从 WorldSite 出发到相邻已 Loaded Wilderness → **需 Advance Ticks** 才 Materialize（非 instant）
5. 中途 Save/Load → 位置与 Destination 延续
6. 王尘 Join Party → Background Travel 立即停止
7. `AtWorldSite` 时 Debug 面板显示 `PresenceHex`

**Deferred：** Autonomous AI 驱动 Background Travel、Background Combat UX、PlayerParty 直调 API Footprint canonicalize

---

## 11. 相关 ADR / 文档

- [2K Background Character](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)
- [164 Phase 2C Surface Exit](./164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md) — Boundary Connection 几何真源
- [41-roadmap](./41-roadmap.md)
