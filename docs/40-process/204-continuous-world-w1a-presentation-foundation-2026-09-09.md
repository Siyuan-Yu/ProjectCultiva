# Continuous World W1A — Multi-Surface Presentation Foundation

> 状态：**ACCEPTED / SEALED**｜日期：2026-09-09
> 上级：[ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)、[203](203-continuous-2d-open-world-world-surface-direction-2026-09-09.md)  
> 范围：**Zero Gameplay Behavior Change**；不是 W1B，不代表 Continuous Outdoor World 已实现。

## 目标与切口

W1A 只解除 Host Presentation 对“同一时刻一张 Active LocalMap”的基础设施限制。现有 `WorldPosition` / Canonical Position、`LocalMapSession.ActiveMapLayoutId`、SurfaceExit、PlayerParty / Background / FormalArmy travel、Combat、Save 与 Content schema 均未改动。

审计结论是当前阻塞点在 Host：`HostDemoTileMap.Rebuild()` 会全量清图，并调用 `HostInteractSpots`、`HostMapObjectRegistry`、`HostFarmFieldRegistry` 的全局清空；`HostMoveController` 仍只绑定一个 `WalkGrid`。W1A 将前者拆为 owner-scoped incremental presentation，后者只增加纯 Core composition 工具，**不切换正式移动**。

## 实现

- 新增 transient `SurfacePresentationInstance`：instance key、source `MapLayoutDefinition`、普通 2D placement offset、GameObject root、Loaded state。它是 Presentation / Loading owner，绝不进入 Domain ID 或 Save。
- `HostDemoTileMap` 新增 `BuildLayoutInstance(instanceKey, layout, placementOffset)` 与 `RemoveLayoutInstance(instanceKey)`；同一 layout 可按不同普通平移同时建立两份 hierarchy。旧 `Rebuild(session)` 保持 legacy wrapper：清除全部 instance 后只建立 `legacy:active-localmap`。
- `HostInteractSpots`、`HostMapObjectRegistry`、`HostFarmFieldRegistry` 新增 `BeginOwnerBuild`、owner-aware register、`RemoveOwner`、`ClearAll`；旧 `Begin*Rebuild()` 仍是 single-map compatibility wrapper。删除 owner A 不影响 owner B；重复登记去重，Unity destroyed reference 在查询时清理。
- 新增纯 Core `WalkGridComposer`。输入为 `WalkGrid + presentation placement offset`；要求 CellSize 与共享 cell lattice 兼容；输出为 bounds union，未覆盖 cell blocked，overlap 任一 blocker 优先。仍复用 `GridPathfinder + WalkGrid`，没有接入 `HostMoveController`。

## 明确保留

- `LocalMapSession.ActiveMapLayoutId` 仍是当前正式 Gameplay / snapshot / materialization context；没有改成 loaded collection。
- `HostMoveController._walkGrid`、`_boundLocalMapId`、`_pathLocalMapIds` 和 LocalMap transition invalidation 完全保留。
- 没有加载第二张正式 Outdoor LocalMap 给玩家；没有无缝跨 Hex、SurfaceExit / ingress / egress / travel / combat / territory / save / JSON / HexWorld 改动。

## 验证

- Unity batch compile：Core / Data / Unity Host / Tests 编译 `ERROR_CS=0`。
- 定向 EditMode：`WalkGridComposerTests` + `SurfacePresentationRegistryTests`，**7 / 7 passed**。
- 全量 EditMode：1184 total、812 passed、352 failed；失败覆盖 Army / Battle 等非 W1A 领域，未作为 W1A 通过声明。是否为既有基线问题仍需在独立回归任务中归因。
- `git diff --check` 通过。

## 封板记录

- 制作人 Unity 人工 sanity acceptance：**通过**。
- Regression Attribution Gate：**PASS**。W1A 前 Full EditMode 为 `1177 total / 805 pass / 352 fail`，W1A 后为 `1184 total / 812 pass / 352 fail`；新增 7 项 W1A 定向测试全部通过，352 个失败均确认为既有基线，Confirmed W1A regression 为 0。
- 除非 W1B 暴露可明确归因的 blocker，W1A 基础设施不再修改。

## W1B 风险

W1A 已提供多 instance 和 registry ownership，但 W1B 仍须明确：正式邻接 Surface 的 source placement、loaded-set lifecycle 与 GameObject destroy 时机；EntityView / LocalMapVisibility / interaction 的 active-map ownership；以及把 composite grid 接入 Outdoor movement context 前如何保留现有 `_boundLocalMapId` invalidation。它们尚未实现，不应在 W1A 直接切换。
