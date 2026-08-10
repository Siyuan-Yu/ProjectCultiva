# 111 · Content Studio · 事件编辑器用法

> 状态：**可用（Studio v0.1）**｜日期：2026-08-10  
> 编辑对象：`type = contentEvent`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 这个编辑器干什么

新建／修改内容事件：正文、触发方式、地点／任务过滤、一次是否、触发条件、玩家选项及每个选项的 outcomes。Host 会以打断层（CIF）弹出选项。

## 怎么打开

左侧 **事件**，或从总览点某 `contentEvent` 的「打开事件」。

## 字段说明

| 字段 | 含义 |
|------|------|
| `id`／`name` | 唯一 id、显示名 |
| `body` | 剧情正文（不是 `description`） |
| `trigger` | `manual`／`onArrive`／`onExplore`／`onQuestCompleted`／`onQuestFailed` 等 |
| `locationId` | 可选；到点／勘察过滤 |
| `questId` | 可选；与任务完成等联动 |
| `once` | 默认 true，只触发一次 |
| `conditions` | 触发前条件 |
| `choices[]` | `id`／`text`／`conditions`／`outcomes` |

## 日常操作

1. 选事件或 **+ 新事件**（写入路径含 `event` 的 JSON，如 `ch01_reference_events.json`）。
2. 写好 `body` 与 `trigger`；需要地点时在下拉里选 `locationId`。
3. 配 `conditions`（常与任务 Flag 对齐）。
4. 为每个 choice 写文案与 `outcomes`（`setFlag`、`discoverSite`、`relationDelta`…）。
5. **保存到磁盘** → 总览校验 → Unity Play，走到触发点看打断弹层。

## 建议

- `onExplore`：首次勘察该地点时弹（如灵泉异响）。
- `onArrive`：抵达即弹（如砍柴老人）。
- `manual`：开局或脚本主动推（如晨点说明）。
- 选项分支用不同 Flag，任务 `completeConditions` 认其中任一故事 Flag。

## 注意

- 正文字段是 **`body`**，不是 `description`。
- 触发条件字段是顶层 **`conditions`**，不是 `triggerConditions`。
- `once: true` 时同一存档不会重复弹；调试可清会话或换 Flag。
