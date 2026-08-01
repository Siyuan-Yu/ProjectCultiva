# 第一章内容命名与结构规范

> Chapter Production Toolkit｜与 [80 制作流程](80-chapter-content-production-guide.md)、`SCHEMA.md` 配套  
> 目标：大型章节内容可协作、可校验、可检索

## 1. DefinitionId 总规则

格式：`namespace:local_id`（BaseGame 的 namespace 固定为 `base`）

| 类型 | 前缀 | 示例 |
|---|---|---|
| 章节 | `chapter_ch{NN}_` | `base:chapter_ch01_shell` |
| 开局 Scenario | `scenario_` | `base:scenario_chapter1_harness` |
| 任务 | `quest_ch{NN}_` 或 `quest_` | `base:quest_ch01_harness_arrive` |
| 内容事件 | `event_ch{NN}_` 或 `event_` | `base:event_ch01_harness_ping` |
| 地点 | `loc_` | `base:loc_village_edge` |
| 角色／NPC | `character_` | `base:character_village_recruit` |
| 功法 | `cultivation_` | `base:cultivation_qingyun_manual` |
| 机缘点 | `site_` | `base:site_abandoned_cave` |
| 资源 | `resource_` | `base:resource_spirit_herb` |
| 据点／设施 | `settlement_`／`facility_` | `base:settlement_qingshi_cave` |

规则：

- `local_id` 使用 **小写＋下划线**，禁止空格与中文。  
- 章节相关内容优先带 `ch01`／`ch02`… 前缀，便于过滤。  
- 显示名（`name`）可用中文；**逻辑只认 id**。

## 2. Story Flag 命名

| 前缀 | 含义 | 示例 |
|---|---|---|
| `story:` | 章节剧情／进度旗标 | `story:ch01_day0_started` |
| `quest:` | 任务完成／阶段结果 | `quest:ch01_harness_arrive_done` |
| `event:` | 事件选项结算结果 | `event:ch01_harness_resolved` |
| `explored:` | **运行时**探索写入（勿手写生产） | `explored:base:loc_village_edge` |

规则：

- Flag **必须有生产者**（quest／event outcome 或 dayBeat.`setFlags`）；校验器会检查。  
- 分支旗标：`story:ch01_branch_<slug>`。  
- 禁止无前缀的裸字符串（除历史兼容骨架）。

## 3. Quest 命名与链

- Id：`base:quest_ch01_<slug>`  
- 完成奖励至少写一个 `quest:ch01_<slug>_done`（或等价）Flag，供链上下游使用。  
- **任务链**写在 chapter.`questChainIds`（有序）；前序 `Completed` 后自动接下一环。  
- `autoOffer`：开局杂项可用；主线更推荐 dayBeat／chain／事件 `startQuest`。

## 4. Event 命名与链

- Id：`base:event_ch01_<slug>`  
- 每个可结算选项必须写入结果 Flag（`event:` 或 `story:`）。  
- `eventChainIds`：制作计划清单；真正触发靠 `trigger`＋条件或 dayBeat.`contentEventIds`。

## 5. NPC／人物

- Id：`base:character_<slug>`  
- Scenario `spawns`：`entityKind=character|npc`；可招 `recruitable=true`  
- 驻地：`world_regions.locations[].residentNpcDefinitionId`

## 6. 章节结构（推荐）

```text
chapter_ch01_*
  plannedDays
  questChainIds[]     → Q1 → Q2 → …
  eventChainIds[]     → 关键事件列表
  dayBeats[]
    dayIndex 0        → setFlags + 主线第一环
    dayIndex N        → 条件（storyFlag）解锁中段
```

配套文件建议：

```text
Data/
  chapters.json / chapter1_*.json
  quests… / content_events…
Authoring/Templates/   ← 从模板复制再改
```

## 7. 校验与测试

1. 保存 JSON 后：菜单 `XianXia/Content/Validate BaseGame Package` 或跑 EditMode。  
2. Harness：`PlayableDayOptions.OpeningScenarioId = "base:scenario_chapter1_harness"`。  
3. Host：`F3`／`F4` 查 Flag／跳日。
