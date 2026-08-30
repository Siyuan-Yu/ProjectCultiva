# 159 — Encounter 作用域 Lingering、参战名单与大地图路线预览（2026-08-24）

> **飞书同步：** 本轮 **未** 同步（制作人要求先本地 commit + 文档）。

## 背景

在 Hex 战略层、多战场残留、WeakBandit 验收与大地图 UX 连续迭代后，working tree 长期未 commit。本轮将下列已验收/已落地改动一次性入库，避免再次因无 Git 锚点而难以回滚或对比。

## 交付摘要

### 1. Encounter 作用域与多战场隔离

- **LingeringBattlefieldRegistry / LingeringBattlefieldState**：Hex → BattlefieldId 注册；每场残留独立 Spawn 列表与 Participant 冻结。
- **BattlefieldSpawnScope**：LocalMap scoped spawn 访问层；禁止 Battle1 spawn 泄漏到 Battle2。
- **LingeringBattlefieldParticipantService**：Lingering 再进优先 Registry 冻结 Participants，禁止 Living-only 重查世界。
- **StrategicEncounterHostilityService**：遭遇 LocalMap 敌对/可见判定。
- **BattleOfferService / StrategicEncounterSpawner / StrategicEncounterResolveService**：ActivateOffer、ApplyPending、Auto/Manual 统一读 ParticipantSnapshot。

### 2. WeakBandit 参战名单（Encounter Assembly）

- **EncounterAssemblyTrace**：`[ENCOUNTER-ASSEMBLY-TRACE]` 开发 trace。
- **BattleInterruptQueue.CollectEnemyReinforcements**：Hex 支援半径 1 格；合法增援展开为 EntityId 级 Participant。
- **StrategicEncounterSpawner.TryPrepareSnapshotEnemyParticipants**：Manual/Auto 从 Snapshot 刷敌军，不再仅 primary stack。
- **LocalMapVisibility**：遭遇图仅显示正式 Participant / scoped spawn；修复 `IsActiveStrategicEncounterMap` 误判荒村为遭遇图导致村民消失。

### 3. 大地图路线预览（Presentation Only）

- **HostWorldMapPanel.RefreshSelectedArmyPathPreview**：仅当前选中的 **我方** 且 **Moving** 的 FormalArmy 显示 Hex 路线 overlay。
- 取消 Army 选择立即 Clear overlay；不修改 MoveOrder / Pursuit / Session。

### 4. Hex 右键 / 残留 / 多战场测试

- **HexRightClickResolver**、**HexActiveEnemyArmyQuery**、**StrategicResidualPresentationQuery** 等查询服务。
- EditMode：**MultiEncounterLifecycleTests**、**MultiBattleAnchorLifecycleTests**、**EncounterAssemblyTests**、**LingeringExitPositionTests** 等。

### 5. 内容与工具链

- **ch01_hex_world.json** 内容更新（与 Editor WYSIWYG 对齐）。
- **WorldGraphEditor** 性能/视图 Host 增量；**Shared.Tests** 新增。

## 已知限制 / 未做

- Snapshot 多 Lingering save/load：**延期**
- Legacy `StrategicEncounterRuntime._lingeringBattlefieldHexes` 与 Registry 双轨：**待后续收敛**
- Unity EditMode 全套在 Editor 已打开时可能 batch 失败：**需关 Editor 后跑**
- 飞书 docId 映射：**本轮未更新**

## 手操 Smoke Test（建议）

1. NEW GAME → 青石荒村 LocalMap 村民正常
2. WorldMap 选中移动中我方军团 → 仅其路线预览
3. 取消选择 → 路线消失，移动继续
4. 攻击 Active Enemy / Auto Battle / Lingering 再进
5. WeakBandit×1 Manual：仅 1 敌参战（四匪同 Node 不泄漏）

## 相关文件（核心）

| 区域 | 代表路径 |
|------|----------|
| Registry / Scope | `LingeringBattlefieldRegistry.cs`, `BattlefieldSpawnScope.cs` |
| Participant | `BattleOfferService.cs`, `StrategicEncounterSpawner.cs`, `BattleInterruptQueue.cs` |
| Visibility | `LocalMapVisibility.cs`, `StrategicEncounterHostilityService.cs` |
| WorldMap UX | `HostWorldMapPanel.cs` |
| Tests | `MultiEncounterLifecycleTests.cs`, `EncounterAssemblyTests.cs` |
