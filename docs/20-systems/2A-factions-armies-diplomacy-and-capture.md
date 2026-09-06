# 势力、军队、外交与战略占领

> 状态：**设计规则已拍板｜Control Asset Territory + FactionFlag V1 已实现、人工验收并封板**｜优先级：P0｜最后更新：2026-09-06
> 上级：`docs/00-project/00-overview.md`  
> 关联：`24`、`26`、`27`、`28`、`113`、`138`、`ADR-0024`、`2K`、`ADR-0026`  
> 被引用：`03-glossary.md`、`34`、`41-roadmap`  
> **本页是战略势力层（Faction／外交／War／Capture／Army 军事规则）的产品真源。**  
> **玩家控制模型／PlayerParty／连续世界／「跨点是否必须 Army」以 [2K](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) 为准。**  
> **本阶段不写实现代码。** 当前 Host 中的 `PartyWorldPresence`／`ArmyStack`／RTS 多选等为 **Prototype**，见各过程文档 historical 注记。  
> **Hex Territory / Multi-Hex WorldSite / Dynamic Bandit（2026-08-24）：** 见 [2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)。Territory／Site Footprint／Bandit 专题以 **2J** 为准；本文 § 中 **Node Owner / Node Territory** 表述为 Legacy，Pure Hex 下以 **ControlFactionId + TerritoryRegion** 为准。
> **FactionFlag V1（2026-09-06）：** 阵营旗是非 Character 战略目标，攻击必须通过正式 War 门槛。Anchor+完整一环内的真实防守 FormalArmy 会建立 BattleOffer；旗本身不是参战 Character，战后不自动续拆。几何与领地求解以 2J 为准。
> **SEALED baseline（2026-09-06）：** Control Asset Territory、FactionFlag 战略建筑交互、Authoring、SaveLoad 与 WorldMap 图层已完成人工验收；封板边界与 Future 见 [200](../40-process/200-control-asset-territory-and-faction-flag-v1-sealed-2026-09-06.md)。除明确 Bug / Regression 外不改变 V1 first-claim、EstablishedOrder、SupportArea 与快照 authority。

---

## 六条铁则（2026-08-22 拍板）

1. **修士不是匿名兵力数字。** 所有修士都是持久 `Character`。
2. **真实 Character ≠ 全员实时 Actor。** 离屏角色采用分级／数据模拟（Cold / Strategic / Hot）。
3. **Character 与 Army 是两层。** Army 是正式**军事远征组织**（2026-08-25：[2K](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)），不再是「世界移动资格」。
4. ~~**不加入 Army 就不能跨 Node 战略移动。** 一人出征也必须先成立一人 Army。~~ → **SUPERSEDED（2026-08-25）**。普通 Character／PlayerParty 可在 HexWorld 旅行；FormalArmy 仅军事远征。见 2K OLD-01／02、ADR-0026。
5. **Node／Site 防御来自真实世界状态。** Resident Character + Garrison Army + Formation；禁止临时凭空刷修士。
6. **战略战斗结果必须改变真实世界。** 死亡、伤势、Army 损失、Ownership、资源变化最终都回写真实世界状态。

> **Development Acceptance UI（2026-08-27）：** LevelTester 使用统一 **`HostLevelTesterCheatPanel`**（`` ` `` 或顶栏「Cheat Tools」）中的 Diplomacy 区手操验证 War/Alliance/Vassalage；**非产品 UX**。旧 `HostStrategicAcceptancePanel`（F8）已移除。

---

## 0. 与现有系统的关系

| 层级 | 真源 | 说明 |
|------|------|------|
| 个人关系 | `RelationshipLedger`（`28`） | 角色间好恶、历史事件；**不是**势力外交 |
| 角色隶属 | `FactionMembership`（`34`） | 角色当前正式所属势力 |
| 势力外交 | 本文 §16～§28 | Opinion / Trust / Threat、Alliance、Vassalage、War |
| 宏观地图 | `WorldGraph`（`113`） | WorldNode / WorldRoute |
| 占点 | 本文 §29～§37 + `26` | CaptureObjective、Owner 直接易主 |
| 接战 Prototype | `138`～`150`、`ArmyStack` | 历史已落地行为；正式 Army 模型以本文 + ADR-0024 为准 |

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
- **FormalArmy** = 正式军事远征组织：公开进攻、Capture、战争参与、WorldMap 常驻 Leader 标记。  
- **只有 PlayerParty 或 FormalArmy** 拥有 AttackWorldSite／CaptureWorldSite。  
- 组军与编制管理允许在 Army faction 的任意 **Effective Territory Hex**；成员须真实位于同一 Hex。Garrison 仍为 WorldSite-only（见 [ADR-0028](../40-process/43-decisions/ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md)）。

> **Prototype 注记：** Host 大地图仍以选中 FormalArmy 为主要 RTS 操作（`139`／`152`／`154` historical）。迁移见 [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)。

---

## 5. Character 与 Army 是两层对象

| 对象 | 职责 |
|------|------|
| **Character** | 人物：境界、关系、行为、伤势、生死 |
| **Army** | 让一组真实 Character 获得 WorldGraph 战略移动、攻击、追击、驻扎能力的**组织载体** |

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

Character **未**加入 Army 时 → 驻留于某一 WorldNode（Resident Character）。

Resident 可在该 Node 的 LocalMap：工作、修炼、闭关、社交、执行任务、生活。

**Resident 不能**主动离开 Node 进行战略行动。想离开必须先组成 Army。

---

## 9. Resident Character 的防守规则

敌人**正式攻击**某 Node 时，该 Node 防守力量来自：

- 所有合法 **Resident Character**
- 驻扎于该 Node 的友方 **Garrison Army**
- Node 的 **Formation**／阵法
- 未来其他真实防御设施

**限制：** 仅是 Resident、未组成 Army 的 Character：

- **不能**主动到 Node 之外支援
- 即使敌军 Army 已在附近 Route 上，也**不能**跑出去打
- 只能等敌方真正攻击 Node 后参与 **Node Defense**

想主动出击 → 必须先组成 Army。

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

战后 **Downed（Incapacitated）** 与 **Visible Corpse（Dead）** 必须脱离 FormalArmy（见 `ArmyService.DetachNonLivingMembersAtBattlefield`）。

- **不是 FormalArmy**：无 Leader、不可 Move／Attack／Pursuit、不进 Army List、不占 Army Capacity。
- **真实单位仍是 Character**（保留 CharacterId／Faction／LifeState／Corpse）；禁止只存匿名 Count。
- **战略位置**：`WorldAgentPresence.Mode = AtHex` + `HexCoord`（Encounter Hex）。Residual 运行时路径禁止再读 Legacy Node／Route。
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

- 势力列表从当前 `SimulationWorld.Strategic` 被正式引用的势力汇总：玩家势力、活动战争、联盟、附庸、FormalArmy、WorldSite 与 `TerritoryRegion`；展示名称从已安装的 faction Content 元数据读取。
- 当前关系统一经 `FactionDiplomacyRelationQuery` 查询：`自己 → 战争 → 联盟 → 直接附庸 → 普通`。战争优先保证起事后不会继续把旧附庸显示为宗主关系。
- 关系方向以观察者为准：A 是 B 的宗主时，`GetRelation(A, B) = 附庸`，`GetRelation(B, A) = 宗主`。
- 页面可读取领地区域数、FormalArmy 数、宗主／附庸和任意两势力之间的当前关系；不得在 Host 拼装 War／Alliance／Vassalage 规则，也不得读 `strategicOpening` 作为当前状态。
- 宣战、议和、结盟、解除联盟、建立／解除附庸等动态外交 mutation 属于下一阶段；本页不显示占位或禁用操作按钮。

### 19.2 ControlCore 军事占领的 WorldSite 解析与战争门槛（2026-09-05）

主管府等 `ControlCore` 的 `LocationId` 是 LocalPlace 身份，不能直接与 `WorldSite.LocalMapId` 比较。唯一正式解析链为：

`ControlCore.LocationId → WorldRegion.Location.LocalMapId → WorldSite.LocalMapId → WorldSite.SiteId`

`CaptureObjectiveService.TryResolveControlCoreSite` 是复用入口。已存在且能验证的 `CaptureObjective.SiteId` 优先；新解析成功后回填该字段。`JobRuntimeBootstrap` 可能早于 `WorldRegionBootstrap`，所以注册时允许暂未绑定，WorldRegion 就绪后重绑，攻击开始与占领完成仍必须懒解析兜底。

若解析出的 WorldSite 有 Owner，且攻方不等于 Owner，则 `TryBeginMilitaryAssault` 与 `TryCompleteWorldSiteCapture` 都必须要求 `WarGateService.CanMilitaryCapture`。无主 Site 保持既有行为。占领完成必须使用同一解析所得 `SiteId` 经 `WorldSiteTerritoryTransferService.Transfer` 变更 Site 与 Territory；Host 的两个主管府攻击入口只调用领域预检，领域伤害路径仍是最终 gate。

### 19.3 可重复 WorldSite 占领 V1（2026-09-05）

`WorldSite.OwnerFactionId` 是当前政治归属的唯一真源；`TerritoryRegion.ControlFactionId` 与 Hex 控制色只由 `WorldSiteTerritoryTransferService.Transfer` 同步。`ControlCore` 只表示可重复攻破的建筑物理状态，`CaptureObjective` 只表示耐久与占领读条，二者均不保存 Owner。

成功事务固定为：验证战争、破门与读条 → Transfer → Core／Objective 恢复满耐久与零读条 → 重建玩家 SettlementAuthority → 发出一次 `WorldSiteCaptured`。因此 Transfer 失败不会留下局部占领。新 Owner 可立即防守，未来的残破恢复／资源维修属于 **ControlCore Recovery V2**，本轮不实现。

旧 Snapshot 的 `CaptureObjective.Completed` 仅为迁移标记：Restore 时不改写 Owner，而是迁移为满耐久、零读条、Runtime false。历史 `site_captured:*` 与 Ch01 政治成立旗标可以保留；它们表示「曾发生」，绝不表示「当前拥有」。普通居民、巡卫与既有 FormalArmy 也不因 Site 易主自动改角色势力。

玩家的住房／课表权限由 `SettlementAuthoritySync.Rebuild` 根据**当前**玩家拥有的 ControlCore Site 全量重建；失去最后一个权限来源必须撤销权限。`PlayerControlled` 与 `AllCompletedForSite` 仅保留旧代码兼容，禁止进入新的占领 authority。

### 19.3 不做系统强制的战后保护期（2026-08-22 拍板）

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

所有可军事占领的 Strategic Node **必须配置至少一个 CaptureObjective**。

| Node 类型 | 示例 |
|-----------|------|
| 荒村 | 主管府 |
| 矿区 | 管理核心建筑 |
| 城市 | 城主府／核心建筑 |
| 宗门 | 宗门大殿／阵枢 |

具体内容由地图配置；占领系统**不应硬编码**「主管府」。

> **与 Prototype 关系：** 当前 LocalMap 已实现 `ControlCore`（主管府）流程（`121`）；正式占点 generalize 为 `CaptureObjective`，语义沿用 HP→0→Capture Zone→持续占领。

---

## 32. 多个 CaptureObjective

重要 Node 可有多个核心（如东侧核心、西侧核心、城主府）。

**已确认：** 有几个就必须**全部完成**。不做占 2/3、任选一个、Primary Objective 单独胜利。

```text
All CaptureObjectives Completed → Node Captured
```

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

手动攻城进入对应 LocalMap／Encounter。

- **进攻方：** 完成**全部** CaptureObjectives
- **守方：** 保护核心；受阵法等 Node Defense 影响
- **胜利条件：** All CaptureObjectives Completed = Node Capture Success（**不是** Kill All Enemies = Win）

### 37.1 Capture 成功后的手动战收尾（2026-08-22 拍板）

当**全部 CaptureObjective 完成**后：

1. **Node Capture Success** 已成立
2. **Node Owner** 直接切换为进攻方 Faction（`OwnerFactionId`／`ownerId`）
3. 玩家在 LocalMap 中可以点击 **「结束战斗」**
4. 点击后进入**战斗结算页面**

**不要**要求玩家继续追杀地图上所有残余敌人才能结束。

### 37.2 残余守军：真实 Character 结算（2026-08-22 拍板）

手动攻城**结束战斗**时，仍存活的敌方守军**不能凭空消失**。

结算时，每个真实 Character 根据后续规则／概率进入不同结果，例如：

- **Captured（被俘）**
- **Escaped（成功逃脱）**

**具体概率公式：本轮不做。**

**Escaped 的 Character：**

- 仍保持真实 Character 身份
- 可组成一支或多支 **RetreatingArmy**（撤退军团）／**Exile Army**
- 尝试向其 Faction **仍控制的 Node／区域**撤退

若该 Faction 已失去全部领土（**Landless Faction**，§38）：

- **不删除**这些 Character
- 他们仍可作为 **Landless Faction 的 Army** 存在
- 无地势力具体撤退目标／求生 AI — **以后再设计**

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

> **攻城补充：** Fixed WorldSite 的 `CaptureObjective` 在 V1 统一表现为「议政厅」。攻击议政厅时先按该 Site 的全部 footprint 加外围一圈冻结 SupportArea，并按当前 War 的双方收集实际可战 FormalArmy；有防守方军队才出现 BattleOffer。议政厅是敌方战略目标的表现项，不是 Character participant，不参与战斗结束条件；击败守军后不会自动继续拆除，玩家必须再次发起议政厅攻击。

**原则：** Generic Domain 回答「Faction / Army / War / Vassalage **怎么工作**」；Ch01 Scenario 回答「**什么时候**发生」。

| 阶段 | 语义 | 实现边界 |
|------|------|----------|
| Stage 0 开局压榨 | 玩家势力是压迫宗门的正式附庸；该关系由 Scenario `strategicOpening` 提供 | `VassalageBoard` 是关系真源；不在 Generic Bootstrap 偷写剧情关系。 |
| Stage 1 正式军事侵略 | 玩家攻击旧宗主的 FormalArmy 或议政厅 → 确认政治后果 → 解除附庸 → 宣战 | `StrategicMilitaryAggressionService` 为通用事务；`Ch01ScenarioProgressionHooks` 只记录剧情标记。 |
| Stage 2 夺取荒村 | 全部 CaptureObjectives 完成 → `WorldSite Owner` 易主 → 玩家取得第一块真正领土与政治成立标记 | Domain：`CaptureObjectiveService` → `WorldSiteTerritoryTransferService`；Scenario Hook：`Ch01ScenarioProgressionHooks`，不在 Capture Domain 硬编码剧情宣战。 |
| Stage 3 后续附庸谈判 | 战争推进后旧宗门可主动 Offer Vassalage | Hook：`OfferVassalageNegotiation` → 正式 `VassalageBoard`；谈判 UI / 时间 / AI / 数值 **DEFER** |

**Prototype 回归例外：** Ch01 对 Bandit 的自动 `DeclareWar` 仅允许存在于 `Ch01ScenarioStrategicSetup.ApplyPrototypeRegressionDiplomacy`（非正式剧情战争）。

**Cross-ref：** `152` §1.7 presence-based friendly node；`Ch01ScenarioArmyFormationPolicy`（Scenario Adapter only）。
