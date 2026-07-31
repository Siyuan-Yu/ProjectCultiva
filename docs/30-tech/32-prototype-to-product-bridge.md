# Prototype → Product Bridge（Demo 到正式开发桥接）

> 状态：**已冻结（v0.1）** | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 依赖：`33`、`34`、`35-order-and-action-system.md`、`2C`、`2E`、`49-demo-v0.1-prototype-status.md`、`31`  
> 被引用：实现期重构清单、回归验收  
> **目的：** 把 Demo 已验证玩法语义冻结为正式接口需求；后续只换实现，不改语义。  
> **本阶段不写实现代码。不继续扩展 Prototype MonoBehaviour。**

## 1. 桥接原则

1. Demo 是语义探路，不是正式架构实现。  
2. 已验证语义 = 接口契约。  
3. 禁止借重构偷偷改玩法；改语义必须先改文档。  
4. **项目已停止扩展 Demo**（突破／夺府／潜行判定等进入正式设计阶段）。  
5. 正式重构：**替换实现，保留玩法语义**；现有 Demo 作为行为参考与回归验证场景。

## 2. Demo 已验证语义（冻结为需求）

| 语义 | Demo 表现 | 正式不得丢 |
|---|---|---|
| 三人选择与移动 | 点选／框选／Shift；独立移动 | ≥3 可控实体，可扩至第一层上限 |
| 玩家命令优先 | 下令覆盖时间表倾向 | Order 优先级：玩家紧急／队列高于时间表 |
| 时间推进与暂停 | 暂停／1x／2x／5x | 映射 WorldTick + 倍速；暂停停全世界 |
| 时间表 | 全村劳役表 | Schedule；前期只读，夺权后可改 |
| 工作 | 右键／W → 工位 → Working → 产资源 | WorkOrder + WorkAction + 资源交易事件 |
| 资源与任务 | 木／粮／药与每日配额 | ResourceLedger + ObligationLedger |
| 修炼为独立行动 | 灵地入定 Cultivating | Cultivation Order/Action；与工作互斥 |
| 多角色同时不同任务 | 一人修、他人工 | 每实体独立 OrderQueue／ActiveAction |
| NPC 自主运转 | 守卫／主管日程；村民群体 | 层 2 实体日程 + 层 4 群体呈现 |
| 统一行动框架 | Move／Gather／Cultivate | Order → Action 链（**无公开 Intent 层**） |

## 3. Demo 明确未验证

第一次突破事件、真战斗伤害与技能、发现／追捕／潜行、夺府与管理权限、AttributeModifier 管道、正式 Tick、怀疑值／势力敌意独立层、Instance／Route、多队伍离屏。

## 4. Demo 类 → 正式概念映射

| Demo | 正式架构 |
|---|---|
| `GameClock` | `WorldClock`（Tick 推进）+ `ActionClock`（场景行动推进）+ 表现层显示 |
| `CharacterActionController` | `OrderQueue` + `ActionRunner`／ActiveAction |
| `PartyCommandController` | Unity 输入适配器 → 生成 Order（不改 Core 状态） |
| `DemoUnitController` | Unity 表现代理；逻辑位置在 Core `Location` |
| `UnitCultivation` | `CultivationComponent` + 状态值（修为／暴露） |
| `CultivationSystem` | Cultivation Order/Action + 环境 + 隐匿输入 |
| `ScheduleService` | `ScheduleComponent`／ScheduleSystem（可多身份） |
| `ScheduleComplianceTracker` | 义务遵守查询 → 可能产生 DomainEvent |
| `SupervisorAngerSystem` | WorldLedger／Obligation／Relationship（NPC 怀疑／态度）；**不**并入个人隐匿 |
| `WorkSystem`／`WorkSpot`／`WorkZone` | WorkOrder + ReserveWorkPoint／Gather Action + 配置产量 |
| 工作区资源增长 | WorkAction 结算 → `ResourceTransaction` DomainEvent → ResourceLedger |
| `ResourceInventory` | ResourceLedger（可追溯增减） |
| `DailyTaskSystem` | QuestAndObligationLedger + 每日结算 ScheduledEvent |
| `AmbientNpcActor`／`NpcScheduleConfig` | 层 2 Character + Schedule；配置 CSV／JSON |
| `VillageCrowdPresenter` | 层 4 群体呈现；Core 为 SettlementPopulation |
| `UnitOrderPathPreview`／飘字／威胁标 | 纯 Unity 表现；订阅快照／事件 |
| `DemoPrototypeHud` | 临时 IMGUI；正式 UI 另立 ADR-0009 |
| `ReplaceableSprite` | 表现资源约定；与 Core 无关；正式经 AssetId |
| `SpiritSiteZone` | Location／区域定义 + 修炼前置条件 |
| （无）Player 类型角色 | `PlayerAgency` + FocusCharacter + ControlAuthority |
| （无）剧情锁血 | 默认可死；`DeathProtectionMode`／TemporaryProtection |
| （硬编码表） | 正式一律 ContentPackage（含 BaseGame） |

## 5. 重构顺序（建议，仍不编码）

```text
1. asmdef：Core / Data / Unity / Tests
2. 基础类型：EntityId、Tick、IRandomSource、Definition 加载与校验
3. WorldTick + ActionClock 调度
4. AttributeModifier 管道
5. OrderQueue + ActionRunner（对齐 Gather／Move／Cultivate）
6. DomainEvent + ScheduledEvent + 最小 Ledger
7. 实体分层与群体组件
8. 用 Demo 场景做回归：三人分工劳动与偷修
9. 再实现第一次突破事件
```

每步验收：**旧 Demo 可复述的玩家操作，在新架构下行为一致。**

## 6. 接口职责摘要（签名实现期再定）

1. `IGameTime`：WorldTick、推进、暂停、倍速。  
2. `IModifierSink`：Add／按来源移除／查询 Final 与溯源。  
3. `IOrderBus`／`IActionRunner`：下达、插队、取消；生命周期见 `35`。  
4. `ISchedule`：查询当前时段活动。  
5. `IResourceLedger`：增减并记录原因。  
6. `IConcealment`：个人风险／NPC 怀疑／势力敌意三套 API。  
7. `IWorldLedger`：分册查询，禁止万能字典。  
8. `IStateSnapshot`：只读给 Unity。  

## 7. 相关文档

| 文档 | 作用 |
|---|---|
| `49` | Demo 玩法快照 |
| `33` | 架构主契约 |
| `34`／`35`／`2C`／`2E` | 实体、命令、属性、事件展开 |
| 本文 | Demo ↔ 正式映射 |

**当前阶段：架构冻结 — 完善契约，审核前不编码。**
