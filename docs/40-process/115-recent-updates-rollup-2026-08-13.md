# 115 · 近期更新收束（MapEditor／Level Tester／物件规则）— 2026-08-13

> 状态：**文档补录**｜日期：2026-08-13  
> 相对提交：`e387f05`（MapEditor Host prefab 管线）之后 → 本轮推送前  
> 相关：[112 MapEditor](112-map-editor-usage.md)｜[114 Level Tester](114-level-tester.md)｜[113 World Graph](113-world-graph-local-map-architecture-revision-v0.1.md)

---

## 1. 一句话

MapEditor 能正经新建／另存关卡到 Levels；物件与分区拆开；Host 对齐错位修好；并新增 **Level Tester** 场景用一份地图 JSON 测逻辑。

---

## 2. 本轮交付对照

| 主题 | 做什么 | 文档／入口 |
|------|--------|------------|
| 设施缩放手柄 | 四角＋四边；往外放大、往里缩小（修「往下拖反而缩小」） | [112](112-map-editor-usage.md) |
| 分区 vs 物件 | 工具板两页：物件／分区；分区仅标记 | MapEditor + `MapKindCatalog` |
| 树／矿／蒲团／墙／路 | 树 1／2／3、矿石 2×2、蒲团 1×1、墙 1×n 挡路、路纯贴图 | [112](112-map-editor-usage.md) |
| 灵泉 | 改为分区 `zoneSpring`；修炼点用蒲团 | 同上 |
| Host 对齐 | 缩放后包围盒中心对齐；房子按实际占地；墙加深可见 | `HostDemoTileMap` |
| Level Tester | 新场景；选一份 map JSON 测 LocalMap 逻辑 | [114](114-level-tester.md)、`Assets/Scenes/LevelTester.unity` |
| 关卡目录 | `Assets/DynamicData/GameData/Levels/` | Level Tester／MapEditor 另存默认 |
| MapEditor 文件 | 新建空图、另存为、打开地图；启动合并 Levels | MapEditor 工具栏 |

---

## 3. 详细说明

### 3.1 MapEditor

- **缩放物件**：选中后四角＋四边红点；按屏幕方向缩放。  
- **工具板**：`1 · 物件`（田／路／墙／树／矿／蒲团／建筑）与 `2 · 分区`（药田区／农田区／住房／林地／矿区／灵泉区）。  
- **新建空图**：输入新 Id → 存到 Levels（不再卡死「只能建 ch01」）。  
- **另存为**／**打开地图**：默认 Levels；Ctrl+S 写当前文件；Ctrl+Shift+S 另存。  
- 启动时合并 `Assets/DynamicData/GameData/Levels/*.json` 进地图下拉。

### 3.2 物件与分区语义

| 类型 | 编辑 | Host |
|------|------|------|
| 分区 `zone*` | 半透明虚线框 | 半透明色块，无交互、不挡路 |
| 药田／农田 | 可拉片 | 每格交互（生长暂缓） |
| 树 treeS/M/L | 一棵棵 | 1×1／2×2／3×3，可砍（Work） |
| 矿石 ore | 2×2 | 可采 |
| 蒲团 cushion | 1×1 | 可修炼（Cultivate） |
| 墙 | 1×n | 挡路 |
| 路 | 1×1 | 纯贴图 |
| 灵泉 | 用分区 | 旧 `spring` 兼容为分区 |

Prefab：`Assets/Prefabs/Environment/Tiles|Buildings|Props/`；缺则菜单 `XianXia/Content/Ensure MapLayout Prefabs`。

### 3.3 Host 表现修复

- 分区色块曾因精灵锚点在脚底，放大后整体上移 → **缩放后对齐包围盒中心**。  
- 房子曾强制 20×20，和编辑器墙框对不齐 → **按 placement 实际 w×h**。  
- 墙提高排序并加深颜色，更易看见。

### 3.4 Level Tester

- 场景：`Assets/Scenes/LevelTester.unity`（菜单可重建：`XianXia/Level Tester/Create Or Update…`）。  
- Inspector：**选择文件…** 选 Levels 下地图 JSON；开局剧本 Id 另填。  
- 复用完整 Host：寻路、RTS、交互点、时间轴、HUD。  
- F12 重载；顶栏显示当前包／地图／剧本。

**流程：** MapEditor 编图 → 存／另存到 Levels → Level Tester 选该 JSON Play → 逻辑过关后再接美术场景。

---

## 4. 已知未做（本轮边界）

- 作物真实生长／成熟计时  
- World Graph 宏观旅行 UI  
- 墙／树正式美术（现为占位 prefab）  
- 整关 manifest（任务包随地图一键换）— 任务仍读 Content 包  

---

## 5. 建议手操检查

1. MapEditor：新建空图 → 画一片农田区＋农田 → 另存到 Levels  
2. Level Tester：选择该 JSON → Play → 田应在区内、墙贴房子  
3. Console：`WalkGrid from mapLayout`／`mapLayout override`  

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 初版：汇总 MapEditor／分区物件／Host 对齐／Level Tester |
