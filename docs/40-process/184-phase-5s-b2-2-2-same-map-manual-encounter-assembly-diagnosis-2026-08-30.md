# Phase 5S-B2-2.2：同图手动遭遇装配诊断

> 状态：已加入运行时诊断，待 LevelTester 人工复验｜优先级：P0｜最后更新：2026-08-30  
> 范围：普通 `WORLD_COMBAT` 复用已加载真实 LocalMap 时的遭遇参与者装配观测；不扩展战斗规则。

## 背景

B2-2.1 已人工确认普通世界手动战斗进入正确的真实 WorldSite LocalMap，主控角色保持可见和原位置。

但敌方 Battle Participants 与我方 FormalArmy Participants 均未在画面中出现。该现象不能直接归因为 `LocalMapVisibility`：必须先区分实体是否已准备、是否被战场作用域追踪、是否已有 LocalMap 表现落点，以及视图是否已重建。

本轮保留 B2-2.1 的既有结论：

- `markPartyInEncounter:false` 保持；普通世界战斗不得把已在真实 LocalMap 的 PlayerParty 重新移到遭遇出生点。
- PlayerParty 的 WorldPresence 保持。
- 真实 LocalMap 的既有参战／追踪实体可见性桥保持，不扩大其条件。

## 静态调用链结论

当前普通世界手动战斗进入链为：

```text
HostStrategicInterruptPresenter.EnterManualEncounter
→ StrategicEncounterSpawner.PlanManualEncounter
→ Encounter.SpawnOnNextMapLoad = true
→ PlayableHostBootstrap.ApplyPartyWorldSitePresentation
→ StrategicEncounterSpawner.ApplyPending(world)
→ _session.RefreshViewableEntityIds()
→ entityViewSpawner.Rebuild(_session)
```

`ApplyPending` 当前只有上述 Host 调用点。它以 `SpawnOnNextMapLoad` 为一次性消费条件：调用后立即置为 `false`，因此后续再次进入该调用点会自然 no-op，不能产生重复敌军或重复 `BattlefieldSpawnScope` 追踪。

结论：从静态代码看，同图复用**没有绕过**该正式调用点；不能在未取得运行时证据前，额外再插入一条平行 `ApplyPending` 调用或重载地图。

## 运行时诊断

`PlayableHostBootstrap` 在以下严格条件下记录一次 `WorldCombatAssembly`：

- `PendingEngagement.IsActive`；
- 已解析的战斗 LocalMap 等于进入前的 ActiveMapLayout；
- 因而确认为普通世界战斗的同图复用。

日志分两条：`Before` 与 `AfterApplyPending`。它们直接读取真实调用点前后的状态，而不重算或替代任何 Marker、战斗或可见性算法。

字段如下：

- `SpawnOnNextMapLoad`、`ParticipantCount`、`SelectedFriendlyCount`、`EnemyStackCount`、`EngagedPartyCount`；
- `TrackedCount`、`LivingTrackedCount`、`PresentedTrackedCount`；
- `EnemyEntityCount`、`FriendlyFormalArmyParticipantCount`；
- `VisibleEnemyCount`、`VisibleFriendlyArmyCount`；
- `ActiveMapLayoutId`、`ResolvedBattleLocalMapId`、`ReuseCurrentLocalMap`。

据此将问题划分为：

| 证据 | 断点判断 |
|---|---|
| `SpawnOnNextMapLoad` 在 After 仍为 `true` | `ApplyPending` 未消费或提前失败 |
| Snapshot 数量存在，但 `EnemyEntityCount` / `TrackedCount` 为零 | 现有敌方准备链未产出实体或未追踪 |
| `TrackedCount` 存在，`PresentedTrackedCount` 为零 | 已准备实体未获得 LocalMap 表现落点 |
| 表现落点存在而 `Visible*Count` 为零 | 才允许继续审计 `LocalMapVisibility` |
| 可见数量存在、画面仍无对象 | 追查 `RefreshViewableEntityIds` / `EntityViewSpawner.Rebuild` 的视图层 |

## 保持不变的边界

- 不重载 LocalMap、不卸载场景、不切 Dedicated EncounterMap。
- 不修改 Participant Gathering、手动／自动资格、Support、Battle completion、Capture、Travel、Pause 或 Camera。
- 不创建敌方 clone；`ApplyPending` 继续使用既有 Snapshot、FormalArmy 真实成员、`BattlefieldSpawnScope` 与残留战场逻辑。
- 不改 Explicit EncounterMap；其既有专用地图装配路径保持。
- 本轮未再改 `LocalMapVisibility`。

## 待人工复验

1. 在真实 WorldSite LocalMap 中触发一次普通世界手动战斗。
2. 控制台筛选 `[WorldCombatAssembly]`。
3. 保存同一次操作的 `Before` 与 `AfterApplyPending` 两行。
4. 依据本页表格判定敌方与 Friendly FormalArmy 分别停在哪个层级，再决定是否需要薄的 Host presentation orchestration。

## 验证状态

- 未运行 Unity Editor、PlayMode 或 Unity Test Runner。
- 本轮仅通过 `git diff --check -- Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`；无该文件新增空白错误。
- 现有非 Unity 的 Core + Data + Runtime Host 临时编译工程不在当前工作区，未伪造全量编译结果。
