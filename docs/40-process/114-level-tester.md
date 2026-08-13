# 114 · Level Tester（逻辑关卡试玩台）

> 状态：**可用**｜日期：2026-08-13  
> 场景：`Assets/Scenes/LevelTester.unity`  
> 入口：`PlayableHostBootstrap` + `LevelTesterHud`  
> 菜单：`XianXia/Level Tester/…`

---

## 完整需求（本场景要覆盖什么）

Level Tester 用来试玩 **某一个大地图节点的 LocalMap**（例如荒村），只测逻辑与灰盒表现，不接最终美术场景。

| # | 需求 | 本版 |
|---|------|------|
| 1 | 独立场景，不污染 DemoParity／美术关 | ✅ `LevelTester.unity` |
| 2 | 加载 Content 包（任务／NPC／课表／region） | ✅ 默认 `Content/BaseGame` |
| 3 | 指定／切换 **mapLayout**（地图建筑／分区／挡路） | ✅ id + 文件路径 + TextAsset |
| 4 | 指定 **openingScenario**（开局任务线／出生） | ✅ Inspector |
| 5 | 刷图：分区／田／树／墙／交互点 | ✅ `HostDemoTileMap` |
| 6 | 寻路 WalkGrid 来自当前 mapLayout | ✅ |
| 7 | RTS：选中、移动、右键劳动／修炼 | ✅ Host 全套 |
| 8 | 时间：暂停／步进／变速、日程 | ✅ |
| 9 | HUD：任务／事件／角色状态 | ✅ Formal + **任务日志 J** + EventFeed |
| 10 | 热重载：改 JSON 后 F12 重进 | ✅ |
| 11 | 顶栏显示当前包／地图／剧本 | ✅ `LevelTesterHud` |
| 12 | Inspector 浏览磁盘 map JSON（Content 在 Assets 外） | ✅ CustomInspector 按钮 |
| 13 | 编辑模式 Import 预览（prefab 刷图，无需 Play） | ✅ Inspector「Import」 |
| 14 | 地图 JSON 真源 Content/BaseGame/Data | ✅ 与 MapEditor 一致 |
| 15 | World Graph 宏观旅行 UI | ❌ 以后 Phase C |
| 16 | 最终美术场景导入 | ❌ 另场景；本台只测逻辑 |

**流程：** MapEditor 编图 → Level Tester 换地图／剧本 Play → 逻辑过关 → 以后再进美术场景。

---

## 第一次打开

1. Unity 菜单 **`XianXia/Level Tester/Create Or Update Level Tester Scene`**（补齐组件；可重复跑）  
2. 打开 `Assets/Scenes/LevelTester`  
3. 选中 **LevelTester** 物体，看 `Playable Host Bootstrap`：
   - **Preferred Map Layout Id**：`base:map_ch01_reference`
   - **Map Layout File Path**：`Content/BaseGame/Data/Maps/ch01_reference_map.json`
   - **Opening Scenario Id**：`base:scenario_ch01_reference`
4. 点 **Import** 可在 Scene 里预览 prefab 布局；或 Play 测逻辑

---

## 怎么换关

关卡地图 JSON 真源：

`Content/BaseGame/Data/`（各 type 在子目录，见 `Data/README.md`）

1. MapEditor 编好后 **Ctrl+S** 写回 Content，或另存到同目录  
2. 选中 **LevelTester** → Inspector 点 **「选择文件…」** → 选 `Content/BaseGame/Data/*.json`  
3. 点 **Import** 在 Scene 里看 prefab 预览；Play 或 F12 测逻辑  

NPC／任务仍来自 Content 包；这里只换「这一张本地图」。

---

## 操作键

| 键 | 作用 |
|----|------|
| Space | 暂停／继续 |
| . ／ N | 步进 Tick |
| [ ] | 变速 |
| F12 | 重载当前配置 |
| F1 | 显隐 Level Tester 顶栏 |
| J | 任务日志（打开时暂停世界；可接／进行中／已完成；追踪显示在右侧任务栏） |
| 中键／键盘 | 相机平移缩放（Host 相机） |

---

## 和 DemoParityHost 的区别

| | DemoParityHost | Level Tester |
|--|----------------|--------------|
| 目的 | 样板回归／签收 | 日常换关测逻辑 |
| 换地图 | 不方便 | id／路径／TextAsset |
| HUD | 偏干净 | 逻辑调试开着 |

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 地图真源改 Content/BaseGame/Data |
| 2026-08-13 | 初版：选图／覆盖 JSON／场景菜单／顶栏 |
