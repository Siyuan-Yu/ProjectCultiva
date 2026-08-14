# 118 · 工区编辑器（WorkArea）

> 状态：已落地｜日期：2026-08-15  
> 真源：`Content/BaseGame/Data/WorkAreas`  
> 工具：`ExternalTools/ContentAuthoring/WorkAreaEditor`（`启动-WorkAreaEditor.cmd`）  
> 人物侧见 [119](119-npc-character-vs-role-template-editors.md)

## 边界

| 工具 | 管 | 不管 |
|---|---|---|
| **WorkAreaEditor** | 工区→逻辑地点、允许哪些活动 | 职业身份、具体某个人、作息时段 |
| **CharacterNpcEditor** | 能否做／优先级／偏好工区／可控制／出场 | 工区几何细节 |
| **MapEditor** | 某张图的空间与挡路 | NPC 倾向 |

职业式 `job_*`（农夫／樵夫）已废弃。运行时按：**人物倾向 → 选活动 → 在允许该活动的工区里按偏好／可用性选地点**；满或耗尽则换工区，再不行按优先级改做别的事。

## 用法

1. 双击 `启动-WorkAreaEditor.cmd`  
2. 编辑工区与 `allowedActivities`  
3. 保存后 Unity 重新 Play  

## 数据流

`activity` → 扫描 `workArea.allowedActivities` → `preferredWorkAreaIds` 排序 → `locationId` → Host 寻路
