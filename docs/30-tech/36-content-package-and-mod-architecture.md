# ContentPackage 与 Mod Ready 架构

> 状态：**已冻结（v0.1）— Architecture Freeze Approved（形状）** | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 依赖：`33`、`2E`、`2C`、`34`、ADR-0013～0016  
> 被引用：数据加载、存档、事件内容、本地化、美术资源管线  
> **本阶段只保证 Mod Ready 架构契约，不实现完整 Mod 系统，不写加载器代码。**

## 1. 正式定位

**Mod 支持是正式长期目标，分阶段实现。**

| 现在保证 | 现在不承诺 |
|---|---|
| ContentPackage 统一管线形状 | 任意 C# 脚本 Mod |
| 命名空间 DefinitionId | Steam Workshop |
| Manifest／依赖／校验契约 | 热重载 |
| 存档记录启用包与版本 | 完整地图编辑器 |
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
