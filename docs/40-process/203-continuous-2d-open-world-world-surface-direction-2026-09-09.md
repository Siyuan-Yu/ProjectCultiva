# 连续 2D 开放世界架构方向与待决问题

> 状态：**DISCUSSION / NOT IMPLEMENTED（架构方向讨论，未实现）**｜优先级：Future Direction｜最后更新：2026-09-09
> 参考基线：`Scripts(20260908-141108)` 与本次讨论时的最新文档／代码
> 关联：`2K`、`2J`、`24`、`41-roadmap`、ADR-0021、ADR-0025、ADR-0026、ADR-0027
> **本页不是 ADR，不取代已采纳的 Freeze、2K 或 ADR；不授权 Runtime、Content、Schema、Travel 或地图迁移实现。**

---

## 0. 记录目的与边界

本记录整理长期产品方向：最终希望普通室外地理呈现为尽可能连续的二维开放世界，而不是由技术分区直接表现为一格一房间的反复换图体验。

这里的“连续”首先是玩家体验与长期架构方向，不等于本轮已经决定 Unity 具体 Streaming 技术，也不等于把整个世界一次性载入内存。本文不改变下列既有事实：

- 当前 `HexWorld` 仍是唯一世界拓扑；
- 当前 Wilderness 仍是 **1 Hex = 1 logical LocalMap instance**；
- 当前 `WorldSite` 仍是 **1 SiteId / 1 LocalMapId**，可拥有多个 Hex 的 footprint；
- 当前 `Surface Exit Trigger Zone`、WorldSite LocalMap、Wilderness/WorldSite transition、Auto Travel 与现有 Travel authority 均继续有效；
- 当前已实现的是逻辑连续位置、投影与过渡，不是完整的无缝 Outdoor Streaming 世界。

任何后续实现必须先另行完成架构决策、系统设计、迁移计划与授权；不得把本页当作实施需求。

## 1. 当前实现：已存在的地图与位置架构

### 1.1 已采纳的空间真源

当前正式模型由 `2K` 与 ADR-0027 约束：

- `HexWorld` 是唯一世界地理拓扑，负责邻接、路径、footprint、战略地形与大尺度空间查询；
- `WorldMap` 是 HexWorld 的总览与 Auto Travel UI，不是第二套位置真源；
- `LocalMap` 是当前位置的 RPG 近景展开；
- `CanonicalWorldSurfacePosition` 是 PlayerParty 在 Wilderness 与 WorldSite 内统一的物理位置真源；
- `AtWorldSite(SiteId)` 是战略 Context，不覆盖物理位置；
- `DerivedPresenceHex = WorldToHex(CanonicalWorldSurfacePosition)` 是派生查询，不是独立 authority。

这里已有的 “surface” 是 Canonical Position 的语义，不表示项目已经存在名为 `WorldSurface` 的完整 Runtime、Chunk 系统或无限室外地图。

### 1.2 当前 Wilderness / WorldSite / LocalMap 行为

| 当前对象 | 当前实现事实 |
|---|---|
| Wilderness | 一个普通 Wilderness Hex 对应一个逻辑 LocalMap 实例，可共享模板、Terrain 或生成规则。 |
| WorldSite | 一个 Site 可占多个 Hex，但仍只有一个 Site identity、一个 LocalMapId 与一张 LocalMap。 |
| 位置映射 | `WildernessLocalWorldProjection`、`HexFootprintSpatialMapping` 与 `WorldSiteSpatialMapping` 把 Local playable bounds 归一化并映射到 Hex／footprint 的连续 world-surface domain。 |
| 跨边界 | `SurfaceExit` 以固定几何触发区和运行时合法性控制从当前 LocalMap 到相邻 Hex／Site 的 transition；允许 Loading/Fade，但禁止跨格 snap 到邻格中心。 |
| WorldSite 进出 | 进入／离开改变 Site Context，不应 snap Anchor、PresenceHex 或 Site center；实际边界连接复用 `SurfaceExitConnection` 与 footprint authority。 |
| 远处角色 | Background Character 与 FormalArmy 已有分层的战略／数据模拟，不要求加载每张 LocalMap。 |

这些资产均是未来方向的候选复用基础，不是要在本轮删除或替换的旧系统。

### 1.3 当前实现与长期方向不能混写

当前实现的“逻辑连续”意味着：世界位置在跨 Hex 与进出 WorldSite 时保持连续语义，WorldMap 与 LocalMap 不应形成割裂的第二套位置空间。它**不等于**当前室外移动已没有 LocalMap context、地图加载或 SurfaceExit。

长期方向的“连续 Outdoor World Surface”则追求：玩家在普通室外步行或未来飞行时，技术分区不应被体验成连续的房间切换。两者存在继承关系，但后者尚未实现，也没有在本页决定迁移路径。

## 2. 产品 North Star：连续 Outdoor World Surface

最终希望玩家在普通室外世界中，从森林走到山谷、从荒野走进村庄并继续走出、穿越城镇与多个战略 Hex 时，尽可能感觉自己始终在同一个二维世界中移动。

未来飞行（斗气化翼）是这一目标的重要验收情境：玩家可在荒野起飞，连续越过山川、道路、城镇与多个战略 Hex，最终抵达远处；不应因为高速跨越而形成“一格一个房间”或反复进出 LocalMap 的感受。Flight 仍是 Future，当前不实现。

### 2.1 Ground 与 Flight 使用同一个世界

未来 Ground Movement 与 Flight Movement 应共享同一个连续 Outdoor World Space，只在 movement rules 上不同：

- Ground 受墙、河流、悬崖、地形、道路等限制；
- Flight 可跨越部分地面障碍，未来可另有高度、禁飞阵法或特殊空域规则；
- 二者不得分别依赖“LocalMap Portal Travel”与另一套“Flight Travel”架构。

### 2.2 一个大世界不是一张巨大地图文件

目标是统一的 Global Coordinate Space 加上可独立制作、生成、保存与 Streaming 的区域块（Chunk、Cell、Surface Patch 或 Region），而不是把全部 Tile、Collider、NPC GameObject 一次性加载。

逻辑上它是一张连续 Outdoor World；开发和运行时可以是模块化的。玩家附近加载高精度地图内容，远处保留必要 Domain State 与后台模拟。这使 Modular Authoring 与 Continuous Open World 可以共存。

## 3. 长期空间分工方向

### 3.1 World Surface（讨论概念）

`World Surface` 是本文的概念名：指玩家真正步行／飞行的连续室外二维空间。它当前不是 Runtime 类型、JSON schema 或现有类名。

长期可将稳定的全局连续位置语义继续建立在 `WorldPosition`／`CanonicalWorldSurfacePosition` 的方向上：

```text
PhysicalHex = WorldToHex(WorldPosition)
```

当前 PlayerParty 的 Canonical Position 已是重要基础，但不能因此声称所有角色、所有 Surface data 或 Streaming 均已完成。

### 3.2 Hex 的长期角色

Hex 不应废弃。长期更适合担当 Strategic / Simulation Spatial Partition：

- WorldMap、战略地形与战略路径；
- Territory、Faction Control、WorldSite footprint、FormalArmy 与 SupportArea；
- Background Travel、World Event 空间查询、移动成本与大尺度位置索引；
- 远方 simulation 分区。

因此 Hex Partition 与 Surface Streaming Chunk 不应预设为同一概念；尤其不得预设 `1 Hex = 1 Streaming Chunk`。具体 chunk 大小、形状、生成和加载策略尚未决定。

### 3.3 连续空间不等于全量模拟

Physical Continuity、Presentation Loading 与 Simulation Granularity 是三个不同问题。

| 距离／职责 | 长期方向 |
|---|---|
| 玩家附近 | Tile、Sprite、建筑、Collider、NPC GameObject、实时 AI、战斗与近景导航可 Full Detail materialize。 |
| 远处区域 | 不加载近景 Tile/Collider/NPC GameObject；只保留世界状态、计划行动、后台旅行、战略结算或未来 World Event state。 |
| 跨区域移动 | 前方区域可提前加载，身后区域可卸载；玩家的 WorldPosition、移动与 Camera continuity 应保持连续。 |

例如玩家位于青石镇时，千里之外 A 城与 B 城可以同时发生 Siege：双方都可由 `FormalArmy`、`WorldSite`、战略 Encounter、Siege State 与 Domain resolution 在后台推进，无需加载两张 City Tile Map 或运行两批实时 Combat AI。玩家接近时，再将其权威世界状态 materialize 为一致的近景表现。

## 4. 现有资产的潜在复用

未来 Continuous World 不等于推倒现有项目。下列现有资产仍有长期价值：

- `WorldPosition` / `CanonicalWorldSurfacePosition`、`WorldToHex`、HexWorld、HexGraph 与战略 pathfinding；
- WorldMap、WorldSite Definition、WorldSite footprint、Territory、Faction/Diplomacy、FormalArmy；
- Background Character / Background Travel、Character Runtime、Social Relations、Combat、Building/Construction 与 SaveGame Domain State；
- `WildernessLocalWorldProjection`、`HexFootprintSpatialMapping`、`WorldSiteSpatialMapping`、SurfaceExit 的连续映射和边界语义；
- WorldGraph authoring 概念，以及现有 WorldSite/Wilderness authored 内容。

当前 LocalMap 未来可能被复用为 `Surface Patch / Authored Patch`：旧 Wilderness LocalMap 可成为一块 authored wilderness patch；旧 WorldSite LocalMap 可成为较大的 town、sect 或 settlement patch。其 terrain、decoration、building、WorkArea、spawn 与 LocalPosition 数据可尽量变成 patch-relative data，再放置到 Global WorldPosition。

是否迁移、如何转换、哪些边缘数据可直接复用，均未决定；本页不授权迁移。

## 5. Wilderness 生产：Procedural Base + Authored Override

当前倾向是：大部分普通荒野由程序生成承担规模，重要区域保留人工制作，并且程序结果必须允许人工编辑／覆盖。

理想的未来 World Editor 可先生成 biome、forest、mountain、river、road、grassland、POI、vegetation，再允许开发者修改地形、道路、桥梁、森林、湖泊、建筑、spawn，或覆盖一片区域、放入完整手工 POI / WorldSite。

青石镇、宗门、重要村庄、大型遗迹外部、特殊山谷等可作为 Authored Surface Patch 嵌入程序世界。程序生成负责 Scale，人工 Authoring 负责重要内容与质量。

Authored Patch 与程序世界的道路、河流、Biome Edge、Terrain Edge、城墙等接缝仍是未解问题。可能需要 Road Connector、River Connector、Authored Boundary Hint 或 Seam Generation，但不在此决定具体方案。

## 6. 城市／城镇／宗门室外的三种保留选项

本记录明确保留三种可能，不替制作人选定其一。

### 方案 A：城市属于 Continuous Outdoor Surface

森林 → 农田 → 城门 → 城市 → 城外荒野都处于同一 WorldPosition 空间；进入城市只改变 Region / WorldSite Context，不切换普通室外地图。其连续感最强，Ground/Flight 自然统一，也允许追逐或飞越城墙。代价是 authored city patch、外围程序地形、城墙道路河流接缝、城市 streaming、NPC materialization、Siege presentation 与编辑器需求更复杂。

### 方案 B：城市作为独立 City / WorldSite Map

Continuous Wilderness Surface 通过某种进入方式转入独立 City Local Space。它较易保留现有 WorldSite LocalMap 与复杂城市设计自由，但 Wilderness → City 更可能产生空间断层，飞行、追逐与穿城体验更难自然处理。

### 方案 C：Hybrid

城镇室外属于 Continuous Surface，而房屋 Interior、地下洞穴、地牢、秘境、洞天、小世界、空间裂缝及特殊异空间仍可为独立 Local Space。这看起来是自然的候选方向之一，但尚未决定。

目标并非“游戏绝不能加载另一张地图”，而是普通 Outdoor Geography 不应因为技术分区而表现为房间切换。

## 7. World Event 与 Flight 的未来约束

下一大 Gameplay 候选仍是 World Event / 江湖事件：秘境、拍卖、护送／劫杀、炼药师大会、比武、宗门大会、商队、奇遇、道侣情感事件、渡劫、遗迹、洞府、争宝与世界冲突等。但在世界空间方向继续讨论期间，**不立即实现 World Event**。

无论最终地图形态如何，未来 World Event 不应把 `LocalMapId` 作为世界位置根 authority；应优先使用 `WorldPosition`、`WorldSiteId` 或未来正式 World Location abstraction，以避免未来 Surface 迁移时整体重写。

Flight 同样暂不实现。其未来验收不应只是翅膀 Sprite 或加速，而应包括在不打开 WorldMap 时连续飞越多个战略 Hex、道路、山脉、村镇和 WorldSite 的室外体验。

本轮不继续 Succession；PlayerParty / ActiveCharacter architecture 保持现状。

## 8. 已基本确定

仅记录已有明确共识，未定项不放入本节：

1. 最终目标是尽可能连续的 2D Outdoor Open World；
2. Ground 与未来 Flight 使用同一个 Outdoor 世界；
3. Continuous World 不等于一张巨大文件；
4. 世界应由可 Streaming 的区域块组成；
5. WorldPosition 应继续承担长期重要的世界位置语义；
6. Hex 应长期保留，但更偏战略 / Simulation Partition；
7. Hex 不必等于 Streaming Chunk；
8. 大量 Wilderness 应主要程序生成；
9. 程序地图必须允许人工 override；
10. 重要 WorldSite / POI 应允许手工制作；
11. 现有 LocalMap Content 应尽可能复用，而不是默认全部废弃；
12. 远处世界不需要加载近景 Presentation；
13. 连续世界与远处后台事件可以同时成立；
14. Flight 暂时不做；
15. 当前不进行地图 Runtime 大迁移；
16. 当前首先记录架构方向，继续讨论。

## 9. OPEN QUESTIONS / 尚未决定

### Q1. 城市／城镇／宗门室外属于哪里？

它们是否属于同一个 Continuous Outdoor Surface、作为独立 City Map，或采用 Hybrid？这是下一次讨论的首要问题。

### Q2. 若城市连续，如何接缝与 materialize？

如何处理 authored city patch、外围程序地形、road connector、river connector、city wall、siege presentation、NPC materialization 与 city streaming？

### Q3. 若城市独立，如何避免割裂？

如何避免 Wilderness → City 割裂、Flight 被城门 Portal 阻断、追逐／战斗中断，以及世界被体验为房间集合？

### Q4. 世界生成策略是什么？

开局一次生成整个 Macro World + Surface Data、先生成 Macro World 再按接近时生成 Surface Detail，还是 Hybrid？

### Q5. Global World Scale 如何稳定？

Hex 与连续 WorldPosition 之间最终采用何种全局一致游戏尺度？不要求现实米制，但必须稳定。

### Q6. Surface Streaming Chunk 如何划分？

Chunk 的大小、形状、加载边界是什么？与 Hex 是否完全解耦？预计会解耦，但尚未定案。

### Q7. 程序 Wilderness 如何跨 Chunk 保持自然连续？

road、river、biome、mountain、cliff 如何连续，且可被人工 override？

### Q8. 现有 Wilderness LocalMap 如何转换？

哪些数据能直接成为 Surface Patch，哪些 LocalMap 边缘、Exit 与 placement 需要迁移？

### Q9. 现有 WorldSite LocalMap 如何转换？

青石镇等完整手工地图应整体成为大型 Patch，还是未来拆分？

### Q10. WorldSite footprint 与 Surface playable region 是否严格一致？

战略 footprint 与连续 Surface 上的 town playable region 是必须严格同形，还是 Macro abstraction？

### Q11. 跨 Chunk Navigation 如何连续？

玩家／NPC 在跨 Streaming Chunk 时如何保持连续寻路？

### Q12. 跨 Chunk Combat 如何连续？

Projectile、追击 AI、范围技能、军队战斗跨 Chunk 时如何保持一致？

### Q13. SaveGame 如何保存程序地图与人工覆盖？

是否采用 Seed + Delta、哪些状态必须显式保存、如何与 authored override 合并？均未决定。

### Q14. 远处城市事件需要何种 simulation 粒度？

Siege、Auction、Tournament、World Event 在无人接近时应模拟到何种 Domain 粒度？

### Q15. 连续城市与战略事件如何 materialize 一致？

Continuous City 与 Background Strategic Siege 在玩家接近时如何还原为一致的近景状态？

## 10. 本轮明确不做

- 不修改 Runtime 代码、Gameplay、地图 Content、JSON Schema、Travel、SurfaceExit 或 WorldSite LocalMap；
- 不实现 Streaming、World Event、Flight、程序地图生成、LocalMap 迁移或 World Editor；
- 不决定城市连续／独立／Hybrid，不指定 Unity Tilemap Streaming、Addressables、Chunk 大小、世界起始尺寸、生成算法或 Seed + Delta；
- 不改变 Player identity、PlayerParty 或 Succession 架构；
- 不将本讨论标记为 SEALED、IMPLEMENTED 或已批准实施。

## 11. 下一次讨论建议

优先回答 Q1：城市／城镇／宗门室外到底选 Continuous、独立 City Map 还是 Hybrid。该选择将影响 authored patch、WorldSite、Flight、Streaming、Siege materialization 与迁移边界；在它之前不应开始 World Event 或 Outdoor Runtime 大迁移。
