# BaseGame Data Schema (Data Pipeline M1-A)

Runtime format: **JSON only** (Excel/CSV conversion not in M1-A runtime load path).

## File layout

```text
Content/BaseGame/
  manifest.json
  Data/
    characters.json
    cultivation.json
    items.json
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

## type = cultivation

| Field | Required | Notes |
|---|---|---|
| `requiredRealm` | no | 境界占位字符串；含 `:` 时按 DefinitionId 引用校验（M1-B） |
| `grantedModifiers` | no | array of grant objects (config only; Core applies later) |

Grant object fields: `targetAttribute`, `operation` (`Fixed`\|`Percentage`), `value` (number), optional `stackingKey`.

## type = item

| Field | Required | Notes |
|---|---|---|
| `maxStack` | no | number ≥ 1；default 1 |

## Sample IDs (M1-A)

- `base:character_labor_disciple`
- `base:cultivation_basic_breath`
- `base:item_rough_wood`
