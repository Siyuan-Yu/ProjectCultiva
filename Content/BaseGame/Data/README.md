# Content/BaseGame/Data

运行时 JSON 真源。Loader 与 ContentAuthoring 编辑器会 **递归扫描** 本目录下所有 `*.json`（子目录内的也会加载）。

## 子目录（按 definition type）

| 目录 | type | 典型文件 |
|------|------|----------|
| `Characters/` | character | `characters.json` |
| `Quests/` | quest | `quests.json`、`ch01_reference_quests.json` |
| `Events/` | contentEvent | `content_events.json` |
| `Maps/` | mapLayout（Separate Space／独立 Encounter） | `ch01_cave_map.json`、`strategic_encounter_arena.json` |
| `LocalPlaces/` | localPlaceSet（Separate Space／独立 Encounter） | `ch01_cave_places.json`、`strategic_encounter_arena_places.json` |
| `Worlds/` | Continuous Surface／world spatial rules | `main_wilderness_surface_v1.json`、`world_spatial_rules.json` |
| `Chapters/` | chapter | `chapters.json` |
| `Scenarios/` | openingScenario | `scenarios.json` |
| `Cultivation/` | cultivation | `cultivation.json` |
| `Items/` | item | `items.json` |
| `Sites/` | opportunitySite | `sites.json` |
| `Resources/` | resource | `resources.json` |
| `SiteEconomies/` | worldSiteEconomy | `site_economies.json` |
| `WorkAreas/` | workArea | `work_areas.json` |
| `Jobs/` | job | `jobs.json` |
| `Schedules/` | schedule | `schedules.json` |
| `Armies/` | 当前为 `npcSquad`；目录名是历史内容分组，不表示 runtime Army authority | `ch01_test_armies.json`、`ch01_huangcun_garrison.json` |
| `Factions/` | strategicFaction（战略势力身份/名/地图色；全局唯一真源） | `factions.json` |

新建内容时，各编辑器默认保存到对应子目录。字段权威见同级的 `SCHEMA.md`。

`Armies/` 当前文件本身就是正常 `npcSquad` 内容；新 NPC group 使用 `npcSquad`／`initialNpcSquadIds`。Runtime Loader **不再支持** `formalArmy`、`initialFormalArmyIds` 或 `hexWorld`，命中时会明确拒绝。

旧 FormalArmy 可使用 `ExternalTools/ContentAuthoring/LegacyRuntimeConverter`，且输入只读、输出为不同且尚不存在的独立副本。`hexWorld`／`openingHexWorldId` 会被该工具检测并拒绝，必须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径，无样例时不猜。转换后的 Army Content 使用 `npcSquad`；ID 规则为：

- 有旧 `runtimeArmyId`：`squad:migrated:<normalizedRuntimeArmyId>`；
- 无旧 `runtimeArmyId`：`squad:legacy:<normalizedDefinitionId>`；
- 旧 Snapshot：`squad:army:<armyId>`。

三条规则不可混用。旧 Hex Snapshot 只有 Q/R 而无精确 Surface 位置时，离线转换必须显式提供 `surface-id` 与正数 `movement-scale`。`movementScale` 是连续世界 movement budget 尺度，不是 `cellSize`；当前 BaseGame `outdoorSurface` 显式配置为 `1.0`。
