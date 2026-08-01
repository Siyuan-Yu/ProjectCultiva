# Chapter Production Framework Plan v0.1

> 状态：**已完成**｜最后更新：2026-08-02  
> 前置：Content Ready 已验收（[77](77-content-ready-milestone-acceptance-report.md)）  
> 验收：[81](81-chapter-production-framework-acceptance-report.md)  
> 制作流程：[80](80-chapter-content-production-guide.md)  
> **目标：制作人可正式开始制作第一章——补齐生产框架，不写第一章剧情。**

## 0. 完成标准

1. **Chapter／Scenario 结构**：章节、计划天数、任务链、事件链、按日 beat  
2. **Story Flag**：事件结果可记录；条件可判断；后续内容可分支  
3. **Content Debug**：跳日、设 Flag、触发／弹出事件、查看角色与 Flag  
4. **制作流程文档**：人物／任务／事件／地点／章节怎么做  

## 1. 不做

第一章具体剧情、大量 NPC、地图美术、正式 UI、战斗、Snapshot 升版。

## 2. 内部 Phase

| Phase | 交付 | 状态 |
|---|---|---|
| CPF-0 | 本计划 | ✅ |
| CPF-A | Chapter 定义＋运行时＋日 beat／任务链 | ✅ |
| CPF-B | Story Flag 历史＋DomainEvent＋条件别名 | ✅ |
| CPF-C | ContentDebugService＋Host F3 面板 | ✅ |
| CPF-D | 内容制作流程文档 | ✅ [80](80-chapter-content-production-guide.md) |
| CPF-E | 骨架 chapter 数据＋验收测 | ✅ |
| CPF-F | 验收报告 | ✅ [81](81-chapter-production-framework-acceptance-report.md) |

## 3. 分层

- **Core：** ChapterBoard／ChapterService／ContentDebugService；Flags 增强  
- **Data：** `chapter` 类型／`chapters.json`；scenario.`openingChapterId`  
- **Unity：** Host 内容调试面板（非正式 UI）  
- **Docs：** 制作流程指南  

## 4. 硬停

Freeze／Snapshot 升版／Core·Data 边界／全新核心玩法。
