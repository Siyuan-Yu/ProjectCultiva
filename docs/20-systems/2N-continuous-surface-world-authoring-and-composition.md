# 连续世界制作、合成与去 Hex 产品方向

> 状态：**Locked Future Direction / Not Implemented（已锁定未来方向／尚未实现）**｜优先级：P0｜最后更新：2026-09-15
> 上级：[总览](../00-project/00-overview.md)｜决策：[ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)
> 关联：[24 世界与据点](24-world-and-settlements.md)、[2J Hex Territory](2J-hex-territory-worldsites-and-dynamic-bandits.md)、[2K RPG-First](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)、[36 ContentPackage](../30-tech/36-content-package-and-mod-architecture.md)
> **本页是未来 World Composer、Fine Editor、Final Continuous Surface 与 de-Hex 产品方向的系统真源；不是 MAP-01 实现说明。**

## 1. 状态边界

### Current Implementation

- Continuous Outdoor runtime、Surface Cell、Runtime Chunk streaming 与 Canonical WorldPosition 已经存在。
- Actual Control 已使用连续 world-space；WorldMap 仍有 HexWorld shell。
- Player travel、WorldSite、FormalArmy、当前 Site authoring/bake 仍保留 Hex/LocalMap compatibility；早期 Surface geography authoring 是 W2A baker/JSON。
- 多数 wilderness surface chunk 仍是 fallback，不能把当前 Surface content coverage 当作完整大陆制作链。

### Locked Future Direction

- 一大陆一张 Final Continuous Surface；普通 outdoor 无 LocalMap transition。
- World Composer 是大陆级 macro authoring，Fine Editor 是 Surface Cell 级局部制作。
- WorldMap 是该 Final Surface 的 LOD，不建立第二套地理、坐标或路线真源。
- 新架构不得新增 Hex Gameplay Authority；按消费者分阶段迁移，而不是一轮删除 Hex。

### Deferred / Open Design Question

Road/river 格式、terrain expansion、邻接边缘、detail scatter、minor POI、Blueprint rotation、terrain compatibility matrix 与 Runtime Chunk profiling 均未实现、也未在本页伪装为已定算法。

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

合成顺序：Macro Base Terrain → Road / River / geography overlays → Detail Patch → WorldSite Blueprint → Manual Final Override → Composition Preview → bake Final Continuous Surface。

默认优先级为 0 / 10 / 20 / 30 / 100，只表达层次；正式 schema、存储字段与冲突 key 仍待 MAP-01 设计。Preview 应尽可能消费同一 compose/bake 规则，让制作人看到真实最终结果。

Road、river 是 overlay，不是单独地图；其 grid connection/spline/hybrid 数据结构尚未决定。World Editor Cell 扩展到邻格时必须考虑自然边缘，但 autotile、Wang tile、template 或 deterministic variation 的具体选择未定。

## 4. WorldSite Blueprint 与 Fine Editor

Blueprint 以 Surface Cell 描述 SiteCore、住房、农田、恢复处、储藏室、城墙、局部道路、工作区、spawn 等，并以世界 origin bake 到 Final Surface。它可以跨多个 Chunk 或 World Editor Cell，但不分裂 SiteId / WorldSite / SiteCore authority。

Fine Editor 面向 Blueprint 与 Detail Patch 的高精度编辑：terrain、水、road、blocker、object、建筑、farm、storage、recovery、work area、spawn 等。它不用于逐块绘制整片大陆。

Blueprint 可清理普通 vegetation、scatter 与小石块；遇到 river、lake、locked cliff 或另一 major Site 必须报可见 conflict。普通村落的 AllowedBaseTerrain 预期偏向 Plain，特殊山门、水域或洞穴 Site 可允许其他地形；字段、兼容矩阵与 rotation 均待实现。

## 5. Runtime、WorldMap 与旅行目标

普通世界运行时只消费 Final Continuous Surface 与 Runtime Chunk streaming。Chunk 卸载只影响表现、缓存和近场 runtime，不是行政、建筑身份或世界编辑边界。

WorldMap 应从同一 Surface 派生战略缩放：显示实际 terrain、水、山、道路、森林、Site、party、NPC squads 与 actual control。地图点击、路线目标和 Site 选择以 exact WorldPosition 为共同坐标，不以 Hex center / Q-R authoring 伪造实际位置。

未来 travel / navigation 继续使用同一 Final Surface 通行语义；本页不重写现有 travel、河桥、navigation 或 streaming。

## 6. 去 Hex 迁移纪律

| 禁止新增的 future Hex authority | 分阶段迁出对象 | 保留到迁出前 |
|---|---|---|
| travel、territory/administration、battle/support range、Site footprint、player WorldMap、new Q/R Content authoring | WorldMap → PlayerTravel → WorldSite → FactionFlag → NPC strategic | 现有 HexWorld、HexCoord、topology、Content/Save adapter |

Hex 的旧历史和兼容实现不能在 MAP-01 或纯文档工作中删除。旧页面中 Pure Hex、一 Site 一 LocalMap、Hex footprint 一类正文均是历史实现或 compatibility 基线；详见本页关联的 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)。

## 7. MAP-01 建议 Proof Scope（未开始）

MAP-01 只建议验证：

1. Composer 在 World Editor Cell 上绘制基础地形；
2. 放置一份 Blueprint；
3. 添加一处 Detail Patch；
4. 显示真实 composition preview；
5. bake 成可读取的 Continuous Surface。

不包含完整 terrain generation、spline/hydrology、自动 road A*、POI、Chunk 尺寸迁移或完整 Hex 删除。任何实现必须另开任务并明确授权。
