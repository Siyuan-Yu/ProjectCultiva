# Hex Territory、Multi-Hex WorldSite 与动态山贼系统

> 状态：**设计规则已拍板（2026-08-24）**｜优先级：P0｜最后更新：2026-08-24  
> 上级：`docs/00-project/00-overview.md`  
> 关联：`2A`、`24`、`26`、`28`、`03-glossary`、`ADR-0024`、`ADR-0025`、[`155`](../40-process/155-hex-strategic-worldmap-migration-2026-08-23.md)、[`158`](../40-process/158-hex-world-content-authoring-pipeline-2026-08-23.md)  
> 被引用：`03-glossary.md`、`41-roadmap`  
> **本页是 Pure Hex 战略空间下 Territory / WorldSite Footprint / Dynamic Site 的正式设计真源。**  
> **PresenceHex 已由 [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) 改为 Derived（CanonicalWorldSurfacePosition → WorldToHex）；见 [2K §6](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。本页 Footprint／Anchor 占地规则不推翻。**  
> **本阶段不写实现代码、不改 JSON、不做技术审计。**

> **⚠️ 2026-09-03 · TerritoryRegion V1 已实现并封板（见 [192 TerritoryRegion V1 硬化](../40-process/192-phase2j-territory-region-v1-base-layer-2026-09-03.md)）。**
> 上一条 2026-08-24「本阶段不写实现代码」仅指当时；本节以下设计规则已在 2026-09-03 V1 落地。

## Implementation Status（2026-09-03 · TerritoryRegion V1 implemented）

- **TerritoryRegion V1 implemented**：`TerritoryRegion` / `TerritoryRegionBoard`（hex→region O(1) 索引 + Register overlap 硬校验）/ `TerritoryControlService`（唯一写入口）/ `TerritoryInvariantValidator` / `WorldSiteTerritoryTransferService`（Site+Region 一次易主事务）。
- **`HexCell.ControlFactionId` authoritative**：每 Hex 最终政治控制唯一真源；Runtime 不另建第二套 controller dictionary。
- **Fixed WorldSite radius-1 initial regions authored**：`ch01_hex_world.json` 30 个 Region = 整个 footprint + 1 跳 ring（地图边缘裁剪），已固化进 Content。
- **Runtime 不根据 radius 重算 Territory**：`Region.Hexes[]` 是唯一 membership 真源；没有 RecalculateAllTerritories()。
- **Initial overlap forbidden by current producer decision**：Region 重叠 = Content validation failure（loader/validator fail；Board.Register throw）；不做 distance / tie-break 自动裁决（见 §6.12 SUPERSEDED 提示）。
- **Dynamic WorldSite 无 Territory**：`TerritoryRegionId == ""` → 无 Region、无 Territory 色；Capture 无 Region fallback 只改 Owner。
- **WorldMap Territory tint**：淡 faction tint（强度 0.22）每帧实时 resolve `ControlFactionId`（terrain fill 内混合，0 GameObject per Hex）；易主后自动刷新，无需 invalidate。
- **Snapshot**：只存 `TerritoryRegionControllerSnapshotDto`（RegionId + ControlFactionId，不含 Hexes/Geometry）；Load 经 `TerritoryControlService.SetRegionController` 恢复，Development 下校验 Owner == Region Controller == 每 Hex。
- **Future player-built rule（仅记录，不实现）**：新建 WorldSite 只能取得当前无 Territory 归属的候选 Hex（first claim wins）；已属某 Region 的 Hex 永不因后来建设被抢走；之后不动态重新分界。

---

## Supersede 声明

| 旧概念 / 文档表述 | 本页正式规则 |
|---|---|
| **Node Territory**、**Node Owner** 作为战略领土真源 | **Hex Territory**（`Hex.ControlFactionId`）+ **TerritoryRegion** |
| **Route ownership**、Route-based borders | Pure Hex 相邻 Hex `ControlFactionId` 差异生成边界 |
| **Node-based WorldSite location**、单 Hex Site 假设 | **WorldSite.FootprintHexes[]**；Multi-Hex Site 仍为 **1 SiteId / 1 LocalMapId** |
| **Site = Territory**（Footprint 等于势力版图） | **WorldSite Footprint** 与 **TerritoryRegion** 严格分离 |
| 山贼作为可外交的正式 Territorial Faction | 山贼 = **Non-Diplomatic Hostile Faction**；每寨独立 **Bandit Faction** |
| Owner / Controller 双层主权 | **Site Owner = Region Controller**；每 Hex 仅 **0 或 1** 个 `ControlFactionId` |
| `TerritoryRadius` Runtime 动态重算 | **TerritoryRadius** 仅用于 **初始内容生成**；Runtime 读固化 `Region.Hexes[]` |

> **Node / Route** 在战略空间层已被 Pure Hex supersede（见 ADR-0025、155）。本文 **不** 重建 Node Territory 或 Route ownership 语义。  
> 与 [2A](2A-factions-armies-diplomacy-and-capture.md) 冲突时，**Territory / WorldSite / Bandit 专题以本文为准**；外交 / Army / Capture 流程仍见 2A，本文补充 Hex-native 空间语义。

---

## 0. 战略空间真源：Pure Hex

正式战略世界模型 = **Pure Hex**。

| 概念 | 职责 |
|---|---|
| **HexCoord** | 六边形格坐标 |
| **HexCell** | 单格地形与可达性等 |
| **HexWorld** | 整张战略 Hex 地图 |
| **WorldSite** | 战略地点实体（Fixed 或 Dynamic） |
| **FormalArmy.CurrentHex** | 军队当前所在 Hex |

**Node、Route 不再作为正式战略空间真源。** 历史文档中的 WorldNode / ownerId 仅作 Legacy 或内容迁移参考；Runtime 领土与 Site 判断必须以 Hex-native 规则为准。

---

## 1. 三个核心概念必须彻底分离

**Faction**、**WorldSite**、**Hex Territory** 是三个不同概念，**绝对不能绑定成一个概念**。

### 1.1 Faction

回答：**「这群 Character / Army 属于谁？」**

- 统一使用 **FactionId**（Character、Army、外交、战争共用）
- 第一版分两种政治语义：**Territorial Faction** 与 **Bandit Faction**（见 §2）

### 1.2 WorldSite

回答：**「这个地方是什么？」**

例如：青石荒村、青云路、城镇、宗门、矿山、关隘、动态山贼寨。

- 描述地点身份、Footprint、LocalMap、Capture 目标等
- **WorldSite Owner** 与 **Hex Territory Controller** 在不同 Site 类型中可能具有不同语义（见对照表 §12）

### 1.3 Hex Territory

回答：**「这个 Hex 当前由哪个正式政治势力控制？」**

- 第一版每 Hex 仅 **ControlFactionId = 某 Territorial Faction** 或 **None**
- 不做争议领土、双重主权、控制百分比

---

## 2. Faction 第一版：两种政治语义

### 2.1 A. 正式政治 / 领土势力（Territorial Faction）

例如：宗门、王朝、城邦、正式玩家势力、其他正常战略 Faction。

| 能力 | 支持 |
|---|---|
| 拥有 Hex Territory | ✓ |
| 拥有 Fixed WorldSite | ✓ |
| 正式 Diplomacy（Opinion / Alliance / Vassalage 等） | ✓ |
| MapColor | ✓ |
| War / Alliance / Vassalage | ✓ |
| Capture Fixed WorldSite | ✓ |

### 2.2 B. 动态山贼 Faction（Non-Diplomatic Hostile Faction）

山贼仍使用统一 **FactionId**，但属于 **Non-Diplomatic Hostile Faction**。

**硬规则：**

- 没有外交、没有 Opinion 外交玩法
- 没有 Alliance、Vassalage、Non-Aggression Pact、Military Access
- 不能议和、不能正常谈判
- 对 **所有其他 Faction** 永远敌对
- **不参与** 正常 Territory Color；**不拥有** 正式 Hex Territory
- **不能** 正式 Capture Fixed WorldSite

**禁止** 创建 `BanditGroupId` 作为另一套敌我身份系统。Character / Army 仍统一引用 **FactionId**。

### 2.3 不同山贼寨之间的关系

- **每一个动态山贼寨** 属于 **自己的独立动态 Bandit Faction**
- 例如：**黑风寨** 与 **飞鹰寨** 不是同一个山贼帝国；**互相也视为敌对**
- 同一寨子产生的 Character / Army 共享同一 Bandit Faction → 内部不会互相攻击
- 不同寨子 **FactionId 不同**

---

## 3. Fixed WorldSite 与 Dynamic WorldSite

必须明确存在两种 WorldSite 来源。

### 3.1 A. Fixed WorldSite

例如：青石荒村、青云路、城市、宗门、矿山、关隘、渡口、其他世界初始地点。

| 特点 | 说明 |
|---|---|
| 来源 | 正式 World Content / World JSON |
| 位置 | 开局固定；SiteId 固定 |
| 生命周期 | 不会因一次战斗消失；不会因易主被删除 |
| Capture | 只改变 Owner / Territory 等 **状态**；地点本身永远继续存在 |

例如：青石荒村被 Faction A → Player → Faction B 反复占领，它仍然始终是 **青石荒村**。

### 3.2 B. Dynamic WorldSite（第一版主要用于山贼寨）

| 特点 | 说明 |
|---|---|
| 来源 | Runtime 动态生成 |
| 位置 | 初始大地图 JSON 不固定实例位置 |
| 生命周期 | 可 **永久被摧毁**；Instance 被摧毁后删除 |
| 再生 | 以后可在其他合法 Hex 生成 **新的 Dynamic WorldSite Instance** |

概念上类似 Civilization 6 Barbarian Camp。

---

## 4. 动态山贼寨（Dynamic Bandit Camp）

### 4.1 Footprint：永远只占 1 Hex

**硬规则：** Bandit Camp **Footprint = exactly 1 Hex**。

- 不考虑 2 / 4 / 6 Hex 大型山贼城市
- 山贼规模变大通过 **更多 Character、更多 Army、更强营地内容** 表达，**不** 通过扩大战略 Footprint

### 4.2 生成位置

山贼寨 **只能** 生成在 **无主区域**。

**核心条件：** `Hex.ControlFactionId == None`

**同时至少满足：**

- Hex 可到达、可驻扎
- 不是不可用地形
- 当前 Hex **没有任何 Fixed WorldSite**
- 当前 Hex **没有其他 Dynamic WorldSite**
- **不与已有 WorldSite Footprint 重叠**

**绝对不能** 生成在：青石荒村、青云路、城市、矿山、宗门、固定道路地点、其他已有 WorldSite 所在 Hex。

### 4.3 正式势力领土内不生成新山贼寨

若 `Hex.ControlFactionId != None` → **不能** 成为新的 Bandit Camp Spawn Candidate。

第一版 **不做** 正式势力境内随机刷匪窝。正式 Territory 当前被视为已有基础政治秩序。  
（以后「治安崩溃 / 境内匪患」另做高级系统。）

### 4.4 边境外无主地允许生成

某 Hex 本身是无主地，**即使紧邻** 正式 Faction Territory，仍然允许作为山贼寨候选。  
第一版 **不要求** 必须距离势力边境 N 格。

### 4.5 已有山贼寨后来被领土覆盖

例：黑风寨先生成在无主地；后来玩家占领固定 WorldSite，TerritoryRegion 扩展后，黑风寨所在 Hex 变成玩家 Territory。

**正式决定：**

- 黑风寨 **不会自动消失** → 继续作为 **历史遗留匪患** 存在
- 从这一刻起，这片正式 Territory 内 **不会再生成新的山贼寨**
- Spawn restriction **只限制新生成**，**不负责** 自动删除既存 Dynamic Site

### 4.6 山贼寨被摧毁

摧毁山贼寨 → 删除 **Dynamic WorldSite Instance**；该 Hex 恢复成普通 Hex。

- 若其 Territory 为 None → 继续是无主地
- **不要** `DestroyBanditCamp()` → `DeleteAllBanditCharacters`
- 真实 Character 仍按真实战斗结果处理（Dead / Downed / Escaped / Alive）

### 4.7 山贼 Army 可以离开寨子

山贼寨只是 **来源 / 根据地**。其 Army 可以离开寨子，在世界中游荡、追击、攻击、移动。

若寨子被拆而某支 Bandit Army 仍然活着 → **不会凭空消失**，成为 **无大本营的流窜山贼 Army**。

### 4.8 动态山贼 Faction 生命周期

Bandit Camp 被摧毁 **≠** 立刻删除 Bandit Faction。

只要仍存在 Living Character、Army、Residual Character 或其他真实引用 → 该 Bandit Faction **继续存在**。

只有当 **Camp = 0、Living Character = 0、Army = 0、Residual = 0**，且没有任何其他有效引用 → 才允许彻底清除该动态 Faction Runtime。

### 4.9 山贼不能正式 Capture 固定 WorldSite

第一版 Bandit Faction **不能** 执行正式 Fixed WorldSite Capture。

- 不能占领青石荒村然后获得青石荒村 TerritoryRegion
- 未来可有 Raid（抢资源、杀居民、烧建筑、袭击）→ **不是本轮系统内容**
- 第一版 **不要** 让 Bandit 成为正式政治 Territory Owner

### 4.10 生成节奏与最小距离

存在以下 **配置概念**（本轮不锁最终数值）：

| 配置 | 说明 |
|---|---|
| **MaxActiveBanditCamps** | 世界上山贼寨最大同时存在数量 |
| **SpawnAttemptInterval / RespawnCooldown** | 按周期 / 延迟尝试生成；**不** 采用「拆一个立刻补一个」 |
| **MinBanditCampDistance** | 新山贼寨与已有山贼寨至少保持 Hex distance ≥ N |

**Prototype 推荐：** `MinBanditCampDistance = 8`（Data / Config driven，**不要硬编码**）。  
具体边界包含方式实现时按项目统一 Hex distance 语义。

### 4.11 LocalMap Template

- 不同山贼寨实例 **不需要** 一寨一个 LocalMap JSON
- 同类型山贼寨可共享一个 **LocalMap Definition / Template**（例如 `bandit_camp_basic`）
- 黑风寨（SiteInstance A）与飞鹰寨（SiteInstance B）可共用 `bandit_camp_basic`
- 但 **SiteInstanceId、FactionId、Character、Army、LifeState、Battle result、Camp destroyed state** 完全独立
- 以后可增加普通山寨、山洞匪窝、大型山寨等不同 Template

---

## 5. Multi-Hex WorldSite

### 5.1 Fixed WorldSite 可占多个 Hex

固定 WorldSite 允许 1 / 4 / 6 / 8 / 9 Hex 或其他合理数量。

- 小地点通常 1 Hex
- 较大城镇 / 宗门可多个 Hex
- 多 Hex 只是战略视觉上「多个普通 Hex 拼成了一个战略意义上的大格子」

### 5.2 永远仍然只有一个地点

例如：一座城占 4 Hex → **绝对不代表 4 个 WorldSite**。

正式：**1 WorldSiteId、1 OwnerFactionId、1 LocalMapId、1 Capture state、1 TerritoryRegion、1 地点身份**。  
`FootprintHexes` 只是该 Site 在 WorldMap 的战略占地。

### 5.3 永远只对应一张 LocalMap

例如：青云城 Footprint = H1, H2, H3, H4 → 仍 **WorldSiteId = QingyunCity、LocalMapId = QingyunCityLocalMap**。  
四个 Hex **全部进入同一张 LocalMap**。**禁止** 一个 Footprint Hex 对应一张 LocalMap。

### 5.4 进入规则

若 `Army.CurrentHex ∈ WorldSite.FootprintHexes` → Army 被视为 **physically at this WorldSite**。

- 我方 Army 位于 Footprint 中 **任意一个 Hex** 都可以：RightClick →【进入 XXX】→ 进入 **同一张 LocalMap**
- 判断必须是：**Site.Footprint.Contains(Army.CurrentHex)**
- **禁止** 写成 `Army.CurrentHex == Site.AnchorHex`

### 5.5 不同 Footprint Hex 进入第一版完全一样

从 H1 进入与从 H4 进入第一版 **完全相同**（同一 LocalMap、Population、Site State、Capture State）。  
第一版 **不做** 东门 / 西门不同 SpawnPoint。  
以后若 EntryHex 只影响 LocalMap 出生点，仍然 **不会** 产生不同 LocalMap。

> **ADR-0027 supersede：** 5D-B2a 起，进入**目标 Site** 时按真实来向选择 footprint 入口 Hex（ingress 位置随方向，不再无条件 Anchor）。仍不产生不同 LocalMap 实例；EntryHex 影响 ingress 落点与出生位置。

### 5.6 AnchorHex 的正式职责

Multi-Hex WorldSite 仍可有 **AnchorHex**，主要用于：

- Site 图标中心、DisplayName 放置
- 编辑器定位、摄像机 Focus、默认视觉中心

**AnchorHex 不是**「Army／Character 是否在这个 Site」的唯一判断条件。  
**禁止** 用 AnchorHex 作为 PlayerParty AtSite 时的 World Position（ADR-0027 #5）；仅保留 Site 图标／标签／编辑器参考点／默认镜头焦点职责。

### 5.6.1 PresenceHex（2026-08-25，见 2K）

Character 位于该 Site 的 **LocalMap** 时，HexWorld 层统一视为位于固定 **PresenceHex**（必须 ∈ Footprint；可与 Anchor 相同或不同）。  
Runtime **不**根据 LocalMap 内坐标动态归属 A/B/C/D。Authoring／Editor 编辑 Deferred。产品真源：[2K §6](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。

> **ADR-0027 SUPERSEDED：** PresenceHex 不再是固定 Authoring 值；改为 **DerivedPresenceHex**（`LocalPosition → WorldSiteSpatialMapping → CanonicalWorldSurfacePosition → WorldToHex`），仅查询/cache。本条保留历史描述。

### 5.7 Footprint 必须显式保存

正式内容数据保存 **WorldSite.FootprintHexes[]**（明确 HexCoord 列表），**而不是** `SiteSize = 4` 然后 Runtime 自动猜形状。

- 工具可提供 4 / 6 / 9 格模板帮助生成
- 最终 JSON / Content **必须固化** 明确 HexCoord 列表
- Runtime **只读取** 最终 Footprint

### 5.8 Footprint 必须连通

Fixed Multi-Hex Site 的 `FootprintHexes` **必须** 是一片连续 Hex。  
不允许同一城市由几块互不相连 Hex 组成。Editor / validation 未来应对此提供 Warning / Error。

### 5.9 不同 WorldSite Footprint 绝对禁止重叠

**永久硬规则：** 一个 Hex **最多属于一个** WorldSite Footprint。  
不允许青云城与青石关共享同一 Hex，否则 Site identity、LocalMap entry、Population、Capture 全部产生歧义。

### 5.10 Footprint 不阻挡普通移动

大型城市占 6 Hex **并不意味着** Army 不能经过这些 Hex。Footprint 只表示战略地点范围；Army 仍可进入、驻留、穿过。

未来城墙、城门、围城、关隘封锁影响移动，应由 **Passability / Siege / Gate** 等系统实现。**不要** 让 WorldSite Footprint 自己承担阻挡职责。

> **World Travel 路由例外（5D-A 起，ADR-0027 #8）：** 在 PlayerParty / FormalArmy 的**世界旅行寻路路由**层，所有**非目标** WorldSite footprint 默认 **blocked**（不可当普通道路穿过）；目标 Site 可到达。若 A→B 因某 Site 阻断，由 **Dynamic MandatoryTransitSite**（反事实 permeability probe，逐 Site 临时移除 footprint 验证 A→B 连通且 ProbePath 真实经过该 Site）动态识别该 Site 为本次路线的必经点 —— 必经点是路径中的**动态关系**，不是 Site 固有属性。本条「可穿过」对 Army 战略存在语义（驻留／进入／穿过 Site 归属）保留，不覆盖旅行路由 blocked。

### 5.11 同一 Multi-Hex Site 内的 Army

只要 `Army.CurrentHex ∈ Site.Footprint` → 战略语义上 Army 就属于「当前位于这个 WorldSite」。

例：青云城 6 Hex，Army A 在 H1、Army B 在 H5、Army C 在 H6 → 三支 Army 都属于「当前驻于青云城」。  
进入 LocalMap 时都属于该 Site 的真实 Population 候选；攻击 / 防守 Site 时也都属于该地点的战略守军来源。  
具体战斗一次如何进场以后由 Encounter / Reinforcement 规则决定。

### 5.12 多 Hex Site 名称 / 图标只显示一次

WorldMap **不要** 在 6 格每格都显示「青云城」。只在 **AnchorHex** 或 Site 视觉中心附近显示一次 DisplayName / Main Icon。Footprint 通过整体底色 / outline 表现。

---

## 6. Hex Territory 与 TerritoryRegion

### 6.1 唯一政治控制状态

第一版每个 Hex 政治控制只有：

- **ControlFactionId = 某正式 Territorial Faction**，或
- **ControlFactionId = None**

不做：争议领土、双重主权、控制百分比、Influence 数值、50% A / 50% B、法理 Owner / 实际 Controller 双层。

一个 Hex 永远有 **0 或 1** 个正式政治控制者。

### 6.2 Territory 与 WorldSite Footprint 严格分离

| 概念 | 回答的问题 |
|---|---|
| **WorldSite Footprint** | 这个地点本体在战略地图上有多大？ |
| **Territory（TerritoryRegion）** | 这个 Faction 当前政治上控制哪些 Hex？ |

例：青云城 WorldSite Footprint = 6 Hex，但青云城辖区 TerritoryRegion 可能 = 50～100 Hex。  
**绝对不要** `WorldSite.Footprint == Faction Territory`。

### 6.3 TerritoryRegion 正式存在

第一版采用 **TerritoryRegion**，用途：

- 地图内容组织、WorldSite 辖区
- 批量 Territory transfer、Capture 后整块易主
- Editor / Content generation

**TerritoryRegion 不是另一套政治控制真源。** 最终每个 Hex 仍有明确 **ControlFactionId**（通常与所属 Region 的 Controller 一致）。

### 6.4 固定 WorldSite 对应自己的辖区 Region

第一版初始 Fixed WorldSite **原则上都拥有自己的 TerritoryRegion**（青石荒村、青云路、矿山、城池、宗门等分别拥有自己的 Region）。

即使 Region 初始 Owner = None 也可以存在（无主废村、无人矿山等）。

### 6.5 无主 Fixed WorldSite

Fixed WorldSite 可以 **OwnerFactionId = None**；对应 TerritoryRegion **ControlFactionId = None**。  
以后玩家第一次合法占领 Site → Site Owner → Player，Region Controller → Player。

### 6.6 没有 WorldSite 支撑的地方不产生正式势力领土

**制作人明确决定：** 第一版 Territory **主要来自** Fixed WorldSite 对应的 TerritoryRegion。

没有绑定 Fixed WorldSite 的普通荒野 → **ControlFactionId = None**。Army 走过去 **不会自动 Claim**。  
（以后建城、建立据点、开拓、殖民另做扩张系统。）

### 6.7 TerritoryRadius（仅内容生成参数）

每个 Fixed WorldSite 拥有一个用于初始 Region 生成的 **TerritoryRadius**（或项目风格下等价配置）。

**Prototype 推荐：**

- 小地点：radius = 1
- 较大城镇 / 宗门：radius = 2

**不要写死** Village 永远 1、City 永远 2。Site Type 可提供默认值，但具体 Site 配置可覆盖。

### 6.8 TerritoryRadius 从整个 Footprint 向外计算

- 若 WorldSite 只有 1 Hex，radius 1 = Site Hex + 周围一圈
- 若 WorldSite 有 4 Hex，radius 1 = 从 **整个 4 Hex Footprint** 向外扩一圈
- **绝对不是** 只从 AnchorHex 计算 radius → 大型 Site 天然拥有更大的辖区面积

### 6.9 TerritoryRadius 只用于初始内容生成

**硬规则：**

```text
WorldSite.FootprintHexes + TerritoryRadius
  → Generate initial TerritoryRegion.Hexes[]
  → 固化到 World Content / JSON
```

游戏 Runtime **只读取** 最终 `Region.Hexes[]`。**Runtime 不根据 radius 动态重算辖区。**  
城市升级、战争 **不会** 重新跑 Radius 算法。

### 6.10 初始 Territory 自动生成

制作人不希望第一版手工逐 Hex 刷所有势力辖区。第一版根据：

- 现有 Fixed WorldSite
- 现有 Faction Owner
- WorldSite Footprint
- TerritoryRadius

**自动生成** 一版初始 TerritoryRegion，生成结果再固化为明确 `Region.Hexes[]`。  
以后编辑器可提供手工调整 / Brush，但第一版初始版图 **优先自动生成**。

### 6.11 初始势力领土不要太大

生成第一版 Territory 时 **不要** 让现有势力迅速瓜分整张地图。每个势力初始控制范围应 **比较克制**，地图需保留 **大量无主 Hex**（探索空间、山贼生成空间、后续扩张空间、战略缓冲区、世界荒野感）。

Prototype 推荐：小 Site radius 1、大 Site radius 2 作为初版范围。

### 6.12 不同 Faction Region 候选重叠

> **⚠️ SUPERSEDED 2026-09-03（制作人决定）：** Initial authored Territory may not overlap；Overlap is content validation failure。
> 若两个 initial Region 包含同一 Hex → Content error（loader/validator fail、Board.Register throw），
> 由设计调整 Site 位置 / footprint；**不做**「距离最近 / SiteId tie-break / 谁先生成谁拿」自动裁决。
> 本节与 §6.13 / §6.17 的 distance-competition / tie-break 描述仅保留为历史；Runtime 只读固化 Region.Hexes[]（§6.14 不变）。

若两个不同 Faction 的 Site 初始 TerritoryRadius 覆盖同一个普通 Hex：

- 该 Hex 归 **距离各自 WorldSite.Footprint 最近** 的一方（使用正式 Hex distance）
- **不是** 谁先生成谁拿

### 6.13 距离相同：确定性 Tie-break

若一个 Hex 到两个竞争 Site Footprint 距离完全一致 → 使用 **确定性 Tie-break rule**（SiteId 排序、Priority 或其他确定性方式，实现时决定）。  
要求：同一份世界数据每次生成结果一致；**不能 Random**。

### 6.14 生成以后固化

无论初始 Radius 还是重叠竞争，**只服务初始地图生成**。最终 `TerritoryRegion.Hexes[]` 必须明确保存；Runtime **不重复竞争**。

### 6.15 同一 Faction 相邻 Region 不 Merge

例：青云城 Region A 与青石镇 Region B 都属于青云宗，即使两块战略地图上相邻甚至完全连成一大片 → Domain 仍然 **Region A、Region B 两个独立 Region**。**不要** 自动 Merge 成一个 Region。

### 6.16 同 Faction Region 视觉无缝

虽然 RegionId 不同，但 ControlFactionId 相同 → WorldMap 应看起来是一整片连续的势力领土。Region A 与 Region B 之间第一版 **不画政治边境线**。玩家看到的是「一个国家」，Domain 仍知道不同辖区属于不同核心 Site。

### 6.17 WorldSite Footprint 在 Region 生成中优先级最高

一个 Site 的 Territory 自动生成 **不能吃掉另一个 WorldSite 的 Footprint**。

规则：先确定所有 `WorldSite.FootprintHexes` → 这些 Hex **不可被** 其他 Site Region 通过 Radius 覆盖 → 普通 Territory Hex 再参与 distance competition。

### 6.18 Site Owner 与 Region Controller 永远一致

**永久硬规则：** 若 `FixedWorldSite.OwnerFactionId = F` → 其绑定 `TerritoryRegion.ControlFactionId = F`。

**不做** 城市 Owner = A、乡下 Controller = B；**不做** 法理主权 / 实际控制双层。

### 6.19 没有核心 Site 的 Region 第一版不可 Capture

若未来存在纯地图组织 Region 但没有 Primary WorldSite → 第一版 **不提供** 直接 Capture Region（当前 Capture 媒介是 WorldSite）。  
未来建城、建据点、特殊战略目标可让这种区域获得新的 PrimarySite。

### 6.20 不做 Army 走过无主地自动 Claim

第一版 FormalArmy 进入无主 Hex **不会** 自动把 Hex 变成自己的颜色，也 **不提供**「宣称此格」按钮。Territory 扩张当前主要来自 **Fixed WorldSite Capture → Region Transfer**。

---

## 7. Capture 固定 WorldSite

### 7.1 整个地点一次 Capture

Fixed WorldSite 是 **整个地点一次 Capture**；Multi-Hex Site **不是** 逐 Hex 攻占。

例：青云城占 6 Hex → 攻击其中任意 Footprint Hex，目标仍是 **同一个 WorldSite** → 进入 **同一 LocalMap** → 完成 **同一套 CaptureObjective**。

### 7.2 Capture Success 后 Region 整块易主

当 Fixed WorldSite Capture Success：

1. `WorldSite.OwnerFactionId` → AttackerFaction
2. 该 Site 所有 Footprint Hex → AttackerFaction（ControlFactionId）
3. 绑定 **TerritoryRegion 全部 Hex** → AttackerFaction
4. 地图 Territory Color **立即刷新**

**不是** 只改变 Site icon；**不是** 逐格占领 Region。

Cross-ref：军事占点前提（War 等）仍见 [2A §29](2A-factions-armies-diplomacy-and-capture.md)。

---

## 8. 领土与外交语义

### 8.1 和平状态下可以非法进入他国领土

若 Faction A 与 Faction B 当前和平，且 A 没有 Military Access → A 的 Army **仍然可以** 进入 B Territory（**不是** 硬性禁止寻路），但属于 **Unauthorized / Trespassing** 非法军事入境。

第一版至少保留此外交语义。以后可有 Warning、Opinion、驱逐要求、AI Reaction、战争风险等，但 **进入领土本身不会自动宣战**。

### 8.2 Territory 不决定敌我关系

**硬规则：** Faction Territory 只回答「谁控制这里？」敌我合法性继续由 **Diplomacy、War State、Faction Relation** 决定。

例：盟军 Army 位于我的 Territory → **不会** 因为不是 Owner 就自动变敌人。

### 8.3 山贼是外交例外

Bandit Faction **不走** 正常外交 Query；永远敌对。UI 未来 **不应该** 对山贼出现完整外交菜单。山贼信息界面以后可显示威胁、寨子、Army、赏金等，但 **不是 Diplomacy**。

---

## 9. Territory Visualization

### 9.1 正式 Faction MapColor

所有会拥有正式 Territory 的 Faction **显式配置 MapColor**。**不能** 根据 FactionId hash 作为正式颜色来源。MapColor 长期属于该 Faction 身份；即使 Faction 暂时失去全部领土，颜色仍然保留（未来复国仍用同一 MapColor）。

### 9.2 Bandit 不参与 Territory MapColor

动态山贼 Faction **不需要** 正式 Territory MapColor。可在 Army、Site icon、危险提示使用统一敌对视觉，但 **不会** 在 Territory Overlay 产生自己的国家色块。

### 9.3 无主 Hex 的 Territory 视觉

`ControlFactionId = None` → **不添加** Faction Territory Overlay，直接显示原始 Terrain。玩家可直观看到：有淡色覆盖 = 正式政治控制区；无覆盖 = 无主荒野。

### 9.4 正常地图始终显示淡 Territory Tint

第一版 **不要求** 玩家进入 Political Map Mode 才能看领土。正常 WorldMap **一直显示** 低强度半透明 Faction Territory Tint，同时 **保留地形可读性**。不要把 Terrain 完全涂成纯色政治地图。

### 9.5 视觉强度只是 Presentation

**不是** TerritoryControlStrength；没有 20% / 80% 控制。只是 Renderer：普通 Territory 较淡；WorldSite Footprint 可稍微更明显。**数据层仍然只有 ControlFactionId**。

### 9.6 WorldSite Footprint 的视觉强调

正式 Site 占多个 Hex 时，Footprint 可以比普通 Territory 略明显（稍高 Overlay opacity、轻微边缘、Site footprint outline），让玩家分辨「这一大片都是青云宗领土」与「这 6 格才是青云城本体」。**不要** 变成完全不同的实心色块。

### 9.7 Faction Border

国境线根据相邻 Hex `ControlFactionId` **动态生成**：

| 相邻关系 | 边界样式 |
|---|---|
| Faction A ↔ Faction B | 明显边界 |
| Faction A ↔ None | 稍弱边界 |
| Faction A Region 1 ↔ Faction A Region 2 | **不画** 政治边界（视觉无缝） |

### 9.8 不要存 Border Polygon

第一版 **不需要** 手工保存 FactionBorderPolygon。Pure Hex 直接根据相邻 Hex Controller 是否不同生成边界。

### 9.9 Political Map Mode 暂缓

未来可做 Political Map Mode（Territory Color 更明显、Terrain 更淡）。**第一版不需要**；当前只做普通 WorldMap 的淡 Territory Tint。

---

## 10. 初始 Territory 数据来源

工程以前应已生成过一版 Faction 及对应区域。以后实现时 **优先审计** 当前已有 Faction、WorldSite Owner、旧 Territory / Ownership 相关数据；若存在可复用正式内容，用于生成第一版新 Hex Territory。

**但最终正式结果必须 Pure Hex-native。不要恢复 Node ownership 作为 Runtime 真源。**

---

## 11. 对照表

### 11.1 Fixed WorldSite vs Dynamic Bandit Camp

| | Fixed WorldSite | Dynamic Bandit Camp |
|---|---|---|
| 来源 | World Content JSON | Runtime Spawn |
| 位置 | 固定 | 动态 |
| Footprint | 1～N Hex | **永远 1 Hex** |
| 是否永久存在 | YES | NO |
| 是否可永久摧毁 | NO | YES |
| Capture 后 | 改 Owner | 不适用 |
| 是否拥有 TerritoryRegion | YES | **NO** |
| 是否产生 Territory Color | 根据正式 Owner | **NO** |
| LocalMap | Site 自己的 Map | 可共用 Template |
| 是否可换 Owner | YES | 不走正式 Capture |
| 是否可被删除 | 不因战斗删除 | 可以 |

### 11.2 Territorial Faction vs Bandit Faction

| | Territorial Faction | Bandit Faction |
|---|---|---|
| FactionId | YES | YES |
| Character / Army | YES | YES |
| Diplomacy | YES | **NO** |
| 永久敌对所有其他势力 | NO | **YES** |
| Territory | YES | **NO** |
| MapColor | YES | **NO** Territory Color |
| Alliance | YES | **NO** |
| Vassalage | YES | **NO** |
| Military Access | YES | **NO** |
| Capture Fixed Site | YES | **NO** |
| Dynamic Camp | 通常 NO | **YES** |

---

## 12. 配置字段（概念层）

本轮只记录设计需求；**不规定** 最终 C# 类名或 JSON schema。

### 12.1 Fixed WorldSite

- SiteId
- OwnerFactionId
- AnchorHex
- FootprintHexes[]
- LocalMapId
- TerritoryRegionId
- TerritoryRadius（内容生成参数）

### 12.2 TerritoryRegion

- RegionId
- PrimaryWorldSiteId
- Hexes[]
- ControlFactionId

### 12.3 Territorial Faction

- FactionId
- MapColor
- 是否允许 Territory / Diplomacy 的类型语义

### 12.4 Dynamic Bandit Camp Instance

- DynamicSiteInstanceId
- Hex
- BanditFactionId
- LocalMapTemplateId
- 当前存在状态

### 12.5 Bandit Spawn Config

- MaxActiveBanditCamps
- SpawnAttemptInterval / Cooldown
- MinBanditCampDistance（Prototype = 8）

具体命名：实现前技术审计后决定。

---

## 13. DEFER（本轮不设计）

- Territory Influence、Contested Territory、双重主权、Occupation percentage
- Territory 自动动态扩张、Army 随地 Claim、建城 / 新据点扩张
- Political Map Mode、城门 EntryHex 差异、Siege Wall strategic blocking
- Bandit Raid 具体玩法、山贼境内治安生成
- Bandit Spawn 具体周期、MaxActiveBanditCamps 最终数值、山贼寨更多模板
- AI Territory strategy、AI Bandit behaviour
- 非法入境外交惩罚公式
- Territory economy、Supply、边境税、Territory movement modifier

---

## 14. Hard Invariants

1. 一个 Hex **最多属于一个** WorldSite Footprint。
2. Fixed WorldSite Footprint **必须连通**。
3. Multi-Hex WorldSite **永远只有一个** SiteId。
4. Multi-Hex WorldSite **永远只有一个** LocalMap identity。
5. Army 位于 **任意 Footprint Hex**，都算位于该 Site。
6. WorldSite Owner 与绑定 TerritoryRegion Controller **永远一致**。
7. 每个 Hex **最多一个** ControlFactionId。
8. Bandit Camp **永远 1 Hex**。
9. 新 Bandit Camp **不能** 生成在正式 Territory（`ControlFactionId != None`）。
10. 新 Bandit Camp **不能** 生成在任何已有 WorldSite Footprint。
11. Bandit **不拥有** 正式 Territory。
12. Bandit **不参与** 外交。
13. Bandit 对 **所有其他 Faction 永远敌对**。
14. **不同 Bandit Camp Faction 彼此也敌对**。
15. 同 Faction 不同 Region **不自动 Merge**。
16. Capture Multi-Hex Site 是 **一次 Capture**，不逐 Hex Capture。
17. Capture Site 时，绑定 Region **整块一起转移**。
18. TerritoryRadius **不作为** Runtime Territory 真源。
19. Runtime Territory 读取 **固化后的** Region.Hexes / Hex control。
20. **Node / Route 不再作为** 正式 Territory / Site spatial truth。

---

## 15. 仍待技术审计决定的问题

以下属于 **代码实现方式**，不在本轮设计范围：

- `HexCell` 是否直接加 `ControlFactionId`，还是独立 `TerritoryBoard`
- `WorldSite` 类具体字段与 Legacy `OccupiedHexes` 命名统一
- `TerritoryRegion` JSON schema 最终格式
- Territory Renderer Component 名称与 Overlay 分层
- Bandit Spawn Service 名称与 Tick 调度
- 初始 Territory 生成工具与 Ch01 旧 Node owner 数据迁移脚本
- Hex distance Tie-break 具体字段（SiteId lexicographic 等）
- Multi-Hex Footprint 编辑器模板 UX

---

## 16. 变更记录

| 日期 | 说明 |
|---|---|
| 2026-08-24 | 初版：制作人拍板 Hex Territory / Multi-Hex WorldSite / Dynamic Bandit Camp 全套产品规则 |
| 2026-09-03 | **V1 落地封板**：TerritoryRegion/Board/ControlService/Validator/Transfer；ch01 30 Region = footprint+ring 固化；WorldMap 淡 tint；初始 overlap 规则改为 content error（§6.12 SUPERSEDED）；snapshot 只存 Controller |
