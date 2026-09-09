# Continuous World W1B — First Seamless Wilderness Pair

> 状态：**ACCEPTED / SEALED**｜日期：2026-09-09
> 前置：[204](204-continuous-world-w1a-presentation-foundation-2026-09-09.md)（W1A sealed）、[ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)

## 范围

W1B 是第一个可见 vertical slice：只加载一对正式、相邻、Ground-passable、非 WorldSite 的 Wilderness Hex。它不是全大陆连续 Surface，也不改变 HexWorld、Save schema、LocalMapSession 或旧 SurfaceExit 对其他边界的权威。

## 选择与放置

验收 pair 为 `base:hex_world_travel_mvp_30x15` 的 `Hex (0,1) ↔ Hex (1,1)`：两格都是 Ground-passable、Plain、非 WorldSite，并均解析为 `base:map_wilderness_plain_fallback`。运行时代码不按这组坐标或 map id 分支；它从当前可用的正式 `SurfaceExitConnection` 中选择满足同一约束的一条 cardinal Wilderness→Wilderness 边。

第二张 layout 使用无缩放、无旋转的整数 cell translation，与第一张的 shared edge 对齐；仅接受相同 `CellSize`、尺寸和 shared lattice 的 pair，其他边继续旧 SurfaceExit。此 placement 是 Legacy LocalMap→Future Continuous Surface 的迁移桥接，不是最终 authored SurfaceChunk geometry。

## Runtime lifetime

`ContinuousWildernessLoadedSet` 是 Host transient owner，持有两份 W1A `SurfacePresentationInstance`、一个 internal seam、primary logical context 与 composite `WalkGrid`。它用两个独立 owner 建立/删除 A、B；不写入 Domain identity、Character identity 或 Save。内部 edge 从 `HostSurfaceExitZonePresenter` 的可用 exit 列表移除，故不再作为 SurfaceExit；其他边仍保留 legacy exit。

Core `TryCommitSeamlessWildernessCrossing` 只验证正式相邻、passable Wilderness connection，并把 `CurrentHex`、PartyWorld/LocalMap logical primary context 和 traveling members 提交到 `BoundaryContactWorld`。它明确不调用 legacy `ComputeCrossEdgeWorldPosition` inward spawn，也不要求 Host Clear/Rebuild 或 EntityView 重建。

## W1B Repair（2026-09-09）

- **确定性验收 pair**：`ContinuousWildernessPairSelector` 从正式 SurfaceExit zones 选出 horizontal Wilderness pair；进入 A 或 B 时 `TryActivateAcceptancePair` 激活，并输出 `ActivePairDiagnostic`（A Hex / B Hex / MapLayout / direction）。
- **Presentation ↔ SurfaceLocal**：`ContinuousWildernessLoadedSet` 提供 `PresentationToSurfaceLocal` / `SurfaceLocalToPresentation` / `GetPlacementOffset`；inactive 时 identity passthrough。
- **Primary commit 无 unload**：`WorldTravelService.ApplyWildernessPrimaryContextWithoutUnload` 替代 internal seam 上的 `EnterWildernessLocalMap` → `ApplyLocalMapSessionFromFocus` unload 副作用。
- **AutoTravel preserving commit**：`TryCommitSeamlessWildernessCrossingPreservingLocalVisibleAutoTravel` 保留 HexPath / ExecutionMode / Destination 并推进 Segment。
- **生命周期**：`DeactivateToLegacy` 由 `UnloadActiveLocalMapPresentation` / 离开 pair 的 `ApplyPartyWorldSitePresentation` / legacy outer crossing 前调用；`HostMapGraybox.RebuildOverlaysOnly` 避免 W1B active 时 `HostDemoTileMap.Clear()`。
- **WorldMap → LocalVisible AutoTravel**：internal neighbour 走 composite WalkGrid + seam approach + preserving commit，不 `ExpandLocalMapForCurrentPartyWorld`；temporary unreachable 不再 `CancelTravel` 整条路线。

## Deferred

## 封板记录

- 制作人 Unity 人工验收通过：首个双 Wilderness Surface simultaneous presentation 成功。
- A↔B 为真实连续步行；没有 room transition。pair 外仍保留明确允许的 legacy transition。
- WorldMap 多 Hex AutoTravel 可穿过 seamless pair 与 legacy transition 的混合路径继续执行。
- AutoTravel preservation、Presentation↔SurfaceLocal offset 与 LoadedSet deterministic cleanup 修复完成。
- W1B 只保留为迁移 proof；不得将 exactly-one pair 扩展为 Current Hex + 六个邻居 LocalMap。

邻接 B 在成为 primary 前的普通 Background NPC materialization、所有 WorldSite/城市连续化、多 chunk streaming、combat/projectile 跨 unloaded boundary、flight、world event 与 Save persistence 全部 deferred。
