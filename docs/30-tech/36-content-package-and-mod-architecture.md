# ContentPackage 与 Mod Ready 架构

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

Snapshot v11 保留既有调度字段并完整保存临时绑定；严格校验实体/实例/受控 Squad，恢复不重放邀请、不替换同模板实体、不新增 controllability bool。v1～v10 严格拒绝。任务 ReadyToClaim/Completed/Failed/放弃后锁定 PendingDeparture，安全普通 Surface 经私有生命周期入口离队，共用精确位置与工作清理；原 Squad 仍合法、同 Surface 同落点且可接纳才恢复，否则 singleton。


> **2026-09-22 Current Implementation：** [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md) 的 WorldComposer／FineEditor Production V1、deterministic composition／bake 与 Final Continuous Surface runtime 输入已经实现、验收并封板。自动水文、道路 A*、detail scatter、完整 terrain compatibility matrix、通用 Mod patch 等仍为 Future。

> **2026-09-15 Editor 工具链／旧地图 Content 迁移方向：** [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 补充 **Authoring Source ≠ Runtime Generated Content** 契约（见 §2.3）：`WorldComposition`／`WorldSiteBlueprint`／`DetailPatch` 是 Authoring Source，由 bake 产出的 Final Continuous Surface 才是 Runtime Content。**不要让 Authoring JSON 自动成为正常 runtime DefinitionRegistry authority。**

> 状态：**ContentPackage／BaseGame loader 已实现；外部 Mod 产品能力仍为分阶段设计** | 优先级：P0 | 最后更新：2026-09-25
> **当前不承诺 Mods/ 产品加载、任意脚本 Mod、Workshop、热重载或完整 SDK。**

## 1. 正式定位

**Mod 支持是正式长期目标，分阶段实现。**

| 现在保证 | 现在不承诺 |
|---|---|
| ContentPackage 统一管线形状 | 任意 C# 脚本 Mod |
| 命名空间 DefinitionId | Steam Workshop |
| Manifest／依赖／校验契约 | 热重载 |
| 当前 BaseGame ContentPackage loader、DefinitionRegistry 与校验 | 完整 Mod 管理器／Workshop |
| 白名单 Condition／Effect | 完整事件可视化编辑器 |
| 官方内容也走同一管线 | 复杂冲突自动合并 |
| | 正式外部 SDK |

优先支持的 Mod 类型（数据／资源向）：文本与本地化、美术替换与新增、音频、角色模板、物品、功法、技能、建筑、对话、任务、事件与故事、NPC 日程、地图和地点定义、掉落与内容数据。

## 2. ContentPackage 统一

1. **官方内容与社区内容统一使用 ContentPackage。**  
2. **官方内容不得走专用硬编码加载路径。**  
3. BaseGame 也是一个 ContentPackage。

### 2.1 建议目录

```text
Content/
├── BaseGame/
├── OfficialExpansion/
└── Mods/
    └── ExampleMod/
```

### 2.2 包内结构

每个 ContentPackage 包含：

- `manifest.json`
- `Data/`
- `Localization/`
- `Art/`
- `Audio/`
- `Maps/`

### 2.3 地图 Content：Authoring Source ≠ Runtime Generated Content

> 真源：[ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) §10～§13（契约）；地图比例与合成层见 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)。**MAP-01 Production V1 已实现；Future 自动化范围见 2N。**

| 类别 | 谁编辑 | 谁消费 | 内容（概念名，schema 未锁） |
|---|---|---|---|
| **Authoring Source** | WorldComposer／FineEditor | 只有 baker | `WorldComposition`／`WorldSiteBlueprint`／`DetailPatch`（V1 已落地） |
| **Runtime Generated Content** | Bake 产生 | game runtime loader（只读） | Final Continuous Surface、Runtime Chunk、final terrain／geography、object placements、WorldSite 位置与 content、navigation input、WorldMap LOD／cache input |

- **Authoring Source ≠ Runtime Content**：地图 authoring 源**不应**继续直接塞进 `Content/BaseGame/Data` 并被 runtime loader 当作正式 gameplay definition 加载；推荐独立 authoring root（现存先例：`ContentAuthoring/Worlds/w2a_surface_geography_source_v1.json` → bake → `Content/BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json`）。
- **Runtime Loader 只读 Final Continuous Surface runtime output**；它不需要知道某个位置来自哪个 Blueprint／Detail Patch／旧 LocalMap。
- 现有 `mapLayout`／`localPlaceSet`／`hexWorld`／`worldRegion` 与 W2A／fallback 的逐个迁移分类见 ADR-0037；本页不定义 schema、loader、Mod patch 或迁移。

## 3. DefinitionId 命名空间

格式：`namespace:local_id`

示例：

- `base:item_concealment_herb`
- `base:realm_qi_refining`
- `author.modname:new_manual`

规则：

1. 官方命名空间使用 `base`。  
2. 每个 Mod 拥有唯一 `ModId` 与 `Namespace`。  
3. DefinitionId 一旦发布或写入存档，**不允许**因显示名称修改而随意重命名。  
4. 改名必须提供 `DataMigration`。  

与 `EntityId` 分离：DefinitionId 是定义；EntityId 是实例。

## 4. Manifest 与依赖

`manifest.json` 至少包含：

| 字段 | 说明 |
|---|---|
| `ModId` | 唯一包 ID |
| `Namespace` | DefinitionId 前缀 |
| `Version` | 包版本 |
| `CompatibleGameVersion` | 兼容游戏版本 |
| `Dependencies` | 硬依赖 |
| `OptionalDependencies` | 软依赖 |
| `LoadAfter` | 排序提示 |
| `ContentFolders` | 内容子目录 |

### 4.1 加载规则（契约）

1. `BaseGame` 最先加载。  
2. 依赖关系拓扑排序。  
3. 缺少硬依赖 → 禁用该包并**报错**。  
4. 循环依赖 → **报错**。  
5. ID 重复 → 默认**报错**。  
6. **禁止静默覆盖**。  
7. 修改既有定义必须使用显式 `PatchDefinition` 规则。  
8. 当前只设计 Patch 契约，**不必**实现复杂冲突解决。  

## 5. 资源与本地化引用

内容配置禁止直接引用：

- 绝对路径  
- `GameObject`／`Transform`／Unity Scene 对象  
- Unity GUID 作为公开内容 ID  

统一使用：

- `AssetId`（如 `base:portrait_supervisor_01`）  
- `LocalizationKey`（如 `base.character.supervisor.name`）  

由**资源注册表**从 AssetId 解析实际图片、音频等。

## 6. 数据事件 Mod 白名单

`DomainEvent` 可作为数据事件系统的触发入口。

初期 Mod 事件**只能**使用项目白名单的 Condition 与 Effect。

### 6.1 Condition 示例

`HasTag`、`HasItem`、`AttributeAbove`、`RelationshipBelow`、`AtLocation`、`EventOccurred`、`FactionIs`、`RealmAtLeast`

### 6.2 Effect 示例

`AddItem`、`RemoveItem`、`AddModifier`、`StartQuest`、`ChangeRelationship`、`ScheduleEvent`、`SpawnCharacter`、`ShowDialogue`、`ChangeFactionState`、`RevealKnowledge`

### 6.3 硬规则

1. 禁止配置执行任意 C#。  
2. 禁止 Mod 直接修改 Core 内部对象。  
3. Mod 效果必须经过：Order／Action、DomainEvent、AttributeModifier、Ledger 等正式契约。  
4. 不允许数据内容绕过核心架构直接修改最终属性或世界状态。  

## 7. 存档与 Mod

存档必须记录：

- 启用的 `ModId`  
- Mod 版本  
- 加载顺序  
- 内容 `DataVersion`  
- DefinitionId 命名空间来源  

加载时：

1. 缺少 Mod → 明确警告。  
2. 版本不兼容 → 明确警告。  
3. 未知 DefinitionId → **不能**静默删除。  
4. 可提供「强制尝试加载」，必须说明风险。  
5. 缺失内容进入可诊断错误或降级流程。  

兼容策略：

- 开发期不保证所有 Mod 存档兼容。  
- 正式版本尽量在同一大版本内保持兼容。  

## 8. Mod Roadmap（阶段）

| 阶段 | 内容 | 编码？ |
|---|---|---|
| **A 架构冻结（当前）** | ContentPackage 设计、命名空间 ID、Manifest、注册表契约、存档记录、白名单事件 | **不写加载器** |
| **B Core 早期** | 官方也走 ContentPackage；内部测试包；不改 Core 即可加角色／物品／事件／对话／美术 | 实现加载骨架 |
| **C 垂直切片后** | 本地 `Mods/` 启用禁用、依赖校验、数据／文本／美术／音频／事件 Mod、示例模板、错误日志 | |
| **D 规则稳定后** | 简单事件编辑器、地图辅助、显式 Patch 工具、冲突报告、创作者文档 | |
| **E 发布准备** | 评估 Workshop、游戏内管理器、安全脚本 API | |

## 9. 与 `33`／`2E` 的关系

- DefinitionId 校验含命名空间与改名迁移（见 `33`）。  
- 事件 Effect 创建 Modifier／Ledger 变更必须可溯源（见 `2C`／`2E`）。  

## 10. 验证方式（实现期）

- BaseGame 与测试 Mod 走同一加载器  
- 重复 ID／缺依赖／循环依赖无法静默进游戏  
- 存档含 Mod 列表；缺 Mod 有警告  
- 白名单外 Effect 配置在校验期失败  

## 11. DELAYED-EVENT-01 authoring 与存档契约（2026-09-25）

[ADR-0040](../40-process/43-decisions/ADR-0040-delayed-content-event-authority-and-snapshot-v10.md) 确认 `{"kind":"scheduleEvent","id":"base:event_followup","amount":1}`，Id 必须为存在的 ContentEvent，Amount 必须是 1～Int32.MaxValue 整数日，runtime 再检查 deadline overflow。
Step/Choice 普通 Outcome 复用 Shared editor；不新增节点或作者手填 runtime id。WorldOpportunity expireOutcomes 的窄白名单保持四种轻量结果，不因此扩成延迟触发入口。
Snapshot v10 在 ContentProgress 保存独立队列和 next sequence；旧 v1～v9 严格拒绝。状态 Implementation Complete / Producer Acceptance Pending，具体接线见 [263](../40-process/263-delayed-event-01-content-event-scheduling-2026-09-25.md)。

## SOCIAL-QUEST-01 / Snapshot v11（2026-09-25）

Producer Accepted / Sealed；见 [264](../40-process/264-social-quest-01-secret-realm-social-topic-and-temporary-companion-2026-09-25.md)。questKind 正式 Data→Core 投影，schema/reference validator 同步，未知值拒绝。Snapshot v10→v11 保留 scheduled events，新增 ContentProgress.questCompanions 必备数组；实体/实例/状态/受控 Squad 验证，rehydrate 后再验证 secretRealm。v1～v10 拒绝，无 runtime migration。
