# 99 · Navigation Foundation Milestone 计划 v0.1

> 状态：**进行中**｜验收：[100](100-navigation-foundation-acceptance-report.md)｜日期：2026-08-03  
> 前置：Ch01 Reference／DemoParityHost；移动仍为直线插值  
> **目标：补齐 Demo 0.1 基础导航——可行走网格、A*、玩家／NPC 沿路径到达。**

## 0. 完成标准

1. 地图有可行走区域定义（网格＋障碍格）。  
2. Core 无 Unity 依赖的 Grid／A* 可测。  
3. 玩家点可达位置：角色沿路径到达（非穿障直线）。  
4. NPC 按 Schedule 在房屋／工区／资源点间移动（表现层）。  
5. 简单障碍检测＋多单位基础避让（软分离）。  
6. EditMode 测通过；验收报告落盘。

## 1. 不做

- 战斗寻路、大地图、复杂编队、飞行  
- NavMesh／Physics 依赖  
- Snapshot 升版（路径不入档）  
- 改 Freeze 规则（Core 仍禁 UnityEngine）

## 2. 分层

| 层 | 职责 |
|---|---|
| **Core.Navigation** | `WalkGrid`／`GridPathfinder`／Ch01 默认网格工厂 |
| **Host** | 重建网格、MoveController 沿航点、NPC 日程走位、避让 |
| **Docs** | 本计划＋验收＋Devlog |

## 3. Phase

| Phase | 交付 | Commit 前缀 |
|---|---|---|
| NAV-0 | 本计划 | `docs(nav)` |
| NAV-A | WalkGrid＋障碍＋Ch01 工厂＋测 | `feat(core): walk grid` |
| NAV-B | A*＋测 | `feat(core): grid A*` |
| NAV-C | Host 玩家移动接路径 | `feat(host): path follow move` |
| NAV-D | NPC Schedule 移动＋避让 | `feat(host): npc schedule move` |
| NAV-E | 验收报告＋Devlog＋飞书 | `docs(nav): accept` |

## 4. 坐标

与 `HostPresentationSpace` 一致：世界 XY＝PresentationX／Z；网格 cellSize＝1，覆盖 Demo 地砖 `[-40,-25]…[+40,+25)`。
