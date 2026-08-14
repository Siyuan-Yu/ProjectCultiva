# Content/BaseGame/Data

运行时 JSON 真源。Loader 与 ContentAuthoring 编辑器会 **递归扫描** 本目录下所有 `*.json`（子目录内的也会加载）。

## 子目录（按 definition type）

| 目录 | type | 典型文件 |
|------|------|----------|
| `Characters/` | character | `characters.json` |
| `Quests/` | quest | `quests.json`、`ch01_reference_quests.json` |
| `Events/` | contentEvent | `content_events.json` |
| `Maps/` | mapLayout | `ch01_reference_map.json` |
| `Regions/` | worldRegion | `world_regions.json` |
| `Chapters/` | chapter | `chapters.json` |
| `Scenarios/` | openingScenario | `scenarios.json` |
| `Cultivation/` | cultivation | `cultivation.json` |
| `Items/` | item | `items.json` |
| `Sites/` | opportunitySite | `sites.json` |
| `Resources/` | resource | `resources.json` |
| `Facilities/` | facility | `facilities.json` |
| `Settlements/` | settlement | `settlements.json` |
| `WorkAreas/` | workArea | `work_areas.json` |
| `Jobs/` | job | `jobs.json` |
| `Schedules/` | schedule | `schedules.json` |

新建内容时，各编辑器默认保存到对应子目录。字段权威见同级的 `SCHEMA.md`。
