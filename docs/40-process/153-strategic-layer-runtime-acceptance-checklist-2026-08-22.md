# 153 — 战略层 Host 双入口（角色／军队列表）运行时验收清单

> **日期：** 2026-08-22（修订：Global Strategic UI 架构）  
> **范围：** Host / Acceptance 交互层 only；**不改 Strategic Domain 规则**  
> **状态：** IMPLEMENTED · **STATIC REVIEW PASSED** · **UNITY VERIFICATION DEFERRED**

---

## Global Strategic UI Architecture

**长期原则：**「系统从全局战略 UI 进入，地点从地图 Node 进入。」

| 层级 | 职责 | 入口 |
|---|---|---|
| **Global Strategic Toolbar** | 系统级战略管理 | 大地图工具栏 **「战略」** 区 |
| **Global Strategic Panels** | 全局数据列表 + Detail | Character List / Army List（本轮） |
| **Node Context** | 地点上下文操作 only | 右键节点菜单 |

### WorldMap system-level management → Global Strategic Toolbar

- 不依赖当前选中的 Node  
- 不依赖镜头位置  
- 随时可打开；展示全局数据  

**本轮 IMPLEMENTED：**

| 模块 | 按钮 | 面板 |
|---|---|---|
| Character | 「角色」 | 全局 Character List + Detail + 组军 |
| Army | 「军队」 | 全局 FormalArmy List + Detail |

**未来 RESERVED（未实现，勿标 IMPLEMENTED）：**

| 模块 | 预期职责 |
|---|---|
| Territory | 全局 owned Nodes / 领土状态 |
| Faction / Diplomacy | 全局势力列表；Declare War / Alliance / Vassalage / Negotiation |
| Trade | Faction 级战略交易（非 LocalMap 商人） |
| Mission / Event | 全局任务／事件（若需要） |

实现类：`HostGlobalStrategicToolbar`（`ModuleId` + `ImplementedEntries` 可扩展注册）。

### Node → contextual location actions only

Node **允许：** Inspect、Move target、Attack target、Enter LocalMap、当地驻军／目标／资源查看等。  
Node **禁止：** Character / Army / Diplomacy / Alliance / Vassalage / Trade / Global Territory 的**主要入口**。  
**已删除：** Node → 军团管理 / 节点组军。

### 信息职责划分

- **Character：**「我的人在哪里，在干什么？」  
- **Army：**「我的战略军队在哪里，在干什么？」  
- **Faction / Diplomacy（未来）：**「天下有哪些势力，我和他们是什么关系？」  
- **Territory（未来）：**「我控制哪些地方？」  
- **Node：**「这个具体地点是什么、我能对它做什么？」  

### Faction ≠ Node

Landless Faction 仍应出现在未来 Faction List（外交不依赖 Node 入口）。

### Development vs Product UI

- **F8 战略验收：** Development / Acceptance only；**不是**正式 Diplomacy / Army UI。  
- 正式外交／交易未来从 **Global Strategic Toolbar → Faction / Diplomacy**，不是 Node。

---

## 1. 入口位置

| 入口 | 位置 | 说明 |
|---|---|---|
| **Global Strategic Toolbar** | 大地图底部工具栏，标签 **「战略」** + `[角色]` `[军队]` | 可扩展结构；本轮仅两模块 |
| **角色列表** | Toolbar「角色」 | 全局；永不灰掉 |
| **军队列表** | Toolbar「军队」 | 全局；无军队仍可打开 |
| **战略验收 F8** | 同工具栏（独立 Dev 按钮） | 非 Product UX |

打开大地图：`HostWorldMapPanel`。

**主角团：** 三人 Membership = `base:faction_player`（UI 显示「主角团」）；附庸于压迫宗门。角色列表**不含**压迫宗门 NPC。

---

## 2. 静态审计（代码复核）

| # | 检查项 | 期望 |
|---|---|---|
| 1 | Node 菜单是否仍存在「军团管理」 | **NO** |
| 2 | 玩家是否仍可 Node Army Management | **NO** |
| 3 | Character / Army 是否属于 Global Strategic Toolbar | **YES** |
| 4 | Toolbar 结构是否允许未来增加模块 | **YES** |
| 5 | 是否实现未批准的 Diplomacy / Trade UI | **NO** |
| 6 | Node 是否仍承担任何全局战略系统入口 | **NO** |
| 7 | 0 Army 时 Army 按钮是否仍可打开 | **YES** |
| 8 | Army 单击 Select + Detail；双击 Locate | **YES** |
| 9 | Domain 规则是否被改动 | **NO** |
| 10 | F8 是否被当作正式 Army/Diplomacy UI | **NO** |

---

## 3. 角色列表操作（CHAR / UI-ARM）

### UI-ARM-01 · Character button opens global Character List
1. 打开 WorldMap  
2. Global Strategic Toolbar → **「角色」**  
3. 面板标题含 `[Global Strategic UI]`  

### CHAR-01 / CHAR-02 / ARM-01
（同前版：单击 Detail；双击定位；多选组军）

---

## 4. 军队列表操作（UI-ARM / ARM）

### UI-ARM-02～04 / ARM-02～09
（同前版：empty state、单击 Detail、双击 Locate、Detail 五项操作）

---

## 5. Unity 手操总表

| ID | 步骤 | Pass |
|---|---|---|
| UI-ARM-01 | Toolbar「角色」→ 全局列表 | ☐ |
| UI-ARM-02 | Toolbar「军队」→ 全局列表 | ☐ |
| UI-ARM-03 | 无军队 empty state | ☐ |
| UI-ARM-04 | Node 无 Army Management | ☐ |
| ARM-01～09 | 军队流程 + Detail 操作 | ☐ |
| CHAR-01～02 | 角色单击／双击 | ☐ |
| — | 选中军队后右键移动 | ☐ |
| PUR-01 | 攻击停止敌军 → 追上 → BattleOffer | ☐ |
| PUR-02 | 攻击同路移动敌军 → 持续跟随、不抖、追上接战 | ☐ |
| PUR-VISION | 失去视野自动停止追击 | **DEFERRED** — REQUIRES STRATEGIC VISION / FOG OF WAR（[154 §3.4](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)） |
| RTS-ATTACK-01 | Attack click does not teleport Army | ☐（`ATTACK-POS-01/02`） |
| RTS-ATTACK-02 | Mid-route attack remains position-continuous | ☐（`ATTACK-POS-03/04/06`） |
| RTS-ATTACK-03 | Pursuit repath remains position-continuous | ☐（`ATTACK-POS-05/07`） |

> **Pursuit 验收说明：** 当前阶段允许对 `TargetArmyId` 做 **临时全知** 位置追踪（Vision 未实现）。不要把 PUR-VISION 列为本轮 FAIL；Vision 落地后再验收。

**签注：** _______________　**日期：** _______________

---

## 6. 静态验证

- EditMode：`HostStrategicRosterQueriesTests`、`StrategicFinalClosureTests`（附庸绑定）  
- Host：`HostGlobalStrategicToolbar` + 双 Panel + Node 无组军  

**Unity 手操：** DEFERRED

---

## 7. 相关文件

| 文件 | 职责 |
|---|---|
| `HostGlobalStrategicToolbar.cs` | Global Strategic Toolbar（可扩展 ModuleId） |
| `HostStrategicRosterQueries.cs` | 只读列表（PlayerFaction / 主角团） |
| `HostStrategicCharacterListPanel.cs` | Character 全局 Panel |
| `HostStrategicArmyListPanel.cs` | Army 全局 Panel |
| `HostArmyFormPanel.cs` | Army Detail / Creation（embedded） |
| `HostWorldMapPanel.cs` | Toolbar 集成、Node 仅地点操作 |
| `Ch01ScenarioStrategicSetup.cs` | PlayerFaction + Vassalage 绑定 |
| `scenarios.json` / roster | 主角 `factionId: base:faction_player` |

---

## SUPERSEDED BY HEX STRATEGIC MAP MIGRATION (155 / ADR-0025)

以下验收项依赖 **Node → Route movement / RouteProgress / Route pursuit**，已由制作人正式废弃为战略移动模型：

- 大地图 Node 方框 + Route 连线作为**正式**战略地图呈现
- FormalArmy `OnRoute` / `RouteProgress` / `RouteAnchor` 作为最终位置真源
- Pursuit 基于 RouteId / RouteProgress 的接触与改道验收
- WorldMap Editor：Create Node / Connect Route 工作流

历史记录保留；新验收见 **155 §HEX STRATEGIC MAP RUNTIME ACCEPTANCE**。

---

## HEX STRATEGIC MAP RUNTIME ACCEPTANCE（新增 · IN PROGRESS）

| # | 验收项 | 状态 |
|---|---|---|
| H-01 | 按 M 打开大地图显示**连续 HexGrid**（非 Node 方框+连线） | PARTIAL |
| H-02 | 青石荒村 / 青石路 Site 在 Hex 上，具真实 Hex 距离 | IMPLEMENTED（Domain） |
| H-03 | Road Hex 串联两 Site | IMPLEMENTED（Ch01 builder） |
| H-04 | 选 Army → 右键目的地 → Hex path preview | PENDING |
| H-05 | Army 逐 Hex 移动，StepProgress 视觉平滑 | PARTIAL（Domain yes · Host input pending） |
| H-06 | Attack / Pursuit 无瞬移（Hex 模型） | PENDING（H6） |
| H-07 | Hex contact → BattleOffer | PENDING（H6） |
| H-08 | Manual Battle 结束返回 EncounterHex | PENDING（H7） |
| H-09 | Hex WorldMap Editor 刷地形/道路/放 Site | PENDING（H5） |
| H-10 | Snapshot v3 恢复 Army CurrentHex / mid-step | PENDING（H7） |

### MAP PRESENTATION PASS 1（2026-08-23）

| # | 验收项 | 状态 |
|---|---|---|
| MAP-PRES-01 | Terrain Legend（图例与 Inspect 中文名一致） | IMPLEMENTED |
| MAP-PRES-02 | 矩形 Odd-R 100×50 外观（非 axial 平行四边形） | IMPLEMENTED |
| MAP-PRES-03 | 全图缩放 Fit World to Map Viewport（排除 Info Panel） | IMPLEMENTED |
| MAP-PRES-04 | 全图模式无大面积无意义空白 | IMPLEMENTED（待 Runtime 验） |
| MAP-PRES-05 | WorldSite 程序图标（非 debug 小点） | IMPLEMENTED |
| MAP-PRES-06 | Site 图标不破坏 Hex Picking | IMPLEMENTED（渲染 only） |

