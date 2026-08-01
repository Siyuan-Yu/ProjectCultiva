# Vertical Slice 0.2 验收报告

> 状态：**验收快照（只读总结，本文件不启动下一阶段）**  
> 日期：2026-08-01  
> 前置：VS0.1 已验收（`54`）；开工确认 `56-vertical-slice-0.2-pre-dev-confirmation.md`（已批准）  
> 计划草案：`55-vertical-slice-0.2-plan-v0.1.md`（第一阶段按确认报告收窄）  
> 相关提交：
>
> - `548a095` feat(core): vs0.2 phase a player input bridge  
> - `807196c` feat(core): vs0.2 phase b schedule driver  
> - `a31569c` feat(core): vs0.2 phase c player override  
>
> 测试门禁（Phase C 完成后）：EditMode **89/89 Passed**

---

## 1. VS0.2 目标回顾

VS0.2 验证的是**规则层**闭环，不是可玩产品壳：

```text
RTS 语义的 PlayerOrder 入口
  + 凡人／NPC Schedule 默认行为
  + Player Override（打断计划）
  + 最小规则后果（QuotaDeviation + DomainEvent）
```

冻结口径（来自开工确认）：

| 项 | 口径 |
|---|---|
| 控制 | RTS：选中角色 → 下令 → Order；禁止菜单式「今日行动」替代 |
| Schedule | NPC／凡人默认行为，**不是**限制玩家的菜单锁 |
| 第一阶段 | 仅规则层；无 Unity RTS UI、地图、战斗、主管 AI、ExposureRisk |
| 交付形态 | EditMode／调试 Port 调用即可验收；每 Phase 串行停等确认 |

相对 VS0.1：补上「玩家如何改行为」「无人下令时角色做什么」「打断计划后规则记什么」三条缺口；**不**把第一章完整流程或可玩场景当作本切片目标。

---

## 2. 已完成能力

### Phase A — RTS PlayerOrder 入口

| 能力 | 说明 |
|---|---|
| `IPlayerInputPort`／`PlayerInputPort` | Core 入站：`EntityId` + 命令意图 |
| `PlayerOrderFactory`／`PlayerCommandRequest` | 意图 → `Order(Source=Player)` |
| `LaborAction`／`RestAction` | 日常劳动／休息 Action；`OrderType.Labor|Rest` |
| `DailyTaskComponent`（初版） | 劳动进度计数挂载点 |

**验证点：** 玩家命令经 Port 入队并启动 Action；禁止测试／UI 直改组件旁路。

### Phase B — Schedule 默认行为

| 能力 | 说明 |
|---|---|
| `ScheduleDefinition`／`Block`／`Activity` | 日时段表（Labor／Rest） |
| `ScheduleComponent` | 实体绑定日程定义 |
| `ScheduleDriver` | 空闲且无 Player Order 时注入 `OrderSource.Schedule` |
| `ScheduleOrderFactory` | Schedule → Order |
| `OrderQueue` 优先级 | 入队时 Player 优先于 Schedule |
| Snapshot | 日程定义与实体绑定可恢复 |

**验证点：** 无玩家下令时角色按表自动进 Labor／Rest。

### Phase C — Player Override／QuotaDeviation／DomainEvent

| 能力 | 说明 |
|---|---|
| Override | `PlayerOrder > ScheduleOrder`；打断进行中的 Schedule Action |
| 原因标记 | `OverrideByPlayer` |
| Quota | `RequiredAmount`／`CompletedAmount`／`Deviation` |
| 偏差触发 | Schedule Labor 未完成被打断 → 累加 Deviation |
| 事件 | `ScheduleInterrupted`、`QuotaDeviationCreated` |
| Snapshot | Deviation／`ActiveOrderSource`／进行中 Action 可恢复 |

**明确未绑：** 主管、惩罚结算、关系系统。

---

## 3. 当前形成的完整循环

```text
角色（挂 Schedule + DailyTask）
  ↓
ScheduleDriver 注入 Schedule Order → Labor／Rest Action
  ↓
玩家经 PlayerInputPort 下达 PlayerOrder
  ↓
Override：取消／清空 Schedule 侧；原因 OverrideByPlayer
  ↓
Player Action 优先执行（ActionClock）
  ↓
规则后果：QuotaDeviation + DomainEvent（ScheduleInterrupted／QuotaDeviationCreated）
  ↓
Snapshot 往返后状态一致
```

这是**规则可证明**的循环，不是「点屏幕就能玩」的循环。

---

## 4. 当前不可玩部分

以下**故意未做**；现有实现不足以当作可玩垂直切片产品：

| 能力 | 缺口 |
|---|---|
| Unity RTS 输入表现 | 无镜头／点选／框选／多选下令产品壳；仅有 Core Port |
| 地图 | 无 LocalMap 实例玩法、格子世界 |
| 寻路／移动 | 无 Move Order／Action；无寻路 |
| 战斗 | 无战斗 Action／伤害／站位 |
| NPC 高级 AI | Schedule 是表驱动默认行为，无效用／目标选择／自主战术 |
| 主管系统 | 无主管实体、观察、训斥、愤怒条 |
| ExposureRisk | 确认中已列为延后；本切片未编码 |
| 关系／惩罚产品化 | Deviation 只记账与发事件，不驱动处罚流程 |
| Observe／OpportunitySite | 第一阶段收窄后未做 |

另：完整 Localization、Mods、编辑器工具、Demo 与本 Core 闭环的产品化接线均未作为 VS0.2 交付。

---

## 5. 架构观察

### 5.1 Order 优先级

| 现状 | 观察 |
|---|---|
| VS0.2 子集：`Player > Schedule` | 与 `21`／`35` 全表相比，缺紧急／战斗／待机等档位 |
| Override 在 `EnqueueOrder` 时同步打断 | 清晰；若引入「不可打断」Action，需要显式策略而非静默忽略 |
| `ActiveOrderSource` 记在 ActionState | 便于 Snapshot／调试；后续仲裁日志可复用 |
| 清空 pending Schedule Orders | 避免打断后队列里旧计划复活；相位切换规则仍须与 `35` 对齐另案 |

### 5.2 Schedule 扩展

| 现状 | 观察 |
|---|---|
| 定义 = 时段块 + Activity + 单次 Order 时长 | 适合凡人劳役日；不适合复杂目标栈 |
| Driver 仅在空闲且无 Player 时注入 | 正确保持「默认行为」语义；勿把 AI 塞进 Driver |
| Activity 仅 Labor／Rest | 扩展 Cultivate／Idle／WorkZone 绑定前需 Content 契约与冲突规则 |
| 日长依赖 `WorldTick.TicksPerDay` | 与内容表（96 刻）一致；跨日重置 Quota／Deviation 尚未定义 |

### 5.3 Quota 未来扩展

| 现状 | 观察 |
|---|---|
| 单实体日计数：Required／Completed／Deviation | 最小可测；未按任务类型拆分 |
| 偏差 = 打断时 RemainingTicks 累加 | 可复现；是否等价「产量缺口」需玩法 ADR |
| 不绑主管／关系 | 正确；下游应订阅 `QuotaDeviationCreated`，勿把处罚写进 LaborAction |
| 日切／周切／豁免 | 未做；扩展时避免在 Action 内硬编码处罚 |

### 5.4 Event 扩展点

| 现状 | 观察 |
|---|---|
| `ScheduleInterrupted` payload 含原因串 | 短期够用；枚举化 Reason 可减少字符串约定 |
| `QuotaDeviationCreated` payload 含 delta／累计 | 下游主管／UI 可先听事件 |
| 仍有多处 payload 字符串 | 事件种类增多后建议结构化 payload 或专用 DTO（ADR） |
| 与 Breakthrough 等并存 | DomainEventQueue 模式可继续承载，避免旁路日志当真源 |

---

## 6. 下一阶段建议（只规划，不编码）

**本文件批准验收后，仍不自动开工下一阶段。** 下列仅为候选方向，需单独确认范围与门禁。

| 优先级（建议） | 方向 | 说明 |
|---|---|---|
| 1 | 规则后果消费 | 让 Deviation／事件被「某条规则」读到（仍可不做完整主管 AI）：例如日切结算、简单训斥标记 |
| 2 | ExposureRisk 最小条 | 确认中已挂账的 0–100；与偷修／Observe 接口对齐后再动 |
| 3 | Observe／机会位点接口 | 概念层接口切片；不做地图表现 |
| 4 | Unity 调试 Host | 极薄 RTS 调试下令（非产品 UI），把 Port 接到 Scene |
| 5 | Schedule／Quota Content 化 | JSON／CSV 日程与日配额，摆脱测试内硬编码表 |

**明确不建议紧接着铺开：** 完整地图、寻路、战斗、NPC 高级 AI、主管完整系统、产品级 RTS UI。

---

## 7. 验收结论

| 项 | 结论 |
|---|---|
| Phase A／B／C 目标 | **达成**（规则层） |
| 完整循环（§3） | **可在 EditMode 证明** |
| 可玩产品 | **否**（见 §4） |
| 是否进入下一阶段 | **否**——停止于此；待新的确认／计划后再编码 |

验收签字栏（人工）：

- [ ] 目标回顾与交付一致  
- [ ] 不可玩边界无歧义  
- [ ] 同意「不自动进入下一阶段」  
|
