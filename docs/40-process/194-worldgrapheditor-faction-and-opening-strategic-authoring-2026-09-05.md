# 194 · WorldGraphEditor 势力管理与开局战略创作（2026-09-05）

> 状态：**实现完成，待编辑器人工验收**
> 范围：为已稳定的 `strategicFaction` 与 `OpeningScenario.strategicOpening` 内容链提供设计师编辑界面；不修改 Unity Runtime、存档、Territory 算法、旅行、战斗或外交玩法。
> 前置文档：[193 正式势力内容与 WorldGraphEditor 领土编辑](193-strategic-faction-content-and-worldgraph-territory-authoring-2026-09-04.md)。

---

## 1. 本轮结论

1. `WorldGraphEditor` 的四个固定功能页签为：**地图编辑 → 势力范围 → 势力管理 → 开局战略**。
2. 正式势力唯一真源仍是 `Content/<Package>/Data/Factions/factions.json`；编辑器不在 HexWorld、Scenario 或 Core 保存第二份势力配置。
3. 已有势力的 `FactionId` 永久只读；名称、`mapColor`、`territorySelectable` 和排序可以修改。若未来确需改 ID，必须单独制作全 Content 引用迁移工具。
4. 开局战略编辑器只修改当前选中的 `OpeningScenario` 的 `strategicOpening` Raw JSON 节点；不重建 Scenario 其它字段，不同步其它 Scenario，不修改运行中的 `StrategicBoard` 或任何 SaveGame。
5. 本轮只提供新游戏开局内容创作，不新增游戏内宣战、议和、联盟、附庸、外交 AI 或关系数值玩法。

---

## 2. 势力管理

### 2.1 内容真源与字段

文件：`Content/BaseGame/Data/Factions/factions.json`。

每项 `strategicFaction` 的创作字段：

| 编辑器中文名称 | 内容字段 | 规则 |
|---|---|---|
| 势力名称 | `name` | 非空；用于中文展示 |
| 势力 ID | `id` | 已有势力只读；新建时必须符合 `namespace:local_id` |
| 地图颜色 | `mapColor` | 合法 `#RRGGBB` |
| 可用于领土绘制 | `territorySelectable` | 只控制是否进入 Territory Brush |
| 排序 | `sortOrder` | 小值在前；同值按 ID 稳定排序 |

新建势力默认提供 `base:faction_new` 风格 ID、可用于领土绘制和普通排序序号。若新建时只输入本地名，保存时补为 `base:` 命名空间。计算下一个排序号时忽略 `900` 及以上的特殊排序，因此不会把山匪的 `999` 推导成普通势力的 `1009`。

`territorySelectable = false` 不表示势力失效。它只令该势力不出现在 Territory Brush；角色、FormalArmy、WorldSite 引用和开局战争／外交创作仍可使用它。基础内容中的山匪即为该例。

### 2.2 删除保护

删除前由 Shared `FactionReferenceScanner` 递归扫描整个 `Content/BaseGame/Data`。以下正式引用会阻止删除：

- `CharacterDefinition.defaultFactionId`；
- Scenario Spawn 与 Character Roster 的 `factionId`；
- `FormalArmy.factionId`；
- `WorldSite.ownerFactionId`；
- `TerritoryRegion.controlFactionId` 与独立 Hex 控制权；
- `strategicOpening.playerFactionId`；
- `vassalages`、`alliances`、`initialWars` 中的全部势力字段。

被引用时编辑器列出文件、定义上下文与字段，禁止删除；不会把引用自动清空。无引用势力仍需二次确认，确认后仅从 `factions.json` 删除，保存后 Territory Brush 与后续打开的战略编辑器会读取新目录。

### 2.3 与 Territory 的边界

势力管理只编辑势力定义。它不编辑成员、FormalArmy、WorldSite、TerritoryRegion 或 Hex 所有权。既有的 Territory Brush、WorldSite Footprint／辖区宏操作、无势力擦除与地图 Undo/Redo 保持原行为；保存势力后只刷新其颜色和可选列表。

---

## 3. 开局战略编辑

### 3.1 场景来源与保存范围

编辑器通过 `PackageStore` 加载当前 Content Package 的全部 `type = "openingScenario"` 定义，并以中文场景名称和 ID 显示。不存在任何“三个 BaseGame 场景”的硬编码列表。

当前基础内容的下列三个场景均已配置 `strategicOpening`：

| 场景 | 玩家势力 | 附庸 | 联盟 | 开局战争 |
|---|---|---:|---:|---:|
| `base:scenario_playable_day` | 主角团 | 1 | 0 | 2 |
| `base:scenario_chapter1_harness` | 主角团 | 1 | 0 | 2 |
| `base:scenario_ch01_reference` | 主角团 | 1 | 0 | 2 |

三者是独立定义。编辑其中一个不会自动改写另外两个。

保存时仅执行：

```text
当前 OpeningScenario Raw JSON
  → 替换 strategicOpening 节点
  → PackageStore.SaveDefinition
```

因此 `spawns`、`openingRelations`、日程、地图引用和未来字段均保留其原始 JSON 内容。若某场景没有 `strategicOpening`，编辑器明确显示“尚未配置”；点击创建后必须手动选择玩家势力，不能静默回退为主角团。

### 3.2 可编辑内容

所有下拉框从 `StrategicFactionAuthoring.LoadStrategicFactions(package)` 读取**全部**正式势力，显示颜色、中文名与 ID，不按 `territorySelectable` 过滤。

| 编辑区 | JSON 字段 | 规则 |
|---|---|---|
| 玩家势力 | `playerFactionId` | 必须选择一个存在的正式势力 |
| 附庸关系 | `vassalFactionId → overlordFactionId` | 不能自附庸、同一附庸不能有两个宗主、禁止套娃 |
| 开局联盟 | `factionAId ↔ factionBId` | 不能自联盟、不能重复或反向重复、同一势力不能加入多个联盟、附庸不能独立结盟 |
| 开局战争 | `declarerFactionId → targetFactionId` | 保留宣战方向；不能自战、不能重复或反向重复、不能与联盟为同一势力对 |

保存前使用 Shared `OpeningStrategicAuthoring` 进行上述语义校验。该校验与生产 `ContentReferenceValidator` 的 `strategicOpening` 规则保持相同语义；即时 UI 用于编辑，最终保存门禁拒绝不合法内容并给出中文原因。

### 3.3 未保存修改

势力管理和开局战略都采用显式保存，不会因下拉框选择而立即写盘。切换场景、关闭窗口或删除当前仍在战略编辑中的势力前，编辑器要求保存、放弃或取消；本轮不建立跨 `factions.json` 与 `scenarios.json` 的 Undo/Redo 栈，也不影响既有地图 Undo/Redo。

---

## 4. 深色主题与页签稳定性

### 4.1 开局战略与势力管理窗口

开局战略窗口使用统一深色前景层级：

| 用途 | 颜色 |
|---|---|
| 主标题、正文、下拉项、箭头 | `#F2F4F8` |
| 场景标签、场景 ID、说明、表格列标题 | `#C7D0DA` |
| 未配置开局战略等提示 | `#FFBD70` |
| 分隔线与弱边界 | `#46515F` |

窗口继承应用的深色输入、ComboBox、按钮和悬停／焦点样式。势力下拉项的颜色块、中文名和 ID 均显式使用浅色文字，避免默认系统黑字落在深色背景。势力管理窗口也继承同一主前景并把标签切换为明亮灰。

### 4.2 四个固定页签

原本左侧栏宽度为 180px，标准 WPF `TabPanel` 在四个中文页签下会自动换行；WPF 为将选中项放入激活行会重排多行页签头，造成“点击后页签位置变动”的观感。

现改为：

- 左侧栏宽度 340px；
- 页签头使用单行、四列固定 `UniformGrid`；
- 选中时只改变背景、边框和文字颜色。

因此页签顺序永久为：**地图编辑 → 势力范围 → 势力管理 → 开局战略**。不再因选择、文字长度或自动换行而重新排布。

---

## 5. 明确未做事项

- 不新建假势力 `none`、`neutral`、`unowned`；无主地继续仅是 Territory Brush 工具语义。
- 不把 Character Membership、FormalArmy 成员编辑搬入 WorldGraphEditor。
- 不改变 `factions.json`、`strategicOpening` 的 Runtime Loader、`StrategicBoard`、Snapshot 或 SaveGame 契约。
- 不改 TerritoryRegion、WorldSite ownership、Hex painting、Travel、Camera、Battle、外交运行时或 Unity 场景。
- 不做跨文件 Undo/Redo、势力 ID 迁移、外交编辑器或外交玩法。

---

## 6. 验证与人工验收

### 6.1 已执行

- WorldGraphEditor Release 构建：**0 warning、0 error**。
- Shared.Tests：**50/50 通过**。
- `git diff --check`：无空白错误；仅有工作区既有 CRLF 规范提示。

### 6.2 待人工验收

1. 新建临时势力、修改名称和颜色、关闭／重开后确认持久化；确认已有 ID 不可编辑。
2. 关闭“可用于领土绘制”，确认该势力从 Territory Brush 消失、但仍出现在开局战略下拉框。
3. 删除无引用临时势力；再尝试删除主角团，确认列出 Character、Territory、Scenario 等引用并阻止删除。
4. 在 `base:scenario_ch01_reference` 中检查当前玩家势力、1 条附庸、0 联盟、2 场战争；改玩家势力保存后确认只改该场景的 `strategicOpening.playerFactionId`。
5. 添加并删除一条合法联盟；验证自联盟、自战争、联盟与战争同对、反向重复均不能保存。
6. 依次点击四个页签，确认顺序不移动；打开开局战略，确认标题、说明、列标题、下拉项、按钮和分隔线在深色背景上清晰可读。

---

## 7. 实现位置

| 位置 | 职责 |
|---|---|
| `ExternalTools/ContentAuthoring/Shared/StrategicFactionAuthoring.cs` | 势力读取、保存与 ID／颜色校验 |
| `ExternalTools/ContentAuthoring/Shared/FactionReferenceScanner.cs` | 删除势力前的全 Data 引用扫描 |
| `ExternalTools/ContentAuthoring/Shared/OpeningStrategicAuthoring.cs` | `strategicOpening` Raw JSON 投影、定点写回与语义校验 |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/FactionManagerWindow.cs` | 势力管理窗口与删除保护显示 |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/OpeningStrategicEditorWindow.cs` | 开局战略窗口、中文势力下拉与深色主题 |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/MainWindow.xaml(.cs)` | 固定页签入口与刷新现有 Territory Brush |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/App.xaml` | 固定四列页签头样式 |
