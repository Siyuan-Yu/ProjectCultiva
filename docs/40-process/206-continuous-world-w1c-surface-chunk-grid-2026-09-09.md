# Continuous World W1C — First Real Surface Chunk Neighborhood

> 状态：IMPLEMENTING｜优先级：P0｜最后更新：2026-09-09
> 前置：[W1B](205-continuous-world-w1b-first-seamless-wilderness-pair-2026-09-09.md)（ACCEPTED / SEALED）、[ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)

## 决定与边界

W1C 建立独立的矩形 `SurfaceChunkCoord(X,Y)` 网格。Strategic Hex 仍只负责 Territory、FormalArmy、Background Travel、WorldSite footprint、战略地形和 WorldMap；它覆盖同一 `WorldPosition`，但不决定 chunk placement、加载或地图文件边界。

因此禁止 `SurfaceChunkCoord = HexCoord`、`one Hex = one SurfaceChunk` 与以 Hex neighbor 加载 LocalMap rectangle。一个 chunk 可以覆盖多个 Hex 的部分，一个 Hex 可以被多个 chunks 覆盖；两类边界可彼此横穿。

`OutdoorWorldSurfaceDefinition` / `OutdoorSurfaceChunkDefinition` 是 W1C 的最小基础内容结构。`SourceMapLayoutId` 仅是 legacy authored-source bridge：现有 tile、blocker、object 与 decoration 可以暂借用，绝不表示未来 chunk 永远等于 LocalMap。W1C 不增加 Save `WorldSpaceId`。

## Provisional metric

首版统一采用 `CellSize=0.028 WorldPosition units / source cell`、`ChunkWidth=1.4`、`ChunkHeight=1.4`。50×50 fallback MapLayout 因而恰好填满一个 `1.4×1.4` SurfaceChunk；Unity presentation 统一按约 `35.714 presentation units / WorldPosition unit` 映射。这个比例让当前 `hexSize=1` 的 strategic grid 真正横穿多个 Chunk，同时所有 chunk 均使用同一比例；它仍是 **PROVISIONAL / TUNABLE** prototype 参数，而非 ADR 永久契约。

## Runtime contract

### 制作人授权的空间修复（2026-09-09，仍为 W1C）

World→Hex 统一为 fractional axial / cube rounding / Odd-R inverse；forward 与拓扑索引不变。Road 只表示道路，不能作为 Ground gate。新增纯 Core `ContinuousSurfacePrototypeGroundLegality`，只作为迁移原型的安全防线，拒绝缺失格、Water 与不可通行格；非法候选在写 Canonical 之前拒绝，Host 回到最近合法位置并停止表现路径，保留 TravelPlan。跟随者使用同一检查。

5×5 authored coverage 与 radius-1 runtime 保持；WorldMap debug 绘制 authored chunk rect union 的外边，不以 Hex 染色。真实越出 coverage 后恢复当前位置 legacy Wilderness。制作人验收前只做定向 pure tests、离线 compile sanity 与 diff check，不运行全量或 Unity 人工测试。

- `OutdoorSurfaceCoordinateMapper` 集中处理 WorldPosition↔presentation↔chunk↔chunk-local；WorldPosition 是唯一物理 authority，不引入 SurfacePosition Domain authority。
- 玩家 chunk 的 radius-1 方形 neighborhood 是最多 3×3。加载采用 deterministic owner `surface:<surfaceId>:chunk:<x>:<y>` 的 add/remove diff，禁止 `ClearAll → rebuild 9`。
- chunk seam 与 strategic Hex seam 都不是 `SurfaceExit`、exit zone 或 gameplay transition。前者只影响 streaming，后者只由 WorldPosition 的稳定 Hex 派生更新战略 context。
- loaded set 的 composite WalkGrid 只覆盖已加载 chunks；物理 rectangle overlap 必须显式 diagnostic/error，不能静默叠加。
- 离开 surface coverage 时才做一次 continuous→legacy handoff，保留 AutoTravel HexPath、destination 与 intent；离开时清所有 surface owners、composite grid、loaded index 和 movement context，最终 LoadedChunkCount=0。

## W1C acceptance scope

目标 region 为 5×5 chunks（坐标 -2..2 × -2..2；`OriginWorld=(0.1660254,0.8)`，运行时 radius-1 neighborhood 最多同时加载 3×3），围绕原 W1B Wilderness `(0,1)/(1,1)` 区域，并在实际 placement 下横跨至少四个 Strategic Hex。验收必须能看到“换 chunk 但 Hex 不变”与“换 Hex 但 chunk 不变”两种情况；这两项不成立即 W1C 失败。

本轮只含 Static Surface、PlayerParty/followers、streaming、WorldPosition、strategic context 与 AutoTravel。NPC 预 materialize、FormalArmy、combat、WorldSite、Bake tool、flight 和 World Event 均不在范围内。

## 本轮复验入口与验证

LevelTester / Cheat Tools → Diagnostics：勾选「WorldMap 显示 W1C authored coverage 外框」。WorldMap 青色线是实际 authored rectangles 的 union 外边（含缺口），不是道路、Hex 或 loaded 3×3 范围。框外 Road 继续延伸不代表 W1C 缺失加载；仅真实跨出后回 legacy Wilderness。

制作人分别复验：① 右上等斜向移动时 canonical/derived/committed/marker 同方向；② Road=false 且 Passable=true 允许；③ Water/不可通行/缺格拒绝并停在上一个安全点，不回 Hex center，不清 TravelPlan；④ 真实 outer edge 交接，内部 chunk/Hex seam 不切房间。

离线 Core/Data/Host/Tests compile sanity 与 targeted pure tests 已执行；测试涵盖正负奇偶行中心、六 shared edges、六 corners 周围 polygon half-plane、四 diagonal directions、mapper +Y、commit hysteresis/self-heal/Legacy 不变、合法性与拒绝前后的 canonical/presence/travel 状态。没有运行 Full EditMode、PlayMode 或 Unity 人工测试；W1C 尚未标记 ACCEPTED。

## Outer Boundary Handoff Repair（2026-09-09，等待复验）

`OutdoorSurfaceBoundaryEgressResolver` 以 authored chunk world-rect union 查询 `Inside / CrossingOuterBoundary / Outside`，并沿当前 canonical→desired ray 求 first boundary contact 与集中配置的 just-outside epsilon。它不读取 loaded set 或 WalkGrid，因此“3×3 外但 5×5 内”继续 streaming，“5×5 外”才交接。

WASD 在 CompositeWalkGrid 拒绝前调用统一 `TryHandoffContinuousSurfaceToLegacy`；LocalVisible AutoTravel 先走向同一 boundary contact，近到可达阈值后调用它，保留 Domain travel plan。交接先用精确 WorldToHex 和 prototype ground guard 提交 just-outside canonical/presence，再清 W1C owner/grid/context/path、展开 destination legacy Wilderness；Water、不可通行或缺失 destination 显示 `BlockedByStrategicGround`，允许留在边缘。Diagnostics 增加 `SurfaceEgressStatus` 及 `NextOutsideHex/Terrain/Passable`。
