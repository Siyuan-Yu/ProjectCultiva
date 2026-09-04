# 193 · 正式势力内容与 WorldGraphEditor 领土编辑（2026-09-04）

> 状态：**实现完成，待 Unity／编辑器人工验收** ｜ 日期：2026-09-04  
> 关联：[2J Hex Territory、Multi-Hex WorldSite 与动态山贼系统](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)／[192 TerritoryRegion V1 基础层硬化](192-phase2j-territory-region-v1-base-layer-2026-09-03.md)  
> 范围：把地图势力名称、颜色和可编辑资格从散落的代码常量收束到正式 Content；为 WorldGraphEditor 补齐可保存、可校验、可撤销的领土创作工具。本文件不改变 Capture、外交、Army、旅行或 TerritoryRegion V1 的运行时政治规则。

---

## 1. 本轮结论

1. `strategicFaction` 是正式 Content 定义，唯一回答“这个政治势力是谁、叫什么、地图用什么颜色、能否作为领土控制者”。
2. `factions.json` 只保存真实势力；**“无势力／无主地”不是势力定义**，而是 WorldGraphEditor 势力笔刷中的固定工具项。
3. `WorldSite.Footprint`、`TerritoryRegion.Hexes[]`、独立荒野控制记录三者保持不同职责；编辑器不能以涂色为由删除辖区结构。
4. 空 `OwnerFactionId`／`ControlFactionId` 是合法的无主状态；不是 `null`、`None` 字符串，也不是虚构的 `base:faction_none`。

---

## 2. 正式势力 Content

### 2.1 文件与字段

正式文件：`Content/BaseGame/Data/Factions/factions.json`。

每个定义使用 `type = "strategicFaction"`，字段如下：

| 字段 | 含义 |
|---|---|
| `id` | 稳定 `FactionId`，全项目统一引用 |
| `name` | 中文展示名称 |
| `mapColor` | `#RRGGBB` 地图表现色；加载期严格校验 |
| `territorySelectable` | 是否可在领土编辑中成为控制势力 |
| `sortOrder` | 编辑器列表排序；同序按 ID 稳定排序 |

当前基础内容包含：主角团、压迫宗门、沧澜渔盟、南堰庄盟、朔风堡、东林海会、西津渡帮、山匪。山匪仍是合法 `FactionId`，可用于 Character／FormalArmy 等引用，但 `territorySelectable = false`，不能获得正式 Territory。

### 2.2 明确不属于势力定义的数据

`strategicFaction` 不保存成员、Army、WorldSite、TerritoryRegion、Hex 归属、外交运行时状态或 Capture 状态。这些分别归 Character／FormalArmy 内容、HexWorld 内容与运行时世界状态所有。禁止为了“无主地”新增假的 faction 内容。

---

## 3. Data 与 Runtime 接线

### 3.1 加载与展示目录

- `DefinitionSchema`、`ContentPackageLoader`、`DefinitionRegistry` 支持 `strategicFaction`。
- `StrategicFactionDefinition` 是 Data Content 定义；加载器拒绝未知字段、重复 ID、非法颜色。
- `StrategicFactionContentInstaller` 在内容包成功加载后，把定义转为 Core 的 `StrategicFactionPresentation` 并安装到 `StrategicFactionCatalog`。
- 若加载的临时内容包没有 `strategicFaction`，安装器重置到既有 fallback，避免残留上一次内容包的展示状态。
- `StrategicFactionAuthoringQueries` 提供按 `sortOrder` 稳定排序的编辑器只读投影；编辑器不复制一套 faction JSON 解析规则。

### 3.2 交叉引用校验

`ContentReferenceValidator` 会校验以下非空 `FactionId` 都能解析为正式势力：

- `formalArmy.factionId`；
- scenario、spawn、roster 的 faction 引用；
- `WorldSite.ownerFactionId`；
- `TerritoryRegion.controlFactionId`；
- `standaloneTerritoryHexes[].controlFactionId`。

未知 ID 为 Content error。使用 `territorySelectable = false` 的势力作为 Site、Region 或 standalone 的领土控制者同样是 Content error。空字符串不做 faction 引用校验，因为它的正式含义是无主。

---

## 4. HexWorld 领土内容模型

### 4.1 三种空间数据

| 数据 | 含义 | 是否可由势力笔刷直接改变 |
|---|---|---|
| `WorldSite.Footprint` | 地点本体占用的 Hex | 否；归 Footprint 编辑 |
| `TerritoryRegion.Hexes[]` | Fixed WorldSite 的固化辖区几何 | 对默认辖区只随整块宏操作保留或重算；不按单格删除 |
| `standaloneTerritoryHexes[]` | 不属于任何 Region 的荒野单格明确控制权 | 是；普通势力笔刷可添加／改写，无势力笔刷可删除 |

`standaloneTerritoryHexes[]` 的每项是 `{ q, r, controlFactionId }`。它不能越界、不能重复、不能与 Region 重叠，也不能落在任何 WorldSite Footprint 内。

### 4.2 WorldSite 默认辖区宏操作

编辑器命中 WorldSite 的 Footprint 或默认外围一圈时，不做单格操作：

1. 使用命中的 Site；
2. 更新 `site.OwnerFactionId`；
3. 更新绑定 Region 的 `ControlFactionId`；
4. 以该 Site 的 Footprint 与一圈外围生成/保持默认辖区几何；
5. 保持 `RegionId`、`PrimaryWorldSiteId` 与 Region 本身。

因此，给一个无主 Site 重新涂势力无需新建 Region；将 Site 设为无主也不删除 Region。`OwnerFactionId == TerritoryRegion.ControlFactionId` 仍是必须保持的关系。

---

## 5. WorldGraphEditor「势力范围」页

### 5.1 正式势力列表

列表只显示 `territorySelectable = true` 的真实 faction，并按 `sortOrder` 排序。展示名称和颜色均来自 `factions.json`，不再从地图现有 Owner ID 临时拼列表。

「管理势力…」窗口只管理真实 `strategicFaction`：可编辑名称、颜色、领土可选资格和排序；删除前会扫描 Content 引用，已被引用的 faction 不可删除。

### 5.2 固定无势力笔刷

势力范围列表第一项固定为：

```text
□ 无势力 / 无主地
```

该项不参与 `sortOrder`，也不受搜索筛选隐藏。它只存在于 Territory Brush List，不使用 faction 色；列表和当前笔刷栏均用深灰空心方框表示，避免被误解为“白色势力”。

选择后的行为：

| 操作目标 | 左键／左拖 | 右键／右拖 |
|---|---|---|
| 普通 standalone 荒野 Hex | 删除 standalone 控制记录；本来无主则 no-op | 同左 |
| WorldSite Footprint 或默认辖区 | 整个 Site 的 Owner/Controller 置空，Region 几何保留 | 同左 |

普通 faction 笔刷的右键仍是快捷擦除；无势力笔刷的价值是允许左键连续大面积清除。

### 5.3 预览、Undo 与 Inspector

- 普通 faction 笔刷使用该势力颜色预览。
- 无势力笔刷使用低透明灰色覆盖与描边；命中 WorldSite 时预览 Footprint 加外围一圈，明确提示会清除整块辖区控制。
- 每次拖笔只推入一次 Undo 快照；同一笔划重复经过同一 Site 只执行一次宏操作。因此 Ctrl+Z 一次可恢复一次拖笔涉及的全部 Hex／Site。
- Inspector 对空控制权和空所属势力显示“无”，不展示空字符串、`null` 或虚构 ID。

### 5.4 保存与验证

WorldGraphEditor 通过 `HexWorldContentJson` 保存 `standaloneTerritoryHexes`、Site Owner 与 Region Controller。保存前运行 HexWorld 校验：空控制权合法；但 Region 几何、双向绑定、Footprint 覆盖、跨 Region 重叠、standalone 冲突与 faction 引用仍必须正确。校验错误时禁止保存。

---

## 6. 明确边界与未做事项

本轮没有：

- 新增 `base:faction_none`、`neutral`、`unowned` 或任何伪势力 Content；
- 修改 TerritoryRegion V1 的 Runtime authority、Capture、外交、Army、旅行或战斗逻辑；
- 允许山匪取得正式 Territory；
- 为无主 Site 删除 Region，或让 standalone 覆盖 Site Footprint／Region；
- 将 WorldGraphEditor 领土编辑错误放回 LocalMap 的 MapEditor。

现有 WorldMap Territory Border 的表现逻辑不因编辑器无势力笔刷改变；无主 Hex 的 `ControlFactionId` 为空，运行时不应显示任何势力边界。

---

## 7. 验证记录与人工验收

### 7.1 已执行

- WorldGraphEditor Release 构建：**0 warning、0 error**。
- `Shared.Tests`：**50/50 通过**。
- 覆盖了正式 faction 列表／颜色／排序／引用扫描、standalone 涂刷与 JSON round-trip、WorldSite 宏涂刷、单笔 Undo、无主 standalone no-op、无主 Site 清空控制但保留 Region 几何。
- `git diff --check`：无空白错误（工作区只出现既有换行规范提示）。

### 7.2 尚未执行

- 未运行 Unity Editor、PlayMode 或 Unity Test Runner。
- 未进行本轮 WorldGraphEditor 的人工点击验收。

### 7.3 人工验收清单

1. 打开 WorldGraphEditor 的「势力范围」页，确认第一项永远是“□ 无势力 / 无主地”。
2. 用正式势力在十个普通荒野 Hex 涂刷，确认保存后出现对应 `standaloneTerritoryHexes`。
3. 选择无势力并左拖其中五格，确认五格恢复无主，Ctrl+Z 一次恢复整个拖笔。
4. 对任一 WorldSite 涂主角团，确认 Footprint＋外围一圈整块变色。
5. 选择无势力并点击该 Site 任一辖区格，确认 Site Owner、Region Controller 均为空，Region ID、Primary Site 和 Hex 几何仍保留。
6. 保存、重开编辑器与启动游戏，确认无主 Hex 的 `ControlFactionId` 为空且不显示 Territory Border。

---

## 8. 相关实现位置

| 位置 | 职责 |
|---|---|
| `Content/BaseGame/Data/Factions/factions.json` | 正式势力静态内容 |
| `Assets/Scripts/Data/Content/StrategicFactionDefinition.cs` | Data 定义 |
| `Assets/Scripts/Data/Content/StrategicFactionContentInstaller.cs` | Data → Core 展示目录安装 |
| `Assets/Scripts/Data/Content/ContentReferenceValidator.cs` | faction 交叉引用校验 |
| `ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldEditorDocument.cs` | 领土画笔、宏操作、撤销和保存前数据编辑 |
| `ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldContentValidator.cs` | 编辑器 HexWorld／领土校验 |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/MainWindow.xaml(.cs)` | 势力范围 UI、无势力伪笔刷、预览和 Inspector |
| `ExternalTools/ContentAuthoring/WorldGraphEditor/FactionManagerWindow.cs` | 真实 faction 管理窗口 |

