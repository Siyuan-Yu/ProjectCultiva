# 事件、未来事件与世界账本（2E）

> 状态：**设计已冻结；runtime 部分实现；SAVE-01 Producer Accepted / Sealed** | 优先级：P0 | 最后更新：2026-09-24
> 依赖：`33` v0.2、`34`、`2C`、`2F`、`28`、ADR-0017  
> 当前实现与磁盘边界见 [247 系统现状总表](../40-process/247-project-handoff-current-state-2026-09-18.md#当前系统现状总表2026-09-22) 与 [257 SAVE-01](../40-process/257-save-01-content-progress-persistence-v1-2026-09-23.md)。

## 0. 当前实现边界（2026-09-23）

- `DomainEvent` 流、内容 Flags、Quest／Chapter／ContentEvent／Counter／Daily 等 runtime board 已用于条件、结算与 UI；`ContentOutcomeApplier` 的事务 memento 继续只负责一次结算回滚，磁盘持久化由独立 `ContentProgressSnapshotHelper` 负责。
- `WorldSnapshot.CurrentSchemaVersion` 为 7。必需的 `contentProgress.hasAuthority=true` 保存 Flags／History、全部 Quest runtime（含 Inactive／Active／ReadyToClaim／Completed／Failed）、ContentEvent fired keys、Chapter runtime、Counters、Daily marks 与 LocationLabor ticks／harvests。
- Active dialogue、当前 Step／Choice、Topic Selection 与 UI 状态不进入 Snapshot；`ContentEvents.HasActive` 时 `SnapshotService.CaptureJson` 返回“请先完成当前对话/事件后再保存。”完成原子交互后可正常保存。
- restore 先恢复动态 runtime，再由 `RuntimeContentShellBootstrap` definitions-only 注册 Quest／Event／Chapter 定义并校验恢复 ID；不执行 OpeningInventory、OpeningScenario、章节激活或 opening flag。New Game 与 Load 共用 `PlayableSimulationLoopFactory` 的四个 day handlers。
- v1～v6 缺少完整 Content Progress authority，统一明确拒绝并要求新开局。实体、空间、Encounter、Separate Space、WorldOpportunity、WorldActivity、背包、关系与随机等既有 Snapshot authority 保持原职责。

### SAVE-01 Content Progress Persistence V1（2026-09-23）

- Flags 为 authoritative replace，History 保序；Quest runtime 直接恢复而不重新 `StartQuest`；ReadyToClaim 不自动领奖，Completed／Failed 不复活。
- fired key 按现有稳定字符串完整 round-trip，覆盖 global、perTarget、perActorTarget；restore 后 Active Event 始终为空。
- Chapter 直接恢复 ActiveChapterId、start day 与 applied beat keys，不调用会清 beat 的 `Activate`；definitions rehydrate 后验证 active chapter 和已应用 day beat。
- Counter／Daily／LocationLabor 使用稳定 key/value authority；Daily 保存实际 marked day index，LocationLabor 保存 opaque composite key，serializer 不拆解。
- EVENT-02 提供两条实际 Content 链：带 publicNotice／定位的临时行商是主验收入口，受伤散修为第二条示例；两者都由接受 Choice 的同一 outcome transaction 设置 accepted flag 并 `startQuest`，拒绝无 Outcome。Active Event 以统一 Player Accessible Stock 判断灵药：在可访问己方战略物资网络时，resource 为 PartyInventory＋合格 WorldSitePublicStock；离开网络时只看 PartyInventory；非 resource 永远只看 PartyInventory。`removeStock` 使用相同语义并与 hand-in flag 原子结算。
- 完整实施、验收路线与最终封板记录见 [257](../40-process/257-save-01-content-progress-persistence-v1-2026-09-23.md)。制作人已于 2026-09-24 确认当前内容验收通过，状态 **Producer Accepted / Sealed**。

### EVENT-02 World Opportunity Director V1（2026-09-23）

- `WorldOpportunity` 是 Continuous Surface 上动态生成的真实 NPC 机会，不等于 legacy `OpportunitySite`。Director 只补玩家当前 Surface，并以 Surface 当日首次 Tick 为随机刷新门禁；其它 Surface 只处理已有实例到期。
- 实例持有稳定 ID、模板、Surface、真实 EntityId、创建日与到期日。该 authority 最初以 Snapshot v6 additive optional 字段落地，SAVE-01 升至 v7 后字段与恢复语义保持不变。
- onTalk 的 `npcDefinitionId`、`npcTags[]`、`worldOpportunityId` 均为可选 AND binding，直接检查当前 TargetEntityId；三者为空仍允许 global contextual Event。
- V1 只接受 `worldVisible` 与 `publicNotice`，只生成 NPC。hidden 与动态 WorldObject 均未实现。详见 [255](../40-process/255-event-02-world-opportunity-director-v1-2026-09-23.md)。

### EVENT-02A Persistent World Activity Feed（2026-09-23）

- `WorldActivityBoard` 是已获知世界机会的持久活动动态，不是 QuestBoard、ContentEvent 或通用通知中心；V1 只接 `WorldOpportunity publicNotice`。
- publicNotice 生成时以 Opportunity InstanceId 为 SourceId 创建 unread Active Activity，并额外播放一次 Toast；Snapshot restore 只恢复状态，不重播 Toast。worldVisible 不创建 Activity 或 History。
- Activity 随 source Opportunity 到期、死亡或 Removed 转入 History；History 最多保留最近 100 条。可公开准确位置的条目只能在同一 Surface 聚焦现有 Gameplay camera，不移动 PlayerParty、不导航、不切 Surface。
- `worldActivityRuntime` 保持既有 additive authority；SAVE-01 升至 v7 后未改变其 shape。Active Activity 缺少对应 Opportunity Instance 时仍严格判为 `SnapshotInvalid`。详见 [256](../40-process/256-event-02a-persistent-world-activity-feed-2026-09-23.md)。

## 0.1 EVENT-01 Final 已批准实施契约（2026-09-22）

本轮制作人授权在现有 ContentEvent／ContentOutcome transaction／HostDialogue／EventEditor 上扩展互动事件与多步骤对话。旧 95／96 的“多段对话树不做”是当时切片边界，本次是后续扩展，不是历史遗漏的 bug。

- 主动 onTalk 与 onInspect 共用 `ContentInteractionContext` 和同一 `ResolveInteractionCandidates`：Trigger → target binding → repeat gate → Conditions → 最高 Priority；唯一候选直接 Begin，同级多个由 TopicText（Name／短 Id 回退）选择，按 Id 稳定排序。话题选择可取消，正式事件必须正常 Finish，不自动串播候选。
- interaction 条件仅在专用 helper 中放宽：Actor 属于 PlayerParty 时，须存在一名有效成员满足整组条件；不拼接不同成员的条件。不改变全局 AllPass，也不改变实际 Actor 或 Outcome subject。
- `ContentInteractionContext` 固定记录 Actor、可选 Target EntityId、TargetKind、稳定 TargetKey、目标定义／实例 ID 和显示名。NPC TargetKey 保持原数字 EntityId payload；WorldObject TargetKey 为 `<kind>:<stable-id>`。once=false 可重复；once=true 缺省 onceScope=global；perTarget／perActorTarget 统一按 TargetKey。active context 与 fired 仍只在 session runtime／事务回滚，不接 Snapshot。
- Step 为 Id／SpeakerRef／Text／NextStepId／Outcomes／Choices。SpeakerRef 空为旁白，@actor／@target 为绑定实例，显式人物 DefinitionId 必须合法且 runtime 可唯一解析。
- 无 Choices 时“继续”原子应用 Step.Outcomes 后进入 NextStep 或 Finish；有 Choices 时最终复核条件，原子应用 Step.Outcomes 与 Choice.Outcomes，再按 Choice.NextStepId 前进或 Finish。失败不前进、不记 fired。只有 Finish 发布 ContentEventResolved；Choice.Text 不自动当玩家台词。
- Choice 支持 disabled（默认，显示 RequirementText，缺省“条件未满足”）／hidden。Step graph 禁止环、无效引用、重复 Id、mixed legacy authority；有 Choices 的 Step 禁止 Step.NextStepId。startMinigame 只允许 terminal choice。
- 无 Steps 的旧 body／choices 映射为隐式单 Step；onTalk 默认 @target，其他 trigger 旁白；源 JSON 不迁移。非 onTalk 保持既有 trigger 仲裁与 Interrupt，但共用 Step runtime。
- onInspect 使用 `worldObjectKind` 加可选 `worldObjectId`；空 ID 匹配同类，非空匹配 exact stable instance。Host 只对有候选的对象显示“调查”，远处先用既有移动接近；抵达后按稳定身份重新解析对象并重新跑 eligibility，正式打开 Event 时才暂停。查看详情／恢复／工作／攻击／拆毁等原行为保留。
- EventEditor 新事件默认 Steps，中文列表＋表单复用 Shared 条件／结果编辑器；Trigger 切换 onTalk 人物绑定或 onInspect 对象 kind／可编辑稳定 ID 下拉，旧事件保留兼容编辑区。EVENT-02 随机 World Opportunity Director、ContentIntent／跟随演出与剧情 runtime persistence 不在 EVENT-01。

## 0.2 EVENT-EDITOR-V2 Authoring 边界（2026-09-23）

- Event 是独立 canonical Template；按人物／世界物体浏览只投影现有 binding，不把 Event ownership 移入 NPC/Object。
- Visual Flow Graph 只编辑当前 `entryStepId + steps[] + nextStepId`，节点类型只有 Step。Choice 仍是 Step 内的选项行；Condition/Outcome 仍是结构化字段，不升级为 Graph Node。
- Runtime 不新增 Graph/Dialogue VM/Branch schema。外部旧包的 legacy `body/choices` reader 保留；BaseGame active ContentEvent 已全部迁移为 Steps，EventEditor 不再长期编辑双 authority。
- Editor-only layout 位于 `Content/BaseGame/Authoring/EventEditor/layouts.v1.json`，不在 Runtime Loader 扫描的 `Data` 根下，Snapshot 与正式 Content authority 均不依赖坐标。
- Working copy、dirty、undo/redo 和保存前 validation 属 authoring transaction；不改变 `ContentOutcomeApplier` 的 runtime transaction。
- 本节记录的是 EventEditor V2 封板当时的 authoring 边界；EVENT-02 后续已实现 NPC Opportunity 与 tag/template binding，但仍未增加 Graph node 或动态 WorldObject。

### V2.1 Graph-first 制作面

- Graph 是常用对话制作主界面：Step 节点内直接选择 Speaker、编辑多行正文，Choice 行内直接改文字并从各自输出口拖线；Inspector 保留 Condition、Outcome、RequirementText、hidden/disabled、Event 设置与技术字段。
- 每个 Step 左侧显示纯编辑器 input port，右侧 Next/Choice output port 以 Bezier 箭头连接。连接可选中、Delete 或右键断开；拖线期间目标 input 高亮，释放到节点正文同样可连接，自连接会被拒绝。
- output 释放到空白处只弹出创建菜单，不先改变 working copy；可选择玩家、互动对象、旁白或指定人物，创建后自动连接、定位并聚焦正文。空白画布右键也可创建 Step。
- 自动布局仍是简单 Left → Right DAG 排列，但纵向间距会估算正文与 Choice 数量；“专注模式”仅折叠两侧 authoring panel。上述能力只编辑既有 Steps/Next 与 editor-only layout，不增加 Runtime 字段。

### V2.2 Dirty 与对话分类

- EventEditor 的 dirty 以当前 Working JSON＋Graph layout 与 clean baseline 的真实差异为准；打开、选择、pan/zoom 不产生 dirty，正文或持久化 layout 改动产生 dirty，改回原值或 Undo 回 baseline 后自动恢复 clean。新建未保存 Event 在首次保存前始终 dirty。
- “保底对话”只用于 `onTalk + 具体 NPC binding + priority 0 + repeatable + Event conditions 为空`。V2.4 已取消“状态对话”等推导类型；其它 Event 只显示 Priority、条件数量与 repeat scope 等事实。
- 单 Step Event 可以完整表达“一次 NPC 发言＋玩家 Choice＋结束”，不按中文句号机械拆 Step；Step 是流程单位而不是句子单位。

### V2.3 Editor Working Document canonicalization

- Runtime 仍允许省略默认字段；EventEditor 打开文档时只在内存 working copy 中补成 canonical representation，再建立 clean baseline。仅打开不会写回磁盘。
- canonical defaults 覆盖 Event 的 name/trigger/priority/topicText/once/onceScope/conditions、Step 的 speakerRef/text/outcomes/choices，以及 Choice 的 text/conditions/outcomes/nextStepId/unavailableMode/requirementText；可选 binding/location/quest 空字符串继续按 sparse semantics 移除。
- Graph layout 的 prune/default-node 补齐发生在 Session baseline 之前；纯 Graph refresh、选择、缩放不再调用会修改 layout 的 `Prune/GetOrCreate`。

### V2.4 显式保底对话

- Event Inspector 的“作为该人物的保底对话”是现有字段组合的 authoring 开关，不是 Runtime 字段。开启时固定 onTalk、Priority 0、可重复、Event Conditions 为空，并锁定这些控件；取消后切为普通 Event 编辑，默认 Priority 10。
- 同一 NPC 最多一条满足保底事实的 Event；新建/勾选第二条会提示并可跳转现有保底，保存验证也会报告旧包中的歧义。
- Browser 只给保底加“★ 保底”，其它 Event 使用原 Name/Topic，并以 `[Pxx]`、`[条件n]`、`[一次/每目标一次/每角色×目标一次]` 展示真实配置。

### V2.5 人物 Definition 选择

- EventEditor 的 NPC binding 与指定 Speaker 仍以稳定 Character DefinitionId 为 gameplay identity。Authoring UI 从当前已加载 ContentPackage 的 `character` definitions 构建可读目录，显示中文名、ID 与相对包根目录的来源文件，并支持三字段搜索和来源筛选。
- 来源路径只用于制作视图，不进入 `ContentEvent.npcDefinitionId`、`speakerRef` 或 Runtime Registry；人物移动 JSON 文件后，既有 Event 引用仍按 DefinitionId 生效。

### V2.6 Repeat／Once／Fallback 与全局人物来源

- Repeatable 只跳过 fired gate，仍须满足 Trigger、Binding、Conditions、交互资格与 Priority arbitration；完成 Repeatable Event 不写 fired state。Once Event 仅在真正完成时按 global／perTarget／perActorTarget scope 写 fired state。
- 保底仍是 `onTalk + P0 + once=false + 0 Event Conditions` 的 authoring shortcut，不是 Runtime 类型；只在没有更高 Priority 合法候选时进入最高 candidate layer。
- EventEditor 顶部“人物来源”是全局、纯 authoring filter。它统一约束按对象人物 Browser、Event NPC、New Event NPC 与指定 Speaker 的候选，但不改当前 binding、不进入 Content/manifest/Runtime，也不产生 dirty。

### V2 Final Patch 与封板

- 最后一次人物来源按规范化 Package Root 保存在 editor-local `%LOCALAPPDATA%\XianXia\EventEditor\settings.json`；仅恢复当前 Package 仍存在的 source，失效值回退“全部来源”。该设置与 Content、layout、Runtime、Undo/Redo、dirty 完全分离。
- 制作人于 2026-09-23 完成 EventEditor V2 实际验收，当前状态 **Producer Accepted / Sealed**。后续 EVENT-02 扩展 NPC Opportunity 与通用 onTalk binding；SAVE-01 已实现通用剧情 runtime persistence。ContentIntent 与新 Runtime event framework 仍未实施。

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
