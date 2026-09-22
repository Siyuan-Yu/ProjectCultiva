# 事件、未来事件与世界账本（2E）

> 状态：**设计已冻结；runtime 部分实现；通用内容状态磁盘持久化 Proposed / Not Implemented** | 优先级：P0 | 最后更新：2026-09-22
> 依赖：`33` v0.2、`34`、`2C`、`2F`、`28`、ADR-0017  
> 当前实现与磁盘边界见 [247 系统现状总表／Proposal](../40-process/247-project-handoff-current-state-2026-09-18.md#proposal通用内容状态磁盘持久化尚未授权)。

## 0. 当前实现边界（2026-09-22）

- `DomainEvent` 流、内容 Flags、Quest／Chapter／ContentEvent／Counter／Daily 等 runtime board 已用于当前会话内的条件、结算与 UI；`ContentOutcomeApplier` 的 runtime 事务回滚用于一次结算的全有或全无，**不是磁盘存档**。
- `CaptureRuntime`／`RestoreRuntime`（存在于部分 board／事务辅助）只服务运行中回滚或局部事务；不能据此声称 `WorldSnapshot` 已保存相应系统。
- 当前 `WorldSnapshot` 已实际覆盖实体与关键组件、PlayerParty／Squad／世界空间、CharacterEncounter、背包、关系 ledger、随机状态，以及窄范围洞府 taken-loot 等字段。Separate Space 只有 DTO／capture helper，`JsonSnapshotSerializer` 尚未读写 `strategic.separateSpace`，不能声称磁盘 round-trip 已接线。
- 通用 Quest、Flags、ContentEvents、Chapters、ContentCounters、ContentDaily 尚未形成完整磁盘 round-trip authority。洞府 `loot:*` 的专用 taken-loot 保存不能推导为通用 Story／Content Flags 已保存。
- Snapshot restore 当前只运行 `RuntimeContentShellBootstrap`，未重注册 New Game 使用的 Quest／ContentEvent／Chapter definitions；后续不能只保存 runtime state，还须在不重跑 opening 结果的前提下恢复 definitions shell。
- 下一步只形成 Proposal：任务状态／期限、永久选择、已触发事件与章节、计数／每日限制，以及“待选择事件弹窗打开时禁止保存还是保存待处理身份”的规则。制作人尚未选择，也未授权实现。

## 1. 这个系统解决什么问题

把“刚刚发生了什么”“未来何时发生”“世界长期记得什么”分成三层，避免各系统私自倒计时，并支撑差异化：世界记得玩家行为、知识可按角色／势力分割。

## 2. 三层结构（已冻结）

| 层 | 概念 | 职责 |
|---|---|---|
| 1 | `DomainEvent` | 刚刚发生的事实 |
| 2 | `ScheduledEvent` | 未来要发生的事 |
| 3 | `WorldLedger` | 必须长期保留的结构化世界记忆 |

```text
Action / System 结算
  → 发布 DomainEvent
  → 更新 WorldLedger（若需要）
  → 可能登记 ScheduledEvent
  → Unity / 调试读取事件流与快照
```

## 3. DomainEvent

表达**已经发生**的事实。

### 3.1 最小字段

| 字段 | 说明 |
|---|---|
| `EventId` | 唯一 ID |
| `EventType` | 类型 |
| `Tick` | 发生 Tick |
| `ActorRefs` | 发起者 |
| `TargetRefs` | 目标 |
| `LocationRef` | 地点 |
| `Payload` | 结构化载荷 |
| `CauseEventId` | 因果链父事件（可空） |
| `CorrelationId` | 同一次流程关联（可空） |

### 3.2 示例类型

- `CharacterStartedCultivation`
- `CharacterWasObserved`
- `DailyQuotaFailed`
- `SettlementCaptured`
- `CharacterKilled`
- `CharacterIncapacitated`
- `CharacterCaptured`
- `CharacterWentMissing`
- `BreakthroughCompleted`
- `FactionMembershipChanged`
- `FactionLeadershipChanged`
- `CharacterLeftFaction`
- `ResourceGained`／`ResourceSpent`
- `ModifierApplied`／`ModifierRemoved`（可选，调试向）

## 4. ScheduledEvent

表达**未来**要发生的事。

### 4.1 最小字段

| 字段 | 说明 |
|---|---|
| `ExecuteTick` | 执行 Tick |
| `EventDefinitionId` | 定义 |
| `Context` | 上下文（实体、地点、参数） |
| `CancellationKey` | 取消键（同键可取消／替换） |

### 4.2 用途示例

- 敛息效果到期  
- 每日配额结算  
- 商人到达  
- 突破阶段推进  
- 延迟报复  
- 伤势恶化  

### 4.3 禁止

**禁止**每个系统自己维护独立倒计时字段作为唯一时间机制。  
短期表现层动画冷却可以在 Unity；**逻辑到期**必须进 ScheduledEvent 或统一 Tick 扫描表。

## 5. WorldLedger（分册，非万能字典）

禁止一个万能 `Dictionary<string,object>` 充当世界记忆。至少分册：

| 分册 | 内容 |
|---|---|
| `RelationshipLedger` | **关系唯一真源**（事件历史累积；最终值由本册计算） |
| `FactionLedger` | 势力态度、外交、宣战等 |
| `TerritoryLedger` | 所有权、控制核心、关键设施状态 |
| `QuestAndObligationLedger` | 任务、配额、义务 |
| `KnowledgeLedger` | 谁知道什么（见 §6） |
| `HistoryLedger` | 长期历史摘要（见 §7） |

## 5A. RelationshipLedger 唯一真源（v0.2）

见 ADR-0017。

本项目强调恩怨、历史、因果与长期关系变化；关系**不是**简单可直接赋值的最终数值。

Ledger 至少保存每条 `RelationshipEvent`：

| 字段 | 说明 |
|---|---|
| 时间 Tick | 何时发生 |
| 来源／原因标签 | 为何变化 |
| 影响对象 | 谁对谁 |
| 影响数值 | 如 +30／-50 |
| 关联 DomainEvent | 可追溯 |

示例：A 救 B → +30；B 叛 A → -50；**最终关系值 = Ledger 聚合计算**。

`RelationshipComponent` 仅：运行时缓存、查询优化、UI 展示。  
**禁止**直接修改最终关系值；**禁止**绕过 Ledger 写关系。

---

必须区分：

1. **世界事实是否发生**  
2. **哪个角色／势力知道**  
3. **知道程度**：`Known` / `Suspected` / `Unknown`

示例：

| 命题 | 世界事实 | 玩家 B | 守卫甲 | 主管 | 宗门 |
|---|---|---|---|---|---|
| 玩家 A 已炼气 | 是 | Known | Suspected | Unknown | Unknown |

## 6A. 数据事件白名单（Mod Ready）

初期内容／Mod 事件仅允许白名单 Condition／Effect（完整列表见 `36`）。  
禁止任意 C#；效果必须经 Order／Action、DomainEvent、AttributeModifier、Ledger。  

## 7. 隐匿三层与账本的关系

保持分离（与 `33` §6 / `2F` 一致）：

1. 个人隐匿风险  
2. NPC 怀疑值  
3. 势力敌意／关系  

三者可通过 DomainEvent 互相影响，**不能**合并成一个数值。  
怀疑值落在 NPC／Relationship；势力敌意落在 FactionLedger；个人风险落在角色状态值。

## 8. 存档目标（快照模型；不得与当前已接线范围混淆）

以下是冻结设计目标；当前实际接线范围以上文 §0 与 [247](../40-process/247-project-handoff-current-state-2026-09-18.md) 为准：

- 当前世界快照（实体、组件、资源、地图关键状态、LifecycleState）  
- 未执行 `ScheduledEvent`  
- 随机流状态（`IRandomSource`／分系统流）  
- 必须长期保留的重要历史（History／各 Ledger）  
- `PlayerAgency`（含 FocusCharacterUnavailable）  
- LifecycleState（Dead ≠ Removed）  
- 启用的 ContentPackage／ModId、版本、加载顺序、DataVersion、命名空间来源（见 `36`）  
- 最近一段调试事件日志（环形缓冲，可裁剪）  

**不**从开局重放全部 DomainEvent 恢复世界。

### 8.1 保证范围

只保证：

- 正常存档恢复  
- 固定 Seed 自动测试可复现  
- 关键事件可追踪  

不做完整游戏回放。

### 8.2 版本与 Mod

- 开发期允许旧存档失效，但破坏性变更必须升级 `SaveVersion`。  
- 正式大版本内尽量提供独立 `SaveMigration`／`DataMigration`。  
- 缺 Mod／版本不兼容必须警告；未知 DefinitionId 禁止静默删除。  

### 8.3 死亡与生命周期

- `Dead` 必须进入快照与 History；普通复活不得默认可用。  
- `Missing`／`Captured`／`Incapacitated` 与 `Dead` 语义不得混用。  
- 角色死亡可触发：任务失败／关系结束／传承结算／Knowledge 更新等 DomainEvent。  

## 9. 什么进入长期历史

只有会影响以下内容的事件进入长期 History／Ledger：

- 关系  
- 因果  
- 身份  
- 领地  
- 剧情  
- 传承  
- 势力态度  

普通采集每一次飘字不必进 HistoryLedger；可留在短日志。

## 10. 与 Modifier／Order 的衔接

- 长期属性影响：`DomainEvent` → 状态 → `AttributeModifier` → 到期 `ScheduledEvent` 移除（见 `2C`）。  
- 延迟报复：杀人 DomainEvent → 登记未来 ScheduledEvent → 到期生成追杀／态度变化。  

## 11. 仍待确定

- [ ] EventType 枚举第一批完整清单  
- [ ] History 压缩与归档策略（多少 Tick 裁短日志）  
- [ ] Knowledge 传播规则（对话、审讯、异象）  
- [ ] CancellationKey 冲突策略细则  

## 12. 验证方式（实现期）

- 系统中不存在“私有 float cooldown 决定逻辑到期”的泄漏（抽检）  
- 存档不含全量事件溯源重放路径  
- Knowledge 查询：同一事实对不同主体返回不同知道程度  
- 破坏性 schema 变更未升 SaveVersion 时 CI 失败
