# BaseGame Data Schema (Data Pipeline M1-A / VS0.1)

Runtime format: **JSON only** (CSV is authoring input via M1-B importer; not runtime).

## File layout

```text
Content/BaseGame/
  manifest.json
  Data/
    characters.json
    cultivation.json
    items.json
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
| `type` | yes | `character` \| `cultivation` \| `item` |
| `name` | no | 可读显示名（非完整 Loc） |
| `displayNameKey` | no | Loc key 预留 |
| `nameKey` | no | Loc key 预留 |
| `tags` | no | string array |

**Strict mode:** any other field → load fail. Duplicate `id` (any type) → fail. Invalid `id` → fail.

## type = character

| Field | Required | Notes |
|---|---|---|
| `baseAttributes` | no | object: AttributeId name → number (`MaxHp`/`Attack`/`Defense`/`Speed`) |
| `spiritRootPlaceholder` | no | VS0.1 灵根占位；无玩法公式 |
| `initialRealmPlaceholder` | no | VS0.1 初始境界占位；无突破逻辑 |

## type = cultivation

| Field | Required | Notes |
|---|---|---|
| `requiredRealm` | no | 境界占位；Slice 0.1 支持 `Mortal`／`凡人` |
| `cultivationSpeed` | no | 每修炼 tick 增加的 Progress（Core 解释） |
| `breakthroughProgress` | no | 凡人→炼气所需 Progress（Core 解释） |
| `grantedModifiers` | no | array of grant objects (config only; Core applies later) |

Grant object fields: `targetAttribute`, `operation` (`Fixed`\|`Percentage`), `value` (number), optional `stackingKey`.

## type = item

| Field | Required | Notes |
|---|---|---|
| `maxStack` | no | number ≥ 1；default 1 |

## Sample IDs

- `base:character_labor_disciple`
- `base:character_protagonist`
- `base:character_companion_a`
- `base:character_companion_b`
- `base:cultivation_basic_breath`
- `base:cultivation_qingyun_manual`
- `base:item_rough_wood`
