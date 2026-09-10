# Continuous World W1D — Default Wilderness Continuous Surface Cutover

> 状态：**NOT ACCEPTED / SUPERSEDED**｜日期：2026-09-10
> 前置：[206](206-continuous-world-w1c-surface-chunk-grid-2026-09-09.md)（W1C sealed）、[ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)

## 正常 authority

普通 `base:hex_world_travel_mvp_30x15` Wilderness 在 canonical `WorldPosition` 命中 `base:surface_main_wilderness_v1` 时，自动进入 Continuous Surface；优先级为 Main Surface > W1B pair > legacy Wilderness。W1C `base:surface_w1c_wilderness_acceptance` 标记 `acceptanceOnly`，仅 debug action 可显式启用，normal resolver 永不把它作为候选。

Main Surface 使用 W1C 已验证的 `CellSize=0.028`、`Chunk=1.4×1.4`、radius-1/最多 3×3 incremental streaming。646 个明确 authoring-time chunks 覆盖 30×15 playable HexWorld 的 canonical bounds；这不是 runtime procedural generation、不是 Hex-to-Chunk 映射，也不代表整大陆都 materialized。

## Superseded Gateway route

本页原先的 `Continuous Wilderness ↔ WorldSite Gateway ↔ Legacy WorldSite LocalMap` 过渡路线已在两次制作人验收后判定 **NOT ACCEPTED / SUPERSEDED**。它在正常 Outdoor authority 已连续化后仍引入双向 `WorldPosition ↔ AtWorldSite`、Surface owner ↔ ActiveMapLayout 的生命周期，导致 Site context 泄漏、视图/镜头不稳定与不可逆入口。不得继续以此路线交付或修补。

后续实现真源为待新建的 Outdoor WorldSite Surface Migration V1 记录：所有当前 playable Outdoor WorldSite 直接成为 Main Continuous Surface 的 PhysicalRegion；Legacy LocalMap 仅保留 authoring/migration source 与旧存档兼容。

## Historical migration boundary

Wilderness 内部跨 Chunk/Hex 不生成 SurfaceExit。WorldSite 在迁入 Continuous geometry 前仍是 Legacy LocalMap，并通过 temporary `Continuous Wilderness ↔ WorldSite Gateway ↔ Legacy WorldSite LocalMap` 过渡：gateway 只取正式 `WorldSiteFootprintExitConnectionResolver` 的 `BoundaryContactWorld`，首次触及 footprint 即复用正式 ingress；不得普通走入 footprint。Site 出口若命中 Main coverage，Core 直接提交 `AtWorldPosition`/成员 presence 并清 Site focus，Host 直接 activate Main Surface，不经过 one-Hex Wilderness LocalMap；覆盖外继续旧路径。

Continuous activation 明确清理旧 Site local places/graybox/interactions，确保 party/follower view 并 frame camera。`HostMapGraybox` 只清自己的 overlay，不能触碰 chunk、entity view 或 gateway owner。Interior、Cave、Dungeon、Secret Realm、Manual Battle 仍走 legacy presentation。Loaded chunks、owner、composite grid 均 transient，不进入 Save。

## Deferred

鼠标点击 outer-surface egress、WorldSite continuous geometry、NPC/FormalArmy multi-chunk materialization、真实 Geography、Flight 与 World Event 均不在 W1D。
