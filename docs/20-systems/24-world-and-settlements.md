# 世界与据点

> **2026-09-15 Future Direction / Not Implemented：** [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) 已锁定 Final Continuous Surface、World Composer/Fine Editor、WorldMap LOD 与逐步去 Hex 的产品方向。下文现有 Region／LocalMap／格子与旧 WorldGraph 规则须按其历史或 Legacy Compatibility 语境阅读；**MAP-01 未开始，当前 runtime 不因本注记改变。**

> 状态：Continuous Outdoor 与 SiteCore 最终设计已确认；实现部分存在、迁移／核查及制作人验收待完成 | 优先级：P0 | 最后更新：2026-09-12
> 上级：`docs/00-project/00-overview.md`
> 关联：`33` v0.2 §8、ADR-0021、`26`、`27`、`25`、`22`、**[2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)**
> **世界结构（历史实现说明，现为 Legacy Compatibility）：** [113 World Graph + Local Map](../40-process/113-world-graph-local-map-architecture-revision-v0.1.md) 描述的节／路由／按需 LocalMap 模型已被 [ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md)与 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) supersede；113 页已加 Legacy 注记，不再是新 Outdoor 制作依据。
> 下文 §2「Region = 较大连续区域」已被 113 取代：历史 Freeze 段落保留备查，**不要按连续大区实现新内容**。
> **⚠️ 2026-08-24 Pure Hex supersede（Legacy Compatibility）：** 战略空间曾以 **HexWorld + WorldSite.FootprintHexes**（ADR-0025、155）为真源；Multi-Hex Site、TerritoryRegion、Fixed/Dynamic WorldSite 规则见 **[2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)**。**当前仍有 Hex compatibility consumers，但 future product authority 已由 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) supersede。**
> **⚠️ 2026-09-12 当前目标：** [ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md) 的 Continuous Outdoor 已从 Future 入口提升为正式目标；SiteCore 与范围见 [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)。下方旧 World／Region／LocalMap 内容仅作历史实现说明。

## 1. 这个系统解决什么问题

世界是所有内容的容器，也是扩张玩法的棋盘。要能承载从荒村到多城尺度跃迁，又避免 3D 开放世界成本。

<a id="continuous-outdoor"></a>
## 2. 当前世界结构：Continuous Outdoor + 独立空间

- 普通探索、旅行和建设发生在每大陆的 Continuous Outdoor World Surface；村、镇、城、宗门的室外区域不因 Site、Hex 或 Chunk 边界切探索场景。
- 真实户外位置由世界空间身份与连续坐标表示。Streaming Chunk、制作 Patch、Strategic Hex 各司其职，不要求一对一或边界对齐。
- Hex 保留战略叠加和摘要；不得用 Hex terrain、footprint 或格心反推河桥通行、人物位置、Site 精确边界或战场裁切。
- 基础大陆采用“程序初稿 → 人工调整 → Bake → 按 Chunk 加载”；存档保存动态游戏状态，不为每个存档重新随机生成基础大陆。
- 不同大陆、真正 Interior／洞府／地下／独立空间仍可切换；临时独立战场不恢复“每 Hex 一小图”或户外城市房间化。
- 扩大 3×3 显示窗口不等于全局导航覆盖；完整路线与地图生产仍需单独验证。

<a id="worldsite-sitecore"></a>
## 2.1 WorldSite 与 SiteCore

- V1 一个 WorldSite 只有一个 SiteCore。预设议政厅固定存在、不可拆除，可升级和正式接管；玩家势力旗可成为一个可拆／可毁的新 Site 核心。
- 核心等级决定理论行政／建设覆盖；示例尺寸不是硬常量。建筑再按自身地形、占地、碰撞与许可判断能否放置，河流仍可属于辖区。
- 当前 Site 是由真实位置与有效控制解析的上下文。HomeSite、Faction 和当前物理 Site 分离；所有权／范围改变不移动人物，也不重置日程、出生点或命令。

## 20. 历史结构：World → Region → LocalMap（已被当前目标替代）

> 本节保留 Architecture Freeze v0.2／ADR-0021 的历史背景；普通户外目标已由 ADR-0031／0032 替代。Interior 等真正独立空间仍保留。

```text
World
 └── Region       （较大连续区域，例如一座城市区域）
      └── LocalMap （独立加载：山洞／秘境／洞府／遗迹等）
```

### 20.1 World

整个修仙世界。承载 Region 间关系、战略观察、跨 Region 路线（Route）。
**不做**整片大陆完全连续无缝大地图。

规模方向（体验目标，非硬编码屏数）：暂定约 3 块大陆级分区、合计约 30 个城市级 Region。

### 20.2 Region

一个较大的**连续区域**（如「青石城区域」），内部优先保持连续地图体验，可包含：

- 城镇中心、荒村、矿山、森林、农田、妖兽区、灵地、周边道路等

支持：行走、战斗、飞行、路途中遭遇。
尺寸**可变**；荒村及周边可约 3～4 个当前视野；完整城市区域可以更大。
技术上允许 Chunk／流式；体验上连续。

跨 Region 旅行使用 **Route**（进度、危险、遭遇池）；队伍非瞬移。

### 20.3 LocalMap

独立加载地图，由 Region 内入口进入。用于：

- 山洞、秘境、洞府、遗迹、特殊剧情空间

实例状态（已拿宝物、已清敌人、机关、所有权等）**永久保存**。

### 20.4 废弃表述

以下与 v0.2 冲突的描述**作废**：

- 「大陆 → 城市区域 → 格子」作为与四类地图混用的旧主叙事
- 「统一约 10 屏／1.5 屏」作为硬规格

格子仍是 Region／LocalMap 内的最小空间单位（见下节）。

不做 3D 开放世界。缩放分层（近／中／远景）为体验目标。

## 3. 地图格子系统

地图采用**小格子**设计，不是大区域格子。方向类似《了不起的修仙模拟器》的格子粒度。

| 对象 | 占用 |
|---|---|
| 角色 | 约占 1 格 |
| 建筑 | 占多个格子 |

建筑示例（仅作规模感，非定稿）：

| 建筑 | 占地示例 |
|---|---|
| 房屋 | 约 4×5 格 |
| 酒馆 | 约 4×5 格 |
| 大型建筑 | 更多格子 |

每个格子是最小空间单位。地形、资源、灵气、建筑占位都以格子（或格子集合）表达。

### 3.1 地图编辑器

未来用**地图编辑器**编辑格子地图与出口连接，**不直接在 Unity 中手动摆放所有内容**。

编辑器本身是工具链问题；本阶段只锁定“数据驱动的格子地图”这一设计方向。

## 4. 地图数据结构方向

每个城市区域的地图数据至少需要包含以下维度。本阶段只定字段方向，不定数值与存档格式。

### 4.1 地形

例如：平原、森林、山地、河流、火山。

影响：视觉表现、可采集资源、属性环境倾向、通行与战斗地形。

### 4.2 资源

例如：木材、矿石、药材、灵草、妖兽。

资源可附着于格子或格子区域，供采集、狩猎与据点生产。

### 4.3 灵气数据

包括：

- **基础灵气浓度**
- **属性灵气倾向**：火、水、木、金、土、雷、风、毒等

不同地点拥有不同属性倾向。属性与 `2B-attributes-and-affinity.md` 一致，是能力倾向，**不做传统五行相克**。

> **待确定：** 「水」是否保留为独立属性，还是并入冰。最新灵根名单为火、金、土、木、雷、风、冰、毒；本文与环境表暂保留“水”示例并标注本问题。

灵气浓度与属性环境如何影响修炼，见 `25-cultivation-and-breakthrough.md`；多据点汇聚见 `26-territory-management.md`。

### 4.4 建筑数据

包括：房屋、洞府、仓库、聚灵设施、防御设施，以及生产类建筑等。

建筑占多格，拥有功能、容量、归属与状态。

### 4.5 人口数据（凡人群体）

凡人采用**群体模拟**，不按人逐个存档。

例如：一栋房屋容量 500 人，当前 420 人。

详细规则见 `27-characters-and-population.md`。

### 4.6 NPC 数据

关键 NPC **实体化**，包括：商人、村长、重要人物、剧情人物。

可拥有：姓名、性格、关系、任务、库存等。

## 5. WorldGraph 战略层补充（2026-08-22）

> **历史实现范围：** 下表描述旧 WorldGraph／Node／Army 版本，不是当前产品门槛。当前普通角色可按自身 AI／Policy／任务或玩家 Active 移动；FormalArmy 无地图移动、宣战或占领类型特权；Site 接管按唯一 SiteCore 和同源 Encounter。远方角色仍不开放逐人 RTS 控制。

宏观 WorldGraph 战略规则以 [113](../40-process/113-world-graph-local-map-architecture-revision-v0.1.md) 与 [2A 势力、军队、外交与战略占领](2A-factions-armies-diplomacy-and-capture.md) 为准：

| 概念 | 说明 |
|------|------|
| **WorldNode.ownerId** | 节点直接 Owner（语义名 **OwnerFactionId**）；**无** Controller 双层；战争占点成功后直接易主 |
| **Resident Characters** | 旧 Node 驻留状态；“不能跨 Node”已被统一 WorldPosition／移动计划替代 |
| **Garrison Armies** | 驻扎于己方 Node 的 Army；**不**自动解散；仅 Disband 解除 |
| **Army 编组** | 增减成员／换 Leader／解散**仅**能在己方 Node；禁止跨 Faction 混编 |
| **CaptureObjectives[]** | 旧多目标模型；V1 当前只有一个 SiteCore，实际接管才改 Owner |
| **Formation** | 阵法；计入 Node Defense |
| **Army 位置** | 旧 AtNode／OnEdge 模型；跨点必须 Army 已替代 |

> 当前目标见 2K／ADR-0034；旧表仅用于迁移核查。

## 6. 结构：连续区域 + 可占领区块

- 城市区域内部的荒村、矿山、灵地等区块可以探索、占领、建设、劫掠、保护，或交给部属管理。
- **CaptureObjective 占领**（generalize 自控制核心）：详见 `26-territory-management.md` 与 [2A](2A-factions-armies-diplomacy-and-capture.md)；须处于 War 才能军事占点。
- 山脉、河流、危险区域与出口影响通行与行军。
- 飞行等境界能力会改变通行规则（见 `22-realms-and-abilities.md`）。

## 6.1 势力生态

- 宗门、家族、城镇政权、妖族与散修势力争夺人口、资源与灵地。
- 每个可占领区块都有归属、守护力量、产出、人口、灵气浓度与关系网络。
- 占领不是换个颜色：新领主要面对民心、旧势力关系、资源恢复与高阶报复。

## 7. 正魔大战与玩家的位置

世界中可以同时发生**规模远大于玩家当前势力的正魔大战**。

- 游戏前期玩家只是偏僻地区的小势力，**不应立即成为天下大战的核心**。
- 正魔大战为玩家提供发展窗口：大宗门无暇顾及偏远地区。
- 玩家可以选择暂时寄人篱下，借助大宗门发展。
- 大宗门可能派使者检查玩家当前情况，详见 `28-jianghu-relations.md`。

这体现本作的核心特色之一：**江湖关系会影响宏观势力发展**。

## 8. 后期空间网络

悟道修士可在据点建设空间锚点，并将两个锚点连接为空间虫洞，使两地人员极快往来。

- 两端据点都需要控制权、许可或内应。
- 有建设成本、维护资源与通行容量。
- 可集中远方产出、快速调动援军，也可被敌人封锁、破坏或反向入侵。
- 它新增连接关系，不删除原有出口／道路。

具体约束强度**待确定**，详见 `22-realms-and-abilities.md` 的悟道能力。战略空间网络归悟道还是羽化，或两者分层，仍**待确定**。

## 9. 未决问题

- [x] 世界结构：大陆 → 城市区域 → 格子地图。
- [x] 规模目标：3 大陆、每大陆约 10 个城市区域；城市区域约 10 屏，大陆约 100 屏。
- [x] 城市区域是连续地图，内部含城镇中心、荒村、矿山、森林、农田、妖兽区、灵气点。
- [x] 区域四周出口由地图数据决定；未设出口则无连接。
- [x] 采用小格子；角色约 1 格，建筑占多格。
- [x] 地图数据包含地形、资源、灵气、建筑、人口群体、关键 NPC。
- [x] 未来用地图编辑器编辑，不在 Unity 中手摆全部内容。
- [ ] 单格的实际边长／像素尺度，以及“约 10 屏”的精确换算。
- [ ] 自由缩放时，格子与建筑在不同缩放级别如何表现。
- [ ] 三块大陆之间如何往来，是否存在早期无法跨越的限制。
- [ ] 行军是否需要补给与粮草，还是仅计算时间成本。
- [ ] 第一阶段先做几个城市区域／内部区块？建议先做一个起始区域 + 少量可占领区块。
- [ ] 「水」属性在地图灵气数据中的去留（与 2B 同步）。
