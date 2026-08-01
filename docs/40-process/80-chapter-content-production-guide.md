# 第一章内容制作流程指南

> 适用：Chapter Production Framework 已就绪后  
> 目标：制作人按本流程在 **Data JSON** 中制作第一章（不改 Core 规则代码）  
> 骨架样例：`base:chapter_scaffold_01`（非正式剧情）

## 0. 总原则

1. **内容在 Data，规则在 Core。** 新增人物／任务／事件／地点／章节 → 只改 `Content/BaseGame/Data/*.json`。  
2. **先骨架后正文。** 先挂 ID／条件／奖励／Flag，再填文案。  
3. **用 Story Flag 串分支。** 事件结果 `setFlag`／`setStoryFlag`，后续用 `storyFlag`／`hasFlag` 判断。  
4. **用 Content Debug 验收。** PlayableHost：`F3` 面板／`F4` +1 日；或跑 EditMode 验收测。  
5. **不做：** 地图美术、正式 UI、战斗、大量无关 NPC（第一章范围外）。

字段权威：`Content/BaseGame/Data/SCHEMA.md`。  
命名规范：[84](84-chapter-content-naming-standards.md)。  
模板：`Content/BaseGame/Authoring/Templates/`。  
校验：菜单 `XianXia/Content/Validate BaseGame Package`。  
第一章 Harness：`OpeningScenarioId = base:scenario_chapter1_harness`。

---

## 1. 制作人物（Character／NPC）

**文件：** `characters.json`  
**类型：** `character`

| 步骤 | 动作 |
|---|---|
| 1 | 新 `id`（`base:character_…`），写 `name`／`baseAttributes` |
| 2 | 填 `personalityTags`／`backgroundTags`／`talentTags`（天赋影响见 Core `TalentGrowthRules`） |
| 3 | 在 `scenarios.json` 的 `spawns[]` 挂上：主角 `entityKind=character`，路人 `npc`＋`recruitable` |
| 4 | 需要开局地点驻留：在 `world_regions.json` 地点上设 `residentNpcDefinitionId` |
| 5 | Host 开局检查：实体出现、标签正确；必要时 EditMode 断言 |

---

## 2. 制作任务（Quest）

**文件：** `quests.json`  
**类型：** `quest`

| 步骤 | 动作 |
|---|---|
| 1 | 定义 `id`／`name`／`description` |
| 2 | `offerConditions`：何时可接（地点／Flag／境界…） |
| 3 | `completeConditions`：完成判据（如 `exploredLocation`） |
| 4 | `rewards`／`failResults`：`setFlag`／`addStock`／`grantProgress`／`startQuest`… |
| 5 | `autoOffer=true` 或挂到地点 `questOfferIds`／章节 `dayBeats.questOfferIds`／`questChainIds` |
| 6 | Debug：`Dump` 看 `quests=…=Active/Completed`；或 Port `StartQuest` |

**任务链：** 写在章节 `questChainIds` 有序列表——前一任务 `Completed` 后自动 `TryStart` 下一环。

---

## 3. 制作事件（ContentEvent）

**文件：** `content_events.json`  
**类型：** `contentEvent`

| 步骤 | 动作 |
|---|---|
| 1 | `trigger`：`onExplore`／`onArrive`／`onQuestCompleted`／`manual` |
| 2 | `locationId` 或 `questId` 过滤上下文 |
| 3 | `conditions`：含 `storyFlag` 做分支门槛 |
| 4 | `choices[]`：`id`／`text`／`outcomes`（务必写 Flag 结果） |
| 5 | `once` 默认 true；调试可用 F3 **Force Present** |
| 6 | 章节可列入 `eventChainIds`（文档／计划链）；日 beat 可用 `contentEventIds` 弹出 |

玩家选择后：`ResolveContentChoice`；结果进 Flag → 驱动后续任务／日 beat。

---

## 4. 制作地点（World Location）

**文件：** `world_regions.json`（`worldRegion.locations[]`）

| 步骤 | 动作 |
|---|---|
| 1 | `id`／`name`／`kind`／`adjacentIds`／`presentationX/Z` |
| 2 | 探索产出：`resourceOnExploreId`／`Amount`；机缘：`opportunitySiteId` |
| 3 | 进入门槛：`enterConditions`（如须先探索村口） |
| 4 | 任务挂载：`questOfferIds` |
| 5 | Host：`Y` 旅行／`T` 探索验证；失败即检查条件与邻接 |

---

## 5. 制作章节（Chapter）

**文件：** `chapters.json`  
**类型：** `chapter`  
**开局绑定：** `scenarios.json` → `openingChapterId`

| 步骤 | 动作 |
|---|---|
| 1 | `plannedDays`、`questChainIds`、`eventChainIds` |
| 2 | `dayBeats[]`：按 `dayIndex`（相对开章日）设 `setFlags`／`questOfferIds`／`contentEventIds`／`conditions` |
| 3 | Scenario 填 `openingChapterId` |
| 4 | 验收：开局有 `ChapterActivated`；`F4` 跨日看 beat；Flag／任务链推进 |

**建议章节目录习惯（文案侧）：**

```text
Chapter N
  Day 0 beat → 开场 Flag／主线任务
  Quest chain → Q1 → Q2 → …
  Event chain → 关键节点（由探索／任务／日 beat 触发）
  Story flags → story:chN_* 命名空间
```

---

## 6. 推荐验收清单（每段内容）

- [ ] JSON 能被 ContentPackageLoader 加载（无未知字段）  
- [ ] EditMode 或 Host：接任务 → 完成 → Flag 出现  
- [ ] 事件选项后，后续地点／任务／日 beat 行为变化符合设计  
- [ ] `F3` Dump：chapter／flags／quests／角色境界与地点可读  
- [ ] 未误改 Freeze；未要求升 Snapshot（会话态即可）  

---

## 7. 快捷键（PlayableHost）

| 键 | 作用 |
|---|---|
| F1 | 原 HUD |
| F2 | 事件 Feed |
| F3 | **内容调试面板** |
| F4 | **跳 +1 日**（面板打开时） |
| Y／T | 旅行／探索 |
| 1–4／5–7／8–0 | 行动／社交／分工 |
