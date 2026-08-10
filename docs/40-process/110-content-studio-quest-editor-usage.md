# 110 · Content Studio · 任务编辑器用法

> 状态：**可用（Studio v0.1）**｜日期：2026-08-10  
> 编辑对象：`type = quest`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 这个编辑器干什么

新建／修改任务：描述、是否自动接取、接取／完成／失败条件、奖励与失败结果。字段与 `SCHEMA.md` 一致（**无** objectives 子结构）。

## 怎么打开

左侧 **任务**，或从总览点某 quest 的「打开任务」。

## 字段说明

| 字段 | 含义 |
|------|------|
| `id` | 全局唯一，建议 `base:quest_…` |
| `name`／`description` | 名称与说明（Host 任务提醒会读） |
| `autoOffer` | 条件满足时自动接取 |
| `offerConditions` | 何时可接 |
| `completeConditions` | 全部满足则完成 |
| `rewards` | 完成时 outcomes |
| `failConditions`／`failResults` | 失败线（可空） |

### 常用 condition.kind

- `storyFlag`／`hasFlag`／`missingFlag` + `id`
- `exploredLocation`／`atLocation` + 地点 id
- `stockAtLeast` + 资源 id + `amount`
- `questActive`／`questCompleted`／`hasManual`／`knowsSite`／`realmAtLeast`

### 常用 outcome.kind

- `setFlag`／`clearFlag`／`addStock`／`discoverSite`／`startQuest`／`grantProgress`／`relationDelta`

## 日常操作

1. 下拉选已有任务，或 **+ 新任务**（写入已有 quests 文件，优先匹配路径含 `quest` 的 JSON）。
2. 编辑条件／结果行：选 kind，填 id／amount。
3. **保存到磁盘**。
4. 到总览跑校验；Unity 重新 Play 验证 `autoOffer` 与完成跳转。

## 与第一章 Demo 的配合

参考 `ch01_reference_quests.json`：用 Flag 串阶段（如 `quest:ch01_ref_yard_done`），完成条件用勘察／库存，奖励里 `setFlag` 解锁下一环与事件。

## 注意

- 未知字段会被 Loader／校验拒绝；不要抄错成 `autoAccept`、`objectives`。
- 改 id 后记得同步章节 `questChainIds`、地点 `questOfferIds`、事件 `questId`。
