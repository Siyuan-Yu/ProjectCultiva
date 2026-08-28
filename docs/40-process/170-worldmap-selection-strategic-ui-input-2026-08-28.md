# 170 · WorldMap 选中真源、Attack Order Snapshot 与 Strategic UI 输入优先级（2026-08-28）

> 状态：**已落地 + LevelTester 人工验收通过**｜日期：2026-08-28  
> 上级：[152 大地图 RTS 纪律](152-worldmap-rts-click-discipline-2026-08-22.md)／[166 Phase 3 FormalArmy](166-phase-3-formal-army-continuous-world-2026-08-27.md)／[169 Snapshot 生命周期审计](169-snapshot-faction-test-entity-lifecycle-audit-2026-08-28.md)  
> 游玩入口：`Assets/Scenes/LevelTester.unity`

---

## 1. 一句话

本轮把大地图 **选中状态**、**Attack Order 持久化**、**Strategic UI 指针优先级** 三件事收口：单一 `HostWorldMapSelectionAuthority` 真源；Snapshot 可恢复 `AttackFormalArmy`；修复 Strategic Panel 点击穿透 WorldMap Hex 的 IMGUI 回归。

---

## 2. 背景与问题

| 问题 | 现象 | 根因 |
|------|------|------|
| Army Marker Load 后变「敌军 2人」/ 无法选中 | Save→Load 后玩家 FormalArmy 显示错误 stack 标签 | `EnsurePresentationStacksFromFormalArmies` 误为玩家军注册 Presentation Stack；`ResolvePlayerFactionId` fallback 不一致 |
| Selection 被 Draw/Input 顺序覆盖 | 点 Marker 后又被 Hex 选中清掉 | Marker、Army List、Hex 各自写选中，无统一真源 |
| Attack Order 不持久 | Load 后追击/攻击指令丢失 | Snapshot DTO 缺 Phase3 order 字段 |
| Strategic UI 点击穿透（回归） | 点角色行/Checkbox 选中背后 Hex；Toggle 无法勾选 | 为修 Selection 曾把 `HandleMapInput` 移到 `DrawStrategicRosterPanels` **之前**，Panel `Block` 尚未注册且地图输入 `e.Use()` 吞掉 IMGUI 事件 |

---

## 3. 架构原则（本轮封板）

### 3.1 职责分离

| 职责 | 负责模块 | 禁止 |
|------|----------|------|
| **选中真源** | `HostWorldMapSelectionAuthority` | 靠 Draw/Input 调用顺序决定最终选中 |
| **指针优先级** | `HostUiHitTest` + `HandleMapInput` dispatch | 在 UI 阻挡路径上 `Event.Use()` 导致 Toggle/Button 失效 |
| **Marker 命中** | `TryHitFormalArmy`（地图输入内） | 把 Marker rect 登记进 UI Block（会误挡 Marker 左键） |

### 3.2 WorldMap 指针优先级（正式）

```
1. Strategic UI Controls（整块 Panel Rect，含空白区）
2. WorldMap Entity Marker（FormalArmy / Avatar / Stack）
3. WorldMap Hex / Background
```

- 鼠标在 Strategic UI 上：**地图输入完全不处理**（左键/右键/滚轮缩放均 skip）
- Block UI **不等于** 提前 `Event.Use()`：只告诉 `HandleMapInput`「这次不要处理地图」
- Marker 与 Strategic UI 是两种 Hit：UI 上 Marker/Hex 都不处理；地图上 Marker 优先于 Hex

### 3.3 HostUiHitTest 双通道

| API | 用途 |
|-----|------|
| `Block(Rect)` | 本帧 WorldMap 指针 dispatch + 下一帧 RTS Selection |
| `BlockSelectionWholeScreen()` | 仅下一帧 RTS Selection（大地图打开挡近景点选），**不计入**本帧 `ContainsCurrentGuiPoint` |
| `ContainsCurrentGuiPoint(gui)` | 本帧 Strategic UI / 菜单 / 情报栏等是否遮挡地图输入 |

---

## 4. 代码改动摘要

### 4.1 WorldMap Army Marker（Load 后显示/选中）

| 文件 | 改动 |
|------|------|
| `ArmyStackAdapter` | 玩家势力 FormalArmy **不**注册/清除 Presentation Stack |
| `ArmyWorldMapPresentation.ShouldDrawArmyStackMarker` | 应画头像时 suppress stack marker |
| `HostWorldMapPanel.ResolvePlayerFactionId` | 委托 `HostStrategicRosterQueries`，与 Army List 一致 |
| `WorldMapArmyMarkerDiagnostics` | Domain/Marker/Selection trace（DEBUG/Editor） |
| `ArmyWorldMapPositionTests` | `ARMY_VIS07` 覆盖 Load 后玩家 FormalArmy 头像路径 |

### 4.2 选中真源 `HostWorldMapSelectionAuthority`

| 文件 | 改动 |
|------|------|
| `HostWorldMapSelectionAuthority.cs` | **新增**；`Kind` = `PlayerParty` / `FormalArmy` + `FormalArmyId` |
| `HostWorldMapPanel` | Marker 左键、Army List 行点击、Focus 均写入同一 authority |
| Selection Safety Guard | 移除 `SelectedFormalArmyId 为空 → fallback PlayerParty`；按 `Kind` 分派右键命令 |

### 4.3 Attack Order Snapshot

| 文件 | 改动 |
|------|------|
| `FormalArmyOrderKind.AttackFormalArmy = 3` | 新指令种类 |
| `FormalArmyWorldMotion` | `OrderTargetArmyId`、`SetAttackOrder` / `ClearOrderTarget` |
| `ArmyHexPursuitService` | `BeginAttackArmy` 成功后 `SetAttackOrder`；`RestoreAttackOrderIfNeeded` |
| `WorldSnapshot` / `JsonSnapshotSerializer` | `currentOrderKind`、`orderTargetArmyId` 等 Phase3 字段 |
| `FormalArmyOrderRestoreTrace` / `FormalArmyOrderReplaceTrace` | Load/Replace trace |
| `FormalArmyOrderSnapshotTests` | EditMode 覆盖 Save→Load 后 Attack Order 恢复 |

### 4.4 Strategic UI Input Priority（回归修复）

| 文件 | 改动 |
|------|------|
| `HostWorldMapPanel.OnGUI` | 恢复顺序：`DrawStrategicRosterPanels` → `HandleMapInput` → `HandleCameraInput` |
| `HandleMapInput` | 开头 `ContainsCurrentGuiPoint` → **return，不 Use()** |
| `HandleCameraInput` | UI 上 skip 滚轮/新开中键平移；已进行中键 drag 可继续 |
| `DrawMapToolbar` | 登记 toolbar rect Block |
| `HostUiHitTest` | `BlockSelectionWholeScreen` / `ContainsCurrentGuiPoint` |
| `WorldMapArmyMarkerDiagnostics` | `[WorldMapPointerDispatch]` trace（仅 MouseDown） |
| `HostUiHitTestTests` | 覆盖双通道 hit test |

---

## 5. OnGUI 正式流程（WorldMap）

```
BeginFrame
BlockSelectionWholeScreen()          // 仅 RTS Selection，不挡本帧地图 dispatch
… 绘制地图、菜单、情报栏、支援半径条 …
DrawStrategicRosterPanels            // 登记整块 Panel Block + 绘制 Toggle/Button
TryDismissContextMenus…
if Event.Used → return
HandleMapInput                       // if ContainsCurrentGuiPoint → return（不 Use）
HandleCameraInput                    // UI 上 skip scroll / 新 pan
EndFrame
```

---

## 6. LevelTester 验收清单（已通过）

| CASE | 操作 | PASS 标准 |
|------|------|-----------|
| 1 Character UI | 打开角色列表，连点 主角/同伴甲/同伴乙 | 详情切换；Hex 选中不变 |
| 2 Army Checkbox | 军队列表 → 组建军队，勾选同伴甲/乙 | 一次点击 toggle；不选中背后 Hex |
| 3 Panel 空白 | 点 Strategic Panel 深色空白区 | 无地图操作 |
| 4 Army List Row | 点已有 Army 行 | authority = FormalArmy；回地图右键 Hex 可下令 |
| 5 Army Marker | 避开 Panel，左键 Marker 一次 | 选中 Army；Hex 不覆盖 |
| 6 Normal Hex | 点 UI 外普通 Hex | Hex 选中正常 |

Debug 下点击可看 Console：`[WorldMapPointerDispatch]`（checkbox → `OverStrategicUI=true HandledBy=UI MapInputExecuted=false`）。

---

## 7. 测试

| 测试 | 说明 |
|------|------|
| `HostUiHitTestTests` | 双通道 Block / ContainsCurrentGuiPoint |
| `ArmyWorldMapPositionTests.ARMY_VIS07` | Load 后玩家 FormalArmy Marker |
| `FormalArmyOrderSnapshotTests` | Attack Order Save→Load |
| `FormalArmyPhase3AuthorityTests` | Selection kind 分派（扩充） |

---

## 8. 明确未改 / 不在本轮范围

- FormalArmy Domain 业务规则（组军/Authority 主体逻辑）
- PlayerParty Travel / Army Mid-Travel Redirect
- Faction / Character Restore / LocalMap / Camera
- 不撤销 `HostWorldMapSelectionAuthority`
- 不靠 Draw Order 解决 Selection State

---

## 9. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-28 | 初版：Marker Load、Selection Authority、Attack Snapshot、Strategic UI Input Priority；LevelTester CASE 1–6 人工验收通过 |
