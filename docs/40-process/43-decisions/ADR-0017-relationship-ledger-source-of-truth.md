# ADR-0017：RelationshipLedger 为关系唯一真源

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

## 背景

审计发现 RelationshipComponent 与 RelationshipLedger 可能双写，破坏恩怨／因果可追溯。

## 决策

**RelationshipLedger 唯一真源。** 关系由事件历史累积；最终值由 Ledger 计算。Component 仅缓存／UI／查询优化，禁止直接改最终关系值。

## 影响

见 `33` v0.2 §7、`2E`、`34`、`28`。
