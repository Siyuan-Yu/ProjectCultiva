# 势力、军队、外交与战略占领

> **2026-09-21 LEGACY-FINAL-C Seal：** 正常 runtime 的 TerritoryRegion board／service 已退休。现代政治与行政 authority 仅为 `WorldSite.OwnerFactionId + TerritoryClaim history + WorldSiteAdministrativeControlResolver / Actual Administrative Control`；Hex 控制与旧 TerritoryRegion 仅为旧 Content／Snapshot migration 输入或派生展示。旧 StrategicEncounter、RetreatingArmy、LingeringBattlefield runtime 同步退休。状态：**Producer Accepted / Sealed**，见 [250](../40-process/250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。

> **现行组织／参战规则：** [ADR-0035](../40-process/43-decisions/ADR-0035-unified-squads-and-encounter-scope.md) 与 [23](23-combat.md) §2～3 替代本页旧 FormalArmy 专属产品入口。唯一通用小队、初始仅冲突两队，第三方只从固定范围内有限候选介入；旧 FormalArmy 服务已退出 normal runtime，仅保留严格旧输入迁移。

> **2026-09-22 正式运行依赖退役：** 当前 Runtime Loader 对 `formalArmy`／`initialFormalArmyIds` 与 `hexWorld` 明确报错并拒绝加载，不再存在 `LegacyFormalArmyDefinition`／`LegacyArmyContentToSquadMigration` 等运行时自动迁移链。`LegacyRuntimeConverter` 只无损处理 FormalArmy 与 current authority 完整的 hybrid Snapshot；`hexWorld`／`openingHexWorldId` 只检测并拒绝，必须使用现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。旧 wire key、数值空洞与 ID 规则只用于边界检测／离线转换，不是现代 producer 或可恢复的 runtime enum 成员。

> 状态：现行外交、战争、CharacterEncounter 与 WorldSite 接管链已封板；旧 Army／Territory runtime 段落仅作历史说明｜优先级：P0｜最后更新：2026-09-22
> 上级：`docs/00-project/00-overview.md`
> 关联：`24`、`26`、`27`、`28`、`113`、`138`、`ADR-0024`、`2K`、`ADR-0026`
> 被引用：`03-glossary.md`、`34`、`41-roadmap`
> **本页的 Faction／外交／War／WorldSite Capture 规则仍是产品真源；FormalArmy／Hex Territory runtime 小节是历史规则，不是当前 authority。**
> **玩家控制模型／PlayerParty／连续世界／「跨点是否必须 Army」以 [2K](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) 为准。**
> 新功能仍需另行授权；不得从历史 `ArmyStack`／RTS 多选段落恢复产品入口。
> **Hex Territory / Multi-Hex WorldSite / Dynamic Bandit（2026-08-24 历史基线）：** 见 [2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)。其中 `ControlFactionId + TerritoryRegion` runtime authority 已由上方 LEGACY-FINAL-C 补丁替代；旧 schema 仅留兼容输入。
> **FactionFlag V1（2026-09-06）：** 阵营旗是非 Character 战略目标，攻击必须通过正式 War 门槛。Anchor+完整一环内的真实防守 FormalArmy 会建立 BattleOffer；旗本身不是参战 Character，战后不自动续拆。几何与领地求解以 2J 为准。
> **SEALED historical baseline（2026-09-06）：** Control Asset Territory、FactionFlag 战略建筑交互、Authoring、SaveLoad 与 WorldMap 图层在当时版本已人工验收，见 [200](../40-process/200-control-asset-territory-and-faction-flag-v1-sealed-2026-09-06.md)。该记录继续证明旧能力，不再冻结 `EstablishedOrder` 对 SiteCore 新增范围的全局追溯优先、footprint 精确行政范围或 Army 类型特权；这些冲突点由 ADR-0032／0034 替代。
> **2026-09-12 当前补丁：** 人物／建筑冲突、战内升级与接管、OR 胜利及飞舟／Army 职责以 [ADR-0033](../40-process/43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md)／[ADR-0034](../40-process/43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) 为准；旧 V1 验收不等于这些目标已实现。

---

## 六条铁则（2026-08-22 拍板）

1. **修士不是匿名兵力数字。** 所有修士都是持久 `Character`。
2. **真实 Character ≠ 全员实时 Actor。** 离屏角色采用分级／数据模拟（Cold / Strategic / Hot）。
3. **Character 与 Squad 是当前组织层。** FormalArmy 曾是军事远征组织，现只保留旧 wire 识别与离线转换输入；runtime 不加载也不自动迁移。
4. ~~**不加入 Army 就不能跨 Node 战略移动。** 一人出征也必须先成立一人 Army。~~ → **SUPERSEDED**。普通 Character／PlayerParty／NPC Squad 使用 Continuous Surface world travel；FormalArmy runtime 已退役。见 2K、ADR-0035／0038。
5. **WorldSite 防御来自真实世界状态。** Resident Character + Squad／个人精确位置；禁止临时凭空刷修士。
6. **战斗结果必须改变真实世界。** 死亡、伤势、Squad roster、Ownership、资源变化最终都回写真实世界状态。

> **Development Acceptance UI（2026-08-27）：** LevelTester 使用统一 **`HostLevelTesterCheatPanel`**（`` ` `` 或顶栏「Cheat Tools」）中的 Diplomacy 区手操验证 War/Alliance/Vassalage；**非产品 UX**。旧 `HostStrategicAcceptancePanel`（F8）已移除。

---

## 0. 与现有系统的关系

| 层级 | 真源 | 说明 |
|------|------|------|
| 个人关系 | `RelationshipLedger`（`28`） | 角色间好恶、历史事件；**不是**势力外交 |
| 角色隶属 | `FactionMembership`（`34`） | 角色当前正式所属势力 |
| 势力外交 | 本文 §16～§28 | Opinion / Trust / Threat、Alliance、Vassalage、War |
| 宏观地图 | Continuous Surface／WorldMap | exact WorldPosition 的战略视图 |
| 占点 | 本文 §29～§37 + `26` | WorldSite／SiteCore／Actual Administrative Control |
| 接战 | `CharacterEncounter` + Squad／真实 Character | 旧 `ArmyStack`／StrategicEncounter 仅为历史或迁移输入 |

**个人 `RelationshipLedger` 与 `Faction Diplomacy` 是不同层级，禁止混成一张表。**

---

## 1. Faction 三层数据概念

### 1.1 FactionDefinition

描述：**这个势力是谁**（静态定义）。

| 字段方向 | 说明 |
|----------|------|
| ID | 如 `base:faction_bandits` |
| 名称 | 显示名 |
| 势力类型 | 宗门／家族／政权／散修联盟等 |
| 描述 | 背景文案 |
| 视觉标识 | 地图色、旗帜等 |
| 性格／标签 | 内容过滤、AI 倾向（未来） |

**不放**随游戏进程变化的实时数据。

### 1.2 ScenarioFactionSetup

描述：**该势力在某一剧本／世界开局时拥有什么**。

| 字段方向 | 说明 |
|----------|------|
| 初始领地 | WorldNode `ownerId` 种子 |
| 首府 | 可选 |
| 初始资源 | Faction Resource Wallet 种子 |
| 初始角色 | Character 名册引用 |
| 初始 Army | Army 列表 |
| 初始外交关系 | Opinion / Trust / Threat 种子、stance、条约 |
| 初始联盟／附庸 | Alliance / Vassalage 种子 |

同一 `FactionDefinition` 可在不同 Scenario 中有不同 `ScenarioFactionSetup`。

### 1.3 FactionState

描述：**当前这一局中真正变化的势力状态**。

| 字段方向 | 说明 |
|----------|------|
| 当前领土 | 拥有的 WorldNode 集合 |
| 当前资源 | Faction Resource Wallet |
| 当前真实成员 | 存活 Character 集合（通过 `FactionMembership`） |
| 当前 Army | 运行时 Army 列表 |
| 当前外交 | 对各势力的 Opinion / Trust / Threat、正式关系 |
| 当前联盟 | Alliance 成员身份 |
| 当前附庸／宗主 | Vassalage 关系 |
| 当前战争 | 参与的 War 实体 |
| Landless 状态 | 是否无地势力 |

**炼气人数、筑基人数、综合实力等：原则上从真实状态统计，不额外维护易漂移的重复计数。**

### 1.4 统一 FactionId（2026-08-22 拍板）

**同一个势力只存在一套 Faction 身份。** 以下全部引用**同一个 `FactionId`**：

| 用途 | 字段／概念 |
|------|------------|
| 角色隶属 | `Character.FactionMembership` → `FactionId` |
| 军队归属 | `Army.FactionId` |
| 节点归属 | `WorldNode.ownerId`（语义名 **OwnerFactionId**） |
| 联盟成员 | Alliance Member `FactionId` |
| 附庸关系 | Vassalage Overlord / Vassal `FactionId` |
| 战争参与 | War Participant `FactionId` |
| 其他战略关系 | 同上 |

**禁止**设计 `CharacterFactionId`／`StrategicFactionId`／`DiplomacyFactionId` 三套平行 ID。

`FactionMembership` 描述 Character 与 Faction 的**成员关系**，不是另一套 Faction 实体。

---

## 2. 所有修士都是真实 Character

**禁止**正式产品模型使用：

```text
QiRefiningCount = 30
FoundationCount = 5
GoldenCoreCount = 1
```

来代表不存在的匿名修士。

正确语义：每个修士是持久 `Character`，拥有 CharacterID、`FactionMembership`、Realm、状态、当前地点、当前行为、伤势、Lifecycle、关系与历史等。

势力面板显示「炼气 30、筑基 5、金丹 1」= 从真实 Character Roster **统计**得出。

详见 [ADR-0024](../40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)。

---

## 3. 修士 LOD / 分级模拟

所有修士真实存在，**不意味着**全员 GameObject、全员 Update、全员寻路、全员 Behaviour AI、全员每帧运行。

| 层级 | 名称 | 条件 | 模拟方式 |
|------|------|------|----------|
| Cold | Data Simulation | 离玩家很远 | 只保留 CharacterState；低频／事件驱动：修炼、闭关、工作、任务、受伤恢复、所属地点 |
| Strategic | Strategic Simulation | 属于在 WorldGraph 上活动的 Army | 不需要 LocalMap Actor；Army 记录真实 MemberCharacterIDs |
| Hot | LocalMap / Hot Simulation | 与玩家同需实体化的 LocalMap；手动 Encounter；其他需 Actor 的场景 | 实例化 Actor；离开后战斗结果写回 CharacterState |

**LocalMap Actor ≠ Character 数据实体生命周期。**

---

## 4. Army：正式军事远征组织（不再是唯一世界移动载体）

> **SUPERSEDED（2026-08-25）：** 旧文「任何 Character 都不能脱离 Army 单独跨点移动／1 人也必须 Army」已废除。
> **新真源：** [2K §7–§8](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)。

**现行产品规则：**

- **PlayerParty／Background Character** 可以在 HexWorld 旅行（后台角色走低频率模拟）。
- **FormalArmy** = 组织真实成员执行军事任务、远方自动战斗与驻扎的编组；不因类型获得宣战、Capture 或 WorldMap 移动特权。
- ~~只有 PlayerParty 或 FormalArmy 拥有 AttackWorldSite／CaptureWorldSite。~~ **SUPERSEDED（2026-09-12）：** 政治结果由真人按正式战争与接管条件完成，不由 Party／Army／飞舟类型授予；见 §19.4、§37 与 ADR-0034。
- 组军与编制管理允许在 Army faction 的任意 **Effective Territory Hex**；成员须真实位于同一 Hex。Garrison 仍为 WorldSite-only（见 [ADR-0028](../40-process/43-decisions/ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md)）。

> **Prototype 注记：** Host 大地图仍以选中 FormalArmy 为主要 RTS 操作（`139`／`152`／`154` historical）。迁移见 [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)。

---

## 5. Character 与 Army 是两层对象

| 对象 | 职责 |
|------|------|
| **Character** | 人物：境界、关系、行为、伤势、生死 |
| **Army** | 一组真实 Character 的军事任务与编制载体；不创造成员本来没有的政治或地图移动类型特权 |

Army **不是**匿名兵力池。Army 保存 `MemberCharacterIDs[]`，**不是** `QiRefining=10, Foundation=2`。

境界分布与 CombatPower 从实际成员计算。

---

## 6. Army 成员规则

| 规则 | 说明 |
|------|------|
| 最少人数 | 1 人 |
| 最多人数 | **未定**（见 §暂缓） |
| 互斥 | 一个 Character 同一时间最多属于 1 支 Army |
| 位置互斥 | 不能同时记在 Node Resident Roster 与另一支 Army 的战略位置里 |
| **同势力** | 一支 Army 的成员必须属于**同一个 Faction**（见 §6.2） |

### 6.1 编组地点：Friendly Effective Territory Hex（2026-09-06 收正）

> **SUPERSEDED：** 2026-08-22 的 Friendly Node / WorldSite-only 地点限制由 [ADR-0028](../40-process/43-decisions/ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md) 取代。

**Create、增加成员、移出成员、更换 Leader、解散 Army** — 全部允许在 Army faction 当前有效控制的 Hex 进行。

| 情况 | 能否调整成员 |
|------|-------------|
| Hex Effective Controller == Army.FactionId | ✅ 可以 |
| WorldSite 或 FactionFlag 产生的己方 Effective Territory | ✅ 一视同仁 |
| 盟友／附庸／宗主控制 Hex | ❌ 不可以 |
| 中立／敌方控制 Hex | ❌ 不可以 |

- Create 的全部 selected members、Add 的 candidate 与 Army 必须经 `CharacterWorldPresenceQuery` 解析到**同一个 World Hex**。
- 判定只读取 Territory Resolver 的最终 Effective Controller，不读取 Control Asset 类型、Nominal Coverage 或 TerritoryRegion authored geometry。
- 不同 Hex 直接拒绝；不自动 rally、travel 或 teleport。
- Territory 丢失不影响 Army 已有存在、移动与战斗，但立即阻止当地后续 roster management。

### 6.2 禁止跨势力混编（2026-08-22 拍板）

- 一支 Army 的成员必须全部属于**同一 Faction**。
- **盟友 Character 不能直接**塞进另一势力的 Army。
- 客卿、借将、租借角色若要进入某势力 Army → 须先在 **Character 层**形成该 Faction 的临时归属／临时成员关系，然后才可合法编入该 Faction 的 Army。
- **不**在 Army 层制造跨 Faction 混编特例。

---

## 7. Army 必须有 Leader

每支 Army 必须有 `LeaderCharacterID`。

**当前 Leader 作用（第一版）：**

- Army 代表角色
- 大地图头像
- 成员显示排序第一位

**第一版明确不做：** Leader 战略 Buff、指挥值、统帅能力。

Leader 战死／离队／失效 → 按既定成员排序选择下一名合法成员为 Leader。
若没有任何成员 → Army 无存在意义，应视为不存在。

---

## 8. Node Resident 与 Army：两种战略状态

> **历史迁移范围（2026-08-22 旧模型）：** 本节旧“Resident 离开必须组 Army”已被 2K／ADR-0034 替代。Resident、Party 和 Army 仍是不同组织／模拟上下文，但普通角色可依 AI、Policy、任务或自身合法移动计划离开；不可远程逐人 RTS 控制仍有效。

Character **未**加入 Army 时可作为 Resident Character 保持 HomeSite／日常职责，但其当前物理位置由统一 WorldPosition 表示。

Resident 可在该 Node 的 LocalMap：工作、修炼、闭关、社交、执行任务、生活。

Resident 不需要为离开而强制组 Army。是否出行由角色目标、职责、Policy、任务和合法路径决定。

---

## 9. Resident Character 的防守规则

敌人**正式攻击**某 Node 时，该 Node 防守力量来自：

- 所有合法 **Resident Character**
- 驻扎于该 Node 的友方 **Garrison Army**
- Node 的 **Formation**／阵法
- 未来其他真实防御设施

Resident 与正式守备按职责响应；未编 Army 不等于永远不能离开 Site：

- 日常 NPC 不因战争敌对自动冲锋；是否接近、追击或外援由职责、动机、发现、状态和可达性决定。
- 初始名单仅冲突两队；本地守备身份不自动赋予第三队初始资格。第三方按开战范围内有限候选规则介入，不把全 Site 人口拉入。
- 远方行动保持自动处理，玩家不能逐个附身下令；FormalArmy 可承载组织任务，但不是唯一合法出行形式。

---

## 10. Army 驻扎与解散

Army 到达**己方 Node** 后可 **Garrisoned（驻扎）**：

- Army **仍然存在**（**不会**因到达而自动解散）
- 大地图仍显示 Army 头像；仍保留 Leader、Members 与战略单位身份
- 可随时继续出征

玩家也可在所属势力 Effective Territory Hex **Disband（解散）** Army — **仅**当玩家明确执行 Disband：

- Army 消失
- AtWorldSite 成员回到该 Site；Wilderness 成员保留 Army 当前 WorldPosition／Hex
- 大地图 Army 头像消失

**驻扎 ≠ 解散。** Garrison 仍要求所属势力拥有的 WorldSite；FactionFlag Territory 没有 Garrison facility。只有 Disband 才解除 Army 身份。

---

## 11. Army 战略位置

Army 位置必须是以下之一：

| 状态 | 字段方向 |
|------|----------|
| **AtNode** | 所在 WorldNode |
| **OnEdge** | `FromNode` + `ToNode` + `RouteId` + `Progress`（0..1） |

Army 可以：

- 在 Route 上移动
- 停在 Route 中途
- 在 Route 中途驻停
- 在 Node 驻扎

**不要**只设计成 `CurrentNodeId`。

### 11.1 Army 攻击与追击（PursuitOrder）

玩家对敌军 Army 下达 **Attack / Pursue** → 生成持续 **PursuitOrder**（`TargetArmyId`），Pursuer 沿 WorldGraph 追踪目标直至接战或订单取消。

| 阶段 | 目标位置来源 | 视野约束 |
|------|--------------|----------|
| **当前（Strategic Vision 未实现）** | 可读 Target FormalArmy **实时**战略位置（开发阶段临时全知；**非最终规则**） | 无 |
| **未来（Vision / Fog 第一版）** | 仅当 Target **当前可见**时续追 | 离开有效战略视野 → **自动取消 Pursuit** |

**第一版未来不做：** Last Known Position 续追、`SearchOrder`、侦察追击 AI。Lost Vision → Pursuit Cancel。

**Cross-ref：** [154 §3.4 Future Strategic Vision Integration](../40-process/154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md) — Vision 系统设计时必回看 Pursuit 合法性、位置泄露、BattleOffer。

> **Prototype 注记：** 当前 `ArmyStack` 已有 `NodeId`／`RouteId`／`RouteAnchorProgress` 等字段，与 OnEdge 概念部分对齐；正式 Army 以 MemberCharacterIDs 为准（ADR-0024）。

---

## 12. Faction Army 数量上限（ArmyCapacity）

每个 Faction 同时能够维持／生成的战略 Army **数量有上限**。

用于限制每个势力在大地图上同时能操作多少支队伍。

未来可能与宗门规模、势力等级、制度、建筑等有关。**具体公式未定。**

**ArmyCapacity（势力可同时有多少支 Army）≠ 单支 Army 成员人数上限。** 两者是不同概念。

---

## 13. 编入 Army 会打断当前行为

玩家可将仍属于自己控制范围的 Character 编入 Army，即使该角色正在：工作、修炼、学斗技、社交、闭关、其他普通行为。

**UI 必须明确表达：** 该角色当前在做什么；加入 Army 会中断当前行为。

例：「张三当前正在闭关。将其编入军队会中断闭关。」

确认后：中断当前 Action → 加入 Army。

**不可编入：**

- 濒死、弥留等确实无法行动的状态
- **Captured（被俘）** — 已不属于玩家有效控制范围

---

## 13.1 Hex Battlefield Residual Presentation（战后弥留／阵亡）

> **2026-09-12 SUPERSEDED：** 下述 `Encounter Hex` 残留是旧大地图自动战呈现契约。玩家参与的同源独立 Encounter 结束后，弥留者与尸体都回到各自战前世界锚点，且保持唯一实例；不得统一落在 BattleHex。自动战若继续使用战略 Hex 汇总，也不能覆盖真实角色已有的精确锚点。

战后 **Downed（Incapacitated）** 与 **Visible Corpse（Dead）** 必须脱离 FormalArmy（见 `ArmyService.DetachNonLivingMembersAtBattlefield`）。

- **不是 FormalArmy**：无 Leader、不可 Move／Attack／Pursuit、不进 Army List、不占 Army Capacity。
- **真实单位仍是 Character**（保留 CharacterId／Faction／LifeState／Corpse）；禁止只存匿名 Count。
- **旧阶段战略位置**：`WorldAgentPresence.Mode = AtHex` + `HexCoord`（Encounter Hex）。仅适用于无法提供更精确原世界锚点的旧自动战汇总。
- **WorldMap 聚合（PURE DERIVED）**：Presentation Query 按
  `HexCoord × DynamicRelation(SELF/ALLY/OTHER/ENEMY) × ResidualState(DEAD/DOWNED)`
  派生 Marker；**不**创建 BattleResidualGroup Domain，**不** Snapshot 保存 Relation／Group／Count。
- Relation 每次相对 `PlayerFactionId` 动态计算（War／Alliance）；和平后原 ENEMY 尸体自动显示为 OTHER。
- Marker：统一 Dead／Downed 图标 + 人数角标；Hex 边缘偏移；Active Army 绘制与命中优先于 Residual；右键穿透 Residual。

Cross-ref：ADR-0025 Residual Hex Position；验收见 153 Residual Grouping 段。

---

## 14. WorldMap 显示 Army，不显示所有 Character

**正式产品目标：**

- 未组成 Army 的 Character **不应**作为战略移动头像显示在 WorldMap 上
- 例：荒村有主角三人，均未组军 → 大地图荒村**不显示**三个可移动 Character 头像
- 进入荒村 LocalMap → 仍可见三人
- 玩家将主角组成 1 人 Army → 大地图才出现以主角为 Leader 的 Army 头像

> **Prototype 注记：** 当前 Host 仍在大地图 Node 上显示多个 Character 头像并允许选中移动（`139` §0 historical）。本轮不 refactor Host；以本节为正式目标。

---

## 15. LocalMap 实体化

Node 内若有 Resident 20 人 + Garrison Army 10 人 → 理论世界状态中 30 个真实 Character 都在该 Node。

需要表现时可实例化。渲染距离、性能 LOD、同屏数量、分批 Actor 等**本轮不决定**。

核心原则：**世界数据是真实 Character，视觉实体只是表现层。**

---

## 16. Faction Diplomacy：基础态度（单向）

Faction A 对 Faction B 拥有**独立单向**态度，至少包括：

| 维度 | 范围示例 | 说明 |
|------|----------|------|
| **Opinion** | -100 ~ +100 | 喜欢／讨厌 |
| **Trust** | 可量化 | 是否相信对方；**喜欢 ≠ 信任** |
| **Threat** | 可量化 | 对对方实力／威胁的畏惧 |

A→B 与 B→A **可以不同**。

例：Opinion=-80, Trust=5, Threat=95 → 「非常恨你、不相信你、但非常怕你」→ 支撑被迫臣服、暂时俯首。

---

## 17. 不做 Diplomatic Reputation

**明确不做：** 外交信誉、世界级外交可靠度、`DiplomaticReputation`、`DiplomaticReliability` 等。

不要从其他 4X 自行补回。

---

## 18. 外交记忆

Faction 态度不应只剩一个最终数字。概念上允许记录原因，例如：

- +20 曾共同作战
- -30 杀死本宗长老
- -20 占领祖地
- +10 释放俘虏

用于解释 Opinion / Trust / Threat 的变化。

**本轮不决定**是否复用 `RelationshipLedger` 实现；只记录：势力态度需要保留可解释的历史来源／Modifier／Memory。

---

## 19. 正式外交关系（第一版框架）

| 关系 | 说明 |
|------|------|
| **Peace** | 和平（默认） |
| **War** | 战争（见 §27） |
| **Truce** | 停战 |
| **Non-Aggression Pact** | 互不侵犯 |
| **Military Access** | 军事通行 |
| **Alliance** | 军事同盟（见 §20） |
| **Vassalage** | 附庸（见 §21） |

「友好／敌视」由 Opinion 等态度表达。**能否军事占领**由是否处于 **War** 决定（§29）。

### 19.1 运行时势力／外交只读总览 V0（2026-09-05）

WorldMap 的「战略 → 势力」是**运行时只读可见性**，不是开局内容预览，也不是外交操作界面。

- 势力列表从当前 `SimulationWorld.Strategic` 被正式引用的势力汇总：玩家势力、活动战争、联盟、附庸、Squad 与 WorldSite；展示名称从已安装的 faction Content 元数据读取。
- 当前关系统一经 `FactionDiplomacyRelationQuery` 查询：`自己 → 战争 → 联盟 → 直接附庸 → 普通`。战争优先保证起事后不会继续把旧附庸显示为宗主关系。
- 关系方向以观察者为准：A 是 B 的宗主时，`GetRelation(A, B) = 附庸`，`GetRelation(B, A) = 宗主`。
- 页面可读取领地区域数、FormalArmy 数、宗主／附庸和任意两势力之间的当前关系；不得在 Host 拼装 War／Alliance／Vassalage 规则，也不得读 `strategicOpening` 作为当前状态。
- 宣战、议和、结盟、解除联盟、建立／解除附庸等动态外交 mutation 属于下一阶段；本页不显示占位或禁用操作按钮。

### 19.2 ControlCore 军事占领的 WorldSite 解析与战争门槛（2026-09-05）

主管府等 `ControlCore` 的 `LocationId` 是 LocalPlace 身份，不能直接与 `WorldSite.LocalMapId` 比较。唯一正式解析链为：

`ControlCore.LocationId → WorldRegion.Location.LocalMapId → WorldSite.LocalMapId → WorldSite.SiteId`

> **2026-09-15 SUPERSEDED：** Fixed Core 不再从 LocalMap／WorldRegion 猜 Site，也不再建立 Runtime CaptureObjective。Content 的 outdoor `controlCore` placement 通过 `SiteId + StableId + BoundLocationId` 将 `ControlCoreState.BoundWorldSiteId` 绑定到唯一 WorldSite，`ControlCoreBoard` 维护双向索引。

若解析出的 WorldSite 有 Owner，且攻方不等于 Owner，则 `TryBeginMilitaryAssault` 与 `TryCompleteWorldSiteCapture` 都必须要求 `WarGateService.CanMilitaryCapture`。无主 Site 保持既有行为。占领完成必须使用同一解析所得 `SiteId` 经 `WorldSiteTerritoryTransferService.Transfer` 变更 Site 与 Territory；Host 的两个主管府攻击入口只调用领域预检，领域伤害路径仍是最终 gate。

### 19.3 可重复 WorldSite 占领 V1（2026-09-05）

`WorldSite.OwnerFactionId` 是当前政治归属的唯一真源；TerritoryClaim 是历史空间 authority；TerritoryRegion 与 Hex 控制色是重建得到的 compatibility projection。`ControlCoreState` 保存可重复攻破的建筑物理状态及稳定 Site identity binding，不保存 Owner。

成功事务固定为：验证战争、破门与读条 → Transfer → Core／Objective 恢复满耐久与零读条 → 重建玩家 SettlementAuthority → 发出一次 `WorldSiteCaptured`。因此 Transfer 失败不会留下局部占领。新 Owner 可立即防守，未来的残破恢复／资源维修属于 **ControlCore Recovery V2**，本轮不实现。

旧 v6 Snapshot 的 `captureObjectives` 仅为读取迁移输入：`Completed=true` 恢复满耐久、零读条；否则只迁移 HP 与占领进度。新 Save 仅写 `controlCores`，不写旧目标、SiteId、MaxHp 或 HoldSeconds。普通居民、巡卫与既有 FormalArmy 不因 Site 易主自动改角色势力。

玩家的住房／课表权限由 `SettlementAuthoritySync.Rebuild` 根据**当前**玩家拥有的 ControlCore Site 全量重建；失去最后一个权限来源必须撤销权限。`PlayerControlled`、`AllCompletedForSite` 与运行时 CaptureObjective 已删除。

<a id="conflict-and-building-war"></a>
### 19.4 人物冲突、建筑攻击与战内扩大战争（2026-09-12）

- 攻击人物或其实际同行小队只建立本场人物冲突，不自动替双方势力宣战。私人敌对、势力态度与 War 分开；散修不是共享政治势力。
- 最新建筑规则适用于某势力**有效拥有的所有建筑**。从主世界首次攻击非战争势力建筑时，同一个遭遇窗口同时说明开战和对有效 Owner 宣战；确认后才提交 War 并允许伤害，不连续弹三个窗口。
- 已在人物战中转攻未交战势力建筑时，暂停当前战场并说明新增后果。确认后在同一地图扩大冲突、允许攻击并触发守备；不重开地图、不重置伤势或消耗。取消只取消本次扩大行为。
- Owner 按建筑当前有效归属解析，不按地面颜色猜测。自身／无政治归属对象不得对空 Faction 或自己宣战；中立产权沿用适用规则。
- 已处于 War 时不重复外交确认，但从主世界发起新遭遇仍经过战斗窗口。V1 未授权 AOE 默认不损伤非交战势力建筑；主动指定建筑则走相应确认。
- 确认内容必须揭示该 Owner 当前联盟、附庸或宗主关系所产生的既有连锁后果；本规则不静默解除这些关系，也不把未受攻击的势力误宣战。具体 Ch01 主体映射见 §44 的待核查边界。
- 人物私斗可在同一战场升级战争，升级后正式守备响应。议政厅可按正式条件在场内实际接管，不要求先返回世界。

胜利资格为**本次正式接管完成 OR 本次有效敌方战斗人员被打倒**。未接管不会因打倒人物自动得地；获得结束资格后可留场完成接管。详见 [23 §12.2](23-combat.md)。

### 19.5 不做系统强制的战后保护期（2026-08-22 拍板）

**明确不做**战争结束后的系统强制保护期／宣战冷却。

- 战争结束后可以**直接**回到 Peace。
- **不要**默认创建：X 月内不得再次宣战、强制停战保护、战后免战期。

若玩家**主动签署 Truce** → 属于普通外交协议（§19），**不等于**系统自动生成战后保护期。

> **Prototype 注记：** 当前代码仅有四档 `FactionStance`（Friendly / Neutral / Hostile / War），为 MVP；正式模型以本节为准。

---

## 20. Alliance：独立多方实体

联盟**不是**仅 A-B、A-C、B-C 三对 bilateral 关系。

Alliance 是真正的**多势力政治实体**：

```text
Alliance
  Members: [A, B, C]
```

成员仍是独立 Faction，彼此**平等**。

### 20.1 一独立 Faction 同时最多一个 Alliance（2026-08-22 拍板）

- 一个**独立** Faction 同一时间最多属于**一个**正式 Alliance。
- 其他合作关系通过 Opinion / Trust / Threat、Non-Aggression Pact、Military Access 等表达。
- **Vassal 仍然不能**独立加入 Alliance（§23）。
- **独立 Overlord** 可以加入 Alliance（§23）。

### 20.2 第一版 Alliance 战争绑定（2026-08-22 拍板）

**第一版：Alliance 成员战争绑定。**

- 一个 Alliance Member 进入 **War** → 其他正式 Alliance Member **同步进入该 War**。
- 若某 Alliance Member 同时拥有 **Vassal** → Alliance 战争传播到该 Overlord 后，再按 §28 Overlord-Vassal 规则将其 Vassal 卷入战争。

**第一版明确不做：**

- 防御联盟／进攻联盟两种类型
- 联盟投票
- 成员拒绝参战
- 战争援助请求

（以上以后再扩展。）

---

## 21. Vassalage：上下级关系

附庸**不是** `isVassal = true` 布尔值，而是：

```text
Overlord ↔ Vassal + Obligations
```

附庸仍是独立 Faction，保留：

- 自己的 Node
- 自己的 Character
- 自己的 Army
- 自己的 Resource
- 自己的内部管理权、治理权

**附庸统一拥有自治权。** 本游戏**暂时不做**《全面战争：三国》式高自治／低自治／无自治细分。

---

## 22. 附庸没有独立外交权

附庸内部自治，但**外交不自治**。附庸不能自行：

- 宣战
- 结盟
- 签订独立外交协议
- 加入独立平等 Alliance

外部外交战略由 **Overlord** 决定。

附庸仍拥有自己对其他 Faction 的 Opinion / Trust / Threat。
例：玩家是玄天宗附庸，对白家 Opinion +70，但玄天宗决定对白家开战 → 玩家政治上仍须跟随宗主。

---

## 23. 附庸与 Alliance 互斥

- **Vassal 不能**自己成为独立 Alliance Member
- **拥有附庸的独立 Overlord** 可以加入 Alliance

合法结构示例：

```text
玄天宗 —— 天剑宗 —— 灵月宗   （Alliance 成员，平等）
   |
   ├─ 紫霞宗（Vassal）
   └─ 白家（Vassal）
```

---

## 24. 禁止附庸套附庸

政治结构**只允许一层**：

```text
允许：宗主 → 附庸 A / B / C
禁止：宗主 → 附庸 A → 附庸 B
```

已是 Vassal 的 Faction **不能再收**自己的 Vassal。

---

## 25. Tribute / Vassal Obligation

宗主按周期对附庸提出**贡赋／臣属义务**（如每月／每季度／每年）：

- 灵石、粮食、灵药、其他资源

第一版玩法可先以**周期资源贡赋**为主。领域概念建议使用较宽泛的 **Vassal Obligation**，以便未来扩展：提供材料、派遣队伍、完成任务、上交特殊资源。

**具体周期、数值、算法：未定。**

---

## 26. Independence Desire（独立倾向）

附庸可拥有**独立倾向**，表达当前臣服可能只因畏惧／实力差距，并非永久忠诚。

未来可能受 Opinion、Trust、Threat、双方实力、贡赋压力、宗主保护、宗主战败、外部支持等影响。

**具体公式：不做。**

---

## 27. War：独立实体

战争**不能**仅是 `A.RelationToB = Enemy`。

应存在真正的 **War** 概念，可拥有：

- 多个进攻方
- 多个防守方
- 开战时间
- 参与势力

未来可扩展战争目标、战争分数、和平谈判 —— **本轮不设计**。

---

## 28. 宗主与附庸的战争状态绑定

**已确认：**

- 宗主进入战争 → 附庸跟随
- 附庸受到战争攻击 → 宗主参与
- 独立宗主因 Alliance 进入战争 → 其附庸跟随宗主进入战争

不要扩展多层附庸（已禁止 §24）。

若出现「多个兄弟附庸之间战争状态传播细节」等未覆盖情形 → 列入未决，不要自行创造规则。

---

## 29. 战争是军事占领 Node 的前提

Faction 想通过**军事方式**夺取另一 Faction 的 Node → **必须已处于 War**。

和平状态不允许直接军事占领。

Node 也可通过未来**交易／外交转让**改变 Owner —— **交易系统本轮不做**。

---

## 30. Node 只有直接 Owner，不做 Owner / Controller 双层

**已否决** Civilization / Grand Strategy 式「军事控制者与法理所有者分开」。

本游戏**不要**：

```text
Owner = A
Controller = B
```

战争占领成功后：**Owner = 攻击方**，直接易主。

**不要**要求战后签和平、割地、法理确认、Occupied Territory 之后才改变 Ownership。

---

## 31. CaptureObjective

`CaptureObjective` 是既有实现/API 名称；当前 V1 语义收敛到 WorldSite 的唯一 `SiteCore`。预设 Site 的核心表现为固定议政厅／主管府等，可被攻破防御并通过正式交互接管，但不可拆除。玩家另立势力旗会创建新的 Site，而不是给同一 Site 添加第二个 CaptureObjective。

| Node 类型 | 示例 |
|-----------|------|
| 荒村 | 主管府 |
| 矿区 | 管理核心建筑 |
| 城市 | 城主府／核心建筑 |
| 宗门 | 宗门大殿／阵枢 |

具体表现名称可由内容配置；占领系统不应把所有 Site 的显示名硬编码为“主管府”，但运行语义始终指向该 Site 的唯一核心。

> **与旧实现关系：** LocalMap 阶段已实现 `ControlCore`／`CaptureObjective` 流程（`121`）；这些名字可作为迁移入口。普通户外的正式目标是在同源独立 Encounter 中对实际 SiteCore 完成攻破与接管，不能恢复一 Site 多目标的旧提案。

---

## 32. 多个 CaptureObjective

> **SUPERSEDED（2026-09-12）：** V1 一个 WorldSite 只有一个 SiteCore，不做同城多核心。预设议政厅不可拆，可攻破防御并实际接管；新旗创建另一个 Site。旧“多个核心必须全部完成”只保留为未来多目标扩展的历史提案，不约束 V1。

同一 SiteCore 可以有门、墙、阵法等多个防御结构；破坏这些结构不等于完成接管。战斗结束资格与土地易主分别按 §37：接管完成 OR 打倒本次有效敌人可结束，只有实际接管改变 Owner。

---

## 33. CaptureObjective 占领流程

手动战核心流程（与现有主管府流程一致，generalize）：

```text
攻击 CaptureObjective
  → 建筑／核心 HP 打到 0
  → 进入可占领状态
  → 进攻方角色进入 Capture Zone
  → 持续一段时间
  → 占领完成
```

敌方 Character 可攻击占领者、打断占领、保护核心。

**无需杀光地图上所有敌人**才能夺取节点。

---

## 34. Node Defense 来自真实世界状态

节点遭攻击时，防御力量来自：

- Node **Resident Characters**
- **Garrisoned Armies**
- **Formation**／阵法
- 未来其他真实防御设施

**禁止正式设计：** 「因为 Node 等级是 3，临时凭空生成 15 名炼气守军。」

修士守军必须是真实 Character。

---

## 35. 阵法属于 Node Defense

未来据点可有灵阵／阵法：

- **自动战：** 阵法计入 Node Defense／战斗修正
- **手动战：** 阵法真实影响 LocalMap Encounter

具体阵法设计本轮不展开；占点框架须**兼容**阵法。

---

## 36. 自动攻点

玩家选择 Auto Resolve 时：

进攻 Army 的真实综合战力 vs Node Resident + Garrison Army + Formation 等真实防御力量。

必须战胜据点总体防御，才能完成后续占领／易主。

CombatPower 算法：**本轮不重新设计**；沿用／参考现有自动战框架，后续再调。

---

## 37. 手动攻点

> **2026-09-12 SUPERSEDED：** 下方 2026-08-22 的“全部 CaptureObjectives 才成立胜利”只作历史记录。当前玩家实战进入同源独立 Encounter；场内真人可实际接管议政厅，且**正式接管完成 OR 打倒本次有效敌方战斗人员**任一成立即可获得结束资格。打倒人物但未接管不自动得地；接管成立也不因仍有活守卫而扣留。见 [23 §12.2](23-combat.md)／ADR-0033。

手动攻城使用接战地点当前地形、建筑和核心状态的同源独立 Encounter。

### 37.1 Capture 成功后的手动战收尾（2026-08-22 拍板）

当正式议政厅接管完成后：

1. **Node Capture Success** 已成立
2. **Node Owner** 直接切换为进攻方 Faction（`OwnerFactionId`／`ownerId`）
3. 玩家在 LocalMap 中可以点击 **「结束战斗」**
4. 点击后进入**战斗结算页面**

**不要**要求玩家继续追杀地图上所有残余敌人才能结束。

### 37.2 残余守军：真实 Character 结算（2026-08-22 拍板）

手动攻城**结束战斗**时，仍存活的敌方守军**不能凭空消失、自动收编或清除仇恨**；本次防御结束后回其战前世界锚点并可在主世界后续退却。

当前 V1 不附带完整俘虏／赎金制度，也不在结算时随机把守军改成 Captured。每名参与者先按 [23 §12.1](23-combat.md) 回到自己的战前世界锚点并保留当前生命、伤势、死亡、消耗、关系和势力身份；残存守备可随后在主世界按真实 AI／任务退却。旧 `Captured／Escaped → RetreatingArmy` 概率提案只作历史扩展，不是本轮目标。

---

## 38. Landless Faction（无地势力）

势力失去全部 Node **不立即灭亡**。

若仍拥有：存活成员、可行动成员、Army、组织主体 → 进入 **Landless（流亡／无地势力）** 状态。

仍可活动、战斗、尝试夺回 Node。

**RetreatingArmy／Exile Army**（§37.2 逃脱守军）是无地势力仍可存在的 Army 形态之一。

**不要**因 `TerritoryCount == 0` 直接删除 Faction。

真正的 Faction Destruction 条件：**后续再定义**。

---

## 39. Faction Resource

每个 Faction 应有真实 **Resource Wallet**。

外交、贡赋、战争等所有资源变化使用**同一势力资源体系**。

**不要**为外交单独创造「外交金币」。

---

## 40. Resource Ledger

建议势力资源变化保留可解释收支来源，例如：

- Node Income
- Tribute Income
- Army Upkeep
- Tribute Expense

主要目的：可解释、可调试、后续方便做 UI。

具体经济系统以后设计。

---

## 41. 暂缓 / 明确不做（本轮）

### AI Decision 层（全部暂缓）

- AI 如何从 N 个 Character 中挑选出征成员
- AI 如何决定队长、组军、宣战、求和、臣服、独立

### 数值（全部未定）

- 单支 Army 成员人数上限
- Faction ArmyCapacity 公式
- Tribute 数值与周期
- Independence Desire 公式
- Opinion / Trust / Threat 变化公式
- CombatPower 新公式
- 阵法战力公式
- 残余守军 Captured / Escaped 概率
- RetreatingArmy 撤退路径 AI

### Strategic Vision / Fog of War（全部暂缓）

- WorldMap Fog of War 渲染与更新
- Faction / Army **Strategic Vision** 范围与来源（节点、驻军、侦察等）
- Scout / Detection / Last Known Position
- **Pursuit 失去视野自动取消** — 规则已锁定于 [154 §3.4](../40-process/154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)；实现随 Vision 系统一并交付
- Pursuit 全知追踪（不可见目标仍读 `FormalArmyBoard` 实时位置）— **未来禁止**

### 高级战争（不做）

- War Score、Casus Belli、War Goal
- 和平条约割地、Military Occupation / Controller
- 战争赔款、战后法理归属流程

### 高级外交（不做）

- Diplomatic Reputation
- 联盟议会／投票／成员拒绝参战（§20.2 已列）
- 战后系统强制保护期／宣战冷却（§19.1）
- 联姻、人质、代理战争
- 复杂贸易、情报／间谍系统

### 其他

- 交易／外交转让 Node Owner（未来方向，本阶段不占点实现）
- `26` §2.2「外交接管」占点 —— **superseded** 为 future 交易／外交转让；本阶段占点仅 War + CaptureObjective

---

## 42. 未决问题

- [ ] `PartyWorldPresence` 与正式 `Army` 是取代、桥接还是共存？（本轮不决定）
- [ ] 势力 Diplomatic Memory 是否独立 Ledger，还是 Faction 专用 Modifier 列表？
- [ ] 多个兄弟附庸在复杂战争中的状态传播细节
- [ ] Faction Destruction 完整条件
- [ ] ArmyCapacity 与单队人数上限的具体数值
- [ ] CaptureObjective 与现有 `ControlCore` 配置的迁移路径
- [ ] Snapshot 是否纳入 FactionState / Army / War（见 `62`；须单独确认 schema）

---

## 43. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-22 | 初版：制作人拍板战略 Faction / Army / Diplomacy / Capture 框架；仅文档，未编码 |
| 2026-08-22 | 第二轮：己方 Node 编组限制；驻扎不自动解散；统一 FactionId；禁止混编；无战后保护期；一势力一 Alliance + 联盟战争绑定；Capture 收尾与残余守军结算 |
| 2026-08-22 | Final Closure：Ch01 Scenario 边界 — 开局从属为 Scenario state（非 Generic Vassalage）；荒村 Capture → 玩家政治成立 → 与旧宗门 War → 未来 Vassalage 谈判（Hook only，数值/UI DEFER） |

---

## 44. Ch01 Opening Scenario 边界（Final Closure，2026-08-22）

> **2026-09-05 修订：** 本节的独立「主动起事」按钮流程已被正式军事侵略事务取代。玩家攻击正式军事目标时，若尚未战争，先确认政治后果，再由 `StrategicMilitaryAggressionService` 完成必要的解除附庸／退出联盟与宣战。第一章仅在该事务成功攻击旧宗主时记录 `ch01:rebellion_started` 剧情标记。

> **2026-09-12 攻城流程：** Fixed WorldSite 的 V1 SiteCore 表现为议政厅。任何新的玩家实战都在首击前出现 Encounter 确认，不以“存在防守 FormalArmy”为前提。战场从接战地点当前地形、门墙桥和议政厅状态构建；正式守备、实际同行和有限关系候选分别进入。议政厅可在同一战场内被实际接管；接管完成或打倒本次有效敌人任一成立即可获得结束资格，不要求返回世界后再次攻击。议政厅不可拆，防御被攻破不等于删除实体。

**原则：** Generic Domain 回答「Faction / Army / War / Vassalage **怎么工作**」；Ch01 Scenario 回答「**什么时候**发生」。

必须分开核对四个主体：角色 `FactionMembership` 表示人物当前成员身份；`PlayerAgency.ManagedFactionId` 表示玩家可管理的政治主体；建筑 Effective Owner 决定本次建筑攻击归责对象；外交／War 由具备政治身份的 Faction 发起。四者不得凭“玩家控制了这个角色”相互推导。当前文档能确认 `strategicOpening`、VassalageBoard、军事侵略事务与剧情标记各自存在过，但新规则下 Ch01 开局各阶段的具体 ID 映射和无有效政治主体时的安全失败仍需实现前定向核查；不得为补空缺新造 Faction 或把普通人物攻击硬接 DeclareWar。

| 阶段 | 语义 | 实现边界 |
|------|------|----------|
| Stage 0 开局压榨 | 玩家势力是压迫宗门的正式附庸；该关系由 Scenario `strategicOpening` 提供 | `VassalageBoard` 是关系真源；不在 Generic Bootstrap 偷写剧情关系。 |
| Stage 1 正式军事侵略 | 玩家明确攻击旧宗主有效拥有的建筑等政治军事目标 → 确认适用后果 → 按既有联盟／附庸链处理政治脱离与宣战 | 现有通用事务与 Scenario Hook 是待核查接线；人物私斗不得误触发，既有联盟／附庸连锁后果不得静默跳过。 |
| Stage 2 夺取荒村 | 战场内正式接管议政厅 → `WorldSite Owner` 易主 → 玩家取得第一块真正领土与政治成立标记；仅打倒本次对手不自动得地 | 现有 Domain 名称是实现历史；新生命周期待迁移。不在 Capture Domain 硬编码剧情宣战。 |
| Stage 3 后续附庸谈判 | 战争推进后旧宗门可主动 Offer Vassalage | Hook：`OfferVassalageNegotiation` → 正式 `VassalageBoard`；谈判 UI / 时间 / AI / 数值 **DEFER** |

**Prototype 回归例外：** Ch01 对 Bandit 的自动 `DeclareWar` 仅允许存在于 `Ch01ScenarioStrategicSetup.ApplyPrototypeRegressionDiplomacy`（非正式剧情战争）。

**Cross-ref：** `152` §1.7 presence-based friendly node；`Ch01ScenarioArmyFormationPolicy`（Scenario Adapter only）。


## 2026-09-15 CW-08 / CW-09：玩家 SiteCore 战争

制作人授权规则见 [235](../40-process/235-sitecore-warfare-worldsite-takeover-2026-09-15.md)／[236](../40-process/236-world-object-interaction-fixed-core-capture-closure-2026-09-15.md)。固定核心由 `CoreIsRemovable=false` 判定，攻破后建筑仍存在，在原建筑交互范围持续占领；己方存活人物在场且没有存活敌方参战者争夺才计时，离开或争夺归零。占领经 `WorldSiteCoreWarfareService` → `WorldSiteTerritoryTransferService` 改同一 Site 的 Owner 并恢复核心满耐久。ClaimId、AcquiredOrder、农田 identity/crop、人物 faction/home/squad 均不改。

`CoreIsRemovable=true` 的势力旗被击毁即移除物理旗、令原 Site inactive；保留原 Owner 与 Claim 历史，不占领、不自动变成己方旗。攻方通过正常建造建立新旗、新 Site 和新 Claim。资产继续存在，行政管理者由原 CW-05 查询动态接续。

正常玩家入口统一为 SiteCore Warfare → real Character/Squad → CharacterEncounter，退出两条旧 Siege/BattleOffer 编排。按精确 Surface/WorldPosition、目标 Site 等级范围和 War side 选守军，最近者优先、EntityId 升序打破平局。战中目标仍限定同一 frozen range，最多一个未完成 Site 目标；新守军追加原 roster，不回血、不重置冷却或候选。

目标捕获或摧毁完成可 ReadyToEnd；击倒敌人也可 ReadyToEnd，但不自动占地。ReadyToEnd 仍可攻击与占领当前目标。仅玩家发起 SiteCore 战争属于本轮；NPC 自动攻城、普通建筑战争、产权及居民政治后果延期。
