# 168 · LevelTester Development Cheat Tools 统一整理（2026-08-27）

> 状态：**已入仓**｜验收场景：仅 `Assets/Scenes/LevelTester.unity`  
> 操作真源：[114-level-tester.md](114-level-tester.md)  
> 范围：**整理现有 Cheat**；不新增 Missing Cheats；不整理 Acceptance Fixtures

---

## 1. 任务摘要

将 LevelTester 分散的旧 Debug / Acceptance Panel 与 Development Hotkey 收敛为 **一套** `HostLevelTesterCheatPanel`，作为长期开发基础设施。

**原则：**

- Cheat Tools 仅是 Development Entry / Presentation / State Construction Adapter
- 正式 Domain 规则仍在 `ArmyService`、`BackgroundCharacterTravelService`、`ContentDebugService` 等
- 不为各系统占用 F-Key；不把 Cheat 塞进 WorldMap / FormalHud 等正式 Gameplay UI

---

## 2. Unified Cheat Tools

### 2.1 新文件

| 文件 | 用途 |
|------|------|
| `Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs` | 主 Panel（IMGUI Window + Foldout + ScrollView） |
| `Assets/Scripts/Unity/Host/HostLevelTesterSnapshotOps.cs` | Save/Load 静态逻辑（从旧 `HostSnapshotPanel` 提取） |
| `Assets/Scripts/Unity/Host/LevelTesterCheatTimeSection.cs` | Time / Simulation |
| `Assets/Scripts/Unity/Host/LevelTesterCheatBackgroundSection.cs` | Background Character |
| `Assets/Scripts/Unity/Host/LevelTesterCheatFormalArmySection.cs` | FormalArmy |
| `Assets/Scripts/Unity/Host/LevelTesterCheatContentSection.cs` | Content |
| `Assets/Scripts/Unity/Host/LevelTesterCheatDiplomacySection.cs` | Diplomacy |

### 2.2 顶层 Tab 结构

| Tab | 能力 |
|-----|------|
| **Time** | Step 1 Tick、Advance N（clamp 500）、Advance 1 Day、Speed 1/2/5/20x |
| **Background** | 选角色、Travel To Site/Hex、Cancel、只读 Travel 状态；`debugOverrideLocalOccupant` 标注 Debug Override |
| **FormalArmy** | Create（**明确 Leader**）、Army 下拉、Disband、Travel、Incap 目标选择、Sync Casualties |
| **Content** | Set/Clear Flag、Force Event、Dump |
| **Diplomacy** | Declare War、Alliance、Vassalage |
| **Snapshot** | Save/Load Snapshot、Reset LevelTester Session（二次确认） |
| **Battle** | DEBUG: Next Solo Auto-Battle Guaranteed Incapacitation |
| **Diagnostics** | Strong Hex Separation（纯视觉） |

顶层固定页签导航；不使用全页面纵向 Foldout + 滚轮。页签内内容超出时才用局部 ScrollView。

### 2.3 打开方式

- 键盘 **`` ` ``**（BackQuote）
- `LevelTesterHud` 顶栏 **「Cheat Tools」** 按钮

### 2.4 Wiring

- `PlayableHostBootstrap.Start()` → `EnsureLevelTesterComponents()` 在 LevelTester 上下文自动挂 `LevelTesterHud` + `HostLevelTesterCheatPanel`
- `TryInitialize()` / `RebuildPresentationAfterLoad()` 中 `levelTesterCheatPanel.Bind(this, selectionController)`
- `XianXia/Level Tester/Create Or Update Level Tester Scene` 创建场景时直接挂 Cheat Panel

---

## 3. Current Cheat Capability（最终保留）

| 系统 | 入口 / API |
|------|------------|
| Time | `SimulationLoop.TickOnce`；`ContentDebugService.AdvanceDays`；Speed 走 `HostDebugHud` 真源 |
| Background | `BackgroundCharacterTravelService` Site/Hex/Cancel |
| FormalArmy | `ArmyService` / `FormalArmyContinuousTravelService` / `CombatLifeStateService` |
| Content | `ContentDebugService` Set/Clear Flag、Force Event、Dump |
| Diplomacy | `StrategicAcceptanceCommands` War/Alliance/Vassal |
| Snapshot | `Session.CaptureSnapshotJson` / `RestoreSnapshotJson` + `RebuildPresentationAfterLoad` |
| Session | Reset → `TryInitialize()` |
| Battle | `AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated` |

**本轮未新增：** Teleport、Kill/Heal、JumpToDay、StartQuest、Fixture Library 等 Missing Cheats。

---

## 4. Removed Legacy Entries

### 4.1 删除的旧 Panel

- `HostBackgroundTravelDebugPanel`（F12）
- `HostFormalArmyDebugPanel`（F11）
- `HostStrategicAcceptancePanel`（F8）
- `HostContentDebugPanel`（F3/F4）
- `HostSnapshotPanel`（F5/F9）

### 4.2 删除的 Development Hotkey / Wiring

- Bootstrap：`.`/`N` Step Tick、`[`/`]` 变速、`R` Rebuild
- 各旧 Panel 独立 F-Key（F3/F4/F8/F11/F12/F5/F9）
- `HostCommandBridge`：`enableDebugKeys`、`showDebugButtons`、Demo OnGUI、`IssueTravelNextAdjacent`
- `HostWorldMapPanel`：进入近景(调试)、Battle Debug Toggle、Hex Separation Toggle、战略验收 F8 按钮

### 4.3 Placeholder / Prototype

- `Invoke Tribute Hook (Placeholder)`（Diplomacy）
- WorldMap「进入近景(调试)」
- CommandBridge 旧 Demo Debug Button Bar

---

## 5. Bootstrap / LevelTester Wiring

Play 后 LevelTester 上下文仅一套 Cheat Tools UI：

1. `LevelTesterHud` + `HostLevelTesterCheatPanel`（Scene Tool 或 Bootstrap Ensure）
2. **不再** AddComponent 任何旧 Debug/Acceptance Panel
3. `LevelTester.unity` 已移除序列化 `HostSnapshotPanel`；Bootstrap 字段为 `levelTesterCheatPanel`

---

## 6. Domain Services Preserved

以下 Service **未删除**，Cheat 仅作 Presentation/Entry：

- `ArmyService`、`FormalArmyContinuousTravelService`
- `BackgroundCharacterTravelService`
- `CombatLifeStateService`
- `ContentDebugService`
- `StrategicAcceptanceCommands` / `WarGateService`
- `Session.CaptureSnapshotJson` / `RestoreSnapshotJson`
- `SimulationLoop.TickOnce`
- `AutoBattleCasualtyService`（Debug Hook）
- `TributeService`（Domain 保留；Placeholder UI 已删）

---

## 7. Acceptance Fixtures Untouched

本轮 **未修改**：

- `Ch01ScenarioStrategicSetup`
- 三支 Bandit FormalArmy、Bandit War、CasualtyTest Army
- 固定敌军 Hex、Player Camp Site、Auto casualty fixture

---

## 8. Documentation Updated

| 文档 | 变更 |
|------|------|
| [114-level-tester.md](114-level-tester.md) | Cheat Tools 说明；更新操作键；移除旧 Panel/Hotkey 指引 |
| [2A-factions-armies-diplomacy-and-capture.md](../20-systems/2A-factions-armies-diplomacy-and-capture.md) | Development Acceptance UI 指向统一 Cheat Panel |

历史 Phase 文档（165/166/167 等）保留当时 F11/F12 事实；验收入口以 **114 + 本文** 为准。

---

## 9. Tests & Known Issues

### 9.1 已执行

- 全项目 grep：旧 Panel 类名零 `.cs` 引用
- grep：F3/F4/F5/F9/F11/F12、`stepTickKey`、`rebuildKey`、`IssueTravelNextAdjacent` 等零存活入口
- Unity 2022.3.6f1 batch 编译通过（`tools/run-editmode-tests.ps1`）
- EditMode 测试已改为 `HostLevelTesterSnapshotOps`（`HostSnapshotPhaseGTests`、`HostPlayableDayPhaseHTests`）

### 9.2 已知缺口（Load 后 View 重建）

`HostSnapshotPhaseGTests` / `HostPlayableDayPhaseHTests` 在 Snapshot Load 后 **tick 恢复正常**，但 `ViewSpawner.SpawnedCount` 仍为 0。

**原因：** Snapshot 恢复 Domain（`World` / `WorldPresence` / `PlayerPartyTravel`），不恢复 Host 侧 `PartyWorld` 缓存；Load 后 `LocalMapVisibility` 可能过滤全部实体。

**Snapshot Load ↔ View 重建** 仍为已知缺口；Cheat Tools 迁移不包含 Snapshot 修复（见单独任务）。

**不影响：** Cheat Tools 迁移本身；LevelTester 手动 smoke test（构造状态 → Advance Tick → 观察 Foldout 只读区）仍可按 114 操作。

### 9.3 未执行

- Unity PlayMode / 视觉验收（需 Editor 内人工）

---

## 10. 人工验收工作流（示例）

**Background Travel：** Cheat Tools → Background Character → 选角色 → Site/Hex → Travel → Time 区 Advance N → 查看 Travel State

**FormalArmy：** Cheat Tools → FormalArmy → 选 Site → 勾选 Members → **明确 Leader** → Create → Travel → Advance N → Incap → Sync Casualties → 查看 Army 状态

---

## 11. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-27 | 统一 Cheat Tools 入仓；删除旧 Panel 与 Development Hotkey |
