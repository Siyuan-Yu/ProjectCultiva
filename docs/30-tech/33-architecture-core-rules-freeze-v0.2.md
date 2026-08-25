# 架构核心规则冻结 v0.2

> 状态：**已冻结（v0.2）** + **2026-08-21 补丁：ADR-0023** + **2026-08-22 补丁引用：ADR-0024** | 优先级：P0 | 最后更新：2026-08-22  
> 上级：`docs/00-project/00-overview.md`  
> 依赖／展开：`31`、`32`、`34`、`35`、`36`、`2B`、`2C`、`2E`、`21`～`28`、`2F`、`2G`、`24`、**[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)**  
> 被引用：全体正式实现、AGENTS、路线图  
> **本文件是架构冻结阶段的主契约（v0.2）。** 相对 v0.1 纳入审计修补：关系权威、双时间、生命周期、Focus 失能、开局 Membership、地图三层、Core M1 范围。  
> 变更须：升版本号或记补丁＋写 ADR、记入 `42-devlog.md`。  
> 旧版：[`33-architecture-core-rules-freeze-v0.1.md`](33-architecture-core-rules-freeze-v0.1.md)（已由本文件取代）。

> **⚠️ 2026-08-22 读 Freeze 前须知（ADR-0024，不升 v0.3）：**  
> - **修士 = 持久真实 Character + LOD**；**禁止**用 `CultivatorPopulation` 匿名计数代表修士或修士战争。  
> - **修士战略 Army** = 真实 `MemberCharacterIDs[]` 载体；**`ArmyGroup` 仅**凡人／大规模非修士军队（ADR-0008 部分 superseded）。  
> - 战略 Faction / 外交 / 占点真源：[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)。  
> - **Hex Territory / Multi-Hex WorldSite / Dynamic Bandit（2026-08-24）：** [2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)。  
> - 当前 Host `PartyWorldPresence`／`ArmyStack`／WorldMap RTS 多选为 **Prototype**；详见 §4／§10 注记。  
> - **2026-08-25 RPG-First（[ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)／[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)）：** 单 Active／PlayerParty／连续 HexWorld；**废除「跨点必须 Army」**；FormalArmy=军事远征层。**不升级 Freeze v0.3**；本注记为 v0.2 补丁引用。

## 0. 冻结纪律

1. 本文列出的规则视为 **v0.2 已确认**。  
2. Demo 仅作语义参考；正式实现 **替换实现，不改已冻结玩法语义**。  
3. 未写入本文的内容仍属「待确定」。  
4. 文档包入口：`34`、`2C`、`2E`、`35`、`36`；审计见 `50-architecture-freeze-review-report-v0.1.md`。  
5. Core Milestone 1 范围见 §22／ADR-0022；**不得**借第一阶段偷做跨 Region 离屏、真战斗、完整势力领导、Mod 文件夹。

---

## 1. 总架构边界（P0）

1. 普通 C# 组合模型，不采用 Unity ECS（ADR-0002）。  
2. Core／Data 禁止 `UnityEngine`。  
3. Unity：输入、画面、动画、音效、镜头、导航表现、UI。  
4. Unity 不得直接改 Core。  
5. 数据流：`Unity 输入 → Order` → Core 执行 Action → Unity 读 Snapshot／DomainEvent。  
6. CSV／JSON 真源；同类型单一格式；SO 仅缓存（ADR-0004）。  
7. `DefinitionId`（`namespace:local_id`）与 `EntityId` 分离（ADR-0015）。  
8. 全部随机经可保存的 `IRandomSource`；存 `WorldSeed` 与分系统流。  
9. 不做完整回放；快照存档（ADR-0005）。  
10. 关键规则优先整数／缩放整数；概率 0～10000；百分比 10000=100%。  
11. 校验失败阻止进游戏，禁止静默默认。  
12. 官方与 Mod 统一 ContentPackage（ADR-0014）；当前只 Mod Ready（ADR-0013）。  
13. 禁止 `IsPlayerCharacter`／单一 `FactionId` 包办四权（ADR-0012）。  
14. **`DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity`**（ADR-0020）。

---

## 2. AttributeModifier（P0）

见 `2C`。公式：

```text
Raw = (Base + Σ Fixed) × (1 + Σ Percentage)
Final = Clamp(ApplyAllowedSpecialRules(Raw), Min, Max)
```

长期属性变化必须经 AttributeModifier。状态值／资源池（含 **`PersonalConcealmentRisk`**，禁止再用 ExposureAccumulation 作正式名）不硬塞进管道。

---

## 3. 双时间：WorldTick + ActionClock（P0）

见 ADR-0018、`21`、`35`。

| | WorldTick | ActionClock |
|---|---|---|
| 是什么 | **世界唯一时间轴** | **单个行动的剩余／已耗持续时间** |
| 负责 | 日期、昼夜、季节、NPC 成长、世界事件、ScheduledEvent、离屏世界推进 | 采集／移动／修炼／施法等 Action 的 Duration 消耗 |
| 禁止 | 被 Action 替代 | 独自改变世界日期／昼夜 |

关系：

```text
WorldTick 推进
  → 消耗相关 ActiveAction 的 ActionClock（Duration）
  → Duration 归零 → Action 完成 → DomainEvent
```

- 1 Tick = 15 游戏分钟；1 日 = 96 Tick。  
- **禁止两套独立世界时间。**  
- 暂停／倍速统一影响 WorldTick（从而影响 ActionClock 消耗）。  
- **战略接战补丁（ADR-0023，2026-08-21）：** `BattleOffer`→Manual／PostBattle 期间 **不推进** WorldTick；战术暂停与战略冻结分离；Resolve 后恢复开战前 pause／倍速。仍只有一条 WorldTick，不是第二时钟。  
- **Core M1**：单 Region 全仿真；跨 Region 离屏 Action **不做**（§22）。若未来做离屏，仍只推进 WorldTick，再按同一规则扣 ActionClock，不另开时间轴。

阶段顺序（职责不可缺，实现可微调）：收令 → 推进 WorldTick → ScheduledEvent → 生成 Order → 推进 Action（扣 ActionClock）→ 结算 → DomainEvent → Ledger → Snapshot。

> 若 `StrategicClockFreeze` 生效：跳过「推进 WorldTick」及依赖其的战略 Travel／Schedule 消费；遭遇内战术表现可用 Host 表现时钟。

---

## 4. 实体与四层模拟（P0）

见 `34`。Character 组合成长；禁止互斥继承树。

> **2026-08-22 后续决策（[ADR-0024](../40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)）：** 所有修士 = 持久真实 Character + LOD 模拟（Cold / Strategic / Hot）。**不再**用 `CultivatorPopulation` 匿名计数代表修士战争真源。凡人仍允许 Population 聚合。战略 Army／外交／占点以 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 为准。**不升级 Freeze v0.3**；本注记为 v0.2 补丁引用。

---

## 5. Order／Action（P0）

见 `35`。仅 Order／Action；单 ActiveAction；Action 可序列化。Action 完成不直接写 Final 属性或关系最终值。

---

## 6. 事件与 WorldLedger（P0）

见 `2E`。DomainEvent／ScheduledEvent／分册 Ledger。  
**RelationshipLedger 是关系唯一真源**（§7／ADR-0017）。

---

## 7. RelationshipLedger 唯一真源（P0）— v0.2

见 ADR-0017、`2E`、`28`、`34`。

- 关系由**事件历史累积**，不是裸最终值。  
- Ledger 保存：关系事件、来源、时间、影响对象、影响数值、原因标签。  
- 例：A 救 B → `+30`；B 叛 A → `-50`；**最终关系值由 Ledger 计算**。  
- `RelationshipComponent` **仅**运行时缓存／查询优化／UI；**禁止**直接改最终关系值或绕过 Ledger。

---

## 8. 地图三层（P0）— v0.2

见 ADR-0021、`24`。

```text
World
 └── Region          （较大连续区域，如一座城市区域）
      └── LocalMap   （独立加载：山洞／秘境／洞府／遗迹等）
```

| 层 | 含义 |
|---|---|
| **World** | 整个修仙世界；Region 间关系、战略观察；跨 Region 旅行用 Route（非整片大陆无缝） |
| **Region** | 连续体验优先：荒村、矿山、森林、农田、城镇中心、道路；可行走／战斗／飞行／途中遭遇 |
| **LocalMap** | 由 Region 入口加载的独立地图；实例状态永久保存 |

- **废弃**与本节冲突的「大陆→城市区域→格子」旧三级叙事中与四类地图混用的过时尺度硬约束（统一 10 屏／1.5 屏等）；Region 尺寸可变。  
- 格子仍是 Local／Region 内的空间单位。  
- 不做 3D 开放世界。

---

## 9. 多队伍与离屏（P0 形状／M1 不做跨区）

统一 WorldTick。镜头内完整表现。  
**Core M1 不做跨 Region 离屏模拟**；形状保留供后续：同 Region 远处简化、跨区低频、远方抽象。

---

## 10. 战斗／AI／军队（方向／边界）

同 v0.1：战术 RTS+暂停；时间表+效用。**M1 不做真战斗与完整 NPC AI。**

> **2026-08-22（[ADR-0024](../40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)）：**  
> - **修士 FormalArmy** = 真实 Character 成员 + **军事远征**载体；**不再**要求跨 Hex 必须经 Army（[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)）。外交／Capture 仍见 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)。  
> - **`ArmyGroup`** 收窄为凡人／大规模非修士军队聚合（[ADR-0008](../40-process/43-decisions/ADR-0008-army-group-aggregate.md) 部分 superseded）。  
> - 当前 Host `ArmyStack`／`PartyWorldPresence`／RTS 多选为 Prototype；historical 验收见 `139`～`154`。

**2026-08-21 补丁（ADR-0023）：** 战略 `BattleOffer`／Manual Encounter 为 Modal，冻结战略 WorldTick；废弃「战斗期间战略世界继续推进」为默认。详见 `21` §10、`23` §12、[144](../40-process/144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)。

---

## 11. 世界变化永久保存（P0）

建筑／所有权／道路桥梁／关键节点／秘境状态／重要 NPC／事件／势力关系／关键地形玩法变化必须保存。

---

## 12. 炼气能力／突破／隐匿（P1 方向）

同 v0.1。隐匿三层不合并；正式状态值名 **`PersonalConcealmentRisk`**。

---

## 13. 角色生命周期（P0）— v0.2

见 ADR-0019、`34`。

```text
Alive
  → Incapacitated
       → 恢复为 Alive（Recovered 是过渡结果，不是长期枚举值）
       → Captured
       → Missing
       → Dead
```

| 状态 | 含义 |
|---|---|
| `Alive` | 正常可行动 |
| `Incapacitated` | **不是死亡**；重伤、无法战斗／正常工作、等待处理 |
| `Captured` | 被俘 |
| `Missing` | 失踪 |
| `Dead` | **永久死亡** |
| `Removed` | **独立状态**：不再参与当前模拟（临时实体清理、离开模拟范围、数据移除） |

**禁止** `Dead = Removed`；**禁止** Removed 自动代表死亡。  
默认可永久死亡；`IsStoryImportant ≠ CannotDie`；`TemporaryProtection` 规则同 v0.1。

---

## 14. PlayerAgency 与 Focus 失能（P0）— v0.2

见 ADR-0020、`34`。

```text
PlayerAgency
- FocusCharacterId
- FocusCharacterUnavailable   // bool 或子状态
- ActiveControlMode           // Character | FactionLeadership
- ControlledEntityIds
- ManagedFactionId            // nullable
```

分离：

- `DirectControl` ≠ `FocusCharacter`  
- `FactionLeader` ≠ `FocusCharacter`  
- `ControlledEntity` ≠ `PlayerIdentity`  

**FocusCharacter 不可用时（重伤／被俘／失踪／暂时无法行动）不立即改变玩家身份**；置 `FocusCharacterUnavailable`。

若存在同行者／代理／合法继承关系 → **继续游戏**（可临时改直接控制目标，焦点身份规则另见继承）。  
否则：早期 → **GameOver**；后期 → **继承流程**（细则待内容阶段；形状已冻）。

失去势力领导权：移除势力管理权限，保留人物控制；旧势力 AI 继续。

---

## 15. 开局 Membership（P0）— v0.2

见 `2G`、`34`。支持第一章：荒村生活 → 偷修 → 隐藏 → 击败主管 → 夺据点。

| 对象 | FactionMembership | FactionRole | ControlAuthority |
|---|---|---|---|
| 三名初始角色 | **当前压迫他们的宗门** | 杂役弟子／劳役弟子 | 玩家**直接控制** |
| 主管 | **同一宗门** | 管理者 | 监督与处罚权限（非玩家 DirectControl） |

---

## 16. ContentPackage／Mod Ready（P0 形状）

见 `36`。M1 只建基础结构，**不**做 Mods/ 文件夹加载。

---

## 17. Core Milestone 1 范围（P0）— v0.2

见 ADR-0022。

**验证：** 未来系统可建立在统一 Core 上。

**包含：** EntityId／DefinitionId／SourceRef；WorldTick；IRandomSource；ContentPackage 基础；Entity 基础；AttributeModifier；DomainEvent；Order／Action；Snapshot 存档；**单 Region** 运行验证。

**不做：** 跨 Region 离屏；完整势力领导；真战斗；完整 NPC AI；Mod 文件夹加载；大地图战争。

---

## 18. 与 Demo

见 `32`。不扩展 Demo；替换实现、保留语义。

---

## 19. 冻结清单速查

| 章节 | 状态 |
|---|---|
| 1 总边界 | 冻结 |
| 2 Modifier | 冻结 |
| 3 双时间 | **v0.2 澄清冻结** |
| 4～6 实体／Order／事件 | 冻结 |
| 7 RelationshipLedger | **v0.2 冻结** |
| 8 地图三层 | **v0.2 冻结** |
| 9～12 离屏形状／战斗方向／保存／炼气隐匿 | 形状／方向 |
| 13 生命周期 | **v0.2 冻结** |
| 14 PlayerAgency／Focus 失能 | **v0.2 冻结** |
| 15 开局 Membership | **v0.2 冻结** |
| 16 Mod Ready | 形状冻结 |
| 17 Core M1 | **v0.2 冻结** |

## 20. 下一步（不编码）

1. 人工审核 v0.2。  
2. 通过后可规划 Core M1 实现（仍须另开任务）。  
3. 突破规格／炼气术法清单仍可并行写设计。
