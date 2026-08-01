# BaseGame Data Schema (Data Pipeline M1-A / VS0.7–1.0 / Content Ready / Chapter Production)

Runtime format: **JSON only** (CSV is authoring input via M1-B importer; not runtime).

## File layout

```text
Content/BaseGame/
  manifest.json
  Data/
    characters.json
    cultivation.json
    items.json
    sites.json                 # opportunitySite
    scenarios.json             # openingScenario
    resources.json             # VS0.8
    facilities.json            # VS0.8
    settlements.json           # VS0.8
    world_regions.json         # VS0.9 / Content Ready enterConditions
    quests.json                # Content Ready
    content_events.json        # Content Ready
    chapters.json              # Chapter Production Framework
    chapter1_harness_*.json    # Chapter Production Toolkit（测试壳，非剧情）
  Authoring/Csv/
  Authoring/Templates/         # 第一章 JSON 模板（不被 Loader 扫描）
    characters.csv
    cultivation.csv
    items.csv
```

Each data file:

```json
{
  "schemaVersion": 1,
  "definitions": [ { ... } ]
}
```

Allowed file-level fields: `definitions`, `schemaVersion`.

## Common definition fields

| Field | Required | Notes |
|---|---|---|
| `id` | yes | `namespace:local_id`；须匹配 manifest.namespace |
| `type` | yes | 见下表 |
| `name` | no | 可读显示名 |
| `displayNameKey` / `nameKey` | no | Loc 预留 |
| `tags` | no | string array |

**Strict mode:** 未知字段 → fail；重复 `id` → fail。

### type 一览

`character`｜`cultivation`｜`item`｜`opportunitySite`｜`openingScenario`｜`resource`｜`facility`｜`settlement`｜`worldRegion`｜`quest`｜`contentEvent`｜`chapter`

## type = character

| Field | Notes |
|---|---|
| `baseAttributes` | MaxHp／Attack／Defense／Speed |
| `personalityTags`／`backgroundTags`／`talentTags` | 合并进 PersonalityProfile（顺序：personality→background→talent→tags） |
| `spiritRootPlaceholder`／`initialRealmPlaceholder` | 占位 |

## type = cultivation

| Field | Notes |
|---|---|
| `requiredRealm` | `Mortal`／`凡人` 等 |
| `cultivationSpeed`／`breakthroughProgress` | Core 解释 |
| `grantedModifiers` | Fixed／Percentage grants |

## type = item

| Field | Notes |
|---|---|
| `maxStack` | ≥1，默认 1 |

## type = opportunitySite

| Field | Notes |
|---|---|
| `allowsCultivation` | bool |
| `offeredManualId` | 可选功法 id |
| `description` | 文本 |

## type = openingScenario（VS0.7+）

| Field | Notes |
|---|---|
| `scheduleId`／`openingFactionId` | 开局日程／势力 |
| `openingSettlementId` | VS0.8 据点定义 |
| `openingWorldRegionId` | VS0.9 区域定义 |
| `openingChapterId` | Chapter Production：开局激活章节 |
| `spawns[]` | 见下 |
| `openingRelations[]` | from／to／delta／reasonTag／mutual |

### spawn entry

`definitionId`、`entityKind`（character｜npc）、`displayName`、`assignOpeningFaction`、`factionRole`、`bindSchedule`、`bindDailyTask`、`recruitable`、`workRole`（Labor｜Gather｜Cultivate）

## type = resource（VS0.8）

`name`／`nameKey`

## type = facility（VS0.8）

`laborResourceId`／`laborAmountPerWorker`、`gatherResourceId`／`gatherAmountPerWorker`、`cultivateProgressBonusPerWorker`

## type = settlement（VS0.8）

`initialStock[]`（resourceId／amount）、`facilities[]`（facility id 字符串）

## type = worldRegion（VS0.9）

| Field | Notes |
|---|---|
| `startLocationId` | 开局地点 |
| `locations[]` | id／name／kind／adjacentIds／resourceOnExplore*／opportunitySiteId／residentNpcDefinitionId／presentationX／presentationZ／`enterConditions[]`／`questOfferIds[]` |

## type = quest（Content Ready）

| Field | Notes |
|---|---|
| `autoOffer` | 条件满足时自动接取 |
| `offerConditions`／`completeConditions`／`failConditions` | condition 对象数组 |
| `rewards`／`failResults` | outcome 对象数组 |

### condition.kind

`atLocation`｜`hasFlag`｜`missingFlag`｜`realmAtLeast`｜`knowsSite`｜`stockAtLeast`｜`questActive`｜`questCompleted`｜`exploredLocation`｜`hasManual`

### outcome.kind

`setFlag`｜`clearFlag`｜`addStock`｜`startQuest`｜`relationDelta`｜`grantProgress`｜`discoverSite`

## type = contentEvent（Content Ready）

| Field | Notes |
|---|---|
| `trigger` | `onExplore`｜`onArrive`｜`onQuestCompleted`｜`manual` |
| `locationId`／`questId` | 触发上下文过滤 |
| `once` | 默认 true |
| `conditions`／`choices[]` | choice：id／text／conditions／outcomes |

会话态：Quest／ContentEvent／Flags **不进 Snapshot v1**。

## Sample IDs

- `base:character_protagonist`／`companion_a`／`companion_b`／`village_recruit`／`herb_gatherer`
- `base:cultivation_qingyun_manual`／`wood_whisper`
- `base:scenario_playable_day`
- `base:settlement_qingshi_cave`／`facility_meditation_mat`
- `base:region_qingshi`／`loc_labor_camp`／`loc_cave_mouth`／…
- `base:site_abandoned_cave`／`resource_rough_wood`／`resource_spirit_herb`
- `base:quest_scout_herb_slope`／`quest_listen_herb_whisper`
- `base:event_herb_whisper`
- `base:chapter_scaffold_01`

## type = chapter（Chapter Production）

| Field | Notes |
|---|---|
| `openingScenarioId` | 文档关联（运行时由 scenario.`openingChapterId` 反向绑定） |
| `plannedDays` | 计划天数（制作参考＋Dump） |
| `questChainIds[]` | 有序任务链：前序 Completed 后自动接下一环 |
| `eventChainIds[]` | 事件链清单（制作／计划；触发仍靠 explore／beat／条件） |
| `dayBeats[]` | `dayIndex`／`conditions`／`questOfferIds`／`contentEventIds`／`setFlags` |

会话态：Chapter／Quest／ContentEvent／Flags **不进 Snapshot v1**。
