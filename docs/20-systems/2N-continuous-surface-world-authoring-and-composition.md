# 连续世界制作、合成与去 Hex 产品方向

> 状态：**Locked Direction / MAP-01 Production V1 Implemented / Producer Acceptance Pending**｜优先级：P0｜最后更新：2026-09-16
> 上级：[总览](../00-project/00-overview.md)｜决策：[ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)（地图方向）、[ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)（Editor 工具链与旧 Content 迁移方向）
> 关联：[24 世界与据点](24-world-and-settlements.md)、[2J Hex Territory](2J-hex-territory-worldsites-and-dynamic-bandits.md)、[2K RPG-First](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)、[36 ContentPackage](../30-tech/36-content-package-and-mod-architecture.md)、[41 路线图](../40-process/41-roadmap.md)
> **本页是 World Composer、Fine Editor、Final Continuous Surface 与 de-Hex 产品方向的系统真源。** MAP-01 Production V1 实现范围与验证见 [242](../40-process/242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)。Editor 工具链／生命周期与旧 Content 迁移分期见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

## 1. 状态边界

### Current Implementation

- Continuous Outdoor runtime、Surface Cell、Runtime Chunk streaming 与 Canonical WorldPosition 已经存在。
- Actual Control 已使用连续 world-space；WorldMap 仍有 HexWorld shell。
- Player travel、WorldSite、FormalArmy、当前 Site authoring/bake 仍保留 Hex/LocalMap compatibility；早期 Surface geography authoring 是 W2A baker/JSON。
- 多数 wilderness surface chunk 仍是 fallback，不能把当前 Surface content coverage 当作完整大陆制作链。
- External `WorldComposer` 与 `FineEditor` 已实现 MAP-01 Production V1 authoring source、同引擎预览/Bake 和链接工作流；Legacy Migration Bridge 已把当前 Main Surface、W2A geography 与黄村空间内容导入独立 Authoring Source，但 runtime 仍未接入新的 bake artifact，现有 `Data/**` 保持权威。

### Locked Future Direction

- 一大陆一张 Final Continuous Surface；普通 outdoor 无 LocalMap transition。
- World Composer 是大陆级 macro authoring，Fine Editor 是 Surface Cell 级局部制作。
- WorldMap 是该 Final Surface 的 LOD，不建立第二套地理、坐标或路线真源。
- 新架构不得新增 Hex Gameplay Authority；按消费者分阶段迁移，而不是一轮删除 Hex。

### Deferred / Open Design Question

自动水文、道路自动寻路、detail scatter、minor POI、terrain compatibility matrix、Runtime Chunk profiling，以及新 bake artifact 的 runtime 接入仍为后续范围。MAP-01 V1 已锁定的 source schema、确定性 terrain expansion、道路/河流曲线、Blueprint quarter rotation 与合成顺序见 §3 和 [242](../40-process/242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)。

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

WorldMap 应从同一 Surface 派生战略缩放：显示实际 terrain、水、山、道路、森林、Site、party、NPC squads 与 actual control。地图点击、路线目标和 Site 选择以 exact WorldPosition 为共同坐标，不以 Hex center / Q-R authoring 伪造实际位置。

未来 travel / navigation 继续使用同一 Final Surface 通行语义；本页不重写现有 travel、河桥、navigation 或 streaming。

## 6. 去 Hex 迁移纪律

| 禁止新增的 future Hex authority | 分阶段迁出对象 | 保留到迁出前 |
|---|---|---|
| travel、territory/administration、battle/support range、Site footprint、player WorldMap、new Q/R Content authoring | WorldMap → PlayerTravel → WorldSite → FactionFlag → NPC strategic | 现有 HexWorld、HexCoord、topology、Content/Save adapter |

Hex 的旧历史和兼容实现不能在 MAP-01 或纯文档工作中删除。旧页面中 Pure Hex、一 Site 一 LocalMap、Hex footprint 一类正文均是历史实现或 compatibility 基线；详见本页关联的 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)。

## 7. MAP-01 Production V1（已实现，待制作人验收）

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
