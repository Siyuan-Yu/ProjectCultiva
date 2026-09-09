# 连续 2D 开放世界 / World Surface 架构方向

> 状态：**ARCHITECTURE DIRECTION DECIDED / NOT IMPLEMENTED**｜优先级：Future Architecture｜最后更新：2026-09-09
> 正式决策：[ADR-0031 Continuous Outdoor World Surface Architecture](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)
> 关联：`2K`、`2J`、`24`、`41-roadmap`、ADR-0021、ADR-0025、ADR-0026、ADR-0027
> **本页锁定目标架构方向，但不授权 Runtime、Content、Schema、Scene、Prefab、地图、Travel 或存档迁移。**

---

## 0. 结论与边界

本轮已经拍板：普通 Outdoor Geography 的长期目标是 **Continuous Outdoor World Surface**。一个大陆内的荒野、村镇、城市街道与城墙外侧、宗门山门／庭院、道路、农田、森林、山脉、河流等，属于同一个连续的 Outdoor Physical World；城市不因“是城市”而默认切入独立 City Map。

这不是“已经实现”。本页必须与当前 Runtime 严格分开理解：现有 LocalMap、SurfaceExit、WorldSite LocalMap、SpatialMapping、当前 transition / mapping / Travel authority 都继续正常使用；本次更新本身不允许删除、迁移或改写它们。

真正离开当前 Physical World Space 才允许 Space Transition：Interior、Cave、Underground、Dungeon、Secret Realm、Pocket World、洞天、小世界及其他特殊异空间仍可长期使用独立 Space / Portal / LocalMap。不同大陆也是不同的 Continuous Outdoor World Surface；现阶段跨大陆是 `WorldSpace A → transition / teleport → WorldSpace B`，不设计航海或跨海连续飞行。

## 1. Current Implementation（仍是现行实现契约）

- `HexWorld` 是当前唯一世界拓扑；Wilderness 仍为 **1 Hex = 1 logical LocalMap**。
- `WorldSite` 仍是一个 Site identity、一个 LocalMap identity，可拥有 Strategic Hex footprint。
- `CanonicalWorldSurfacePosition`、`WildernessLocalWorldProjection`、`HexFootprintSpatialMapping`、`WorldSiteSpatialMapping`、SurfaceExit 与现有 transition / Travel authority 继续工作。
- Outdoor → Outdoor 仍会经现有 LocalMap／SurfaceExit transition；这不是新架构已经落地的证据。
- `WorldSite LocalMap normalized projection → footprint domain` 仍是当前有效的 mapping；ADR-0027 继续是 Canonical WorldPosition、Context 与 Physical Position 分离的正式基础。

## 2. Target Architecture（已决定、尚未实现）

### 2.1 世界位置与核心空间概念

长期 Outdoor Physical Position 的最高权威为 **`WorldSpaceId + WorldPosition`**：从物理位置查询所属 Strategic Hex、Surface Chunk、WorldSite Physical Region 与 Presentation Context，绝不从“当前哪张 LocalMap”反推世界位置。

| 概念 | 长期职责 |
|---|---|
| **World Surface** | 一个大陆真实连续的 Outdoor Physical World。 |
| **Surface Chunk** | 制作、存储与 Runtime Streaming 的基本单位；不是 Gameplay Boundary。 |
| **Strategic Hex** | WorldMap、Territory、Faction Control、FormalArmy、WorldSite Strategic Footprint、Background Travel / simulation、战略路径与地形摘要。 |

`Surface Chunk ≠ Strategic Hex`；两套 Grid 不要求对齐，绝不恢复 `1 Hex = 1 LocalMap` 或建立 `1 Hex = 1 Streaming Chunk` 的长期绑定。当前 `CanonicalWorldSurfacePosition` 是迁移基础，未来升格为 Outdoor Physical Authority。

### 2.2 Fixed Baked Base World + Dynamic Save State

最终世界是开发者制作并 Bake 的固定世界，不是玩家开档随机生成的大陆。流程为：

```text
Procedural Initial Draft → Global Macro Geography → Manual Editing / Override
→ Bake → Surface Chunks → Runtime Streaming
```

程序生成服务开发工具和规模，人工负责最终质量；Runtime 不重新随机生成基础 Geography。Base World Surface 是 Runtime immutable，包含 terrain、elevation、road、river、water、mountain、cliff、静态建筑／墙／装饰／碰撞等。Save 只保存 Character、Army、Faction、WorldSite owner、Territory、旗帜、门／箱／机关／特殊结构、Quest / Event 等动态 Domain state；不保存地形 tile delta，也不支持挖填、地形形变、改河、修山、runtime 画路或 Minecraft 式世界编辑。

Road、River、Biome macro shape、Mountain range / terrain field 必须先以全局连续定义生成和人工修改，再 Bake 到各 Chunk 的局部表现；开发者不应手工逐 Chunk 对接河流或道路端点。

### 2.3 连续 Gameplay 与分层模拟

Chunk 是 loading / storage / authoring boundary，**不是 Gameplay Boundary**。Player / NPC movement、follow、chase、combat、aggro、projectile、skill、road、river、wall 与 future flight 均可自然跨 Chunk；near-player combat / chase 有保护范围，不能因过边界 dematerialize 或结束。远离后才可降级。

Near Player 使用 Full Simulation（presentation、collider、realtime AI、combat、interaction、surface navigation）；Far 使用 Domain / Background Simulation（Character state、schedule / travel、FormalArmy、WorldSite、strategic battle / siege）。`Dematerialize` 只销毁 presentation / realtime simulation，绝不销毁 Entity；再次靠近必须从 Domain State materialize 正确结果，不补演玩家未见的逐帧历史。

长期对象分三层：**Fixed Surface Content**（无独立持久 identity 的 terrain、road、river、普通树石／装饰、普通静态建筑／墙／collision）随 Chunk streaming；**Stateful Gameplay Object**（door、gate、chest、mechanism、flag、destructible bridge、special structure、ControlCore-like object）有稳定 ID，Base 定义“是什么／在哪里”，Save 定义当前状态；**Domain Entity**（Character、PlayerParty、FormalArmy、Faction、WorldSite、Background Character、future significant World Event）独立于 Chunk 持续存在。原则：**Chunk owns presentation; Domain owns identity and state**；Stable ID 不得以 Chunk ID 为长期 identity 组成部分。

Outdoor Character 的空间真相始终在同一 Continuous World：近处用 precise `WorldPosition`，远处可用 route、progress、origin、destination、start / arrival time、schedule phase，但必须确定性恢复合理 WorldPosition，禁止只存 `CurrentHex` 后随机生成在 Hex 中心。Schedule anchor 同理。FormalArmy 远处可 Strategic / Hex-first；玩家靠近时必须由 Hex route / progress 确定性映射到 Surface。

Navigation 是双层：Near 是连续 Realtime Surface Navigation，理解真实 walkability、barrier、水、elevation connection、cost 与 collision；Far 是 Strategic / Background Travel，使用 Hex、global road、mountain pass、river crossing、terrain cost、WorldSite / route。算法可不同，但必须描述同一个世界。

### 2.4 地形、地点、战略摘要

采用 **Continuous 2D Surface + Discrete Elevation Level + Barrier Edge + Connection + Water Region**，不做连续 3D heightmap / 2.5D physics height。Cliff / Wall 是 barrier；Slope / Stair / MountainPath / Bridge 是 connection；Mountain 不天然等于不可走，Ground Traversal 由实际 barrier 决定。

Continuous Surface 是真实地理权威；Strategic Hex Terrain 是战略摘要。Surface / Macro Geography 可 auto-derive Hex summary，开发者可 override 为最终 strategic metadata（movement cost、road bonus、river crossing、chokepoint 等）。Terrain Tag 不反向强迫整块 Hex 的真实表面只有一种地形。

Territory 仍严格 Hex-based。WorldSite 不再等于“一张地图”，而是有 identity、Strategic Footprint、Physical Region、Gameplay State 的重要地点；footprint 是 Hex 集合的战略范围，不等于 Exact Physical Boundary，不要求城墙沿 Hex、城区填满 footprint 或两者同形。

### 2.5 Flight 与 Future World Event

Flight 是尚未实现的独立 Traversal Mode，仍在同一个 World Surface。Flight 可连续跨 Chunk、Hex、荒野、城市、宗门、山河；普通地面障碍不作为其 navigation barrier。未来禁飞阵法、天幕等可另设 `AerialBarrier`，不预设另一世界空间或复杂 Aerial NavMesh。

World Event 本轮只记录 North Star，**不进入实现**：它是能拥有 WorldSpace + WorldPosition / Region 的真实 Domain Event，可 Near materialize、Far background resolve，后果进入 Character / Faction / WorldSite / Social 等 Domain。倾向由真实世界 state / eligibility 经受控概率创建，生命周期可为 `Planned → Active → Resolved → Aftermath → Expired`，Importance 可分 Major / Standard / Ambient；不围绕玩家随机刷事件。

## 3. 已关闭的讨论问题

以下原讨论问题已由 ADR-0031 关闭：城市／村镇／宗门室外归属；世界为固定 Bake 还是 runtime 随机地形；Base World 与 Save 的边界；World Surface / Chunk / Hex 分工；Chunk 是否 Gameplay Boundary；战略地形与真实地理关系；Territory / WorldSite footprint / physical region 的关系；Outdoor Position 权威；普通 Outdoor SurfaceExit 的长期命运；2D elevation 语义；Flight 单一世界空间约束；分层 materialize / dematerialize 与 Character / Army 的位置连续性；World Event 的长期定位。

## 4. Migration Direction（不授权实施）

`WorldPosition`、`HexFootprintSpatialMapping` 的 World↔Strategic Hex query 思想、WorldSite Physical Region / Strategic Footprint 都长期保留。当前 Wilderness / WorldSite LocalMap 可逐步成为 Surface authored source；迁移可调整边界、真实尺度、道路／河流连接、外围、少量建筑与 city edge，不要求像素级保留。普通 Outdoor 的 SurfaceExit 最终退出主链；Portal / SpaceTransition 长期保留给真正独立的 Space。

## 5. Implementation-time Decisions（仍 Deferred）

- Surface Chunk size / tile dimensions / file format / serialization schema / streaming radius；Unity unit、Hex 精确米数、角色速度、Mount / Flight 倍率。
- Realtime navigation 最终算法、Tilemap / Renderer streaming、collider bake、Addressables、具体 Surface editor 与 multi-chunk UX。
- road / river bake、irregular terrain connector、save 具体字段、dematerialize distance、simulation tick rate。
- 大陆间玩法、Flight gameplay、World Event implementation。

体验尺度方向已经决定但不是米制规格：普通 Hex 步行横穿约 3–5 分钟；主要城市间约 30–35 分钟；每大陆约 6–8 个主要城市；端到端步行目标不超过约 3 小时。Mount / Flight 明显更快，具体倍率 Deferred。

## 6. 非目标

本轮不改 Runtime / C# / JSON Content / Schema / Scene / Prefab / 地图资源 / Save；不新增 WorldSurface、SurfaceChunk、Streaming、Navigation、Flight 或 World Event Runtime；不删除 LocalMap / SurfaceExit；不迁移地图，也不改变任何 Gameplay behavior。
