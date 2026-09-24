# ADR-0040：Delayed ContentEvent authority 与 Snapshot v10

> 状态：Design Confirmed / Implemented / Producer Acceptance Pending | 优先级：P0 | 日期：2026-09-25
> 来源：制作人 DELAYED-EVENT-01 明确实施授权；不修改 Freeze 正文、不改变既有稳定 ID 或枚举数值。

## 决策

在 SimulationWorld 增加 ScheduledContentEventBoard。scheduleEvent 沿用 Id=ContentEvent DefinitionId、Amount=正整数游戏日；禁止参数字典、Graph Node、另造剧情 VM。
每次创建独立 scheduled-event:N，保存绝对 ScheduledAtTick/ExecuteTick、原 Actor/Target/Issuer EntityId、TargetKind/Key/DefinitionId/DisplayName、runtime 推导的 OpportunityInstanceId。WorldTick 是唯一时间轴，日期长度使用现有常量。

事务 memento 包含 pending 列表和 next sequence。成功进入既有 Active Event 后消费，并标记 ActiveScheduledInstanceId；跳过普通 RepeatAllowed，结束不写 global once。普通触发不变。

Host 只提供一个 safe-present 决策。战斗、Dialogue/Topic、Modal、restore/transition 不安全时整个 due instance 保留；安全后 Core 按 deadline/sequence 查原定义、原身份和原 Actor+Context 的 Event Conditions，失效或条件失败取消，不找替身。

Snapshot 由 v9 升至 v10，ContentProgress 新增 NextScheduledEventSequence/ScheduledEvents。v1～v9 明确拒绝；Capture/Serialize/Parse/Restore/definitions-only validation 贯通。Pending 即使来源已失效也可能在未来才到期，因此允许原失效身份被原样保存和恢复；现存身份错配拒绝，缺失/失效由到期 dispatcher 取消，绝不修复成新 NPC。
Active Event/Dialogue 的既有禁存规则保持，pending 不增加禁存范围。

## 取舍与边界

不做通用取消键、重试策略或 Knowledge 系统。WorldObject 复用 Opportunity、旗、破坏物与建造资产 authority；没有可判断失效的既有 authority 时仅保存稳定身份，不以 Host View 是否加载作为失效证据。
只保存 pending，不保存已呈现事件中途执行态；这与既有 Active 禁存边界一致。

## 产品方向

当前 DELAYED-EVENT-01；下一 SOCIAL-QUEST-01；之后 Full Trading → Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。
Knowledge / Rumor / Information Propagation：Future / Only if gameplay later proves it necessary。旧“KnowledgeLedger 已冻结/近期实施”的推断 superseded，future-not-authorized。

实现与人工验收见 [263](../263-delayed-event-01-content-event-scheduling-2026-09-25.md)。本 ADR 记录已授权决定，不表示制作人已完成运行验收或 Git 封板。
