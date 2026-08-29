# Phase 2C：Surface Edge Transition 与 Canonical Exit Trigger Zone（2026-08-26）

> **⚠️ 2026-08-30 · 「未改契约边界：…PresenceHex / WorldSite Aggregation」部分被 [ADR-0027](43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED（Site 内改为 Spatial Mapping + DerivedPresenceHex）。本页 Exit Trigger Zone 几何规则保持。**

> 状态：**Phase 2C 已人工验收封板（2026-08-26，含 Follower LocalMap Transition Bugfix）**｜最后更新：2026-08-26  
> 产品契约真源：[2K §5.8](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026 #12](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)  
> **不写飞书同步**；本文件记录 Runtime／Presentation 落地要点。

---

## 1. 本轮范围

| 项 | 状态 |
|----|------|
| Continuous WorldPosition 真源／到达地点回滚修复 | ✅ 已封板 |
| Surface LocalMap Edge → Neighbor Hex／Site Exit | ✅ 已封板 |
| Edge Ping-Pong Guard（Disarm／Rearm／TransitionInProgress） | ✅ 已封板 |
| Ordinary Hex Actual Connections + 方向投影 + Overlap Resolution | ✅ 已封板 |
| WorldSite Full-Footprint Boundary Connections + 方向投影 | ✅ 已封板 |
| Canonical Surface Exit Trigger Geometry + Presentation | ✅ 已封板 |
| PreciseWorldDestination | 仍禁止 |
| Background／FormalArmy continuous | Deferred |

**未改契约边界：** AutoTravel、WorldMap Marker、PresenceHex、WorldSite Aggregation、No PreciseWorldDestination。

---

## 2. Canonical Exit Trigger Geometry（正式规则）

### 2.1 Geometry ≠ Availability

| 概念 | 决定因素 | 可变性 |
|------|----------|--------|
| **Canonical Geometry** | LocalMap PlayableBounds + `ExitTriggerDepth` + Hex 方向划分 | 同一 LocalMap **永远相同** |
| **Runtime Availability** | Neighbor／Footprint／Terrain passable／World edge | 只决定 Enabled／Disabled |
| **Visible Zones** | Geometry ∩ Availability | 显隐可变；**Bounds 不可变** |

禁止：用角色 LocalPosition、EntryDirection、WorldPosition、CurrentHex、Disarm／Rearm 状态去改 Zone 大小或位置。

### 2.2 ExitTriggerDepth

- **含义：** 从 PlayableBoundary **向内**延伸的 Trigger 深度（Gameplay，非纯美术）。
- **存放：**
  - MapLayout JSON 字段 `exitTriggerDepth`（可选）
  - Runtime：`LocalMapSession.ExitTriggerDepth`（进图时由 Host 从 MapLayout 同步）
  - 默认：`1.25` world units（`SurfaceExitZoneCalculator.DefaultExitTriggerDepth`）
- **禁止：** 按地图半宽比例膨胀成「宽边提示带」。

### 2.3 Detection 语义

```text
角色已位于某 Enabled Exit Trigger Zone
+ 继续产生对应向外 movement intent
→ 尝试 Surface Edge Transition
```

- 站在 Zone **外**：不触发  
- **刚踏入** Zone：不自动触发  
- 在 Zone 内继续向外（或出 playable bounds）：触发  

Detection 与 Presentation 共用：

- `IsInExitTriggerBand` + 方向分类（`PointBelongsToDirection`）
- 覆盖矩形：`AppendCoverageRects`（沿边窄条，无视觉 padding）

### 2.4 Surface / Interior

| LocalMap | Exit Zone / Hex Edge Transition |
|----------|----------------------------------|
| WorldSite Surface | 启用（Geometry 固定；Availability 按 Site 出站） |
| Wilderness Surface | 启用（Availability 按 CurrentHex 邻格） |
| Interior（Active ≠ Overworld） | **不显示、不启用**；继续 Door／Leave Interior |

---

## 3. 关键代码入口（实现索引）

| 职责 | 位置 |
|------|------|
| Canonical Geometry / Coverage / Visible | `SurfaceExitZoneCalculator` |
| Exit Band / Exit Trigger Intent | `WildernessLocalWorldProjection` |
| 合法性（只读 Availability） | `PlayerPartyWildernessTransitionService.TryEvaluateSurfaceExitLegality` |
| Ping-Pong Gate | `PlayerPartySurfaceEdgeGate` |
| Host Detection | `HostPlayerPartyController`（`TryResolveExitTriggerIntent`） |
| Presentation | `HostSurfaceExitZonePresenter` |
| Depth 同步 | `PlayableHostBootstrap.SyncExitTriggerDepthFromActiveMap` |
| EditMode | `PlayerPartyContinuousWorldPhase2CTests`（`EXITZONE_*`／`EDGE_*`） |

---

## 4. 已知人工验收要点

1. 青石荒村首次进入：边缘应有**窄**半透明 Exit Zone  
2. 出村再进村：Geometry（位置／宽度）与首次 **完全一致**  
3. 图内移动：Zone **不**随角色移动变形  
4. Zone 外不切图；进 Zone 不自动切；Zone 内继续向外才切  
5. Interior：无 Surface Exit Zone  

---

## 5. 与 Phase 2C 封板关系

本文件记录的 Edge Transition + Exit Zone + Actual Connections 为 Phase 2C 完整竖切。  
**Phase 2C 已于 2026-08-26 人工验收封板**（Continuous Player World Movement／Ordinary Hex Actual Connections／WorldSite Full-Footprint Boundary Connections）。
