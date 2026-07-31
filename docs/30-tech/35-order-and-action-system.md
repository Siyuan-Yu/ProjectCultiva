# Order 与 Action 系统

> 状态：**已冻结（v0.1）** | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 依赖：`33`、`34-entity-and-component-model.md`、`21-core-loop-and-time.md`  
> 被引用：`32`、战斗／工作／修炼系统、Unity 输入层  
> **本阶段不写实现代码。**  
> 公开概念只保留 **Order** 与 **Action**；**不再**额外增加公开 Intent 层。

## 1. 目标

统一“谁想做什么”与“怎么做成”的执行模型，使玩家、AI、时间表、事件脚本走同一套 Action 系统。

## 2. 两个公开概念

| 概念 | 含义 |
|---|---|
| `Order` | 角色**想做什么**（意图级指令） |
| `Action` | 角色**如何完成**（可推进、可序列化的执行单元） |

示例：

```text
GatherWoodOrder
  → MoveToAction
  → ReserveWorkPointAction
  → GatherAction
  → DepositResourceAction
```

原则：**玩家与 AI 的区别只在于谁生成 Order**；执行 Action 使用同一套系统。

## 3. Order 来源

- 玩家
- AI（效用／日程）
- 时间表
- 紧急反应
- 事件脚本

## 4. 每实体执行容量（第一版）

每个完整模拟 `Character` 拥有：

| 槽 | 说明 |
|---|---|
| `ActiveAction` | 当前正在执行的一个 Action |
| `OrderQueue` | 待执行 Order 队列 |
| `InterruptContext` | 中断原因、损失、检查点信息 |

**第一版不做多并行动作槽。**

被动恢复、环境效果、持续状态效果 **不是** 并行 Action；它们走 Tick 结算／StatusEffect／Modifier。

## 5. Order 优先级

数值越小越优先（示意）：

1. 玩家紧急命令  
2. 生存／战斗紧急反应  
3. 普通玩家队列  
4. 强制社会义务  
5. 时间表  
6. 自主需求  
7. 待机  

**优先级 ≠ 可执行性。** 高优先级 Order 仍必须通过 `CanStart`／Preconditions；失败必须返回明确原因。

## 6. 前置条件与失败

所有 Order／Action 必须检查，例如：

- 已重伤（`Incapacitated`）
- 正在不可中止突破阶段
- 被控制
- 无法到达
- 缺少资源
- 工位已被占用且不可共享

即使是玩家命令，也可以失败；Unity 必须展示 `ReasonRef`／失败原因，不得静默丢弃。

## 7. Action 生命周期

```text
Pending → Starting → Running ⇄ Paused → Completed
                              ↘ Failed
                              ↘ Cancelled
                              ↘ Interrupted
```

| 状态 | 含义 |
|---|---|
| Pending | 已创建未开始 |
| Starting | 进入开销／前摇 |
| Running | 推进中 |
| Paused | 全局或局部暂停 |
| Completed | 成功结束 |
| Failed | 条件失败／超时等 |
| Cancelled | 主动取消 |
| Interrupted | 被更高优先级打断 |

### 7.1 必须支持的操作

- `CanStart`
- `Start`
- `Advance`（由 ActionClock／Tick 驱动，见 `33` 时间模型）
- `Pause`／`Resume`
- `Cancel`
- `CreateResult`

### 7.2 可序列化

Action **必须可序列化**。存档后必须能继续：

- 移动
- 工作
- 修炼
- 突破
- 战斗动作

## 8. 中断规则

| 规则 | 含义 | 示例 |
|---|---|---|
| `FreelyInterruptible` | 随时中断，无额外损失 | 移动 |
| `InterruptibleWithLoss` | 可中断，损失约定进度 | 普通修炼损失当前周天进度；技能施法损失部分灵力 |
| `InterruptibleAtCheckpoint` | 仅检查点可安全中断 | 部分突破阶段 |
| `Uninterruptible` | 不可中断（或仅极端情况） | 特定突破关键段 |

其他示例：

- 采集：可随时中断；**已获得资源保留**
- 普通修炼：中断损失当前周天进度
- 突破：检查点中断或不可中断（按阶段配置）

## 9. 与时间模型的关系

- **WorldTick**：日程、世界事件、离屏计划、长期成长。  
- **ActionClock**：当前场景中移动、战斗、施法、采集过程、修炼动作过程。  
- 暂停／倍速统一影响全世界；玩家暂停则 Action 也暂停。  
细则见 `33` 时间章节。

## 10. NPC AI 如何使用本系统

采用：**时间表 + 效用评分 + 简单行动计划**。

- 不做完整 GOAP。  
- 不为每类 NPC 建完全独立的大型行为树。  

流程：

```text
时间表默认目标
  → 需求／危险／命令／事件修改效用权重
  → 选择目标
  → 生成有限 Order
  → 统一 Action 系统执行
```

守卫示例：默认巡逻 → 发现可疑则调查 → 受击自保／呼叫 → 生存需求过高时处理需求。

### 10.1 可解释性（强制）

调试界面必须能显示：

- 当前目标
- 候选目标评分
- 当前 Order
- 当前 Action
- 放弃原因

## 11. Unity 边界

```text
Unity 输入 → Order
Core 执行 Order / Action
Unity 读取 StateSnapshot / ViewModel / DomainEvent
```

Unity **不得**直接改 Core 的 ActiveAction／队列／属性。

## 12. 仍待确定

- [ ] Order 配置表字段与脚本事件生成 API
- [ ] 突破各阶段的 Interrupt 配置表
- [ ] 多队伍切换时离屏 Action 的 Advance 频率
- [ ] 战斗技能 Action 与移动 Action 的互斥细则（第一版仍单槽）

## 13. 验证方式（实现期）

- 同一 Gather 流程：玩家下令与 AI 下令最终 Action 类型一致
- 存档读档后 Running 的采集／修炼可继续且进度不丢约定字段
- 玩家紧急命令可打断时间表 Order，但不可跳过 Uninterruptible 突破段
- AI 调试面板能列出评分与放弃原因
