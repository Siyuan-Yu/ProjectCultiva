# Continuous World — Outdoor WorldSite Surface Migration V1

> 状态：**OUTDOOR WORLDSITE CONTINUOUS MIGRATION RUNTIME-CORRECT / PENDING PRODUCER ACCEPTANCE**｜优先级：P0｜最后更新：2026-09-10
> 前置：[W1C](206-continuous-world-w1c-surface-chunk-grid-2026-09-09.md)（ACCEPTED / SEALED）｜替代：[W1D Gateway route](207-continuous-world-w1d-default-wilderness-cutover-2026-09-10.md)（NOT ACCEPTED / SUPERSEDED）

## 决定

当前 playable world 的全部 Outdoor `WorldSite` 迁入 `base:surface_main_wilderness_v1`。普通 Outdoor 中，`PlayerPartyWorldMotion.LocationKind` 始终为 `AtWorldPosition`；`WorldSite` 保持战略 identity、footprint、owner、Capture/Siege/ControlCore authority，但不再是进入一张 `LocalMap` 的 physical transition。

`WorldSite.LocalMapId` 暂保留为 legacy authoring/migration source 和旧存档兼容，正常 Outdoor runtime 禁止按它设置 `ActiveMapLayoutId`。Interior/Cave/Dungeon/Secret Realm 等独立空间仍是 Portal / LocalMap。

## V1 物理模型

每个 Outdoor Site 具备 baked `PhysicalRegion`：`SiteId + SurfaceId + footprint-derived world polygon/anchor + legacySourceMapId`。`WorldPosition → PhysicalRegion` 只产生 `CurrentOutdoorWorldSiteId` context，不产生第二份 position authority，也不切换地图。

每个显式 source placement 经 `WorldSiteSpatialMapping` 一次性换算为固定 canonical WorldPosition、稳定 source id、SiteId、kind、size/metadata，并分配给相交的 SurfaceChunk。Chunk 仅拥有 presentation；Domain/stateful object identity 不以 chunk 编号生成。

## Normal runtime

```text
Wilderness ↔ Village/Town/Sect outdoor ↔ Wilderness
          WorldPosition + Main Continuous Surface only
```

不使用 Gateway、Outdoor SurfaceExit、Outdoor WorldSite LocalMap load、destination spawn 或 camera snap。AutoTravel 穿越 Site 也不断路；DestinationSite 在进入 PhysicalRegion 后向其 baked arrival anchor 正常靠近再完成。

## Compatibility

`AtWorldSite` 仅作为 old-save/未迁 legacy space compatibility。加载已迁 Outdoor Site 的旧 `AtWorldSite` 保存时，使用已有 spatial mapping 或 canonical fallback 转为 `AtWorldPosition` 后再加载 Main Surface。

## Scope / deferred

本轮覆盖当前 playable `travel_mvp_hex_world_30x15` 的所有 Outdoor Sites，不做新 Geography、procedural terrain、Flight、World Event、Interior continuous 或全量 background NPC simulation rewrite。

## 实现结果

- `7/7` `continuousOutdoor` Site 均有 checked-in `PhysicalRegion`、稳定 world-space placement、Site place 与 SurfaceChunk 分配；当前合计 `7 regions / 75 logical source placements / 405 rendered semantic objects / 18 places`。当前 PhysicalRegion 是战略 footprint 近似，不宣称独立城镇 polygon。
- 制作人首次运行发现旧实现把物理格尺寸 `0.01` 当作 source cell，单个 Site 会生成数百万 prefab，导致 Play 黑屏 / Editor 近似卡死。该伪网格路径已移除；runtime 现在直接按 physical rect 与显式 `SourceGridX/Y + SourceCellsW/H` 渲染：`SingleCentered=1`、`ZoneOverlay=1`、`PerCell=SourceCellsW*SourceCellsH`，并对期望/实际数量执行带 Site、Placement、Kind、Cells 的 fail-fast 诊断。
- Chunk materialization 组合 Wilderness base、Site placement 与 blocker WalkGrid；跨 chunk 的 Single/PerCell 以中心点唯一归属，ZoneOverlay 按相交 chunk 裁切，chunk 卸载只释放 presentation owner，placement/cell stable id 不含 chunk index。
- PlayerParty 手动移动、DestinationSite 与 through-site AutoTravel 始终保持 `AtWorldPosition`；`CurrentOutdoorWorldSiteId` 仅由 PhysicalRegion query 更新。
- NewGame 在任何 legacy LocalMap materialization 前读取 authored StartLocation 并直接提交 canonical `WorldPosition`；随后清空 PartyWorld/LocalMap focus，再激活 Main Surface。
- Loaded neighborhood 驱动独立的 continuous materialization set，覆盖 authored resident、AtSite background character、FormalArmy member、AtWorldPosition/AtHex residual、downed/corpse；卸载后立即 prune presentation，不改 Domain presence，也不写 `world.LocalMap.AddOccupant`。
- Site-scoped place registry 以 `SiteId + LocationId -> WorldPosition` 激活；议政厅、住房、工作区、农田、守卫住房与洞府入口不依赖 Site `ActiveMapLayoutId`。chunk、population、places 与 composite WalkGrid 完成后统一重建 overlay。
- ControlCore 继续使用 Domain `ControlCoreBoard` / WorldSite owner 与 Capture/Siege 服务；FactionFlag 使用 canonical world position 与 continuous composite WalkGrid。普通 destructible 与农田状态进入 Domain board 并随 Snapshot capture/restore，稳定 identity 分别来自 placement id 与 `StableId:x:y`。
- Continuous Site 的 Interior/Cave 入口记录精确 Outdoor WorldPosition；退出 Interior 后恢复该位置并重新进入 Main Surface。
- 旧 `AtWorldSite` restore 在 Host 激活前迁为 `AtWorldPosition`；新 Outdoor 运行时清空 Site LocalMap focus，并由 canonical position 重建 context。
- `ContinuousWorldSiteGatewayPresenter` 已删除；normal continuous Site 分支的 ControlCore、FactionFlag、住房、工作目标、NPC/入口查询先走 baked placement 与 site place registry，不用 `MapLayoutPick`。保留的 `EnterWorldSiteAsParty` 与 Site SurfaceExit 分支只服务未迁 legacy compatibility。

## 验证

- Core / Data / Host / EditMode Tests：现有 Unity Bee Roslyn response files 离线编译 `0 error`（仅既有 warning）。
- 使用编译后的 Core/Data 程序集实际执行 `ContentPackageLoader.Load(Content/BaseGame)` 成功，读取 `2` 个 surface。
- BaseGame checked-in bake：`7 regions / 75 unique logical placements / 405 rendered semantic objects / 18 places`；duplicate stable id、invalid chunk/semantic assignment、unresolved placement 均为 `0`，`16` 个跨 chunk placement 均有明确归属/裁切策略。
- EditMode 覆盖 direct renderer 数量契约（road `1x1=1`、wall `6x1=6`、herb `12x12=144`、controlCore `8x8=1`、zoneHousing `12x12=1`）与 destructible/farm Snapshot 字段；本轮只完成离线编译，未在 Unity Test Runner 执行这些 case。
- `git diff --check` 通过；未运行 Full EditMode / Full PlayMode / Unity 人工验收。
