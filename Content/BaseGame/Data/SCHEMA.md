# BaseGame Data Schema (Data Pipeline M1-A / VS0.7)

Runtime format: **JSON only** (CSV is authoring input via M1-B importer; not runtime).

## File layout

```text
Content/BaseGame/
  manifest.json
  Data/
    characters.json
    cultivation.json
    items.json
    opportunity_sites.json   # if present
    scenarios.json           # VS0.7 openingScenario
  Authoring/Csv/
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
| `type` | yes | `character` \| `cultivation` \| `item` \| `opportunitySite` \| `openingScenario` |
| `name` | no | 可读显示名（非完整 Loc） |
| `displayNameKey` | no | Loc key 预留 |
| `nameKey` | no | Loc key 预留 |
| `tags` | no | string array（杂项／兼容） |

**Strict mode:** any other field → load fail. Duplicate `id` (any type) → fail. Invalid `id` → fail.

## type = character

| Field | Required | Notes |
|---|---|---|
| `baseAttributes` | no | object: AttributeId name → number (`MaxHp`/`Attack`/`Defense`/`Speed`) |
| `personalityTags` | no | string array → PersonalityProfile |
| `backgroundTags` | no | string array → PersonalityProfile |
| `talentTags` | no | string array → PersonalityProfile |
| `spiritRootPlaceholder` | no | 灵根占位；无玩法公式 |
| `initialRealmPlaceholder` | no | 初始境界占位 |

Spawn 时合并顺序：personality → background → talent → tags。

## type = cultivation

| Field | Required | Notes |
|---|---|---|
| `requiredRealm` | no | 境界占位；支持 `Mortal`／`凡人` |
| `cultivationSpeed` | no | 每修炼 tick 增加的 Progress（Core 解释） |
| `breakthroughProgress` | no | 凡人→炼气所需 Progress（Core 解释） |
| `grantedModifiers` | no | array of grant objects (config only; Core applies later) |

Grant object fields: `targetAttribute`, `operation` (`Fixed`\|`Percentage`), `value` (number), optional `stackingKey`.

## type = item

| Field | Required | Notes |
|---|---|---|
| `maxStack` | no | number ≥ 1；default 1 |

## type = openingScenario（VS0.7）

| Field | Required | Notes |
|---|---|---|
| `scheduleId` | no | 绑定日程定义 id |
| `openingFactionId` | no | 开局势力 id |
| `spawns` | yes | 开局生成列表 |
| `openingRelations` | no | 开局关系边 |

### spawn entry

| Field | Required | Notes |
|---|---|---|
| `definitionId` | yes | character 定义 id |
| `entityKind` | no | `character`（默认）\|`npc` |
| `displayName` | no | 覆盖定义名 |
| `assignOpeningFaction` | no | bool |
| `factionRole` | no | `LaborDisciple` \| `Member` |
| `bindSchedule` | no | bool，默认 true |
| `bindDailyTask` | no | bool，默认 true |
| `recruitable` | no | bool；PlayableDay 取首个 true |

### openingRelations entry

| Field | Required | Notes |
|---|---|---|
| `fromDefinitionId` | yes | |
| `toDefinitionId` | yes | |
| `delta` | no | 默认 0 |
| `reasonTag` | no | 默认 `opening_companion` |
| `mutual` | no | bool，默认 true |

## Sample IDs

- `base:character_labor_disciple`
- `base:character_protagonist`
- `base:character_companion_a`
- `base:character_companion_b`
- `base:character_village_recruit`
- `base:character_herb_gatherer`
- `base:cultivation_basic_breath`
- `base:cultivation_qingyun_manual`
- `base:cultivation_wood_whisper`
- `base:scenario_playable_day`
- `base:item_rough_wood`
