# 架构核心规则冻结 v0.1

> 状态：**已冻结（v0.1）** | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 依赖／展开：`31`、`32`、`34`、`35`、`36-content-package-and-mod-architecture.md`、`2B`、`2C`、`2E`、`21`～`28`、`2F`  
> 被引用：全体正式实现、AGENTS、路线图  
> **本文件是架构冻结阶段的主契约。** 细则可在系统文档展开，但形状与边界不改。  
> **本阶段不写实现代码。** 变更须：升版本号、写 ADR、记入 `42-devlog.md`。

## 0. 冻结纪律

1. 本文列出的规则视为 **v0.1 已确认**。  
2. Demo 原型仅作语义参考；正式实现以本文与 `32` 为准，**替换实现，不改玩法语义**。  
3. 未写入本文的内容仍属「待确定」，不得当作已冻结。  
4. 文档包入口：`34` 实体、`2C`、`2E`、`35` Order/Action、`36` ContentPackage／Mod Ready。  
5. 增量（死亡／PlayerAgency／Mod Ready）见 §19～§21；ADR-0010～0016。

---

## 1. 总架构边界（P0）— 已冻结

1. 正式逻辑层使用**普通 C# 组合模型**，**不采用 Unity ECS**（ADR-0002）。  
2. `XianXia.Core` 与 `XianXia.Data` **不允许**依赖 `UnityEngine`。  
3. Unity 层只负责：输入、画面、动画、音效、镜头、导航表现、UI。  
4. Unity **不得**直接修改 Core 状态。  
5. 数据流：

```text
Unity 输入 → Order
Core 执行 Order / Action
Unity 读取只读 StateSnapshot / ViewModel 与 DomainEvent
```

6. CSV／JSON 为配置唯一真源（ADR-0004）：  
   - CSV：平面数据表  
   - JSON：嵌套结构与复杂规则  
   - **同一数据类型只能有一种真源格式**  
   - ScriptableObject 只允许作为导入缓存与编辑器／运行时资产  
7. `DefinitionId` 与 `EntityId` 分离；显示名称不能充当 ID；DefinitionId 采用 `namespace:local_id`（ADR-0015；见 `36`）。  
8. 所有随机必须通过可注入、可保存状态的 `IRandomSource`。  
9. 世界保存 `WorldSeed`，并为关键系统提供独立随机流。  
10. **不做**完整游戏回放。  
11. 只保证：正常存档恢复、固定 Seed 自动测试可复现、关键事件可追踪。  
12. 正式版本采用**快照存档**，不使用完整 Event Sourcing（ADR-0005）。  
13. 开发期允许旧存档失效，但破坏性变更必须升级 `SaveVersion`。  
14. 正式大版本内尽量提供独立 `SaveMigration`／`DataMigration`。  
15. Core 关键规则尽量使用整数或缩放整数：Tick、资源、生命／灵力、概率 0～10000、百分比 10000=100%、格子／逻辑位置。  
16. Unity 插值、动画与视觉坐标可以使用 float。  
17. 正式运行前必须校验：ID 重复、引用不存在、循环依赖、非法数值、必填缺失、未知枚举、Modifier 来源无效、ScheduledEvent 目标无效、DefinitionId 改名未迁移、ContentPackage 依赖。错误时**阻止进入游戏**，禁止静默默认值。  
18. 官方内容与 Mod 统一走 ContentPackage（ADR-0014）；当前阶段只做 Mod Ready，不实现完整 Mod 平台（ADR-0013）。  
19. 禁止用 `IsPlayerCharacter` 或单一 `FactionId` 包办归属／职位／关系／控制权（ADR-0012）。

---

## 2. 属性系统与 AttributeModifier 管道（P0）— 已冻结

完整展开见 [`2C-attributes-and-modifier-pipeline.md`](../20-systems/2C-attributes-and-modifier-pipeline.md)。

### 2.1 公式

```text
Raw = (Base + Σ Fixed) × (1 + Σ Percentage)
Final = Clamp(ApplyAllowedSpecialRules(Raw), Min, Max)
```

- Fixed 先加；普通 Percentage **同一加算池**；暂不设多乘区。  
- 不允许每种功法独立计算顺序。  
- 所有长期属性变化必须通过 AttributeModifier；禁止直接改 Final。  
- SpecialRule 白名单：ClampMin／ClampMax／Override／Disable／Convert。  
- 属性与状态值／资源池必须区分；状态值不硬塞进管道。

---

## 3. 时间系统：WorldTick + ActionClock（P0）— 已冻结

详见 ADR-0003、`21`、`35`。

### 3.1 双层时间

| 层 | 职责 |
|---|---|
| `WorldTick` | 世界事件、日程、资源、离屏模拟、NPC 计划、势力变化、突破窗口、长期成长 |
| `ActionClock` | 当前场景中的移动、战斗、施法、采集过程、修炼动作过程、动画对齐 |

- 1 Tick = **15** 游戏分钟；1 日 = **96** Tick。  
- ActionClock **不**拥有独立日期，**不能**脱离 WorldClock 自行推进世界时间。  
- 暂停、1x、2x、5x **统一影响全世界**；玩家主动暂停则全世界暂停。  
- 战斗**不是**独立时间模式；接敌时外部世界继续运转，除非玩家暂停。

### 3.2 重大接敌

- 自动全局暂停并提醒  
- 允许切换查看多队伍  
- 玩家设定自动防御／撤退／拖延等策略后，后台战斗才继续  

### 3.3 Tick／阶段职责顺序（实现可微调，职责不可缺）

1. 接收已排定输入／命令  
2. 更新世界时间  
3. 处理 ScheduledEvent  
4. 生成计划／Order  
5. 推进行动（ActionClock／ActiveAction）  
6. 结算状态与资源  
7. 发布 DomainEvent  
8. 更新账本  
9. 生成只读快照  

禁止各系统用独立 `deltaTime` 做逻辑结算。

---

## 4. 实体与四层模拟（P0）— 已冻结

完整展开见 [`34-entity-and-component-model.md`](34-entity-and-component-model.md)、`27`。

- 顶层：Character／Building／Settlement／Faction + 薄 `IEntity`。  
- Character 用组合模块成长；禁止 Player／Npc／Cultivator 互斥继承树。  
- 四层：可控修士全模拟、关键 NPC 全模拟、`CultivatorPopulation`、凡人统计。  
- 第三／四层不得偷偷创建数千隐藏 Character；重要实体不归并。  

---

## 5. Order 与 Action（P0）— 已冻结

完整展开见 [`35-order-and-action-system.md`](35-order-and-action-system.md)。

- 公开概念仅 Order／Action，无公开 Intent 层。  
- 每完整 Character：一个 ActiveAction、一个 OrderQueue、一个 InterruptContext；第一版无多并行动作槽。  
- 玩家与 AI 只差在谁生成 Order。  
- 优先级表见 `35`；优先级 ≠ 可执行性。  
- Action 必须可序列化，支持中断分级。  

---

## 6. 事件、ScheduledEvent 与 WorldLedger（P0）— 已冻结

完整展开见 [`2E-events-and-world-state.md`](../20-systems/2E-events-and-world-state.md)。

- DomainEvent／ScheduledEvent／WorldLedger 三层。  
- 禁止各系统私有逻辑倒计时。  
- Ledger 分册；Knowledge 区分事实与认知。  
- 隐匿三层不合并。  
- 快照存档 + 未执行日程事件 + 随机流状态。  

---

## 7. 地图与世界结构（P0）— 已冻结

详见 ADR-0006；玩法展开见 `24`。

四类：

1. **WorldMap**：州域、城市、宗门、危险区、跨区域关系；战略观察与远程下令。  
2. **RegionMap**：一座城市及其周边的大型连续区域（市中心、城门、荒村、农田、森林、矿山地表、河流、山岭、道路、军营等）。尺寸可变；**不再**使用“统一 1.5 屏”。荒村及周边可约 3～4 个当前视野；完整城市更大。技术上允许 Chunk／流式加载；体验上保持连续空间。近／中／远景缩放分层。  
3. **InstanceMap**：洞内、矿道、秘境、灵泉内部、建筑内部、剧情空间；由 RegionMap 入口加载；**实例状态永久保存**。  
4. **EncounterMap／Route**：跨 Region 不造整片大陆连续地图；用 Route（出发、目的、时间、进度、地形、危险、补给、固定节点、遭遇池）。队伍非瞬移；同路敌对可途中遭遇。固定节点永久保存；临时遭遇可生成临时 EncounterMap，细节不必全留，但伤亡／损失／关系／战略后果必须保留。

飞行必须提供真实玩法变化（跨障碍、不依赖道路、侦察、多方向接近、截击），且重要区域可有禁空／结界／防空等反制。

总览中旧“三级结构”表述以本节为准升级为：**WorldMap + RegionMap + Instance／Route**。

---

## 8. 多队伍与离屏模拟（P0）— 已冻结

详见 ADR-0007。

- 玩家可同时拥有分散多区域队伍并切换查看；统一世界时间持续推进。  
- 当前镜头：完整表现。  
- 同 Region 远处：简化代理／纯逻辑，低频更新。  
- 其他 Region 玩家队伍：低频实体模拟，保留路线与任务。  
- 无玩家远方世界：Tick 抽象模拟。  
- “持续模拟”≠“所有地图始终完整加载渲染”。  

世界变化永久保存边界见 §12。

---

## 9. 战斗框架（P0）— 方向已冻结

- RTS 式即时战斗；可随时全局暂停。  
- 玩家可下达移动、集火、技能与站位。  
- **当前地图直接展开**；不做回合制；不做“战斗场景时间停止世界”。  
- 多场同时：首场重大接敌自动暂停提醒；可切换队伍；未查看战斗仅在设定战术姿态后后台结算。  
- 后台姿态预留：避免战斗、保守防御、保持距离、集中突围、正常交战。  

细则玩法见 `23`。

---

## 10. NPC AI（P0）— 方向已冻结

- **时间表 + 效用评分 + 简单行动计划**。  
- 不做完整 GOAP；不为每类 NPC 建完全独立大型行为树。  
- 必须可解释（目标、评分、Order、Action、放弃原因）。见 `35`。  

---

## 11. 军队系统边界（P1）— 边界已冻结

详见 ADR-0008。

- 军队**不是**核心重玩法；不做全面战争式军团系统。  
- 主控仍是开局 1～3 人 → 后期 30～50 修士 + 少量关键凡人／精锐。  
- 架构支持 `ArmyGroup` 群体数据（人数、士气、训练、装备、阵型、指挥者、补给、伤亡、任务）。  
- 不生成 1000 个完整 AI 实体；入镜用有限视觉代理 + Core 群体结算。  

---

## 12. 世界变化永久保存（P0）— 已冻结

必须永久保存：

- 建筑建造／破坏／升级  
- 区域所有权  
- 道路与桥梁状态  
- 关键资源节点  
- 洞穴／秘境探索状态  
- 重要 NPC 状态  
- 重要事件结果  
- 势力关系  
- 地形的关键玩法变化  

普通装饰细节可由规则再生，不要求保存每一块无意义碎片。

---

## 13. 境界：炼气期能力（P1）— 方向已冻结

**境界提升必须带来玩法变化。**

感应境：能感受灵气、基础吸收；不能正式功法与真正灵力战斗能力。  

炼气解锁方向：灵力池、功法修炼、基础术法、灵气感知强化档。  

飞行／踏空／空间等不在炼气冻结范围；术法清单待定。

---

## 14. 第一次突破事件（P1）— 方向已冻结

大境界突破必须是事件（准备／过程／结果），不能只是按钮加境界。  
感应境 → 炼气是第一章核心事件。  
地点／护法／风险／失败细则待 `25`／`2G` 展开。

---

## 15. 隐匿三层（P1）— 已冻结分层

1. 个人隐匿风险  
2. NPC 怀疑值  
3. 势力敌意／关系  

禁止合并为单一暴露条。Demo 的 Exposure／Anger 仅语义参考。

---

## 16. 与 Demo／正式开发

见 [`32-prototype-to-product-bridge.md`](32-prototype-to-product-bridge.md)。

- **不再扩展 Demo 功能。**  
- 替换实现，保留玩法语义。  

---

## 19. 角色永久死亡与临时剧情保护（P0）— 已冻结

详见 `34`、ADR-0010。

1. **默认所有角色允许永久死亡**（普通、核心队友、重要 NPC、剧情人物）。  
2. 死亡后：不自动复活、不自动生成替代者；强绑定剧情／任务／传承／关系可永久消失；允许错过内容。  
3. **`IsStoryImportant` ≠ `CannotDie`。**  
4. `DeathProtectionMode`：`None`（默认）／`TemporaryProtection`。  
5. `TemporaryProtection` 仅内容作者显式配置，且必须含：生效原因、剧情阶段、解除条件、致命结果的替代后果。  
6. 保护不是永久无敌；致命结果可转为重伤濒死／被俘／失踪／强制撤离／修为受损／永久伤势／物品或身份损失（由事件配置）。  
7. 保护只保证当前阶段不立即永久死亡，不保证平安或必然回归。  
8. Lifecycle 至少：`Alive`／`Incapacitated`／`Missing`／`Captured`／`Dead`／`Removed`。  
9. `Dead` 是永久世界状态；禁止普通复活流程随意撤销。  
10. 若未来有复活／逆转死亡，必须作为极高阶明确规则单独设计，**不得**成默认系统能力。  

---

## 20. 势力归属、控制权与 PlayerAgency（P0）— 已冻结

详见 `34`、ADR-0011／0012。

必须分离：

| 概念 | 含义 |
|---|---|
| `FactionMembership` | 当前正式属于哪一势力；可加入／离开／驱逐／被俘变更／自立；离开保留历史 |
| `FactionRole` | 宗主／长老／执事／成员／客卿／俘虏／临时盟友等预定义职位 |
| `Relationship` | 人－人／人－玩家核心／人－势力／势力－势力／人－据点或群体；离开势力不清零 |
| `ControlAuthority` | 直接控制／仅高层命令／纯 AI／暂时失控／可夺回 — **动态权限，非永久类型** |

### 20.1 核心成员离开

开局三人与后续核心成员均可离开（退出／叛逃／加入他势／自立／敌对）。原因须可解释（关系、道心、利益、制度、招揽、求生、事件立场等）。**禁止无预兆随机抽奖**；须有对话／情绪／忠诚／关系／事件／行为异常等前兆。AI 调试须解释考虑离开的原因、影响因素、可挽回条件。离开后保留关系、贡献、历史、仇恨／友好；未来可再加入／结盟／敌对／争领导权。

### 20.2 PlayerAgency

```text
PlayerAgency
- FocusCharacterId          // 始终存在
- ActiveControlMode         // Character | FactionLeadership
- ControlledEntityIds
- ManagedFactionId          // 可空
```

- **Character**：控焦点人物及其当前队伍；不天然拥有整势力。  
- **FactionLeadership**：仍控焦点人物，并因职位获得势力管理（成员／资源／制度／领地／外交）。  
- 势力权限来自角色身份职位，非玩家天生。  
- 离开／退位／驱逐／失领导／放弃势力 → 移除 FactionControl，保留 CharacterControl；旧势力**不消失**：转 AI，保留建筑／成员／资源／制度／关系／历史。  
- 玩家可返回、再加入、争领导、结盟、另建、敌对。  
- FactionLeadership **不是**纯上帝视角；始终有 FocusCharacter。  

---

## 21. ContentPackage／Mod Ready（P0）— 已冻结形状

详见 [`36-content-package-and-mod-architecture.md`](36-content-package-and-mod-architecture.md)、ADR-0013～0016。

- Mod 是正式长期目标；**当前只做架构，不写完整 Mod 系统／加载器。**  
- 官方与社区统一 ContentPackage；官方禁止专用硬编码加载路径。  
- 白名单 Condition／Effect；禁止任意 C# Mod。  
- 存档记录启用 ModId／版本／顺序／DataVersion／命名空间来源。  

---

## 17. 冻结清单速查

| 章节 | 级别 | 状态 |
|---|---|---|
| 1 总架构边界 | P0 | 冻结 |
| 2 Modifier 管道 | P0 | 冻结（公式见 2C） |
| 3 双层时间 | P0 | 冻结 |
| 4 实体／四层 | P0 | 冻结（见 34） |
| 5 Order／Action | P0 | 冻结（见 35） |
| 6 事件／账本 | P0 | 冻结（见 2E） |
| 7 地图四类 | P0 | 冻结 |
| 8 多队伍离屏 | P0 | 冻结 |
| 9 战斗框架 | P0 | 方向冻结 |
| 10 NPC AI | P0 | 方向冻结 |
| 11 军队边界 | P1 | 边界冻结 |
| 12 永久保存 | P0 | 冻结 |
| 13～15 炼气／突破／隐匿 | P1 | 方向／分层冻结 |
| 19 死亡与临时保护 | P0 | 冻结 |
| 20 势力／控制权／PlayerAgency | P0 | 冻结 |
| 21 ContentPackage／Mod Ready | P0 | 形状冻结 |

## 18. 下一步（不编码）

1. 人工审核本增量（§19～§21、`36`、ADR-0010～0016）。  
2. 细化第一次突破事件规格（`25`／`2G`）。  
3. 炼气基础术法最小清单（`22`）。  
4. 审核通过后再进入 Core 骨架编码。