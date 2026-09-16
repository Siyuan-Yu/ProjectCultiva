# MAP-03 — Normal Gameplay Surface Authority Cutover

> 日期：2026-09-16
> 状态：**Implementation Complete / Producer Acceptance Pending**
> 前置：[MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)

## 目标

主 Continuous Surface 的正常 gameplay 以 exact `WorldPosition`、`SurfaceGroundNavigation`、continuous WorldSite context 与 Continuous Outdoor assets 为 authority。HexWorld 与 Outdoor LocalMap 仅保留给 legacy test content、旧存档迁移和 derived debug/compatibility metadata。

## 本轮实施

- **PlayerParty / Opening / WorldSite**：主 Surface 旅行由 `PlayerPartySurfaceTravelService` 从精确当前位置与目的地查询 `SurfaceGroundNavigation`，不以 HexPath 作为启动条件。最终到达按物理距离结算，成员 Presence 保持 `SurfaceId + WorldPosition`；opening 取 baked continuous anchor。正常 WorldSite 上下文由 continuous 行政/物理查询判定，不回退到 Hex footprint。WorldMap 关闭及 Snapshot active focus 不再装载普通 Outdoor LocalMap。
- **旅行存档**：PlayerParty active Surface 旅行保存精确目的地、到达半径与 Site 意图；Content shell 注册导航后按保存的起点重新求路。NPC 后台 Surface 旅行保存精确当前位置、SurfaceId 与 Site 目标，旧 HexPath 字段继续可读；恢复后重建地形路线。缺失新增字段的旧档按原路径读取。
- **FactionFlag**：正常建旗先以 `SurfaceId + WorldPosition` 验证地表和实际控制；`StrategicAnchor` 只从精确位置派生用于旧拓扑。新 runtime FlagId 使用阵营与持久 ID 序列，不嵌入 Q/R。FlagId、Site CoreAssetId 和 SiteId 是身份链；Hex index 只供兼容查询。读档时连续 Site-Core 不要求保存的 AnchorQ/R 在 Hex grid 内，改从存档精确坐标重建派生索引。破坏/重建按 FlagId 与 Site 关联处理。
- **NPC / Squad / FormalArmy**：NPC schedule 的实时精确位置路径保持现有产品行为；后台 NPC 到连续 Site 使用地表寻路与 authored Site arrival，逐段推进时写入个人精确 Presence。FormalArmy/Squad Site 命令同样以精确起点和 authored arrival 求 Surface route；成员从组织位置派生时附 SurfaceId，Site 初始化不再取 AnchorHex center。非 Outdoor Site 继续原逻辑。
- **Encounter / Battle**：正常 FormalArmy 与 PlayerParty 接触以同一 Surface 内的世界距离判断，不要求同格/邻格。BattleParticipantSnapshot 增加 `HasBattleAnchorWorldPosition`、`BattleAnchorWorldX/Y`、`BattleAnchorSurfaceId`，在 offer 冻结时从实际接触点记录，并以 additive JSON 字段读写。参战者战前精确 Presence 在复制与存档中保留；普通连续战后位置与残留以 world anchor 写回，Hex anchor 留给旧模式。独立战场、世界冻结、BattleOffer 与战报流程不重做。
- **Residual / Lingering**：弥留和尸体的个人 `SurfaceId + WorldPosition` 可独立成为稳定空间 authority；旧 residual Hex 仍兼容。Continuous WorldMap 的残留标记直接投影个人精确位置，不靠 Hex 栅格分组。角色生死状态和组织归属规则未改。

## 依赖审计与兼容边界

本次搜索覆盖 `HexWorld.HasGrid`、`WorldToHex`、`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`BattleAnchorHex`、`StrategicAnchor`、`LocalMapId`、`mapLayout`，并按入口分类：

| 类别 | 当前保留含义 |
| --- | --- |
| Normal Authority | Surface navigation、exact WorldPosition、Site continuous arrival/control、个人 SurfaceId、Battle world anchor。 |
| Derived Compatibility | WorldToHex/CurrentHex、Site/Flag AnchorHex、BattleAnchorHex、WorldMap Hex 展示索引；这些不决定普通 Surface 路线、放旗合法性或战后精确位置。 |
| Legacy-only | `PlayerPartyHexTravelService` 的 HexPath/Footprint 旅行、`BackgroundCharacterTravelService` 的 Hex route、FormalArmy Hex route、`WildernessLocalMapFallback`、旧 Outdoor LocalMap、仅 Hex 的旧存档；由 Surface/Outdoor 分支隔离。 |
| 独立区域 | Interior、Cave、Dungeon/Separate Surface 继续允许独立 map transition 与 save/load；本次只退出主 Outdoor 的 LocalMap takeover。 |

仍可能在代码搜索中看到 Hex 字段和旧 LocalMapId；它们服务于派生索引与 legacy/独立区域，不是要求 grep=0。Runtime Chunk streaming、Surface seam 与真实地形寻路规则未修改。

## 验证与制作人验收

离线最小编译：`XianXia.Core`、`XianXia.Data`、`XianXia.Unity` 均通过，0 error；5 条既有 warning。`git diff --check` 通过。未运行 Unity、PlayMode、Test Runner、batchmode 或大型自动测试。

请制作人重点复验：开局主控坐标；WorldMap 规划/关闭与旅行中 Save→Load→续行；连续 Site 出入不装 Outdoor LocalMap；建旗、存档、破坏/重建；NPC 与 FormalArmy 到 Site；接触战斗的锚点和战后原位；弥留→尸体以及 WorldMap 残留标记；Interior/Cave 仍可进入。

## 版本控制

本阶段完成制作人 Unity 人工验收前，所有改动保持未暂存、未提交、未推送。
