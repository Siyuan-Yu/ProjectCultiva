# ADR-0022：Core Milestone 1 范围冻结

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

## 背景

审计要求限制第一阶段实现，避免偷做多系统。

## 决策

**M1 只验证统一 Core 骨架：** Id、WorldTick、IRandomSource、ContentPackage 基础、Entity 基础、AttributeModifier、DomainEvent、Order／Action、Snapshot、单 Region。

**M1 不做：** 跨 Region 离屏、完整势力领导、真战斗、完整 NPC AI、Mods/ 加载、大地图战争。

## 影响

见 `33` v0.2 §17、`41-roadmap`。
