# 112 · MapEditor 用法（格点地图）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-11  
> 工程：`ExternalTools/ContentAuthoring/MapEditor/`  
> 编辑：`type = mapLayout`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 和 RegionEditor 的区别

| | RegionEditor | MapEditor |
|--|--------------|-----------|
| 编辑什么 | 逻辑地点、邻接、任务挂接 | 格点设施／墙／区域大小 |
| 数据 | `worldRegion.locations[]` | `mapLayout.placements[]` |
| 交互 | 表格 | 画布拖拽、缩放 |

做「第一章地图长什么样、药田多大、墙在哪」→ 用 **MapEditor**。  
做「地点之间怎么走、接什么任务」→ 用 **RegionEditor**。  
游戏里地点名字／NPC 落点来自 Region；设施外观／寻路来自 Map。用 `boundLocationId` 把两边绑在一起。

---

## 怎么打开

- 推荐：`ExternalTools\ContentAuthoring\启动-MapEditor.cmd`  
  或 `Apps\MapEditor\MapEditor.exe`（先跑 `.\publish.ps1`）  
- 调试：`ContentAuthoring.sln` → 启动项目 `MapEditor` → F5  
- **不要**打开各工程下的 `bin\Release\...`（已改到 `.build\`，且易过期）

`MapEditor.exe` 若正在运行，`publish.ps1` 会因文件占用失败——先关掉再发布。

---

## 常用交互

| 操作 | 方式 |
|------|------|
| 缩放 | 顶部 **滑条／＋－**；或 **Ctrl+滚轮**（Shift/Alt+滚轮亦可）；`100%`／`Ctrl+0`；`适应`／`Ctrl+1`。不要依赖单独 Alt（Windows 常抢走） |
| 平移 | **中键拖**，或 **空格 + 左键拖**。自由相机，**可以把整张地图拖出视口** |
| 放置 | 左侧选设施 → 空白单击。**1 · 物件**＝地表／树／矿等；**2 · 分区**＝仅标记 |
| 选择／拖移／缩放物件 | 选择工具；拖移；**四角＋四边**红点调大小（往外拖放大，往里拖缩小） |
| 取消／回选择 | **Esc**；画布 **右键** |
| 删除 | **Delete**／Backspace；或工具栏删除 |
| 复制 | **Ctrl+D** |
| 微调 | **方向键** 1 格；**Shift+方向键** 5 格 |
| 撤销／重做 | **Ctrl+Z**／**Ctrl+Y** |
| 保存 | **Ctrl+S**（写回当前文件）；**另存为…**／**Ctrl+Shift+S** → 默认 `Assets/DynamicData/GameData/Levels/` |
| 新建空图 | 输入新 Id → 存到 Levels；不再卡死在「只能建 ch01」 |
| 打开地图 | **打开地图…** 从 Levels 选一张 JSON |
| 光标格坐标 | 右下角实时显示 |

---

## 设施板默认占地（之后可再调）

| kind | 含义 | 默认格 | 游戏里怎么生成 |
|------|------|--------|----------------|
| `zoneHerb` 等 | **分区标记**（药田／农田／住房／林地／矿区／**灵泉**） | 一片 | 半透明色块，**无交互、不挡路**；灵力加成以后再做 |
| `herbField` | 药田地表 | 12×12（可拉片） | **每格** `HerbPatchTile`，可右键劳动（生长暂未做） |
| `grainField` | 农田地表 | 16×12 | **每格** `FarmlandTile`，可右键劳动 |
| `road` | 道路 | **1×1** | 每格 `DirtRoadTile`，**纯贴图** |
| `wall` | 墙 | **6×1**（拉成 1×n） | 每格 `WallTile`，`blocksMovement` 挡寻路 |
| `treeS` / `treeM` / `treeL` | 小／中／大树 | 1×1／2×2／3×3 | 中心一棵树 prefab，可右键砍伐（Work） |
| `ore` | 矿石 | **2×2** | 中心 `OrePile`，可右键采矿 |
| `cushion` | 蒲团 | **1×1** | `Cushion`，可右键修炼（Cultivate） |
| `house` | 小房子 | **20×20** | 中心一个 `SmallHouse` |
| `rock` | 岩石／棚 | 4×4 | 每格 `RockTile` |
| `cave` / `roadHub` | 洞府／枢纽 | 见默认 | 修炼点或建筑 |

旧数据里的 `forest`／`spring` 现按**分区**处理；`mine` 按 **2×2 可采矿石** 兼容。树用 `treeS/M/L`，修炼点用 `cushion` 放在灵泉区内。

Prefab 目录：
- 地表：`Assets/Prefabs/Environment/Tiles/`
- 建筑：`Assets/Prefabs/Environment/Buildings/`
- 树／矿：`Assets/Prefabs/Environment/Props/`（缺则菜单 `XianXia/Content/Ensure MapLayout Prefabs`）

映射表：`Assets/Scripts/Unity/Host/MapKindCatalog.cs`。

---

## 日常操作

1. 打开包（默认 `Content/BaseGame`）；选 `base:map_ch01_reference`
2. **整图尺寸**：改宽／高后点 **应用地图尺寸**（或回车）；预设 `80×50`／`200×100`／`400×200`
3. 缩放用滑条／＋－／Ctrl+滚轮；平移用中键（可拖出地图）
4. 左侧选工具放置；右侧填 `boundLocationId`（如 `base:loc_ref_herb_field`）和是否挡路
5. **Ctrl+S** 写回 `Content/BaseGame/Data/ch01_reference_map.json`

---

## 游戏里怎么跑（不用在 Inspector 换 JSON）

**日常逻辑试玩请用 [Level Tester](114-level-tester.md)**（`Assets/Scenes/LevelTester`）：可在 Inspector 指定 mapLayout 文件／id 与 openingScenario。

旧说明：`DemoParityHost`／`PlayableHost` 默认读仓库 `Content/BaseGame`，也可填 Level Tester 同款字段。

1. MapEditor **Ctrl+S**
2. Unity 打开 **LevelTester**（或 DemoParityHost）
3. Inspector 确认地图路径／id → **停掉再 Play**（正在 Play 不会热更文件，除非 F12 重载）
4. Console 应出现：  
   `[PlayableHost] WalkGrid from mapLayout …`  
   `[PlayableHost] Synced N location presentation(s) from mapLayout`

有 `boundLocationId` 的设施会在启动时把对应 `worldRegion` 地点的 `presentationX/Z` 拉到设施中心（角色／标签跟布局对齐）。

---

## Host 表现规则

- **分区**（`zone*`）：半透明色块，不注册交互、不挡路
- 药田／农田等 **一格一个 prefab**，挂 `HostMapPlotCell`，并注册为交互点（右键劳动；生长以后再做）
- 树／矿石是中心一个物件 prefab，按占地拟合；默认树挡路、矿石可采
- 房子／枢纽是中心一个建筑 prefab，按精灵真实尺寸拟合到占地
- 寻路：仅看 placement 的 `blocksMovement`（墙／树默认勾上）
- 相机启动时框住整张 `mapLayout`

---

## 验证建议

1. MapEditor 拖一堵 `blocksMovement` 墙保存 → Unity 重 Play → 角色应绕开  
2. 放一块药田并填 `boundLocationId` → 游戏里该位置应是药田地砖，且每格可点  
3. Console 确认 `WalkGrid from mapLayout` 的宽高与编辑器一致  
4. 不应再出现「一整块棕色／青色色板盖住地图」；若出现，多半是旧 Play 未停干净

---

## 注意／已知坑

- 挡路只看 `blocksMovement`  
- 保存必须针对 `Content/BaseGame`；改完要 **停 Play 再进**  
- Unity `SimpleJson` 已支持 `\uXXXX`；编辑器现改为直写 UTF-8 中文  
- 曾误把地图 JSON 写成空文件：现已原子写入并拒绝空内容  
- 移动报错 `Collection was modified`（`HostMoveController.TickMoves`）已修  
- `JsonValueKind.True/False` 编译错误已改（项目用自研 JSON，只有 `Boolean`）

---

## 本轮改动清单（上次文档之后 → 2026-08-11）

相对仓库里已提交的 MapEditor 初版，尚未／刚写入文档的增量如下。

### 编辑器工程与入口

- `publish\` 改名为日常入口 **`Apps\`**；`bin/obj` 改到 **`.build\`**（`Directory.Build.props`）
- 增加 `启动-MapEditor.cmd` 等五个快捷脚本；`publish.ps1` 产出到 `Apps/`
- **不要**再开 `bin\Release\net8.0-windows\MapEditor.exe`（易过期）

### MapEditor 交互

- 缩放：`LayoutTransform` 不可靠 → 改格子像素；顶部滑条／＋－；**Ctrl+滚轮**（Alt 常被 Windows 抢走）
- 启动崩溃：XAML 加载时 Slider 触发 `ValueChanged`，`MapScroll` 未就绪 → 已挡
- 平移：中键／空格拖改为 `RenderTransform` 跟手；**自由相机**，可把地图完全拖出视口
- 保存：UTF-8 中文直写（不再 `\uXXXX`）；原子写入，拒绝空文件
- 设施缩放：选中后四角＋四边手柄；按**屏幕方向**缩放（往下拖南侧是变高，不再反着缩）

### 游戏加载／Host

- **不用在 Inspector 换 JSON**；`PlayableHostBootstrap` 读 `Content/BaseGame`；改完须停 Play 再进
- `mapLayout` → WalkGrid；Console：`WalkGrid from mapLayout … WxH`
- `boundLocationId` → 启动时把地点 `presentationX/Z` 拉到设施中心（`MapLayoutPresentationSync`）
- `HostDemoTileMap` 按 `MapKindCatalog` 刷 prefab：药田／农田／路 **一格一块**；房子约 **20×20** 居中
- 每格可交互：`HostMapPlotCell` + 动态 `HostInteractSpots`（旧硬编码点仅作无布局时回退）
- 精灵按真实 bounds **拟合**到占地，并**对齐包围盒中心**（修分区相对田格上下偏移）
- 房子按编辑器实际 w×h 缩放（不再强行 20×20）；墙提高排序并加深颜色便于看见
- 补 prefab：`WallTile`／`RockTile`／`SmallHouse`／树／矿／蒲团（`MapLayoutPrefabEnsure`）
- 相机框住整张 mapLayout

### 热修

- `SimpleJson` 支持 `\u` 转义
- `ReadBool` 改用 `JsonValueKind.Boolean`（修 CS0117）
- `HostMoveController.TickMoves` 不再边遍历边改字典

### 样例数据

- `ch01_reference_map.json`：可改为 200×100 等；保存写回同一文件
- `ch01_reference_region.json`：地点坐标已按当前设施中心对齐过一版（之后以 Play 时同步为准）

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-10 | 初版：WPF MapEditor + mapLayout + Host 读网格 |
| 2026-08-10 | 发布目录改为 `Apps/`；启动用 `启动-*.cmd` |
| 2026-08-10 | 缩放改为滑条／格子像素／Ctrl+滚轮；修启动崩溃；自由平移 |
| 2026-08-11 | Host 按 kind 刷 prefab；药田一格一交互；房子约 20、路 1；地点坐标与布局同步 |
| 2026-08-11 | 补录本轮完整改动清单（入口／缩放平移／prefab／热修） |
| 2026-08-12 | 设施缩放改为四角＋四边手柄，修正「往下拖反而缩小」 |
| 2026-08-12 | 分区 zone*；树 treeS/M/L；矿石 ore 2×2；路纯贴图；生长暂缓 |
| 2026-08-13 | 逻辑试玩改走 [Level Tester](114-level-tester.md)；Host 支持换 mapLayout |
| 2026-08-13 | 新建／另存为／打开地图；Levels 目录；汇总见 [115](115-recent-updates-rollup-2026-08-13.md) |
