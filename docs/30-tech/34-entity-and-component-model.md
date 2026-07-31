# 实体与能力模块模型

> 状态：**已冻结（对齐 Architecture Freeze v0.2）** | 优先级：P0 | 最后更新：2026-07-31  
> 依赖：`33-architecture-core-rules-freeze-v0.2.md`、`03-glossary.md`、`27`、`28`、`36`  
> 被引用：`35`、`2C`、`2E`、`32`、PlayerAgency、Core M1  
> **本阶段不写实现代码。**

## 1. 目标

定义正式逻辑层的领域对象形状：谁是实体、共享什么契约、Character 如何用组合成长、四层模拟如何升降级。作为 `XianXia.Core` 实体设计的唯一依据。

## 2. 总原则

1. 正式逻辑层使用**普通 C# 组合模型**，**不采用 Unity ECS**（见 ADR-0002）。
2. 顶层领域对象按玩法语义划分；禁止为了“统一”让所有实体继承无意义能力。
3. 内容配置**不能**动态创造未知代码组件；可选模块必须是项目预定义、可序列化、可单元测试的类型。
4. 逻辑引用禁止携带 Unity 表现对象。

## 3. 顶层领域对象

| 类型 | 职责 |
|---|---|
| `Character` | 可行动的个体（玩家修士、关键 NPC、已实体化的关注者） |
| `Building` | 建筑与设施（控制核心、工位所属建筑、仓库等） |
| `Settlement` | 据点／聚落（荒村、城镇区块等） |
| `Faction` | 势力（宗门、政权、妖族等） |

另有聚合对象（**不是**完整 Character）：

| 类型 | 职责 |
|---|---|
| `CultivatorPopulation` | 第三层：普通修士群体聚合 |
| `MortalPopulation` / `SettlementPopulation` | 第四层：凡人／据点人口统计 |
| `ArmyGroup` | 军队群体数据（见 `33` 军队边界；非核心玩法） |
| `Party` | 队伍编组（可跨区域） |

## 4. 薄 IEntity 契约

所有顶层实体共享极薄契约，**仅**包含：

| 字段 | 说明 |
|---|---|
| `EntityId` | 程序生成、全局唯一的实例 ID |
| `DefinitionId` | 人工维护、可读、稳定的定义 ID |
| `DisplayName` | 运行时显示缓存；**配置真源为 LocalizationKey**；绝不能充当 ID |
| `EntityTags` | 标签集合（身份、阵营、可交互类型等） |
| `LifecycleState` | 见 §5.4；Character 使用完整生命周期枚举 |

禁止把属性、背包、战斗、修炼塞进基类“以防万一”。

## 5. Character：组合式能力模块

### 5.1 禁止互斥继承树

禁止：

```text
❌ PlayerCharacter / NpcCharacter / CultivatorCharacter 互斥继承
```

同一个 `Character` 通过**获得能力模块**成长。凡人发现灵根后：**不替换对象**，只增加 `CultivationComponent`。

### 5.2 始终存在的基础部分

| 模块 | 职责 |
|---|---|
| `Identity` | 身份、标签、出身、显示信息引用 |
| `Attributes` | 属性容器 + AttributeModifier 挂载点 |
| `ActionState` | ActiveAction、OrderQueue、InterruptContext |
| `Location` | 逻辑位置（地图／格子／Instance 引用） |
| `Inventory` | 物品与数量 |
| `Lifecycle` | 生死与 LifecycleState；死亡保护配置引用 |
| `FactionMembership` | 当前势力归属与历史记录入口 |

### 5.3 预定义可选能力模块

| 模块 | 典型用途 |
|---|---|
| `CultivationComponent` | 修为、功法槽、灵力池、突破状态 |
| `CombatComponent` | 战斗属性派生入口、技能栏、交战状态 |
| `WorkComponent` | 工作熟练、当前工位预约 |
| `ScheduleComponent` | 身份时间表绑定与遵守状态 |
| `SocialComponent` | 对话、隐藏经历挖掘入口 |
| `TradeComponent` | 交易库存／价目引用 |
| `AuthorityComponent` | 配额、惩罚、管事权限（世界内职权，≠ PlayerAgency） |
| `RelationshipComponent` | **只读缓存／索引／UI**；关系真源为 RelationshipLedger（ADR-0017） |
| `FactionRoleBinding` | 当前职位（FactionRole） |

新增可选模块必须：登记术语表 → 写入本文白名单 → 可序列化 → 可测试。**禁止**配置表写“任意组件类名”由反射创建。

### 5.4 Character LifecycleState（v0.2）

```text
Alive → Incapacitated → Alive（Recovered）
                      → Captured | Missing | Dead
Removed  // 独立，≠ Dead
```

| 状态 | 含义 |
|---|---|
| `Alive` | 正常可行动 |
| `Incapacitated` | **不是死亡**；重伤、无法战斗／正常工作、等待处理（含战斗倒下） |
| `Captured` | 被俘 |
| `Missing` | 失踪 |
| `Dead` | **永久死亡** |
| `Removed` | 不再参与当前模拟（清理／离场）；**禁止**等同 Dead |

`Recovered` 表示从 Incapacitated 恢复为 Alive 的**结果**，不单独占长期枚举抢戏。

### 5.5 死亡与 TemporaryProtection

- 默认 `DeathProtectionMode = None`。  
- `IsStoryImportant` **不**推导 `CannotDie`。  
- `TemporaryProtection` 须含：原因、剧情阶段、解除条件、致命替代后果。  
- 详见 `33` v0.2 §13、ADR-0010／0019。

### 5.6 开局 Membership（v0.2）

| 对象 | Membership | Role | Control |
|---|---|---|---|
| 三名初始角色 | 压迫他们的宗门 | 杂役／劳役弟子 | 玩家 DirectControl |
| 主管 | 同一宗门 | 管理者 | 监督处罚（非玩家 DirectControl） |

---

## 5A. 势力归属、关系、职位与控制权

禁止：

```text
❌ IsPlayerCharacter 包办一切
❌ 单一 FactionId 同时表示归属、职位、友好、可否控制
```

| 概念 | 说明 |
|---|---|
| `FactionMembership` | 当前正式势力；可变更；离开保留历史 |
| `FactionRole` | 宗主／长老／执事／普通成员／客卿／俘虏／临时盟友／其他预定义 |
| `Relationship` | **真源在 RelationshipLedger**；Component 只缓存 |
| `ControlAuthority` | 动态权限查询结果；**不是**玩家身份类型 |

### 5A.1 核心成员离开

（同前：可解释前兆；离开保留关系历史于 Ledger。）

### 5A.2 PlayerAgency（v0.2）

```text
PlayerAgency
- FocusCharacterId
- FocusCharacterUnavailable
- ActiveControlMode   // Character | FactionLeadership
- ControlledEntityIds
- ManagedFactionId
```

**分离：** DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity。

Focus 不可用（重伤／被俘／失踪／暂不可行动）→ 置 `FocusCharacterUnavailable`，**不立即改变玩家身份**。  
有同行／代理／合法继承 → 继续；否则早期 GameOver，后期继承流程（ADR-0020）。

失去势力领导权：去掉势力管理，保留人物控制；旧势力 AI 继续。

---

## 6. 四层实体模拟

与 `33` §3 / `27` 对齐，补充升降级纪律：

| 层 | 对象 | 模拟 |
|---|---|---|
| 1 | 玩家直接控制约 30～50 名修士 | 完整 Character |
| 2 | 主管、商人、宗门人物、重要敌人等 | 完整 Character |
| 3 | 普通修士群体 | `CultivatorPopulation` 聚合 |
| 4 | 凡人群体 | `MortalPopulation` / `SettlementPopulation` 统计 |

### 6.1 禁止

- 第三、第四层偷偷创建数千个隐藏 `Character`。
- 为了“看起来热闹”在 Core 里全量个体化。

### 6.2 实体化条件（群体 → Character）

当成员被以下任一关注时，升级为完整实体：

- 玩家关注／点选命名／招募
- 发现灵根或进入修仙体系
- 参与重要事件
- 拥有关系网节点
- 拥有独特物品或伤病／任务／历史事件

### 6.3 归并规则（Character → 群体）

- **重要实体一旦**拥有名字、关系、修炼进度、库存、伤病、任务或历史事件，**不再归并**回群体。
- 只有完全普通、未被关注、无独特状态的临时实体允许归并。

## 7. 共享基础引用类型

| 类型 | 用途 |
|---|---|
| `EntityId` | 实例唯一 ID |
| `DefinitionId` | 定义表 ID |
| `EntityRef` | 对实体的稳定引用（含类型提示） |
| `LocationRef` | 地图／区块／格子／Instance 入口 |
| `SourceRef` | Modifier／效果来源 |
| `ReasonRef` | 人类可读原因／配置原因键 |
| `ModifierId` | AttributeModifier 实例 |
| `EventId` | DomainEvent 实例 |
| `OrderId` | Order 实例 |
| `ActionId` | Action 实例 |
| `Tick` | 逻辑时间点 |

### 7.1 逻辑层禁止引用

- `GameObject`、`Transform`、`Scene` 名称
- 显示名称充当键
- Unity 资源路径作为逻辑身份

Unity 层可维护 `EntityId → GameObject` 的表现映射表，**单向**，不得反向写回 Core 状态。

## 8. 与 Unity 边界

| Core | Unity |
|---|---|
| 拥有实体与组件状态 | 输入 → Order |
| 执行 Order／Action | 读取 StateSnapshot／ViewModel／DomainEvent |
| 禁止依赖 UnityEngine | 画面、动画、音效、镜头、导航表现、UI |

## 9. 仍待确定（不阻断形状冻结）

- [ ] Building／Settlement／Faction 的最小组件清单细则
- [ ] Party 与多队伍存档结构字段表
- [ ] 实体化时从群体抽样属性的算法
- [ ] ArmyGroup 与视觉代理数量上限的具体数
- [ ] TemporaryProtection 替代后果的第一批事件模板
- [ ] 后期继承流程的具体 UI／候选人规则（形状已冻：有继承则继续）
- [ ] Party 与 ControlledEntityIds 字段表

## 10. 验证方式（实现期）

- 凡人加 `CultivationComponent` 不更换 `EntityId`
- 配置无法反射创建白名单外组件
- Core 程序集编译失败若引用 UnityEngine
- 第三／四层压力测试不得出现“隐藏数千 Character”
- 剧情重要角色默认可死；仅显式 TemporaryProtection 挡致命
- Dead 存档后普通流程不能复活
- 失去宗主职位后 ManagedFactionId 清空且旧势力仍 Tick
- Membership／Role／Relationship／ControlAuthority 字段可独立变更
