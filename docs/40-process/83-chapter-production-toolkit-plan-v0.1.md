# Chapter Production Toolkit Plan v0.1

> 状态：**已完成**｜最后更新：2026-08-02  
> 前置：Chapter Production Framework（[81](81-chapter-production-framework-acceptance-report.md)）  
> 验收：[85](85-chapter-production-toolkit-acceptance-report.md)  
> 规范：[84](84-chapter-content-naming-standards.md)  
> **目标：完善第一章内容生产环境；不写第一章剧情正文。**

## 0. 完成标准

1. **第一章内容模板**：Chapter／Day Beat／Quest Chain／Event Chain／Story Flag  
2. **内容验证工具**：JSON 交叉引用（Quest／Event／Flag／NPC／Location 等）  
3. **第一章测试 Scenario**：快速启动／跳日／验证任务链与事件链  
4. **内容规范文档**：ID／Flag／Quest／NPC／章节结构命名  

## 1. 不做

第一章剧情正文、地图美术、正式 UI、战斗、Snapshot 升版。

## 2. 内部 Phase

| Phase | 交付 | 状态 |
|---|---|---|
| CPT-0 | 本计划 | ✅ |
| CPT-A | Authoring 模板包 | ✅ |
| CPT-B | ContentReferenceValidator＋EditMode／Editor 入口 | ✅ |
| CPT-C | scenario_chapter1_harness＋shell chapter＋启动选项 | ✅ |
| CPT-D | 命名／结构规范文档 | ✅ [84](84-chapter-content-naming-standards.md) |
| CPT-E | 验收测＋报告 | ✅ [85](85-chapter-production-toolkit-acceptance-report.md) |

## 3. 硬停

Freeze／Snapshot／Core·Data 边界／全新核心玩法。
