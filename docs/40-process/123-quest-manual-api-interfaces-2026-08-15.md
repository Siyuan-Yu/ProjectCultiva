# 123 · 功法任务条件／奖励接口（2026-08-15）

> 状态：**接口已落地；内容任务另开**｜日期：2026-08-15  
> 飞书：https://my.feishu.cn/docx/XPE5dGfcYoI5iSxuL8zco64QnBc  
> 用途：将老对弈／洞窟探索两条功法任务的 **条件与 outcome 契约**，先于内容装配。  
> SCHEMA：`Content/BaseGame/Data/SCHEMA.md` §quest  
> 相关设计：[122 突破仪式](122-cultivation-breakthrough-host-ritual-2026-08-15.md)

---

## 1. 本轮只做什么

只加引擎／SCHEMA／QuestEditor 字段，**不写**将老 NPC、洞窟事件 JSON。

---

## 2. 新增条件

| kind | 用途 |
|------|------|
| `counterAtLeast` | 对弈胜场等会话计数 ≥ |
| `missingDailyFlag` | 今日尚未做（可再对弈） |
| `hasDailyFlag` | 今日已做 |
| `encounterCleared` | 洞窟遭遇已清（flag `encounter:{id}`） |

已有可复用：`exploredLocation`／`hasManual`／`onTalk`＋`startQuest`。

---

## 3. 新增奖励／结果

| kind | 用途 |
|------|------|
| `addCounter`／`setCounter` | 胜场 +1／设值 |
| `setDailyFlag`／`clearDailyFlag` | 标记／清除「今日」 |
| `learnManual` | 领奖立刻学功法 |
| `setEncounterCleared` | 标记遭遇清除 |

---

## 4. 内容装配模板（下一步填）

**将老对弈：** 对话 `startMinigame`→Host 井字棋；胜 `addCounter`＋`setDailyFlag`；任务完成 `counterAtLeast`×3；奖励 `addStock` 秘籍；背包使用后选炼气队员 `LearnManual`。

**洞窟：** 探索／抉择后 `setEncounterCleared`；完成 `exploredLocation`＋`encounterCleared`；奖励 `learnManual`（或 `discoverSite`）。

---

## 5. 代码

`ContentCounterBoard`／`ContentDailyBoard`；`ContentConditionEvaluator`／`ContentOutcomeApplier`；Quest 进度条认 `counterAtLeast`；EditMode：`ContentQuestApiSliceTests`。
