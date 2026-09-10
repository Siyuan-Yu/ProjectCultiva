# ADR-0031：Continuous Outdoor World Surface Architecture

> 状态：**已采纳 / migration in progress**｜日期：2026-09-09｜最后更新：2026-09-10
> 决策者：制作人  
> 关联：[203](../203-continuous-2d-open-world-world-surface-direction-2026-09-09.md)、[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2J](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)、[ADR-0021](ADR-0021-world-region-localmap.md)、[ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md)、[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)、[ADR-0027](ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)

## Context

当前 Runtime 已有 HexWorld、CanonicalWorldSurfacePosition、Wilderness / WorldSite LocalMap、SurfaceExit 与 SpatialMapping，且这些实现仍是有效现行契约。但普通 Outdoor traversal 仍表现为 LocalMap context 与 transition 的组合，不能满足长期“从荒野、村镇、城市、宗门到山河都在同一物理世界连续移动”的 RPG-First 方向，也无法自然承托 Future Flight、跨区追逐和连续近景／远景模拟。

本 ADR 锁定目标产品架构；不追认任何尚未实现的 Runtime。

## Decision

### 1. Outdoor Physical World

一个大陆内部的 Wilderness、Village、Town、City、Sect outdoor、road、farm、forest、mountain、river、city street、city wall / gate exterior、sect mountain / courtyard 等普通室外地理，属于一个 **Continuous Outdoor World Surface**。城市不因城市身份而使用独立 City Map，也不采用外小内放大的空间作弊。

只有真正离开当前 Physical World Space 才发生 Space Transition。Interior、Cave、Underground、Dungeon、Secret Realm、Pocket World、洞天、小世界和其他特殊异空间可继续独立。每个大陆有独立 Surface；跨大陆当前是 WorldSpace transition，不设计航海或跨海连续飞行。

Outdoor Physical Position 的长期最高权威为 **`WorldSpaceId + WorldPosition`**：

```text
WorldPosition → WorldToStrategicHex / WorldToSurfaceChunk / WorldSite Physical Region Query
```

不从 LocalMap 推断位置。`CanonicalWorldSurfacePosition` 与 Context-vs-Physical separation 是此迁移的正式基础。

### 2. Base World、Chunk 与地理制作

采用 **Fixed Baked Base World + Save-specific Dynamic State**。世界由 `Procedural Initial Draft → global macro geography → developer manual editing / override → bake → Surface Chunks → runtime streaming` 产出；程序生成是 Authoring tool，不是每存档重新生成基础 Geography。Base World immutable，保存 terrain、elevation、水、山、cliff、road、river、静态建筑／墙／环境／碰撞；Save 仅保存 Dynamic Gameplay / Domain State，不保存 terrain tile delta，也不支持任意地形编辑。

Surface Chunk 是 authoring、storage、streaming 的基本单位，**不是 Gameplay Boundary**。Chunk 与 Strategic Hex 不等同、不必对齐；不恢复 `1 Hex = 1 LocalMap` 或建立 `1 Hex = 1 streaming chunk`。Global road、river、biome macro shape、mountain range / terrain field 必须先有全局连续定义、允许人工编辑，后 Bake 为 Chunk 局部表现。

### 3. 战略、地点与地形

Strategic Hex 继续是 WorldMap、Territory、Faction Control、FormalArmy、WorldSite Strategic Footprint、Background Travel / path、Strategic Terrain Summary 和背景模拟的权威。Territory 严格 Hex-based。

Continuous Surface 是真实地理权威；Hex Terrain 是可从 Surface / Macro Geography derive 并允许人工 override 的战略摘要，不能反向规定整块 Hex 真实地形。WorldSite 是有 identity、Strategic Footprint、Physical Region、Gameplay State 的重要 Domain Entity；footprint 是战略范围，**不等于** Exact Physical Boundary，也不等于 Territory。

2D 地形采用 `Discrete Elevation Level + Barrier Edge + Connection + Water Region`：Cliff / Wall 是 barrier，Slope / Stair / MountainPath / Bridge 是 connection；不做连续 3D heightmap / physics height。

### 4. 连续模拟、Navigation 与 Flight

Player / NPC movement、follow、chase、combat、aggro、projectile、skill、road、river、wall 及 Future Flight 可自然跨 Chunk。Near Player 用 Full Simulation；Far 用 Domain / Background Simulation。`Dematerialize` 只销毁 presentation / realtime simulation，不销毁 Entity；玩家参与的 combat / chase 在边界有保护范围，远离后才降级，靠近时从 Domain State materialize。

对象分层固定为：**Fixed Surface Content**（terrain、road、river、普通树石／装饰、普通静态建筑／墙／collision）随 Chunk streaming，且没有独立持久 identity；**Stateful Gameplay Object**（door、gate、chest、mechanism、flag、destructible bridge、special structure、ControlCore-like object）有 stable ID，Base 定义“是什么／在哪里”，Save 定义当前状态；**Domain Entity**（Character、PlayerParty、FormalArmy、Faction、WorldSite、Background Character、future significant World Event）独立于 Chunk 持续存在。**Chunk owns presentation; Domain owns identity and state**；Stable ID 不得使用 Chunk ID 作为长期 identity 的一部分。

Character 的远处 travel 可为 route / progress / schedule 等低精度状态，但必须能确定性恢复合理 WorldPosition，不能只存 Hex 后随机放回中心。FormalArmy 远处可 Hex-first，但接近时也必须确定性映射到 Surface。

Navigation 为同一世界的双层：Near 为 Realtime Surface Navigation，Far 为 Strategic / Background Travel；两层算法可不同，通行语义必须一致。Flight 是 Future 独立 Traversal Mode，使用同一 Surface，可跨 Chunk、Hex、城市、山河；地面 barrier 不自动成为 aerial barrier，未来需要时另建 `AerialBarrier`。

### 5. Future World Event

World Event 不在本轮实现。长期它是有 WorldSpace + WorldPosition / Region 的真实 Domain Event，可 Near materialize、Far background resolve，后果进入 Character / Faction / WorldSite / Social Domain；可采用 `Planned → Active → Resolved → Aftermath → Expired` 生命周期与 Major / Standard / Ambient importance，不围绕玩家随机刷出。

## Current Implementation Boundary

本 ADR **不改变当前已实现契约**：Wilderness 仍为 `1 Hex = 1 logical LocalMap`；WorldSite LocalMap、SurfaceExit、WildernessLocalWorldProjection、HexFootprintSpatialMapping、WorldSiteSpatialMapping、现有 transition / mapping / Travel authority 均继续有效。Outdoor → Outdoor 的 SurfaceExit 仅在未来迁移完成后退出普通 Outdoor 主链；Portal / SpaceTransition 长期保留给真正独立 Space。LocalMap 不直接删除：现有 Wilderness / WorldSite LocalMap 可逐步迁移为 authored Surface source，Interior 等独立 LocalMap 可长期存在。

## Consequences

- Current 与 Target 必须在所有后续文档和实现计划中分开叙述；本决策不是 Runtime migration authorization。
- `WorldPosition` 作为 Outdoor Physical Authority；`HexFootprintSpatialMapping` 的 World↔Hex query 思想、WorldSite Physical Region 与 Strategic Footprint 长期保留。
- 旧 LocalMap authored content 可在迁移时改变边界、尺度、接缝和少量布局，不要求像素级复刻。
- World-scale 体验目标：普通 Hex 步行横穿约 3–5 分钟；主要城市间约 30–35 分钟；每大陆约 6–8 个主要城市；端到端目标不超过约 3 小时。它们不是 Unity unit、米制或速度的硬规格。

## Supersedes / Relationship to Old ADRs

- **部分 SUPERSEDE ADR-0021**：其“普通 Outdoor 跨 Region 必须 Route Transition / 不做整大陆连续”的规则被本 ADR 取代；不删除 Region 作为历史或其他组织概念，且不改写 ADR-0021 历史正文。
- **Clarifies ADR-0025**：Hex 保留为 Strategic / Simulation authority；Continuous WorldPosition 是 Outdoor Physical authority；Hex 不等于 Surface Chunk。
- **Extends ADR-0026**：RPG-First、PlayerParty 与 FormalArmy 军事层不变；本 ADR 给其 Future continuous outdoor physical surface 提供目标空间。
- **Preserves and extends ADR-0027**：Canonical WorldPosition 与 Context-vs-Physical separation 保留；当前 WorldSite LocalMap normalized projection 是 migration bridge，未来会逐步退出普通 Outdoor 主链，而非被判为错误。

## Deferred Implementation Parameters

Surface Chunk 尺寸／tile dimensions／file format／serialization、streaming radius、Unity unit、Hex 精确尺度、角色／Mount／Flight 速度、Realtime Navigation 算法、renderer / Tilemap streaming、collider bake、Addressables、Surface editor 与 multi-chunk UX、road / river bake、irregular connector、Save 字段、dematerialize distance、tick rate、跨大陆玩法、Flight 与 World Event 实现，均在 prototype / implementation-time 决定。

## W1C 验证注记（2026-09-10）

W1C 已人工验收 `SurfaceChunkCoord != HexCoord`、`WorldPosition` 为 Outdoor physical authority、世界坐标 `+X=East/right`、`+Y=North/up`，以及显式 presentation lifetime。Legacy LocalMap 的 `ResolveAuthoritativeWildernessHex` 只服务 legacy migration bridge；Continuous Surface 使用专门的 Hex commit/hysteresis，禁止把 legacy “adjacent derived Hex 永远保持 committed”规则带回主路径。Road 是战略收益，不是 Ground passability；placeholder migration guard 暂拒绝 missing/Water/`IsPassable=false`，未来由真正 Physical Surface Walkability 取代。

## Non-goals

本 ADR 不授权 C#、Runtime WorldSurface / SurfaceChunk、Streaming、Navigation、Flight、World Event、地图迁移、删除 LocalMap / SurfaceExit、JSON / Save schema、Scene、Prefab、地图资源或任何 Gameplay behavior 的修改。
