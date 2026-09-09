# ADR-0021：地图结构 World → Region → LocalMap

- 状态：**已采纳**（修订 ADR-0006 的对外表述）
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

> **⚠️ 2026-09-09 · [ADR-0031](ADR-0031-continuous-outdoor-world-surface-architecture.md) 部分 SUPERSEDE：** 本 ADR 历史正文中“普通 Outdoor 跨 Region 用 Route、且不做整大陆连续”的规则已被 Continuous Outdoor World Surface 目标架构取代。Region 概念本身未被删除；当前 Runtime 仍按本 ADR 的 LocalMap / transition 契约运行，直到另行获批迁移。

## 背景

`24`「三级结构／10 屏」与冻结「四类地图」表述冲突，需统一。

## 决策

冻结三层：**World／Region／LocalMap**。  
Region 内连续体验优先；LocalMap 用于洞／秘境等独立加载。跨 Region 用 Route，不做整大陆无缝。废弃冲突的旧尺度硬约束（统一 10 屏／1.5 屏作为规格）。

## 影响

见 `33` v0.2 §8、`24`。ADR-0006 仍然有效，本 ADR 统一命名与层级。
