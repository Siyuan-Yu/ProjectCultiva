# 118 · 工区编辑器（WorkArea）

> 状态：已落地｜日期：2026-08-15
>
> 生命周期：**Active**（WorkArea 规则编辑长期保留；[ADR-0037 §6](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)）。未来「WorkArea 在世界哪里」可能由 FineEditor 的 Blueprint placement 表达；但规则属性编辑仍有正式消费者，**当前不得删除、也不是 Planned Removal**。
>
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
