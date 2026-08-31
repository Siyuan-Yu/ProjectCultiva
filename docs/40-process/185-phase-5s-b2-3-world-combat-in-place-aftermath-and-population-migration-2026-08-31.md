# Phase 5S-B2-3：WORLD_COMBAT 原地结束 + FormalArmy/Residual 人口迁移 + Manual Battle 世界权威统一

> 状态：**实现完成，编译通过，待 LevelTester 人工验收（未 commit 前的最后一次汇总）**｜优先级：P0｜最后更新：2026-08-31
> 范围：普通真实 LocalMap 手动 WORLD_COMBAT 的显示、原地结束、战后人口持久与战略权威收口；含 P0 定点修复与 FormalArmy/Residual → Loaded LocalMap 正常人口桥。
> 上级：`docs/20-systems/README.md`（2A / 2K）、`docs/40-process/179~184`（Phase 5S 系列）
> 关联：`2A-factions-armies-diplomacy-and-capture.md`、`2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md`、`23-combat.md`

---

## 0. 一句话总结

从「真实 LocalMap 手动战只有 PlayerParty 显示」出发，依次完成：battle participant visibility 修复 → Follower Stop authority leak 修复 → 战斗结束原地保留 LocalMap（不再强制回 WorldMap）→ FormalArmy exact BattleHex commit → **FormalArmy / Strategic Residual → Loaded LocalMap 正常人口桥（LoadedStrategicPopulationMaterializer）** → Manual Battle 入场时 PlayerParty 与 FormalArmy 统一 commit 到 frozen BattleAnchorHex（ManualBattleWorldCommitService）。

核心不变式：**Combat membership ≠ Physical world existence；BattlefieldSpawnScope 结束 ≠ Entity 被世界删除**。战斗结束只结束 Combat/Encounter participation，不结束真实地点的物理现场。

---

## 1. 背景与问题链

| # | 现象 | 根因（确认） |
|---|---|---|
| 1 | 普通 WORLD_COMBAT 进入真实 LocalMap 后只有 PlayerParty 显示 | `LocalMapVisibility.IsEntityVisible` 的真实 LocalMap battle bypass 用 **AND 三重条件**（`IsEngaged AND IsTrackedInCurrentLocalMapScope AND HasPresentationOverride`）。Enemy 通常 `tracked=true, engaged=false`；Friendly FormalArmy 通常 `engaged=true, tracked=false` → 两边都被 WorldSite 常驻人口门禁挡住 |
| 2 | Follower 在 LocalVisible AutoTravel 后停在 takeover 点 | `TickFollowers` 的 `if (_move.IsMoving(id)) continue;` 把「正在移动」当永久跳过条件，`_nextFollowRepath` 被阻断 |
| 3 | Follower 一 Stop 就取消整队 AutoTravel | `HostCommandBridge.IssueTo` 的 Stop 分支无条件 `CancelLocalVisibleAutoTravelIfActive()`；Follower Rebind 的 `OrderFollowerTowardActive(issueStop:true)` 发 Domain Stop → `CancelTravel → CompleteMove` 清掉整队 travel plan（authority leak） |
| 4 | 战斗结束强制打开 WorldMap / FormalArmy 头像在战前位置 | `ConfirmEndBattle` 是 Host policy 无条件 `Open()`；`ParkArmyAtBattleAnchor` 只改 legacy `CurrentHex` 不写 `WorldMotion` → 双权威 |
| 5 | 战后 Army members / 弥留 / 尸体立刻消失 | Army/Residual 只通过 battle-only PresentationOverride + visibility bypass 显示；战斗结束 → `PruneHiddenViews` 隐藏。普通 LocalMap materialization 明确排除 FormalArmy（`LoadedDestinationArrivalMaterializer.IsEligibleCharacter` 有 `TryGetArmyForCharacter` guard）与 incapacitated/corpse（要求 `CanFight`） |
| 6 | Manual Battle 无法正确组装战场（引入人口桥后） | PlayerParty 仍保持 SupportArea（旧「Active 不 teleport」policy）→ `LoadedLocalMapBelongingQuery` Wilderness context 用 `PlayerPartyTravel.CurrentHex`(S)，`ReconcileLoadedStrategicPopulation` 判定 `army.CurrentHex(B) != S` → `ReleaseManagedEntity` 把刚进场的 Army participant 又摘掉 |

---

## 2. 已确认的代码事实（本轮依据）

- **FormalArmy 世界位置已有 authority**：`FormalArmyContinuousTravelService` 持续维护 `army.WorldMotion` + `SyncLegacyFromWorldMotion()` + `FormalArmyMemberPresenceSync.SyncAll`。
- **普通 LocalMap materialization 排除 FormalArmy / 尸体**：`LoadedDestinationArrivalMaterializer.IsEligibleCharacter`：`ArmyService.TryGetArmyForCharacter → return false`；并要求 `CombatLifeStateService.CanFight`。→ 不能简单删 guard（它是 Background Character 专用，带 background travel side effects）。
- **Battle-only materialization 不是正常人口**：`StrategicEncounterSpawner.MaterializeFriendlyParticipantsForRealLocalMap` / `TryPrepareSnapshotEnemyParticipants` 只做 battle presentation / scoped visibility。
- **`BattleParticipantSnapshot.LocalMapResolutionKind`** 已存在（默认 `ExplicitEncounterMap`，`Clear`/`CopyFrom` 已同步），是区分 WorldCombat / Explicit 的唯一真源（禁止用 LocalMapId 猜）。
- **`LingeringBattlefieldRegistry.CommitActiveSession`** 存在 double-commit 风险：`ParkLingeringBattlefield` commit 一次（Active list 清空）后，`FinishOfferResolution` 在 BattlefieldLingering 条件下可能再空 commit，`ClearTrackedIds` + 复制空 active list 会擦掉第一次保存的 tracked IDs。

---

## 3. 架构决策（不可违反）

1. **Manual Entry = 所有实际参战战略单位进入 BattleHex**。Frozen `BattleParticipantSnapshot.BattleAnchorHex` 是唯一真源：PlayerParty + 参战 Friendly FormalArmies + 参战 Enemy FormalArmies 全部 strategic commit 到 BattleAnchorHex。`选择/确认参战 → Army 已进入 BattleHex`，可清除原 travel/order path（正确行为）。
2. **「同一 BattleHex」只指战略世界位置**；LocalMap tactical coordinates 仍分侧摆位（PlayerParty friendly side / Friendly Army formation / Enemy formation），绝不全部放在同一 local coordinate。
3. **World → LocalMap 人口桥**：新增 `LoadedStrategicPopulationMaterializer`（Core 薄 service）+ `LoadedStrategicPopulationQuery`。职责只覆盖当前已 Loaded 的 surface LocalMap 中 FormalArmy living members 与 strategic residual（incapacitated / visible corpse）的 materialize / dematerialize。PlayerParty 由 PlayerParty materializer 管，Background character 由 LoadedDestinationArrivalMaterializer 管，**不合并 movement authority**。
4. **Combat 结束必须真的结束**：Participants / engaged / hostility / freeze 照常清除；禁止用「保留 Combat active」解决显示。禁止为了修显示给 battle participant 永久依赖 Encounter visibility。
5. **WORLD_COMBAT 禁止「结束战斗 = 删除现场实体」**：不得仅因 Encounter 结束执行 `ClearSpawned → FinalizeRemoval(real participant)`。无残留分支改用 `ReleaseWorldCombatScopeWithoutRemovingEntities`（只解 scope 引用）。`DestroyBattlefieldCompletely` 的旧 destructive cleanup 只用于 ExplicitEncounterMap。
6. **multi-hex WorldSite 必须保持具体 BattleHex**：禁止 `InitializeAtWorldSite(...)` / `ApplyMembersAtSite(...)` 把 actual BattleHex 吸回 `Site.AnchorHex` / `PresenceHex`。
7. **PlayerParty WorldSite 内 footprint hex 从 `WorldPosition` 即时派生**（`WorldSiteSpatialMapping.TryResolveDerivedFootprintHex`），不再用 stale `PresenceHex` 作为 battle eligibility authority。
8. **FormalArmy Member Presence authority 收口**：Physical Presence = `FormalArmy.WorldMotion` 派生（`FormalArmyMemberPresenceSync.SyncMember` 先做）；`ArmyPresenceAdapter` 只附加 `CombatPursuitStackId`（SetAtSite/SetAtWorldPosition 会 ClearCombatPursuit，physical 必须先做）。
9. **LingeringBattlefieldRegistry 本轮保留**（不删旧入口），仅修复 double-commit：`FinishOfferResolution` 只在「无 existing battlefield 或 OfferId 不同（同 Hex 新 battle）」时 commit；同 battle（同 Hex + 同 OfferId）已 commit 则跳过。

---

## 4. 实现清单（按轮）

### 4.1 P0-A：真实 LocalMap 手动战 participant visibility
- `LocalMapVisibility.IsEntityVisible`：真实 LocalMap battle bypass 改 **participant 语义**（复用 `StrategicEncounterHostilityService`：tracked spawn / engaged / MandatoryFriendly / selected OptionalFriendly / EnemyPrimary / EnemyReinforcement），并要求 `HasPresentationOverride`。`IsCurrentRealLocalMapBattle` 限定 `Encounter.LingeringLocalMapId == 当前激活 LocalMap == PartyWorld.LocalMapId` 且战斗活跃（`HasEngagedParty || SpawnedEntityIds>0`）→ 其他地图/普通 WorldSite 不豁免。
- `StrategicEncounterSpawner.MaterializeFriendlyParticipantsForRealLocalMap`：selected Friendly（PlayerParty 之外）补 `EntityLocationComponent + PresentationOverride`（StartLocation 基准独立偏移带避开敌军簇）。原始 EntityId、不 clone、不加入 PlayerParty、**不塞 BattlefieldSpawnScope**、不修改 Canonical WorldPosition / FormalArmy ownership。

### 4.2 P0-B：Follower 持续跟随（TickFollowers）
- `HostPlayerPartyController.TickFollowers`：删除 `if (_move.IsMoving(id)) continue;` → moving follower 也允许按 `followRepathInterval`(0.35s) 周期 repath（`ShouldRepathFollower` 节流，不每帧 A*）。
- **Formation slot 稳定**：`followerIndex++` 移到 melee/chop/farm continue 之前，按 `Party.Members` 稳定顺序，每帧恒定。
- 已跟上（dist ≤ followStopDistance）不发新 path；**不做 stale movement 全局 Cancel**（无法安全区分 Follow path 与 schedule 等特殊 path，最小改动）。melee/chop/farm 优先级保持。

### 4.3 P0-C：Follower Stop authority leak
- `HostCommandBridge.IssueTo` Stop 分支：`if (!active.IsNone && id == active) CancelLocalVisibleAutoTravelIfActive();` —— 玩家主动 Stop Active 仍夺回主控取消 AutoTravel；Follower/其他可控角色的内部 Stop（Rebind/OrderFollowerTowardActive/ClearDirectControlFor）只停自己，不再 `CancelTravel/CompleteMove/清 route`。非 LocalVisible 时原行为不变。
- `HostPlayerPartyController.OrderFollowerTowardActive`：`issueStop:true → false`（`OrderEntityToWorldPoint(issueStop:false)` 仍 ClearPath/ClearPending + 重建 Local A* path + 更新 moving 状态，只不走 Domain Stop / HoldPlayerWait）。

### 4.4 Phase 5S：原地结束 + FormalArmy exact BattleHex commit
- `BattleParticipantSnapshot`：`LocalMapResolutionKind`（默认 ExplicitEncounterMap；Clear/CopyFrom 同步）。
- `ArmyHexBattleAnchorService`：新增 `CommitArmyAtExactBattleHex`（`HexMath.ToWorldPosition(exactHex) → WorldMotion.SetAtWorldPosition(pos, exactHex) → SyncLegacyFromWorldMotion() → State=Idle → ArmyPresenceAdapter.SyncFromArmy`）+ `CommitParticipantFormalArmiesAtBattleAnchor`（MandatoryFriendly/selected OptionalFriendly/EnemyPrimary/EnemyReinforcement + Attacker/Defender fallback + stack 兼容解析）；`ParkArmyAtBattleAnchor` 改走 exact commit。
- `ArmyPostBattleSyncService.SyncParticipantFormalArmiesAfterBattle`：收集全部 participant FormalArmyId（去重）→ 跳过已处理的 Attacker/Enemy primary → `DetachNonLivingMembersAtBattlefield` → 重新 TryGet → 有 living 则 ParkArmyAtBattleAnchor + SyncFromArmy。`ResolveAndEnd` 在原有 specialized sync 后追加。
- `HostStrategicInterruptPresenter.ConfirmEndBattle`：Resolve 前 capture `LocalMapResolutionKind` → `completeInPlace = WorldSite || Wilderness`。成功且无下一场 freeze：恢复 saved pause/speed + `NotifyAfterBattleResolved`；**completeInPlace 时禁止 Open/ApplyPartyWorldSitePresentation/Refresh/Reload/Rebuild**，toast「战斗结束，世界时间已恢复」。ExplicitEncounterMap 保持旧 completion/return policy。

### 4.5 Phase 5S-B2-3.1：FormalArmy / Residual → Loaded LocalMap 人口迁移
- **新增 `LoadedStrategicPopulationMaterializer`**：`ReconcileLoadedStrategicPopulation(world, playerParty, wildernessBounds, siteBounds)` 返回 changed 计数。职责：
  - 解析 loaded surface LocalMap（复用 `LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap`）；
  - 扫描 FormalArmies living members（belonging 直接用 `army.WorldMotion`：Wilderness `CurrentHex == LoadedHex` / WorldSite `Site.OccupiesHex(CurrentHex)`）与 StrategicResidualPresenceService residual candidates；
  - belongs → `LocalMap.AddOccupant` + 落点（Wilderness `TryProjectWorldToLocal` / WorldSite `TryWorldSurfaceToLocal`，真实 bounds，不吸 AnchorHex/StartLocation）+ 稳定 formation offset（clamp playable bounds；已有有效 override 不覆盖 → 战斗落点保留）；
  - 不 belongs → `RemoveOccupant` + 清 override，**不动 WorldMotion/WorldPresence**；不碰 PlayerParty / authored NPC / background 归属。
  - `BelongsArmyToLoadedMap` / `BelongsResidualToLoadedMap` 公开给 Query 复用。
- **新增 `LoadedStrategicPopulationQuery`**：`IsMaterializedStrategicCharacterOnLoadedMap(world, id)` —— `LocalMap.ContainsOccupant` + belongs 校验（FormalArmy living member OR StrategicResidualCandidate）。
- `LocalMapVisibility`：battle bypass 之后、WorldSite hard gate 之前插入普通战略人口早期规则（`!onEncounterMap && IsMaterializedStrategicCharacterOnLoadedMap → visible`）。**不依赖 Active Encounter / Snapshot / Engaged / SpawnScope**。
- `LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap`：补 AtHex 支持 —— WorldSite：`AtSite(SiteId==loaded)` **或** `AtHex` 且 `loaded.Site.OccupiesHex(ResidualHex)`；Wilderness：`AtWorldPosition` 派生 hex 或 `AtHex` 且 `ResidualHex == LoadedHex`。
- `ArmyPresenceAdapter.SyncFromArmy`：physical 先走 `FormalArmyMemberPresenceSync.SyncMember`，adapter 只附加 pursuit metadata；删除基于 legacy `army.CurrentHex` 的 AtSite/SetAtHex 投影。
- `LoadedDestinationArrivalMaterializer.ReleaseEligibleOccupantsOnLocalMapUnload`：移除 Army skip → FormalArmy 派生表现 unload 可安全 release（清 occupant + override），下一张图由 reconciler 重 materialize；PlayerParty 特殊规则保持。
- `BattleOfferService.FinishOfferResolution`：double-commit guard —— `WasActiveSessionAlreadyCommitted`（Registry 同 Hex 已有 battlefield 且 `Participants.OfferId` 相同 → 跳过）。
- `StrategicEncounterResolveService`：WorldCombat 无残留分支改 `ReleaseWorldCombatScopeWithoutRemovingEntities`（只清 scope 引用 / ClearActiveEncounterSession，不 FinalizeRemoval world entities）；ExplicitEncounterMap 保留旧 destructive cleanup。`RestoreParticipantsAfterBattle` 在 real-LocalMap battle 也把 selected OptionalFriendly 视为 mustAnchor（PreBattle 只用于未实际参战者）。
- `PlayableHostBootstrap`：
  - Hook 1 `ApplyPartyWorldSitePresentation`：PlayerParty materialize 后、enemy `ApplyPending` 前调 `MaterializeFriendlyParticipantsForRealLocalMap`；Rebuild 前 `ReconcileLoadedStrategicPopulation()`；
  - Hook 2 `StepTick`：`TickOnce` 成功后、`PruneHiddenViews` 前 reconcile（Changed 才 Refresh + SpawnMissingVisibleViews；bounds 按 ActiveMapLayoutId 缓存，unload 清缓存）；
  - 公开 `RefreshLoadedStrategicPopulation()`（reconcile + Refresh + SpawnMissing + Prune，供 ConfirmEndBattle 使用）。
- `HostStrategicInterruptPresenter.ConfirmEndBattle` completeInPlace 分支：`ResolveAndEnd` 成功且无下一场 freeze → `bootstrap.RefreshLoadedStrategicPopulation()`；禁止 Reload/ApplyPartyWorldSitePresentation/WorldMap.Open。

### 4.6 Phase 5S-B2-3.2：Manual Battle 世界权威统一（regression fix）
- **新增 `ManualBattleWorldCommitService`**：
  - `CommitWorldCombatParticipants(world, party, snap, resolution)`：frozen `BattleAnchorHex` 为唯一真源（缺/越界 → 明确 Failure）。FormalArmy 复用 `CommitParticipantFormalArmiesAtBattleAnchor`；PlayerParty：`CaptureTravelingMembers` + `SetAtWorldPosition(battleWorld, battleHex)`；WorldSite 分支：`TrySetAtWorldSitePreservingWorldPosition`（失败不 silent snap）+ `AlignCurrentHex(battleHex)` + 全员 `SetAtSite` + `PartyWorld.SiteId/FocusFormalArmyId=""`；Wilderness 分支：全员 `SetAtWorldPosition` + `PartyWorld.ClearSiteFocus/Mode=AtHex`；`PartyWorld.LocalMapId = resolution.LocalMapId`。
  - `PhysicalSurfaceChanged(previous, resolution)`：Wilderness 按 `previous.WildernessHex == battleHex`、WorldSite 按 `previous.Site.SiteId == resolution.SiteId` 判定（**不用 MapLayoutId 字符串**——多 Hex 可共用同一 asset）。changed → `WorldTravelService.ApplyLocalMapSessionFromFocus` 重置旧 domain session（清 occupant/army/residual/stale override）再进 Battle surface；同 surface 不清 authored NPC。
- `BattleLocalMapResolver.ResolvePendingEngagement` WorldSite 分支：补 `BattleHex = engagement.BattleLocation`；`!HasBattleLocation` → 明确 Failure。不落 AnchorHex/PresenceHex。
- `BattleEngagementSpatialQuery.TryGetCommittedPartyTravelHex` AtWorldSite：`WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(site, motion.WorldPosition, …)` 为正常 authority；`TryResolveSitePresenceHex` 仅 legacy/canonical 缺失 fallback。
- `StrategicEncounterSpawner`：`MaterializeFriendlyParticipantsForRealLocalMap` 重写 —— gate `HasActiveRealLocalMapManualEncounter`（kind WorldSite/Wilderness && HasEngagedParty，战后 Participants.Clear 自动失效）；直接读 frozen snapshot 的 MandatoryFriendly + selected OptionalFriendly、跳过 PlayerParty 成员；**覆盖旧 normal-world override**（不再 `HasPresentationOverride` 即 continue）+ `AddOccupant`。
- `PlayableHostBootstrap.ApplyPartyWorldSitePresentation`：PlayerParty materialize 后、`ApplyPending` 前调用 friendly battle placement（从 EnterManualEncounter 移入，map-loaded 阶段才执行）。Enemy `ApplyPending` 保持原位置。
- `HostStrategicInterruptPresenter.EnterManualEncounter`：删除过早的 friendly placement；改在 resolve + capture previous loaded surface + `CommitWorldCombatParticipants`（`!Success → toast`）后，physical surface changed 时 `WorldTravelService.ApplyLocalMapSessionFromFocus`，再 WorldMap.Close + ApplyPartyWorldSitePresentation。

---

## 5. 修改文件清单

**新增（未 commit）**
```
Assets/Scripts/Core/World/Strategic/LoadedStrategicPopulationMaterializer.cs (+.meta)
Assets/Scripts/Core/World/Strategic/LoadedStrategicPopulationQuery.cs (+.meta)
Assets/Scripts/Core/World/Strategic/ManualBattleWorldCommitService.cs (+.meta)
docs/40-process/185-phase-5s-b2-3-world-combat-in-place-aftermath-and-population-migration-2026-08-31.md
```

**修改（未 commit）**
```
Core/World/Strategic/ArmyHexBattleAnchorService.cs   (exact commit + participant commit + park 改走)
Core/World/Strategic/ArmyPostBattleSyncService.cs    (SyncParticipantFormalArmiesAfterBattle)
Core/World/Strategic/ArmyPresenceAdapter.cs          (physical 走 FormalArmyMemberPresenceSync；只附 pursuit)
Core/World/Strategic/BattleEngagementSpatialQuery.cs (footprint hex 派生)
Core/World/Strategic/BattleLocalMapResolver.cs       (WorldSite BattleHex 保留)
Core/World/Strategic/BattleOfferService.cs           (double-commit guard)
Core/World/Strategic/BattleParticipantSnapshot.cs    (LocalMapResolutionKind)
Core/World/Strategic/LoadedDestinationArrivalMaterializer.cs (unload release Army)
Core/World/Strategic/LoadedLocalMapBelongingQuery.cs (AtHex 支持)
Core/World/Strategic/StrategicEncounterResolveService.cs (WorldCombat 非破坏性 release；Restore 语义)
Core/World/Strategic/StrategicEncounterSpawner.cs    (friendly placement 重写 + gate)
Unity/Host/HostCommandBridge.cs                      (Stop subject guard)
Unity/Host/HostPlayerPartyController.cs              (TickFollowers + OrderFollowerTowardActive)
Unity/Host/HostStrategicInterruptPresenter.cs        (原地结束 + Manual commit 编排)
Unity/Host/LocalMapVisibility.cs                     (participant bypass + 战略人口早期规则)
Unity/Host/PlayableHostBootstrap.cs                  (3 个 hook + RefreshLoadedStrategicPopulation)
```

---

## 6. 验证状态

- Host 全链编译（真实 Unity 2022.3.6f1 dll + Core + Data + 全部 Unity 脚本，强制全量）：**0 错误**（2 个既有无关 warning：HostWorldMapPanel:740 CS0162、HostFormalHud:123 CS0169）。
- `git diff --check`：通过（exit 0）。
- 本轮功能性改动净 diff：16 个 M 文件 +645/−57 + 3 个新 Core service（~560 行）+ 1 个文档。
- 未跑 Unity Test Runner / PlayMode / EditMode（按要求由 LevelTester 人工验收）。

---

## 7. LevelTester 最小复验（人工）

1. **普通 FormalArmy 无战斗**：Army A→Hex A、Army B→Hex B；PlayerParty 去 Hex A 见 A 全部 living members 不见 B；去 Hex B 反之且 A 不残留。
2. **Army 运行时移入/移出**：Player 留 Hex H LocalMap，Army 移入 H → 下一 tick 自动出现；移出 → 表现消失，WorldMap portrait 正常。
3. **WORLD_COMBAT 手动战**：Player + Friendly Army + Enemy Army；进入后 `PlayerPartyTravel.CurrentHex == BattleAnchorHex`、参战 FormalArmy `WorldMotion.CurrentHex == BattleAnchorHex`、三方全部出现、非参战 Army 不出现。
4. **战后**：结束战斗不打开 WorldMap、不 reload、Player 留原位置、Friendly living survivors 留原战斗位置、Enemy incapacitated/visible corpse 留原位置、世界时间恢复、**下一 tick 不消失**。
5. **离开战场再回来**：living Army survivors 按 WorldMotion、residual 按 ResidualHex 自然重现（不需要「进入残留战场」）。
6. **multi-hex WorldSite**：BattleHex 非 AnchorHex → Army 与 Player 均保持 actual BattleHex，不被吸到 AnchorHex/PresenceHex/StartLocation。
7. **S/B 共用 MapLayoutId**：两个不同 Wilderness Hex 用同一 fallback map asset → 仍识别 physical surface 切换，旧 S 人口不残留。
8. **Explicit Encounter**：旧 Dedicated EncounterMap completion / destructive cleanup 行为保持。
9. **Follower**：Active+Followers 在 WorldMap→LocalVisible takeover 后持续跟随、Stop Follower 不取消 AutoTravel、Stop Active 仍夺回主控。

---

## 8. 未决事项 / 后续（非本轮）

- `BackgroundCharacterTravelService`(:750) / `FormalArmyContinuousTravelService`(:492) 的 hex 距离版复制实现统一（历史待办）。
- Lingering Battlefield 旧入口（EnterLingeringBattlefield / AttackLingeringBattlefield / ArmyHexLingeringArrivalService / WorldMap 残留菜单）退休：先等离开后重进时普通 LocalMap 初始 materialization 完整支持 FormalArmy/residual 自动 materialize，再单独移除 UI / context action / travel gateway。
- reinforcement 从地图边缘动态走入（direction-derived ingress）→ B2-5。
- Army/Battle、CurrentHex 全局迁移、PresenceHex/AnchorHex 删除、Save schema 均未分配。

---

## 9. 提交记录

- 本页随 Phase 5S-B2-3 全部代码一起以中文 commit 提交到 `origin/dev`。
