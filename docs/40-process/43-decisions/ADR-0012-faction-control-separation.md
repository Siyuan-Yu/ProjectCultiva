# ADR-0012：势力归属、职位、关系与控制权分离

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（架构冻结增量）

## 背景

单一 `FactionId` 或 `IsPlayer` 无法表达客卿、俘虏、失势领袖、仍友好的前成员等状态。

## 选项

**A. 单一 FactionId + IsPlayer** — 实现快，语义塌缩。  
**B. 分离 FactionMembership／FactionRole／Relationship／ControlAuthority**  

## 决策

选 **B**。核心成员可离开；须可解释前兆，非无预兆抽奖。

## 影响

见 `34`、`27`、`28`、`2E`。
