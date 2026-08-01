# Content Ready Milestone Plan v0.1

> 状态：**已完成**｜最后更新：2026-08-02  
> 前置：VS1.0 架构阶段已验收（[74](74-vertical-slice-1.0-acceptance-report.md)／[75](75-vs0.7-to-1.0-delivery-summary-2026-08-01.md)）  
> 验收：[77](77-content-ready-milestone-acceptance-report.md)  
> **目标：策划可开始制作第一章内容的系统承载状态。不做第一章剧情、不做 Demo 包装。**

## 0. 完成标准

系统能承载（经 Data 定义，无需改 Core 规则代码）：

- 第一章任务（Quest）
- 第一章地点（含进入条件／资源／事件挂载）
- 第一章 NPC（既有 character＋spawn；本里程碑不扩大量配置）
- 第一章功法（既有 cultivation；补天赋成长挂钩）
- 第一章事件（内容事件：触发／选项／结果）
- 第一章成长流程（境界／功法／属性／天赋影响）

玩家核心循环可跑通：选角色 → 分配行为 → 探索地点 → 触发事件 → 修炼成长。

## 1. 不做

第一章剧情正文、大量 NPC、地图美术、战斗、正式 UI、编辑器工具。  
不改 Freeze；默认不升 Snapshot（Quest／内容事件／Flags **会话态**）。

## 2. 内部 Phase

| Phase | 交付 | 状态 |
|---|---|---|
| CRM-0 | 本计划 | ✅ |
| CRM-A | Quest 定义＋运行时＋完成／奖励／失败 | ✅ |
| CRM-B | ContentEvent 定义＋触发／选项／结果 | ✅ |
| CRM-C | Location 进入条件＋事件／Quest 挂载 | ✅ |
| CRM-D | 天赋→修炼／属性成长挂钩 | ✅ |
| CRM-E | 样例骨架数据＋核心循环验收测 | ✅ |
| CRM-F | 验收报告 | ✅ [77](77-content-ready-milestone-acceptance-report.md) |

## 3. 分层

- **Core：** QuestBoard／ContentEventBoard／Flags／规则校验与结算  
- **Data：** quests.json／content_events.json；扩展 worldRegion location 字段；加载映射  
- **Unity：** 本里程碑可不做新 UI；循环由 EditMode＋既有 Port 验证  

## 4. 硬停

Freeze／Snapshot 升版／Core·Data 边界／全新核心玩法方向。
