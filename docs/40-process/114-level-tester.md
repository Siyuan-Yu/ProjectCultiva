# 114 · Level Tester（逻辑关卡试玩台）

> 状态：**LevelTester 工具整理与输入隔离 Implementation Complete / Producer Acceptance Pending**｜日期：2026-10-10
> 场景：`Assets/Scenes/LevelTester.unity`  
> 入口：`PlayableHostBootstrap` + `LevelTesterHud`  
> 菜单：`XianXia/Level Tester/…`  
> 实现索引：[168-level-tester-cheat-tools-consolidation-2026-08-27.md](168-level-tester-cheat-tools-consolidation-2026-08-27.md)；CIVILIAN 唯一当前状态表见 [267](267-civilian-life-01-design-freeze-2026-09-27.md#当前功能与验收状态表唯一维护点2026-10-10)

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
| 10 | 开发 Cheat：构造验收状态 | ✅ **LevelTester Cheat Tools**（`` ` `` 或顶栏按钮） |
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
3. 点 **Import** 在 Scene 里看 prefab 预览；Play 后如需重建 Session，用 Cheat Tools → **Snapshot / Session → Reset LevelTester Session**  

改 JSON 后再点 **Import** 会先清掉预览根下全部旧物件再刷（不必先切走再重进场景）。若仍叠图，先点 **Clear Preview** 再 Import。

NPC／任务仍来自 Content 包；这里只换「这一张本地图」。

---

## LevelTester Cheat Tools（开发验收）

**打开方式：**

- 键盘 **`` ` ``**（BackQuote）
- 或 `LevelTesterHud` 顶栏 **「Cheat Tools」** 按钮

**顶层页签（Tab，2026-10-10）：**

| Tab | 能力 |
|-----|------|
| 当前验收 | CIVILIAN A–G 卡片：食宿、工作/建设、占领/追击、医疗搬运、囚室交接、照料/招降、存读档；卡片只导航/说明，不自动判定通过 |
| 人物与资源 | 原 Battle：队伍数值、弥留、NPC Squad、SUCCESSION / CONTROL-HANDOFF |
| 世界与内容 | 原 Background + Diplomacy + Content：后台旅行、外交、Shop、Auction、Social Quest、延迟事件、动态发现、CIVILIAN 准备与按需诊断 |
| 存档与会话 | 原 Snapshot：Save/Load、全会话 Reset（二次确认） |
| 诊断 | 原 Time + Diagnostics：Core Tick/次日零点、正常帧速度、authority/性能诊断 |

打开面板后通过五个页签切换；每页保存独立滚动位置。顶部固定显示选中人物/EntityId、ActiveCharacter、Site/owner、场景、数据包、Snapshot schema 与最近操作结果。搜索匹配操作名称、别名、说明和里程碑。没有唯一选中人物时，选中人物操作禁用且不回退到 Active。

旧操作迁移：`Time → 诊断`；`Background / Content / Diplomacy → 世界与内容`；`Snapshot → 存档与会话`；`Battle → 人物与资源`；`Diagnostics → 诊断`。仍有用的 Shop、Auction、Social、Delayed Event、Dynamic Discovery 等历史回归入口继续保留。

浮窗使用 `HostUiHitTest` owner-scoped 持久矩形；作弊窗口内的滚轮、中键、右键与选择不会穿透到近景或 WorldMap，窗口外维持原行为。工具本身不暂停世界；只有文本输入获得焦点时临时阻断世界键盘/相机/交互，失焦、隐藏、禁用或销毁时释放。正式 Modal 优先级不变。

时间工具中的跳时正式名称为“推进至次日零点〔仅 Core 数据〕”，显示当前/目标/实际游戏小时。该入口不能证明逐帧 Host 移动；食宿、搬运、押送、追击等使用 1x/2x/5x/20x 正常帧循环。定位统一调用 Host/CameraRig 正式入口，不传送人物或修改业务状态。

Cheat Tools **仅**用于 LevelTester 开发／人工验收，不进入正式 Gameplay UI，不为各系统单独占用 F-Key。

### DYNAMIC-DISCOVERY-01 验收入口（2026-09-24）

进入 **Content** 页签后可确定生成三类动态物体：隐藏石碑、公开包裹、世界可见灵草。隐藏石碑用于验证半径外完全不可见、进入半径后永久揭示，以及 onInspect 后仍保留、选择显式解决后才移除；公开包裹用于验证生成时 Activity／Toast／定位；世界可见灵草用于验证直接呈现且不进入 Activity。该页签另有清理这三类 acceptance instance 的按钮和当前实例／发现／Activity 状态摘要。完整 AL～AQ 路线见 [261](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md)。

制作人已于 2026-09-24 完成主链人工验收，DYNAMIC-DISCOVERY-01 与 MAP-COORD-01 均为 **Producer Accepted / Sealed**。Hidden 入口继续只输出开发验收坐标，不增加 Debug Locate；使用 WorldMap 正式坐标参照判断方向后关闭地图实际探索。

---

## 操作键（正式 Gameplay）

| 键 | 作用 |
|----|------|
| Space | 暂停／继续（FormalHud 时间控制） |
| J | 任务日志（打开时暂停世界；可接／进行中／已完成；追踪显示在右侧任务栏） |
| Q / E / F1 / F6 / F7 / F8 / G 等 | 正式 Gameplay 行动（见 FormalHud） |
| 中键／键盘 | 相机平移缩放（Host 相机） |

> **已移除（勿再使用）：** F3/F4/F8/F11/F12 独立 Debug Panel、F5/F9 Snapshot 快捷键、`.`/`N` Bootstrap Step Tick、`[`/`]` Bootstrap 变速、`R` Runtime Session Rebuild。

---

## 和 DemoParityHost 的区别

| | DemoParityHost | Level Tester |
|--|----------------|--------------|
| 目的 | 样板回归／签收 | 日常换关测逻辑 |
| 换地图 | 不方便 | id／路径／TextAsset |
| HUD | 偏干净 | 逻辑调试 + Cheat Tools |
| 验收场景 | 非主要 | **唯一主要人工验收场景** |

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-10-10 | 五页签整理、当前验收卡片、搜索/固定上下文与结果、浮窗输入隔离、Core 时间与正式定位修正；待制作人人工验收 |
| 2026-08-27 | 统一 `HostLevelTesterCheatPanel`；移除分散 Debug Panel 与旧 Development Hotkey |
| 2026-08-13 | 地图真源改 Content/BaseGame/Data |
| 2026-08-13 | 初版：选图／覆盖 JSON／场景菜单／顶栏 |
