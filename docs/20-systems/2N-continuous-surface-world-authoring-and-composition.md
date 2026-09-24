# 连续世界制作、合成与去 Hex 产品方向
> **2026-09-22 正式运行依赖退役：** Continuous Surface 是正常 Outdoor authority；`SimulationWorld` 无 HexWorld，Core Hex 目录已物理删除，正常产品不编译旧 Hex 几何。Runtime Data 只接受当前 `outdoorSurface`／`npcSquad`。离线转换器只无损处理 FormalArmy 与 current authority 完整的 hybrid Snapshot；旧 `hexWorld`／`openingHexWorldId` 会被检测并拒绝，须使用现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。见 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。
> **2026-09-21 LEGACY-FINAL-C Seal：** normal Continuous runtime 已不再拥有 TerritoryRegion、StrategicEncounter、RetreatingArmy 或 LingeringBattlefield board；现代 residual 只使用 exact Surface position，AtHex／Outdoor LocalMap 只留 legacy input／compatibility。状态：**Producer Accepted / Sealed**，见 [250](../40-process/250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。
> **2026-09-21 LEGACY-FINAL-B Seal：** PlayerParty 正常 New Game、WorldMap travel、direct movement 与 snapshot authority 已统一为 `SurfaceId + exact WorldPosition + SurfaceVisible`。WorldSite／Runtime Chunk／旧 Hex seam 不再建立 Outdoor LocalMap 或改变 modern location kind；Hex、LocalVisible 与 Wilderness transition 只作 compatibility。状态：**Producer Accepted / Sealed**，见 [249](../40-process/249-legacy-final-b-playerparty-continuous-surface-travel-authority-cutover-2026-09-21.md)。
> 状态：**MAP-01 / MAP-02 / MAP-03 / MAP-04 / SPACE-01 与 Player Control Continuity Surface re-anchor 均 Producer Accepted / Sealed**｜优先级：P0｜最后更新：2026-09-24
> 上级：[总览](../00-project/00-overview.md)｜决策：[ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)（地图方向）、[ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)（Editor 工具链与旧 Content 迁移方向）
> 关联：[24 世界与据点](24-world-and-settlements.md)、[2J Hex Territory](2J-hex-territory-worldsites-and-dynamic-bandits.md)、[2K RPG-First](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)、[36 ContentPackage](../30-tech/36-content-package-and-mod-architecture.md)、[41 路线图](../40-process/41-roadmap.md)
> **本页是 World Composer、Fine Editor、Final Continuous Surface 与 de-Hex 产品方向的系统真源。** MAP-01 实现范围见 [242](../40-process/242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)，MAP-02 验收与残余兼容边界见 [243](../40-process/243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)。Editor 工具链／生命周期与旧 Content 迁移分期见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

## 1. 状态边界

当前阶段结论：MAP-01～MAP-04、SPACE-01 与 LEGACY-FINAL-A／B／C 均已完成既有验收；正式 runtime 依赖退役已落实。Normal Outdoor 只以 exact `WorldPosition`、`SurfaceId` 与 `SurfaceGroundNavigation` 为地理 authority；旧 `hexWorld`／`formalArmy` 在 Loader 边界明确拒绝，不再有 runtime migration／compatibility 执行器。Separate Space 继续合法保留 LocalMap infrastructure。

> **2026-09-21 LEGACY-FINAL-A 历史 Seal：** 当时 NPC group runtime zero-state 已通过验收，正式成员、位置与战斗 identity 只使用 Squad + SquadWorldMotion + CharacterId/SquadId；当时保留的 runtime 读取迁移随后已由本页顶部 2026-09-22 离线转换边界取代。

> 本页只记系统与产品方向。当前仓库真实状态、Milestone 表、Known Issues、Do Not Regress、Resume Order 与可复制上下文见 [247 Project Handoff — Current State](../40-process/247-project-handoff-current-state-2026-09-18.md)；MAP-04 审计见 [245](../40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md)；SPACE-01 见 [246](../40-process/246-space-01-separate-space-interior-transition-v1-2026-09-18.md)。

### Current Implementation

- Continuous Outdoor runtime、Surface Cell、Runtime Chunk streaming 与 Canonical WorldPosition 已经存在。
- Actual Control 已使用连续 world-space；MAP-02 已使主 Surface 的 WorldMap 使用 Continuous Surface strategic view、exact world projection 与 Surface terrain/forest cache。正式产品已无 HexWorld consumer。
- MAP-03 已把主 Surface 的 PlayerParty、WorldSite、FactionFlag、NPC/Squad/FormalArmy、BattleAnchor 与 residual 正常位置改为 continuous world-space authority；Outdoor LocalMap 与 Hex 的正常 authority 已退出。Hex/LocalMap 字段仍服务 derived compatibility、旧存档或独立区域路径。实施及制作人验收边界见 [244](../40-process/244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)。
- 多数 wilderness surface chunk 仍是 fallback，不能把当前 Surface content coverage 当作完整大陆制作链。
- External `WorldComposer` 与 `FineEditor` 已完成 MAP-01 Production V1，并经制作人验收；Legacy Migration Bridge 已把当前 Main Surface、W2A geography 与荒村空间内容导入独立 Authoring Source。MAP-02 已发布并加载同源的 Surface WorldMap cache；现有 `Data/**` 仍为 runtime authority。

### Locked Future Direction

- 一大陆一张 Final Continuous Surface；普通 outdoor 无 LocalMap transition。
- World Composer 是大陆级 macro authoring，Fine Editor 是 Surface Cell 级局部制作。
- WorldMap 是该 Final Surface 的 LOD，不建立第二套地理、坐标或路线真源。
- 新架构不得新增 Hex Gameplay Authority；按消费者分阶段迁移，而不是一轮删除 Hex。

### Deferred / Open Design Question

自动水文、道路自动寻路、detail scatter、minor POI、terrain compatibility matrix 与 Runtime Chunk profiling 仍为 Future / Not Implemented；MAP-04 legacy retirement 已完成，不属于该 Future 列表。MAP-01 V1 的 source schema、确定性 terrain expansion、道路/河流曲线、Blueprint quarter rotation 与合成顺序见 §3 和 [242](../40-process/242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)。

## 2. 统一术语与比例

| 名称 | 比例 / 尺寸 | 产品职责 | 非职责 |
|---|---|---|---|
| Surface Cell（连续世界格） | 1×1 | 最小最终 terrain / walkability / footprint / water / road / farm / fine edit | 不是运行块 |
| Runtime Chunk（运行块） | 当前 50×50 Surface Cells | streaming、materialization、navigation cache、runtime 分区 | 不是地图制作、Site 大小、行政范围、编辑格 |
| World Editor Cell（大地图编辑格） | 10×10 Surface Cells | Composer macro terrain authoring | runtime 不读取 |
| WorldSite Blueprint（据点蓝图） | 任意整数 Surface Cell 尺寸 | 固定 Site 的精修布局与 bake 输入 | 不是 runtime scene / map piece |
| Detail Patch（精修块） | 任意 Surface Cell 尺寸 | 局部覆盖与手工修饰 | 不是 chunk |
| Final Continuous Surface | 一大陆一张 | runtime 户外地理唯一真源 | 不保留 source authoring pieces |

例：主要 Surface 为 1900×850 cells 时，Composer 为 190×85 World Editor Cells；边缘使用 ceil(dimension / 10)，允许最后一格为 partial。当前 50×50 Chunk 等于 5×5 World Editor Cells。

150×150 Surface Cells 仅表示一级 SiteCore 的理论行政控制范围；不是 Chunk、Blueprint、World Editor Cell、Natural Region 或 authoring 最小尺寸。

## 3. Authoring 与 bake

制作人手工绘制大陆的宏观地形与关键地理。Terrain/Detail Deterministic Expansion 只把已选择的宏观意图展开为 10×10 cell 细节，例如地表、树木散布、空地及自然边缘；它不是 runtime procedural world generation。

MAP-01 V1 合成顺序：Macro Base Terrain → Forest Coverage → River（最终基础地形为 Water）→ Road → Detail Patch terrain override → WorldSite Blueprint terrain override → Composition World Object / Blueprint object overlay → Composition Preview / Final Continuous Surface Bake。

正式 authoring source 使用 schema v3：基础地形只含 `Plain / Mountain / Water`，覆盖层只含 `None / Forest`；`WorldCompositionDocument` 另含 Runtime Surface Binding 与 `WorldObjectPlacement`。旧 v1/v2 制作源在加载时迁移，未设置的 Fine Cell 表示 Inherit。Preview 与 Bake 必须消费同一 `CompositionEngine`，确定性只依赖 world seed、全局 Surface 坐标与 source intent，不依赖随机调用顺序。

Road、river 是带控制点、宽度/道路等级的 Surface 坐标曲线 overlay，不是单独地图。V1 使用 Catmull-Rom 采样与稳定坐标散列完成预览和 Bake；更完整的自动水文、自动道路及美术 autotile 仍可在保持 schema 可迁移的前提下演进。

## 4. WorldSite Blueprint 与 Fine Editor

Blueprint 以 Surface Cell 描述 SiteCore、住房、农田、恢复处、储藏室、城墙、局部道路、工作区、spawn 等，并以世界 origin bake 到 Final Surface。它可以跨多个 Chunk 或 World Editor Cell，但不分裂 SiteId / WorldSite / SiteCore authority。

Fine Editor 面向 Blueprint 与 Detail Patch 的高精度编辑：terrain、水、road、blocker、object、建筑、farm、storage、recovery、work area、spawn 等。它不用于逐块绘制整片大陆。

Blueprint 支持 quarter-turn rotation，并可携带通用对象 placement：稳定 id、kind、可选 Content/Asset 引用、局部 Surface 坐标、footprint、rotation 与 metadata。缺失/损坏链接、越界、重复 id、重叠与未解决道路河流交叉口进入 Problems；更细的 terrain compatibility matrix 仍待后续阶段定义。

## 5. Runtime、WorldMap 与旅行目标

普通世界运行时只消费 Final Continuous Surface 与 Runtime Chunk streaming。Chunk 卸载只影响表现、缓存和近场 runtime，不是行政、建筑身份或世界编辑边界。

External Control Handoff 仍消费同一 player-centered streaming authority。接管者跨 Surface，或在同 Surface 但落点超出旧 loaded 5×5 时，先释放旧 presentation，再以接管者 chunk 为中心同步 hard re-anchor；落点已在当前邻域内时可复用既有 presentation。独立战场退出若 `NeedsExternalControlHandoff`，只释放旧 battlefield，不先重建旧 Party 邻域。目的地 Surface、正确 ActiveSurfaceId、successor chunk、Site／Squad／Population reconcile 与 successor EntityView 全部就绪后，Camera／Selection 才能切换；不建立第二 camera streaming center。

WorldMap 应从同一 Surface 派生战略缩放：显示实际 terrain、水、山、道路、森林、Site、party、NPC squads 与 actual control。地图点击、路线目标和 Site 选择以 exact WorldPosition 为共同坐标，不以 Hex center / Q-R authoring 伪造实际位置。

当前已确认 UI 规则：战略图标、名称间距和字号按世界空间投影随地图 zoom 缩放；Header、Footer 与按需 Flyout 才保持屏幕空间。不得恢复固定屏幕像素图标／标签方案。Site／Flag 已按该规则实现；静态核对发现 Player marker 仍固定 20×20 px、NPC Squad marker 固定 14×14 px，属于当前实现与规则的差异，尚未授权修复。WorldMap 打开是 planning overlay，不移动玩家；有效目标进入同一 Surface travel 计划，关闭地图后恢复旅行。

未来 travel / navigation 继续使用同一 Final Surface 通行语义；本页不重写现有 travel、河桥、navigation 或 streaming。

## 6. 去 Hex 退役纪律

正式产品不得新增或重新编译 Hex travel、territory／administration、battle range、Site footprint、WorldMap 或 Q/R authoring authority。FormalArmy 与 current authority 完整的 hybrid Snapshot 只可由 `LegacyRuntimeConverter` 在 runtime 外无损转换；旧地图 Content 必须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径，通用转换器只检测并拒绝 `hexWorld`／`openingHexWorldId`，无样例时不猜。输入只读、输出必须是不同且尚不存在的文件。旧页面中的 Pure Hex、一 Site 一 LocalMap与 footprint 正文均为历史记录，不是恢复 adapter 的依据。

## 7. MAP-01 Production V1（已验收／封板）

MAP-01 当前交付：

1. Composer 在 World Editor Cell 上绘制平原／山地／水域和独立的 Forest 密度覆盖；
2. 编辑河流、道路、交叉口，并放置/移动/旋转链接 Blueprint；
3. 从 Surface 框选创建 Detail Patch，交给 FineEditor 逐格精修；
4. FineEditor 编辑 sparse terrain override 与 Blueprint 通用对象 placement；
5. 使用同一 CompositionEngine 显示最终预览并生成带 source hash、行程压缩 terrain、路径、交叉口、Blueprint 与显式世界物件的 bake artifact；
6. Legacy Migration Bridge 从当前旧 Runtime JSON 生成独立 Authoring Source，并可导出不自动安装的 Runtime Compatibility Candidate。

本阶段不包含自动 hydrology、道路 A*、自动 POI、Runtime/Gameplay cutover、Chunk 尺寸迁移或 Hex 删除。

## 8. Authoring Source 与 Runtime Content 的边界

地图相关 Content 未来必须分成两类，**不能继续混在同一个手改 JSON 模型里**：

| 类别 | 谁编辑 | 谁消费 | 内容 |
|---|---|---|---|
| **Authoring Source** | World Composer / Fine Editor | 只有 baker | SurfaceWorldComposition、WorldSiteBlueprint、DetailPatch（概念名，schema 未锁） |
| **Runtime Generated Content** | Bake 产生 | 游戏 runtime loader（只读） | Final Continuous Surface、Runtime Chunk、final terrain/geography、object placements、WorldSite 位置与 content、navigation input、WorldMap LOD/cache input |

- Authoring Source **不得**继续作为正常 Runtime Data 直接塞进 `Content/BaseGame/Data` 并被 runtime loader 当正式 gameplay definition 加载；推荐独立 authoring root（例如 `ContentAuthoring/Worlds/<World>/…`，具体目录名待定）。现存先例：`ContentAuthoring/Worlds/w2a_surface_geography_source_v1.json` → bake → `Content/BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json`。
- 运行时**不需要知道**某个位置来自哪个 Blueprint、哪个 Detail Patch、哪张旧 LocalMap。
- 当前 `base:surface_main_wilderness_v1` 仍是混合容器（chunk source + old Site bake + LocalMap bridge + fallback）；其未来等价物是**纯 bake runtime output**。
- 现状实测（2026-09-15）：646/646 chunk 的基础地形 source 仍是 `base:map_wilderness_plain_fallback`；`siteRegions` / `sourceLocalMapId` 仍是把旧 Map Layout bake 到 Surface 的 **legacy compatibility** 通道。

规则定义与空间 placement 必须分开：WorkArea 的 activity／capacity／privileges／rules 属 Gameplay Content 并继续存在，而「WorkArea 在世界哪里」未来由 Blueprint／FineEditor placement 表达。

**边界**：地图比例、合成层与 MAP-01 proof scope 见本页 §2～§7；Editor 工具链、Editor 生命周期、旧 `mapLayout`／`localPlaceSet`／`hexWorld`／`worldRegion`／W2A／fallback 的逐个迁移分类与 MAP 分期见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

## 8. MAP-02 Continuous Surface WorldMap（已验收／封板）

MAP-02 令主 Continuous Surface 的 WorldMap 直接消费同源地图 cache，按 exact `WorldPosition` 投影、反投影与点选；Surface 模式不再把画面、点击或路线目标降格为 Hex center。Surface terrain、森林与既有 Site／party／路线呈现在同一 world-space 中；旧 Hex WorldMap 分支仅保留给尚未迁出的 compatibility world。

MAP-COORD-01（2026-09-25）在同一投影上增加正式坐标读数：鼠标位于实际地图区域时显示 Surface-local `WorldPosition` X/Y，离开后立即显示“坐标 —”；玩家坐标只读取 `PlayerPartyWorldLocationQuery` 的 authoritative Surface position，不读取 GameObject Transform。`SurfaceWorldMapViewportProjection` 同时负责 world→map 与 map→world，故默认视图、缩放和平移共用严格逆变换；UI 只格式化为一位小数，不改 runtime 值。地图下边缘和左边缘使用随 visible world span 选择的 1/2/5×10ⁿ major interval，维持约 5～10 个参考区间。

坐标显示只提供同一 Surface 的空间参照，不产生对象知识。未发现的 `hiddenUntilDiscovered` Opportunity 仍无 marker、Activity、Toast、locator 或 View；开发验收可读取 LevelTester 输出坐标并在 WorldMap 判断方向，正式玩家不会因此获知隐藏对象位置。

MAP-COORD-01 已随 DYNAMIC-DISCOVERY-01 于 2026-09-24 完成制作人验收并 **Producer Accepted / Sealed**；实现与验收记录见 [262](../40-process/262-map-coord-01-worldmap-world-coordinate-readout-2026-09-25.md)。

MAP-02 本身不是完整去 Hex；MAP-03 正常 gameplay consumer cutover 与 MAP-04 物理清理均已完成并通过制作人人工验收。仍保留的 Hex／Outdoor LocalMap 符号必须能落入 ADR-0038 的旧输入、明确 compatibility、工具或测试边界；[245](../40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md) 是该阶段的历史审计记录。
