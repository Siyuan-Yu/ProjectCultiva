# BaseGame Data Schema (Data Pipeline M1-A / VS0.7–1.0 / Content Ready / Chapter Production)

Runtime format: **JSON only** (CSV is authoring input via M1-B importer; not runtime).

## File layout

```text
Content/BaseGame/
  manifest.json
  Data/
    SCHEMA.md                  # 本文件（Loader 不扫描 .md）
    README.md                  # 子目录说明
    Characters/                # type = character
      characters.json
      ch01_reference_characters.json
    Cultivation/               # type = cultivation｜realmLadder
      cultivation.json
      realm_ladder.json
    Items/                     # type = item
      items.json
    Sites/                     # type = opportunitySite
      sites.json
    Scenarios/                 # type = openingScenario
      scenarios.json
    Rosters/                   # type = characterRoster（Level Tester 名册）
      level_tester_roster.json
    Resources/                 # type = resource
      resources.json
    Facilities/                # type = facility
      facilities.json
    Settlements/               # type = settlement
      settlements.json
    Regions/                   # type = worldRegion
      world_regions.json
      ch01_reference_region.json
    WorldGraphs/               # type = worldGraph（宏观节点图 · [113]）
      ch01_world_graph.json
    LocalPlaces/               # type = localPlaceSet（村内地点表，绑 mapLayout）
      ch01_reference_places.json
    Maps/                      # type = mapLayout（关卡格点）
      ch01_reference_map.json
    Jobs/                      # type = job
      jobs.json
    WorkAreas/                 # type = workArea
      work_areas.json
    Schedules/                 # type = schedule（NPC 日计划）
      schedules.json
    Quests/                    # type = quest
      quests.json
      ch01_reference_quests.json
      chapter1_harness_quests.json
    Events/                    # type = contentEvent
      content_events.json
      ch01_reference_events.json
      chapter1_harness_events.json
    Chapters/                  # type = chapter
      chapters.json
      ch01_reference_chapter.json
      chapter1_harness_chapter.json
    WorkAreas/                 # type = workArea
      work_areas.json
    Jobs/                      # type = job
      jobs.json
  Authoring/Csv/
  Authoring/Templates/         # 第一章 JSON 模板（不被 Loader 扫描）
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

`character`｜`cultivation`｜`realmLadder`｜`item`｜`opportunitySite`｜`openingScenario`｜`characterRoster`｜`resource`｜`facility`｜`settlement`｜`worldRegion`｜`localPlaceSet`｜`worldGraph`｜`mapLayout`｜`spawnTable`｜`quest`｜`contentEvent`｜`chapter`｜`workArea`｜`job`

## type = character

| Field | Notes |
|---|---|
| `baseAttributes` | **MaxHp＝生命上限（血条）**；**Physique＝体魄（肉身属性）**；Attack／Defense／Speed／Stamina；SpiritSense／Comprehension；SpiritPower／Cultivation／MindState |
| `playerControllable` | 编辑器默认「可控制」；出场 `entityKind` 为准（character＝进 CharacterIds） |
| `personalityTags`／`backgroundTags`／`talentTags` | 合并进 PersonalityProfile（顺序：personality→background→talent→tags） |
| `spiritRootPlaceholder`／`initialRealmPlaceholder` | 占位 |

## type = cultivation

| Field | Notes |
|---|---|
| `requiredRealm` | `Mortal`／`凡人`／`炼气` 等 |
| `grade` | 品阶展示，如 `黄阶中级` |
| `effectSummary` | 效果摘要文案 |
| `cultivationSpeed`／`breakthroughProgress` | Core：打坐每 tick（5 游戏分）修为增益；瓶颈修为优先取 `realmLadder` |
| `grantedModifiers` | Fixed／Percentage grants |

## type = realmLadder

| Field | Notes |
|---|---|
| `steps[]` | `fromRealm`／`fromMinor`／`toRealm`／`toMinor`／`progressRequired`／`successPercent`／`majorRealmJump`／`grantSpiritPower`／`bonuses` |
| 感应境 | `Mortal` + minor 0/1/2 = 前/中/后期 |
| 炼气 | `QiRefining` + minor 1–10 |
| 筑基 | `Foundation`；十层→筑基默认低成功率卡点 |

## type = item

| Field | Notes |
|---|---|
| `maxStack` | ≥1，默认 1 |
| `teachesManualId` | 可选；指向 `cultivation`。背包「使用」→ 选人学习；**秘籍不消耗**，可多次传授；一人一本，换功法覆盖 |
| `teachesArtId` | 可选；斗技秘本。样例：`item_art_liezhao_claw`（洞府裂爪击）、`item_art_kaishan_fist`（将老开山拳） |

主动斗技字段（内置注册，非 JSON 暂）：`DamageAttackMult`（攻击力倍率）／`HitCount`（连击）／装备栏 6 格快捷键 1–6。

## type = opportunitySite

| Field | Notes |
|---|---|
| `allowsCultivation` | bool |
| `offeredManualId` | 可选功法 id |
| `description` | 文本 |

## type = characterRoster（Level Tester 名册）

| Field | Notes |
|---|---|
| `entries[]` | 与 openingScenario.spawns 同形：definitionId／entityKind／aiRole／factionRole／scheduleId… |

人物本体在 `Characters/`；本表只回答「试玩时刷谁」。Level Tester 默认读 `base:roster_level_tester`（人物编辑器「导出 Level Tester 名册」）。**不是** Unity 场景里摆好的 GameObject。

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

`definitionId`、`entityKind`（character＝可控制／进 CharacterIds｜npc）、`displayName`、`assignOpeningFaction`、`factionRole`、`bindSchedule`、`bindDailyTask`、`recruitable`、`workRole`（Labor｜Gather｜Cultivate）、`scheduleId`、`aiRole`。人物「可控制」与 `entityKind` 对齐。不再使用职业式 `jobId`。

## type = resource（VS0.8）

`name`／`nameKey`

## type = facility（VS0.8）

`laborResourceId`／`laborAmountPerWorker`、`gatherResourceId`／`gatherAmountPerWorker`、`cultivateProgressBonusPerWorker`

## type = settlement（VS0.8）

`initialStock[]`（resourceId／amount）、`facilities[]`（facility id 字符串）

## type = localPlaceSet（村内地点表 · 正式）

| Field | Notes |
|---|---|
| `mapLayoutId` | 绑定的格点地图 |
| `startLocationId` | 进入该图时队伍落点 |
| `locations[]` | 同 worldRegion.location 字段 |

样例：`LocalPlaces/ch01_reference_places.json`。Ch01 Scenario 用 `openingLocalPlaceSetId`。运行时仍灌入 `SimulationWorld.WorldRegion` 板。

## type = worldRegion（旧 VS 遗留 · 非正式宏观）

> **不是**大世界。宏观用 `worldGraph`。Ch01 村内地点已迁 `localPlaceSet`；本类型仅旧 VS（如青石四地点）保留。

| Field | Notes |
|---|---|
| `startLocationId` | 开局地点 |
| `locations[]` | 见下 |

### location

| Field | Notes |
|---|---|
| id／name／kind／`tags[]`／`allowedActivities[]` | 基础 |
| adjacentIds／resourceOnExplore*／opportunitySiteId／residentNpcDefinitionId | 邻接／探索／机缘／驻地 |
| presentationX／presentationZ／`enterConditions[]`／`questOfferIds[]` | 表现／进入／挂任务 |
| `localMapId` | 该地点所属 LocalMap；空＝地表 |
| `enterLocalMapId`／`enterSpawnLocationId` | 洞口：进入的 mapLayout＋内部落点 |
| `surveySenseRequired` | 已废弃（勘查半径＝角色神识） |

## type = worldGraph（宏观世界图 · [113] 阶段 A）

| Field | Notes |
|---|---|
| `startNodeId` | 开局所在 WorldNode |
| `nodes[]` | 战略点 |
| `routes[]` | 节点之间的道路边 |

### node

| Field | Notes |
|---|---|
| id／name／kind | Town／Village／Sect／Mine／Forest／Ferry… |
| `localMapId` | 可选；有则进 Node 时加载该 `mapLayout` |
| `worldX`／`worldY` | 宏观摆点（不是 Local 格点） |
| `ownerId`／`state`／`tags[]` | 归属／可见态／过滤 |

### route

| Field | Notes |
|---|---|
| id／`fromNodeId`／`toNodeId`／kind | Road／Trail／… |
| `travelCost`／`danger` | 旅行时间代价／危险度（B／E 阶段消费） |
| `ownerId`／`state`／`directed` | 路权／畅通态／单向 |
| `traversalRequirements[]` | 数据可填；运行时旅行暂不检查 |
| `encounterPoolId` | 可选；路上遭遇池（E 阶段） |

样例：`WorldGraphs/ch01_world_graph.json`（约 30 节点；仅荒村绑 `base:map_ch01_reference`）。Host「地图」显示角色所在节点，勾选后点相邻节点组队移动。无通行令门槛。

`openingScenario.openingWorldGraphId`：开局灌入 WorldGraph；缺省仍可只开 region（旧 VS）。

## type = mapLayout（格点地图 · MapEditor）

| Field | Notes |
|---|---|
| `worldRegionId` | 关联的逻辑区域 |
| `originX`／`originY`／`cellSize` | 与 WalkGrid 一致；默认 cellSize=1（约一人一格） |
| `width`／`height` | 格子数（可改大，如整屏约 400×200） |
| `placements[]` | 设施／障碍矩形／刷怪区 |

### placement

| Field | Notes |
|---|---|
| `id`／`kind`／`label` | kind：wall／herbField／…／`spawnZone`（刷怪区）／controlCore… |
| `x`／`y`／`w`／`h` | 格点坐标与大小 |
| `blocksMovement` | true 则写入寻路障碍 |
| `boundLocationId` | 可选；**spawnZone 必填**（NPC 逻辑地点） |
| `lootItemId` | kind=loot |
| `spawnTableId`／`spawnCount` | kind=spawnZone：刷怪表；spawnCount=0 则按表 entries 的 countMin～Max |

## type = spawnTable（刷怪表）

| Field | Notes |
|---|---|
| `name` | 显示名 |
| `entries[]` | `definitionId`（角色）／`weight`／`countMin`／`countMax` |

样例：`SpawnTables/cave_shade_spawn_table.json`；洞府 map 上 `spawnZone` 引用。角色仍用 CharacterNpcEditor 编；**不做独立敌人编辑器**。开局 `SpawnZoneApplier` 按各 map 的刷怪区生成。

样例：`ch01_reference_map.json`。Host 优先用 mapLayout 建 WalkGrid，并按 `kind` 刷 Environment prefab（药田／农田一格一块可交互；房子约 20×20；道路 1×1）。有 `boundLocationId` 时启动会把地点 `presentationX/Z` 对齐到设施中心。无 mapLayout 则回退硬编码网格。用法见 `docs/40-process/112-map-editor-usage.md`。

## type = character

| Field | Notes |
|---|---|
| `baseAttributes` | **MaxHp＝生命上限（血条）**；**Physique＝体魄**；Stamina／Attack／Defense／Speed；SpiritSense／Comprehension；SpiritPower／Cultivation／MindState（2B） |
| `spiritRoots` | 火金土木雷风冰毒 → 0–30 亲和数值（**不是**「金灵根」字符串） |
| `playerControllable` | 人物侧默认；与 spawn.`entityKind` 同步 |
| `preferredWorkAreaIds` | 有序地点偏好 |
| `homeWorkAreaId` | 分配住房工区；Rest／Eat／Idle 优先 |
| `defeatEncounterId` | 可选；被击倒时写入 flag `encounter:{id}`（洞府残影等） |
| `hometown`／`reputation`／`goals[]`／`desires[]` | 社会侧 |
| `initialRealmPlaceholder` | 境界展示占位 |
| `activityCapabilities`／`activityPriorities` | 闲时能否做／权重 |
| `personalityTags`／`backgroundTags`／`talentTags` | 人格档案标签 |

人物表现：**不**为每人生成 Unity Prefab；Host 共用 EntityView，数据驱动差异。见 `docs/40-process/119-npc-character-vs-role-template-editors.md`。

## type = workArea（NPC Simulation）

| Field | Notes |
|---|---|
| `locationId` | 必填，绑定已有 Location |
| `allowedActivities[]` | 允许的 ScheduleActivity 名（含 `Idle` 发呆） |
| `capacity` | 同时可站软点位数（默认 4）；满则换工区／降优先级／发呆 |
| `residentTags[]` | 住房准入（mortal／guard／supervisor）；空＝不限；**仅**约束 Rest／Eat／Idle。主管住房与凡人／巡卫房同属此类 |
| `isControlCore` | **主管府／控制核心**（不是住房）；可攻击、耐久归零后需站立占领 |
| `maxDurability` | 控制核心耐久（样例主管府 100） |
| `defense` | 控制核心防御（每次受击减免，至少仍扣 1） |
| `occupyHoldSeconds` | 破门后我方站立累计秒数才占领（默认 10，可调） |
| `grantsPrivileges[]` | 占领后授予的权限 id（如 `manageHousing`／`manageSchedules`） |
| `offsetX`／`offsetZ` | 相对 Location presentation 中心的偏移（内容数据，非代码硬编码） |

## type = job（已废弃）

职业式 Job 定义已清空；加载器仍兼容。运行时不靠 Job。

管线：人物倾向选活动 → 工区（偏好／可用）→ Move／Work。样例见 `work_areas.json`。编辑器用 **WorkAreaEditor**＋**CharacterNpcEditor**。

## type = schedule（NPC 日计划）

| Field | Notes |
|---|---|
| `blocks[]` | `startTick`／`endTick`（半开区间，日长 288）／`activity`／`orderDurationTicks` |
| `activity` | Labor｜Rest｜Eat｜Cultivate｜Explore｜Patrol｜Inspect｜Idle |

真源：`Schedules/schedules.json`。运行时由 `ScheduleRuntimeBootstrap` 注册；C# `ScheduleDefinition.Create*` 仅作缺省兜底。

## type = quest（Content Ready）

| Field | Notes |
|---|---|
| `autoOffer` | 条件满足时自动接取 |
| `abandonable` | 玩家是否可在任务日志中放弃（默认 false） |
| `deadlineDays` | 接取后有效游戏天数；`0` = 无时限。超时自动 `Failed` 并应用 `failResults` |
| 状态机 | Inactive → Active → **ReadyToClaim（待领奖）** → Completed；奖励仅在领取时发放 |
| `offerConditions`／`completeConditions`／`failConditions` | condition 对象数组 |
| `rewards`／`failResults` | outcome 对象数组 |

`failResults`：任务 **超时**（`deadlineDays`）或 **失败条件** 满足时执行；留空 `[]` 表示无额外后果。常用 `relationDelta`（NPC 发布任务超时降好感）。

### condition.kind

`atLocation`｜`hasFlag`｜`missingFlag`｜`realmAtLeast`｜`knowsSite`｜`stockAtLeast`｜`questActive`｜`questCompleted`｜`exploredLocation`｜`hasManual`｜`laborAtLocation`｜`uniqueLaborAtLocation`｜`uniqueHarvestAtLocation`｜`characterAtLocation`｜`counterAtLeast`｜`missingDailyFlag`｜`hasDailyFlag`｜`encounterCleared`

| kind | 含义 | 主要字段 |
|---|---|---|
| `stockAtLeast` | **小队背包**中该 Id 数量 ≥ amount（非聚落仓库） | `id`／`amount` |
| `laborAtLocation` | 指定角色（或任意）在地点累计劳动 ticks ≥ | `id`（地点）／`characterId?`／`amount` |
| `uniqueLaborAtLocation` | 在地点完成过劳动的**不同角色数** ≥ | `id`／`amount` |
| `uniqueHarvestAtLocation` | 在地点**采到过产出**的不同角色数 ≥ | `id`／`amount` |
| `characterAtLocation` | 指定角色当前在某地点 | `id`（地点）／`characterId` |
| `counterAtLeast` | 会话计数器 ≥ amount（对弈胜场等） | `id`（计数键）／`amount` |
| `missingDailyFlag` | **今日尚未**标记该键（可再对弈／拜访） | `id` |
| `hasDailyFlag` | **今日已**标记该键 | `id` |
| `encounterCleared` | 遭遇已清除（flag `encounter:{id}`） | `id`（遭遇／洞窟键） |

劳动／采集进度由 `LocationLaborProgressBoard` 维护；采集节奏由 Host（约 10s/份＠1x、可自动续采）决定。  
计数／日访由 `ContentCounterBoard`／`ContentDailyBoard` 维护（**不进 Snapshot v1**）。

### outcome.kind

`setFlag`｜`clearFlag`｜`addStock`｜`startQuest`｜`relationDelta`｜`grantProgress`｜`discoverSite`｜`addCounter`｜`setCounter`｜`setDailyFlag`｜`clearDailyFlag`｜`learnManual`｜`setEncounterCleared`｜`startMinigame`

| kind | 主要字段 |
|---|---|
| `relationDelta` | `fromDefinitionId`（单个）／`toDefinitionId`（单个，兼容旧数据）／`toDefinitionIds`（字符串数组，可多目标；`@party` = 当前全体可控角色）／`amount` |
| `addCounter` | `id`／`amount`（默认 +1） |
| `setCounter` | `id`／`amount` |
| `setDailyFlag`／`clearDailyFlag` | `id`（与 missingDailyFlag 同键） |
| `learnManual` | `id`＝`cultivation` 功法定义；**立刻**让领奖角色学（机缘点等）；任务更推荐 `addStock` 秘籍 |
| `addStock` | `id`＝resource **或** item（含功法秘籍） |
| `setEncounterCleared` | `id`→写 flag `encounter:{id}` |
| `startMinigame` | `id`＝小游戏键（如 `ticTacToe`）；Host 拦截打开棋盘，胜负由 Host 写计数／日访 |

**功法任务示例（接口就绪，内容另填）：**

```json
// 对弈选项 outcomes（Host 拦截 startMinigame；胜负另写）
{ "kind": "startMinigame", "id": "ticTacToe" }
// 胜局后 Host：addCounter chess_wins_jiang_lao + setDailyFlag daily:jiang_lao_chess
// 选项／事件 conditions：missingDailyFlag id=daily:jiang_lao_chess
// 任务 completeConditions：counterAtLeast id=chess_wins_jiang_lao amount=3
// 任务 rewards：addStock 秘籍（背包使用后选炼气队员学）
{ "kind": "addStock", "id": "base:item_manual_jiang_lao_legacy", "amount": 1 }
```
## type = contentEvent（Content Ready）

| Field | Notes |
|---|---|
| `trigger` | `onExplore`｜`onArrive`｜`onQuestCompleted`｜`onTalk`｜`manual` |
| `locationId`／`questId`／`npcDefinitionId` | 触发上下文过滤（`onTalk` 配 NPC 的 character definition id；事件编辑器可填） |

**对话发任务：** 不在人物 JSON 上写任务列表。用 `trigger=onTalk`＋`npcDefinitionId`＋选项 `outcomes` 的 `startQuest`。运行时动态靠 conditions／flag／章节 beat／`TryPresentById`。

| `once` | 默认 true |
| `conditions`／`choices[]` | choice：id／text／conditions／outcomes |

**Host 打断呈现（CIF）：** 激活的 contentEvent → 强制暂停＋选项弹层（`ResolveContentChoice`）。  
QuestStarted／Completed → 任务提醒弹层（读 `name`／`description`）。详见 `docs/40-process/95-content-interrupt-system-plan-v0.1.md`。

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
| `questChainIds[]` | 有序任务链：前序目标达成（ReadyToClaim／Completed）后自动接下一环 |
| `eventChainIds[]` | 事件链清单（制作／计划；触发仍靠 explore／beat／条件） |
| `dayBeats[]` | `dayIndex`／`conditions`／`questOfferIds`／`contentEventIds`／`setFlags` |

会话态：Chapter／Quest／ContentEvent／Flags **不进 Snapshot v1**。
