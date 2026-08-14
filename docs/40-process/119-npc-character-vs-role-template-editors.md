# 119 · 工区 vs 人物编辑器（无职业身份）

> 日期：2026-08-15

## Prefab 策略

**不要为每个 NPC 生成一份 Unity Prefab。** Host 共用 `EntityView`；差异来自 Content 数据。

## 正确分层

| 层 | 含义 |
|---|---|
| 人物 `activityCapabilities`／`activityPriorities` | 能否做、优先做什么（不是职业） |
| 人物 `preferredWorkAreaIds` | 劳作等地点偏好（有序；满／耗尽则换） |
| 工区 `allowedActivities` | 这地方能发生哪些活动 |
| 可控制 | `entityKind=character`／`playerControllable` → CharacterIds |

「三个爱砍树、三个爱种田」＝偏好工区＋劳作权重不同，**不是**挂樵夫／农夫 Job。

## 两个 exe

| 工具 | 数据 |
|---|---|
| `WorkAreaEditor` | WorkAreas |
| `CharacterNpcEditor` | Characters＋Scenarios.spawns（无 jobId）；只读显示关联 onTalk 事件 |

## 运行时

1. 闲时／日程给出活动（或按优先级回退）  
2. `ActivityResolver` 在可用工区中选点（偏好优先）  
3. 工区占用／资源耗尽钩子：`WorkAreaAvailability`（当前恒为可用，后续接）
