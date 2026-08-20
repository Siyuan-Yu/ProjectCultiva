# ADR-0018：WorldTick 为世界唯一时间轴；ActionClock 为行动持续时间

- 状态：**已采纳**（澄清并补充 ADR-0003）
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

## 背景

审计发现离屏 Action 推进时钟表述含糊，可能演化为两套世界时间。

## 决策

- **WorldTick**：世界唯一时间轴（日期／昼夜／季节／世界事件／ScheduledEvent）。  
- **ActionClock**：单个 Action 的 Duration 消耗。  
- WorldTick 推进驱动 ActionClock 扣减；ActionClock **不得**改变世界时间。  
- 禁止两套独立世界时间推进。

## 影响

见 `33` v0.2 §3、`35`、`21`。Core M1 不做跨 Region 离屏。

## 补充（2026-08-21）

战略接战／手动遭遇期间 **不推进** WorldTick，仍只有这一条世界时间轴（不是第二套时钟）。见 [ADR-0023](ADR-0023-manual-encounter-freezes-worldtick.md)。
