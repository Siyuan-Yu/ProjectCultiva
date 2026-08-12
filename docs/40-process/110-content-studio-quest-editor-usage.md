# 110 · 任务编辑器用法（QuestEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/QuestEditor/`  
> 编辑：`type = quest`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

新建／改任务：描述、`autoOffer`、接取／完成／失败条件与奖励。

## 怎么打开

- 推荐：`启动-QuestEditor.cmd` 或 `Apps\QuestEditor\QuestEditor.exe`（先跑 `publish.ps1`）  
- 调试：VS 启动项目 `QuestEditor` → F5

## 字段

| 字段 | 含义 |
|------|------|
| id／name／description | 标识与文案 |
| autoOffer | 条件满足自动接取 |
| offerConditions／completeConditions／failConditions | 条件 JSON 数组 |
| rewards／failResults | 结果 JSON 数组 |

条件／奖励在编辑器里用 **JSON 数组文本框**编辑，例如：

```json
[
  { "kind": "stockAtLeast", "id": "base:resource_grain", "amount": 1 }
]
```

常用 kind 见 `SCHEMA.md`（`storyFlag`、`exploredLocation`、`setFlag`、`discoverSite`…）。

## 日常操作

1. 左侧选任务，或 **+ 新任务**（优先写入已有 `*quest*.json`）
2. 改表单与 JSON → **保存到磁盘**
3. 用 PackageBrowser 校验 → Unity Play

## 注意

- 不要用 `autoAccept`／`objectives` 等非 SCHEMA 字段  
- 改 id 后同步章节链、地点 `questOfferIds`、事件 `questId`
