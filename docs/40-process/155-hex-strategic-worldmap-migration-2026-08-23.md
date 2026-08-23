# 155 — Hex Strategic WorldMap Migration

> **日期：** 2026-08-23  
> **状态：** READY FOR ACCEPTANCE（H1–H9 + RimWorld 视觉 IMPLEMENTED · H8 源码待删）  
> **制作人决定：** 战略空间从 Node+Route 图结构迁移为连续 HexGrid + StrategicSite

---

## OLD → NEW

| 旧模型 | 新模型 |
|---|---|
| `WorldNode` 作为图顶点 | `StrategicSite` 坐落于 `HexCoord` |
| `WorldRoute` 连接 Node | **删除**（道路 = `HexTile.IsRoad` / Terrain） |
| `FormalArmy` AtNode / OnRoute / RouteProgress | `CurrentHex` + `HexPath` + `StepProgress` |
| WorldMap：方框 Node + 连线 | WorldMap：连续六边形格 + Site 标签 |

---

## 系统分类

### PRESERVED（不改规则，仅适配位置引用）

- Faction / War / Alliance / Vassalage
- CaptureObjective / SettlementAuthority
- Character / Cultivation / Combat / Quest / Dialogue
- LocalMap（与 StrategicSite 关联）
- Global Strategic Toolbar
- Army membership / Formation UX
- BattleOffer / Auto / Manual Battle
- RetreatingArmy（待 H7 完整 Hex 化）

### MIGRATED

- 战略空间最小单位 → `HexTile`（Axial `q,r`）
- 重要地点 → `StrategicSite` on Hex
- FormalArmy 位置真源 → `CurrentHex`（`UsesHexStrategicPosition`）
- Army 移动 → `ArmyHexTravelService` + `HexPathfinder`（A*）
- WorldMap 绘制 → `HostHexGridDrawing`（H4 初版）
- Ch01 Prototype → `Ch01HexPrototypeMapBuilder`（青石荒村 / 青石路）

### SUPERSEDED

- Route Entity 作为正式战略移动 Domain
- `RouteProgress` / `RouteAnchor` / `OnRoute` 作为最终位置模型
- WorldMap Editor：Create Node → Connect Route
- 153 中所有 Node→Route movement 验收项（见 153 修订）

### DEFERRED

- Fog of War / Vision
- HexOwnerFactionId / Territory / Borders
- AI Threat Director / Offensive Operations
- Trade / Formal Diplomacy UI
- Terrain 最终数值平衡
- Weather / Supply / ZOC / River crossing
- Road building gameplay
- Procedural world generation
- Generic Hex LocalMap Encounter Generator

---

## 制作人约束（INPUT CONTRACT = PRESERVED · MOVEMENT BACKEND = REPLACED）

玩家操作语义**不变**：

| 玩家输入 | 命令语义 | Hex Backend |
|---|---|---|
| 左键我方 FormalArmy | Select Army | 不变 |
| 右键可通行 Hex | MoveArmy(DestinationHex) | `ArmyHexCommandService.MoveArmy` |
| 右键 StrategicSite Hex | MoveArmy(Site.HexCoord) | 同上 |
| 右键敌方 Army/Stack | AttackArmy(TargetArmyId) | `ArmyHexPursuitService` |
| 追击中右键新 Hex | 取消 Pursuit + 新 Move | `CancelPursuitForAttacker` + Move |

**禁止**在 Hex Runtime 激活后从玩家路径调用：`MoveArmyToNode` / `MoveArmyToTargetArmy` / `RouteProgress` pursuit。

### Legacy Runtime Audit（目标状态）

| 检查项 | 目标 | 当前（Hex active） |
|---|---|---|
| Player Move 是否仍调用 WorldRoute | **NO** | **NO** |
| Player Attack 是否仍调用 RouteProgress / StackAnchor | **NO** | **NO** |
| Enemy Army 是否仍使用 Route movement | **NO** | **NO**（Hex active + bandit Hex 初始化） |
| Battle Return 是否仍返回 RouteAnchor | **NO** | **NO**（`ArmyHexBattleAnchorService`） |
| RetreatingArmy 是否仍保存 Route position | **NO** | **NO**（保存 HexQ/R） |
| WorldMap Army icon 是否仍依赖 RouteProgress | **NO** | **NO** |
| 旧 Route movement 是否仍能从玩家 Runtime 到达 | **NO** | **NO**（`HexStrategicLegacyGuard` 拒绝 + Pursuit 早退） |

Hex active 时禁止双轨：玩家 Hex、敌人 Route、或地图 Hex / 实际 Route 并存均属迁移失败。

### HEX-CMD 验收（玩家视角）

| ID | 操作 | 期望 |
|---|---|---|
| HEX-CMD-01 | 左键我方 Army | 正常 Selected |
| HEX-CMD-02 | 右键远处可通行 Hex | Hex path 预览 + 军队移动；不走 Route |
| HEX-CMD-03 | 右键 StrategicSite | 移动到 Site.HexCoord |
| HEX-CMD-04 | 右键移动中敌方 Army | Attack/Pursuit；`TargetArmyId` 保持 |
| HEX-CMD-05 | 追击中右键另一 Hex | 取消 Pursuit + 新 Move |
| HEX-CMD-06 | 全程 | 不得调用正式 Route movement |

EditMode：`ArmyHexCommandTests`（HEX-CMD-02/04/05 子集）。Unity Runtime：**NOT RUN**。

---

| Phase | 内容 | 状态 |
|---|---|---|
| H1 | HexCoord / HexGrid / HexMath / A* / HEX-01~05 | **IMPLEMENTED** |
| H2 | StrategicSite / Site-on-Hex / SITE-01~03 | **IMPLEMENTED** |
| H3 | FormalArmy Hex position / ArmyHexTravel / ARMY-HEX-01~04,06 | **IMPLEMENTED** |
| H4 | WorldMap RimWorld 式 Hex 格渲染 / 悬停 / 选格情报 / 路径高亮 | **IMPLEMENTED** |
| H5 | Hex WorldMap Editor（刷地形/道路/放 Site） | **IMPLEMENTED** — [158](158-hex-world-content-authoring-pipeline-2026-08-23.md) `WorldGraphEditor` + JSON Pipeline |
| H6 | Pursuit / Battle Contact on Hex | **IMPLEMENTED**（相邻格接战） |
| H7 | Battle Return / Snapshot v3 | **IMPLEMENTED** |
| H8 | Legacy Route movement removal | **PARTIAL**（Runtime 守卫；源码待删） |
| H9 | 全 Prototype 地图迁移 | **IMPLEMENTED**（`BuildFullFromWorldGraph`） |
| H10 | 验收 + 153 更新 | **READY FOR ACCEPTANCE** |

---

## 核心代码入口

| 类型 | 路径 |
|---|---|
| Hex Domain | `Assets/Scripts/Core/World/Hex/` |
| StrategicSite | `StrategicSite.cs`, `StrategicSiteBoard.cs` |
| Army Hex Travel | `ArmyHexTravelService.cs` |
| Hex Commands | `ArmyHexCommandService.cs`, `ArmyHexPursuitService.cs`, `HexStrategicRuntime.cs` |
| Position Resolver | `FormalArmyHexWorldPositionResolver.cs` |
| Prototype Map | `Ch01HexPrototypeMapBuilder.cs` |
| Session Bootstrap | `HexStrategicMapBootstrap.cs`, `StrategicBootstrap.cs` |
| Host Drawing | `HostHexGridDrawing.cs` |

---

## Pursuit + Vision（DEFERRED）

HexGrid 为未来 Fog of War 基础。Pursuit 在 H6 改为 `TargetArmy.CurrentHex` 跟踪；视野外取消 Pursuit 规则仅文档记录，本轮不实现。

---

## Snapshot

**Schema v3**（`WorldSnapshot.CurrentSchemaVersion = 3`）：保存 `CurrentHex`, `HexPath`, `StepProgress`, `RetreatingArmy.HexQ/R`。v1/v2 存档 **不兼容**。

## 视觉（RimWorld 式 — IMPLEMENTED）

- 填充六边形格 + 描边（`HostHexGridDrawing` GL 三角扇）
- 地形着色（平原/森林/山地/水域/道路）
- 鼠标悬停高亮、选中格高亮
- 移动路径：格填充 + 中心连线
- Site 名牌悬浮于 Hex 上
- 右侧情报面板：左键选格显示地形/地点详情
- 准确点击拾取：`HexMath.WorldToHex`

仍属后续打磨（非 blocker）：贴图美术、Fog of War、动画过渡。
