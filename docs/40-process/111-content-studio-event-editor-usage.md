# 111 · 事件编辑器用法（EventEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/EventEditor/`  
> 编辑：`type = contentEvent`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

新建／改内容事件：正文、触发、地点／任务过滤、条件、选项与 outcomes。Host 以打断弹层呈现。

## 怎么打开

- VS：启动项目 `EventEditor` → F5  
- 或：`publish\EventEditor\EventEditor.exe`

## 字段

| 字段 | 含义 |
|------|------|
| body | 正文（不是 description） |
| trigger | manual／onArrive／onExplore／onQuestCompleted… |
| locationId／questId | 可选过滤 |
| once | 默认 true |
| conditions | 触发条件 JSON 数组 |
| choices | 选项 JSON 数组（id／text／conditions／outcomes） |

## 日常操作

1. 选事件或 **+ 新事件**（优先写入已有 `*event*.json`）
2. 编辑 body／trigger／地点  
3. 用 JSON 编辑 conditions／choices → **保存到磁盘**  
4. PackageBrowser 校验 → Unity Play 到触发点验证

## 注意

- 顶层条件字段是 **`conditions`**，不是 `triggerConditions`  
- `once: true` 时同会话不重复弹
