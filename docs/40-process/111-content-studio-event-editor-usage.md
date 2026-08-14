# 111 · 事件编辑器用法（EventEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/EventEditor/`  
> 编辑：`type = contentEvent`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

新建／改内容事件：正文、触发、地点／任务过滤、条件、选项与 outcomes。Host 以打断弹层呈现。

## 怎么打开

- 推荐：`启动-EventEditor.cmd` 或 `Apps\EventEditor\EventEditor.exe`（先跑 `publish.ps1`）  
- 调试：VS 启动项目 `EventEditor` → F5

## 字段

| 字段 | 含义 |
|------|------|
| body | 正文（不是 description） |
| trigger | manual／onArrive／onExplore／onQuestCompleted／**onTalk**／… |
| locationId／questId | 地点／任务完成类触发的过滤 |
| **npcDefinitionId** | **onTalk：匹配被对话人物的 character id** |
| once | 默认 true |
| conditions | 触发条件 JSON 数组 |
| choices | 选项 JSON 数组（id／text／conditions／outcomes） |

## 对话发任务（不是人物表硬绑）

1. `trigger = onTalk`，`npcDefinitionId = base:character_…`  
2. 某选项 `outcomes` 含 `{ "kind": "startQuest", "id": "base:quest_…" }`  
3. 人物编辑器只读显示「关联 onTalk 事件」；任务本身不写 NPC 字段  

样例：`Events/ch01_reference_events.json` → 主管训话／催促。

## 日常操作

1. 选事件或 **+ 新事件**（优先写入已有 `*event*.json`）
2. 编辑 body／trigger／地点  
3. 用 JSON 编辑 conditions／choices → **保存到磁盘**  
4. PackageBrowser 校验 → Unity Play 到触发点验证

## 注意

- 顶层条件字段是 **`conditions`**，不是 `triggerConditions`  
- `once: true` 时同会话不重复弹
