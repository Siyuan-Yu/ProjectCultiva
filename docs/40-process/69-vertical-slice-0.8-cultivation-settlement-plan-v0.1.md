# Vertical Slice 0.8 Plan v0.1 — Cultivation & Settlement Simulation

> 状态：**已验收**｜最后更新：2026-08-01｜验收：[70](70-vertical-slice-0.8-acceptance-report.md)  
> 前置：VS0.7 已验收（[68](68-vertical-slice-0.7-acceptance-report.md)）  
> **不改 Freeze；不升 Snapshot schema；无战斗／大地图（VS0.9）。**

## 0. 目标

角色社会模拟 → 小型修仙势力经营：资源、初始据点、设施、弟子分工、生产／修炼日循环。

**完成标准：** 开局拥有可发展初始据点；资源／修炼／分工／关系可长期循环。

## 1. Phase

| Phase | 交付 |
|---|---|
| V8-0 | 本计划 |
| V8-A | Resource／Settlement 库存 Core＋Data |
| V8-B | Facility＋日终生产／修炼循环 |
| V8-C | WorkRole 分工＋AssignWork 命令 |
| V8-D | Scenario／Settlement 开局装配 |
| V8-E | Host HUD 薄展示＋验收测 |
| V8-F | 验收报告 |

## 2. Snapshot

据点／库存／分工 **默认不入档**（避免升 schema）。读档后据点状态不保证。

## 3. 硬停

Freeze／Snapshot 升版／Core·Data 边界／战斗·大地图探索／多据点税制等重大设计。
