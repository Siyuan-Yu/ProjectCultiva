# ADR-0036：连续世界制作与去 Hex 化产品方向

> 日期：2026-09-15
> 状态：**Accepted Direction；MAP-01 / MAP-02 / MAP-03 已 Producer Accepted / Sealed；MAP-04 未开始**
> 决策者：制作人
> 关联：[ADR-0031](ADR-0031-continuous-outdoor-world-surface-architecture.md)、[ADR-0027](ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)、[2N 连续世界制作与合成](../../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[24 世界与据点](../../20-systems/24-world-and-settlements.md)、[2J Hex Territory](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)、[2K RPG-First](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)

## Context

Continuous Outdoor runtime、Surface Cell、Chunk streaming、Canonical WorldPosition 与 world-space Actual Control 已经存在或正在使用。MAP-01 已完成 Composer/FineEditor 与兼容发布，MAP-02 已将主 Surface WorldMap 切到同源的 strategic Surface view，MAP-03 已使玩家旅行、WorldSite、FactionFlag、NPC／Squad／FormalArmy 和战斗位置的正常路径使用 Surface authority；旧 Hex／Outdoor LocalMap 路径仍作 compatibility，当前大量 chunk 仍使用 wilderness fallback。

这不是“当前所有世界地图制作和战略空间都已经迁移”的声明。MAP-01/MAP-02/MAP-03 的已验收范围分别由 [242](../242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](../243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md) 与 [244](../244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) 记录；MAP-04 旧内容物理清理仍须单独授权。

## Decision

### 1. 三种格与唯一运行时地理真源

| 概念 | 定义 | 已锁定职责 |
|---|---|---|
| **Surface Cell（连续世界格）** | 最终户外世界最小实际地形单元；早期 local tile 的正式后继名称 | 1×1 地形、walkability、建筑 footprint、水体、道路、农田与精修 |
| **Runtime Chunk（运行块）** | 技术性的 streaming / materialization / navigation cache 分区 | 当前 50×50 Surface Cells；不是制作单位、WorldSite 尺寸、行政范围或 WorldMap 编辑单位 |
| **World Editor Cell（大地图编辑格）** | Future World Composer 的粗粒度 authoring 格 | 固定 1 = 10×10 Surface Cells；运行时不依赖它 |

最终运行时普通户外地理的唯一真源是 **Final Continuous Surface**。它由 authoring composition + bake 生成；runtime 只读取最终 Surface 与 Runtime Chunk，不读取 Blueprint、Detail Patch、World Editor Cell、Hex authoring grid 或普通地形地图拼片。

当前 50×50 Runtime Chunk 保持不动。未来可在 profiling 后独立评估例如 100×100 的技术迁移；该决定不得反向改变 Surface Cell 粒度或 World Editor Cell 的 10×10 比例。

### 2. 世界制作模型

一块大陆由一张 Continuous Surface 表示。World Composer 以大陆尺度编辑粗粒度自然地理；Fine Editor 在 Surface Cell 精度处理据点蓝图与局部精修。普通荒野、村、镇、城、宗门外部在同一 Surface 上连续行走，不因城镇身份重新设计为 Runtime LocalMap。

真正独立的建筑内部、洞穴、地下、地牢、秘境、洞天及其他物理独立大陆仍可使用独立空间／transition。世界基础地理由制作人手工决定；程序仅作为 editor authoring assistant 与可重复的 Terrain/Detail Deterministic Expansion。禁止把这个方向描述为 runtime procedural New Game world generation；所有新游戏读同一份已 bake 的固定 Content，存档只保存动态玩法状态。

### 3. Authoring layer 与合成

Future composition 采用确定性层级。以下数字表达默认相对优先级，**不是已冻结 schema 字段**：

| 层 | 内容 |
|---:|---|
| 0 | Macro Base Terrain：World Composer 手绘平原、森林、山地、水域/湖泊、沼泽、荒地及未来类型 |
| 10 | Road / River / geography overlays |
| 20 | Detail Patch：任意尺寸的 Surface Cell 级局部精修 |
| 30 | WorldSite Blueprint |
| 100 | Manual Final Override |

同一目标位置按稳定、可解释的 priority key 合成；World Composer 的 Composition Preview 必须尽量复用实际 bake/composition 逻辑，显示真正的最终组合结果，而不是粗略缩略图。

Road、river 是 world-composition overlay 并 bake 入 Final Surface；其 authoring 输入采用 grid connection、spline 或 hybrid，仍是 Open Implementation Decision。

### 4. WorldSite Blueprint 与 Detail Patch

**WorldSite Blueprint** 是村、镇、城、宗门等固定 Site 的细粒度 authored 蓝图。它可为任意整数 Surface Cell 尺寸，例如 80×60、173×125 或 300×240；可跨 Runtime Chunk 与 World Editor Cell，但始终只有一个 SiteId、一个 WorldSite、一个 SiteCore authority。

Blueprint 可包含核心、住房、农田、恢复处、储藏室、城墙、局部道路、工作区、spawn 等。其局部坐标加世界 origin 后 bake 为 Final Surface；runtime 不认识来源 Blueprint。

**Detail Patch** 同样以 Surface Cell 编辑，可任意尺寸，覆盖 Macro Terrain Expansion 的输出，适用于关隘、河岸、废墟、山口等精修。它不是新的 runtime map piece。

Blueprint 内可清除普通植被、散石等小型环境；不得静默抹除河流、湖泊、锁定 cliff 或其他重要 Site。未来 Composer 必须显示冲突。AllowedBaseTerrain、完整兼容矩阵以及 Blueprint 是否支持 rotation 均未实现；rotation 是 Open Editor Feature Decision。

### 5. WorldMap、位置与去 Hex 目标

Future WorldMap 是 Final Continuous Surface 的战略缩放 / LOD，不是另一张独立策略地图。它显示同一世界位置上的真实地形、山水、道路、森林、Site、PlayerParty、NPC squad 与实际控制。WorldMap 点选必须落到 exact Surface WorldPosition／Site，而非先落 HexCoord。

未来 Hex Gameplay Authority 退出旅行、行政/领土 authority、战斗/支援范围、Site footprint、玩家 WorldMap 与新 Q/R Content authoring。不得再为新系统建立依赖 Hex 的空间权威。

这不是一次性删除现有 Hex。迁移顺序锁定为：

1. 禁止新架构新增 Hex authority；
2. 分阶段迁移 WorldMap、PlayerTravel、WorldSite、FactionFlag、NPC strategic；
3. 保留必要的 legacy Content/Save/adapter compatibility，直到对应消费者迁出。

历史 Hex macro terrain authoring 由 Square World Editor Cell authoring 方向替代；Hex topology/index 不在本 ADR 中修改。

### 6. 制作与运行时工作流

World Composer → 在 10×10 Surface Cell 的 World Editor Cell 上绘制 macro terrain → 放置 WorldSite Blueprint → 叠加 Detail Patch、road / river → Composition Preview → bake Final Continuous Surface → runtime 只读取 Final Surface + Runtime Chunk streaming → WorldMap 从同一 Surface 导出 LOD。

Fine Editor 专注 Site Blueprint 与 Detail Patch 的 Surface Cell 级编辑；不要求制作人为每条道路、每个远景格或每棵树逐一进入 Fine Editor。Terrain Expansion 可以产生树木、岩石、植被、地形边缘等细节，但宏观地形类型与位置必须由制作人决定。

初始 MAP-01 仅建议验证最小链路：Composer 10×10 paint、Blueprint placement、Detail Patch、真实 composite preview 与 Continuous Surface bake。它不包含完整 procedural terrain、spline/hydrology、自动道路 A*、POI、Runtime Chunk 调整或完整 Hex 删除。

## Current Implementation vs Locked Future Direction

| 主题 | Current Implementation | Locked Future Direction |
|---|---|---|
| 户外运行时 | Continuous Surface、Surface Cell、Chunk streaming 已存在；部分 surface chunk 仍 fallback | Final Continuous Surface 是普通户外唯一运行时地理真源 |
| 世界位置 | WorldPosition 已大体是正常产品 authority | WorldMap / target / travel 都以 exact Surface WorldPosition 对齐 |
| WorldMap | 主 Surface 已为同一 Final Surface 的 LOD / strategic zoom；未迁出 world 保留 Hex compatibility | 所有产品 consumer 完成 exact world-space 收口 |
| Site / 行政控制 | Actual Control 已是 world-space；现有 Site authoring 有 LocalMap/Hex bridge | Blueprint bake 到 Surface；一 SiteId/一 WorldSite/一 SiteCore |
| 制作 | 早期 W2A baker/JSON、legacy LocalMap/Hex compatibility | Composer + Fine Editor + deterministic composition/bake |
| Hex | 仍支撑多个现有兼容消费者 | 禁止进入新 authority；逐 consumer 迁出 |

## Deferred / Open Design Questions

- Road authoring 采用 grid、spline 还是 hybrid。
- River authoring 采用 grid、spline 还是 hybrid。
- Terrain Expansion 的 autotile / Wang tile / template / deterministic variation 具体算法。
- 邻接 World Editor Cell 的自然边界处理。
- Detail scatter、minor POI 与 biome 细节算法。
- Blueprint rotation、AllowedBaseTerrain 及完整 overlay conflict matrix。
- Runtime Chunk 是否保持 50×50，以及任何迁移的 profiling 门槛。

这些均不阻塞最小 MAP-01 authoring proof，也均不构成当前实现。

## Supersedes / Legacy Compatibility

- **部分 supersede ADR-0031 §2 的旧表述：** Runtime Chunk 不再是 authoring/storage 的基本单位；它只保留为 runtime 技术分区。ADR-0031 其余关于连续户外、WorldPosition 与动态状态边界的现行目标保持。
- **逐步 supersede ADR-0025 / 2J 的 Hex authoring 与 gameplay authority 表述：** 仅影响 future direction；现有 HexWorld、HexCoord、Content、Save、topology 与 adapter 仍是兼容层，不能在本 ADR 下直接删除。
- **补充 ADR-0027：** Canonical WorldPosition 是 future Final Surface / WorldMap 的共同空间基准。
- 历史 ADR、devlog 与封板记录不改写；其正文保留为当时事实，并以本 ADR 的指向标为 Legacy Compatibility / Superseded when migrated。

## Non-goals

本 ADR 不实施 MAP-01；不修改 Assets/Scripts、Scene、Prefab、Content JSON、Snapshot、Editor、terrain renderer、Runtime Chunk 尺寸、Hex topology、WorldMap 行为、travel、navigation、streaming 或存档。它不构成 Unity 人工验收或任何代码开工授权。
