# SPACE-01 — Separate Space / Interior Transition V1

> 日期：2026-09-18
> 状态：**Accepted / Sealed**（Producer Acceptance completed 2026-09-20）
> 第一份正式样板：**废弃洞府（Cave）**
> 提交 checkpoint：`c05a3d2`（主体实现）；Final Hardening seal checkpoint = `Seal SPACE-01 separate-space ownership and persistence`
> 新会话入口：[247 Project Handoff — Current State](247-project-handoff-current-state-2026-09-18.md)
> 制作人约束：本次 Seal checkpoint 已明确授权；不打开 Unity；不运行 PlayMode／Unity Test／batchmode。

## 目的

建立可复用于 Cave／Interior／Dungeon／独立秘境／特殊房间的统一 **Separate Space** 切换机制，而不是继续给 Cave 打零散补丁。

## Continuous Outdoor vs Separate Space

| | Continuous Outdoor Surface | Separate Space |
|---|---|---|
| 例 | `base:surface_main_wilderness_v1` | `base:map_ch01_cave` |
| 位置权威 | Continuous `WorldPosition` + SurfaceId | ActiveMapLayoutId + Interior local position |
| Streaming | Runtime Chunk | 独立 MapLayout／WalkGrid／LocalPlaceSet |
| 进入后 | — | 暂时离开 Outdoor playable presentation |
| 离开后 | — | 回到进入前 exact Surface return |

`LocalMapSession` 正式收窄为 **Separate Space Session**（类型名保留兼容）。不再使用「LocalMap = 所有局部世界」旧 Outdoor 语义。

## SeparateSpaceSession 字段

- `IsActive`／`IsInInterior`
- `ActiveMapLayoutId`／`ActiveLocalPlaceSetId`
- `SpaceKind`（Cave／Interior／Dungeon／SeparateMap）
- `EntryLocationId`／`ReturnLocationId`／`EntryReason`
- `HasOutdoorReturn` + `ReturnSurfaceId` + `ReturnWorldX/Y`

## Transition lifecycle

Domain 真源：`SeparateSpaceTransitionService`
`ExplorationService.EnterLocalMap`／`LeaveLocalMap` 为薄封装。

### Enter（Outdoor → Separate）

1. resolve entrance／target map／LocalPlaceSet／spawn
2. validate
3. capture Outdoor exact return（SurfaceId + WorldX/Y）
4. establish SeparateSpaceSession
5. move PlayerParty membership（非 FormalArmy／旁观 NPC）+ deterministic formation
6. Host：deactivate Outdoor presentation → build MapLayout → materialize occupants → camera

任意中途失败：Host 回滚 PlaceSet；不得半进洞。

### Leave（Separate → Outdoor）

1. validate session
2. clear Interior places／session
3. restore exact Surface return
4. Host：reactivate Surface streaming → neighborhood → camera

禁止返回 Hex／Site center／fake entrance center。

## Return authority

唯一 Outdoor return authority：`ReturnSurfaceId` + `ReturnWorldX/Y` + `ReturnLocationId`。
不依赖 Hex／AnchorHex／PresenceHex／Overworld MapLayout。

## Occupant / visibility authority

- Separate Space 内：PlayerParty occupants + Active MapLayout／LocalPlaceSet 居民
- Outdoor WorldSite／Surface chunk／Background NPC **不参与**当前 playable presentation（Domain 保留）
- 正式入口：`LocalMapVisibility.IsEntityVisibleInCurrentPlayableSpace`（SeparateSpace-first）

## Cave reveal 状态机

| 状态 | 行为 |
|---|---|
| Hidden | Domain 入口存在；视觉隐藏；不可 pick；近距气息 hint |
| Revealed | KnownSites Discover；placement 可见；可 pick |
| Revealed + Far | 右键 → 仅走向 approach |
| Revealed + Near | 右键 → Enter 菜单 |
| Interior | Separate Space |

**Survey 成功只做：** KnownSites.Discover + reveal visual + 轻量 toast。
**禁止：** activate Interior places／改 ActiveMapLayoutId／建 Session／deactivate Surface／move Party。

Survey UX：神识不足／距离太远用轻量 toast；成功「神识扫过，洞府入口显露。」自动消失。删除 Survey 成功后的 `RefreshMapStampsOnly()`（会触发旧 MapLayout 全图刷新）。

## Snapshot schema（additive）

`StrategicSnapshotDto.SeparateSpace`：

- IsInSeparateSpace／SpaceKind／ActiveMapLayoutId／ActiveLocalPlaceSetId
- Entry／Return location／ReturnSurface + WorldX/Y
- OccupantIds／ActiveCharacterId／EntryReason

`LoadedLocalMapCharacterPlacements`：Active Separate Space 全部 persistent Character local placement（不限 occupants）。

### Load-inside-Cave

Restore SeparateSpaceSession → activate Cave places → build MapLayout → restore placements → remain inside。
**禁止** Load 后默默送回 Outdoor。

### Old-save migration

若 placements／推断 mapId 指向 Cave 等 Separate Map，但无新 DTO：用 PlayerPartyTravel exact position 重建 return authority。信息不足 → 明确 `SnapshotInvalid`，不 teleport 荒村。

## Content boundary

| ID | 归属 |
|---|---|
| `base:loc_ref_cave` | Outdoor entrance only（Surface sitePlaces） |
| `base:loc_cave_chamber` | Interior only（`places_ch01_cave`） |
| `base:map_ch01_cave` | `spaceKind: cave` |

## Separate Space Combat Policy

两个清晰模型，禁止混用：

| | Continuous Outdoor | Separate Space |
|---|---|---|
| 战斗入口 | CharacterEncounter → Independent Battle Field | Direct Local Combat（当前 MapLayout） |
| UI | BattleOffer／手动·自动／参战名单／战后 Report | 无上述弹窗；右键攻击直接追击／普攻／技能 |
| Scope | Surface presence／support／intervention | 仅 active Separate Space 内真实 entity |
| Freeze | StrategicClockFreeze／Independent Field | 不触发；Cave 本身即 battle space |

Core：`SeparateSpaceCombatPolicy.IsInPlaceCombatSpace`／`AreBothInActiveSeparateSpace`／`IsEntityInActiveSpace`。
`CharacterEncounterService.RequiresEntry` 在双方均属 active Separate Space 时返回 false。
Host：`HostNpcMeleeAssault`／`HostNpcContextMenu`／`HostCharacterEncounter.Request` guard 均走 in-place。
`HostPlayerPartyController.TickCombatFollow` 对 Separate Space occupants 自动协战。

### Combat Save/Load V1 边界

Separate Space 中 Save 继续保存 HP／life state／SeparateSpaceSession／local positions。
瞬时 HostNpcMeleeAssault target／cooldown 不持久化；Load 后可为「不在攻击动作中」。
真实伤害／死亡／弥留必须保留；Load 不得因缺 CharacterEncounter snapshot 把敌人复活或把 Party 移出 Cave。

## Entry Party Rule

进入 Separate Space = 当前真正随队成员全部自动 transition：

- Authority：`PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty`
- 进入：Active + living current companions（必须均为当前合法 PlayerParty member）
- 不进入：incapacitated／corpse／FormalArmy-controlled／已脱队
- **无合法 transition member → 明确失败；禁止 fallback 抓取全部 Player Character**
- Active Character 也必须满足同一 membership rule，不得绕过
- 已删除 `HostLocalMapEnterPrompt` 队员选择弹窗
- Host API：`IssueEnterSeparateSpace(leader, entranceId)`
- 已删除无 consumer 的 legacy wrapper `IssueEnterLocalMapWithParty`

## Leave Party Rule

离开只带走：**当前 PlayerParty + `ShouldMemberTransitionWithParty == true`**。

不带走（保留 Interior `EntityLocation`／Separate Space ownership／local placement／HP／life state）：

- incapacitated／downed／dying／corpse
- detached companion／stranded friendly
- 已退出当前 Party 或不满足 transition gate 的角色

再次进入同一 Separate Space 时，按现有 visibility rule 可在原空间看到他们。禁止「清空整个 LocalMap 玩家角色」式退出。

## PlayerParty Life-State Membership（Acceptance Fix）

PlayerParty member 进入 `Incapacitated`、`Dead` 或 `Removed` 后：

- 若仍有合法 Alive／CanFight member，先按既有稳定顺序完成 Active succession，再将非战斗成员通过 `SquadMembershipService.LeaveToSingleton` 脱离 `squad:player`；恢复后不自动重新入队。
- 若全队没有可行动成员，为 `TemporarilyUnavailable`／`AllMembersDead` 与 recovery／succession 保留必要的 terminal control membership，不允许把 controlled squad 删除为空。
- terminal compatibility membership 不提供 follow、travel、Separate Space transition 或 Outdoor party materialization authority。
- active battle lock 可暂时延迟 singleton transfer；生命状态 gate 立即生效，并由 Host tick、Separate Space Leave 与 Snapshot restore 的幂等 reconcile 在解锁后完成脱离。

进入 Separate Space 后，真正 transition 的成员以及 active map 中已有 local placement 的 persistent Character 均清除旧 Outdoor personal `WorldPresence`。洞内 `EntityLocation` + local placement 是空间真源；return point 只由 SeparateSpaceSession 的 `ReturnSurfaceId/ReturnWorldX/ReturnWorldY` 保存。Leave 只给当前合法 transition members 恢复 Outdoor presence，stranded／downed／corpse 保留原洞内 location 与 placement。

Snapshot restore 会 reconcile 本轮之前仍把非战斗成员留在 `squad:player` 的存档；active Separate Space occupant／placement 会移除 stale Outdoor presence，placement restore 不重新入队。该行为已通过 Producer Acceptance。

### Save/Load Acceptance Fix：Snapshot authority 与 Opening migration

新格式存档中，Character 没有 Outdoor `WorldPresence` 可以是明确且合法的结果。`EntityLocationSnapshotAuthorityComponent.SnapshotFieldPresent == true` 表示 Snapshot 已经正式表达该 Character 的 `EntityLocation` 以及是否存在 Outdoor presence；`RestoreMissingLegacyOpeningPresences` 不得把“没有 saved WorldPresence”解释为旧档缺失并恢复 Opening anchor。

- EntityLocation Snapshot authority 优先：即使 `HasEntityLocation == false`，新格式明确保存的 absence 也禁止 Opening inference。
- Active Separate Space ownership 优先：occupant 或属于 active map 的 Interior Character 无条件跳过 Opening migration。
- stranded Interior Character 在 PlayerParty 离开后仍以原 Cave `LocationId` 与 local placement 为空间真源；Outdoor Save/Load 后重新进入同一 Cave 时按该 authority 重新呈现，不加入 PlayerParty，也不创建 Outdoor presence。
- 只有 `SnapshotFieldPresent == false` 且同时缺少 saved/current WorldPresence、FormalArmy ownership 的真正旧档 Character，才保留既有 best-effort Opening anchor migration。

`ContinuousOpeningSpawnPresenceResolver` 的 New Game／真实旧档语义保持不变。该行为已通过 Producer Acceptance。

### Local Placement Acceptance Fix：Down freeze 与 Leave flush

Separate Space persistent local placement 必须在 presentation teardown 前写回 Domain；Party 已离开后再 Save，无法从已销毁的 Cave `EntityView` 恢复角色最后的真实位置。

- Down／Death presentation boundary 先从当前 View 捕获 exact local position，再取消移动并更新弥留／尸体表现。
- Separate Space Leave 在取消移动、提交 Core Leave、清 session／LocalPlaces 和重建 presentation 之前，对 active map 全部 persistent Character 执行 final placement flush；范围不限 occupants。
- LevelTester「选中角色进入弥留」在正式 `CombatLifeStateService.TryEnterIncapacitated` 成功后立即走同一个 Host capture helper。
- Party 离开后，stranded inactive-space Character 的长期位置继续由自身 `EntityLocationComponent.PresentationOverride` 保存；Outdoor Snapshot 直接序列化该字段。
- `LoadedLocalMapCharacterPlacements` 仍只辅助 active loaded map restore，不扩展为 inactive Cave 的第二套 authority。
- `EntityViewSpawner` 仍优先使用 saved PresentationOverride；fallback stack 只用于真实缺失 placement 的 Character，不代替 persistence truth。

该行为已通过 Producer Acceptance。

### Outdoor Snapshot Rebuild Acceptance Fix：保留 inactive-space placement

Producer Acceptance 确认：Outdoor Snapshot presentation rebuild 曾遍历 `world.Entities.All` 并清除每个 `EntityLocation.PresentationOverride`，从而在 Snapshot 已正确恢复 Cave exact placement 之后，再次破坏 inactive Separate Space Character 的持久位置。

- Snapshot 恢复的 inactive-space `LocationId + PresentationOverride` 是 persistence truth；当前地图未加载不授权删除它。
- `ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore` 不再全局清除 EntityLocation placement。
- Outdoor PlayerParty、普通 personal `WorldPresence` Character、FormalArmy 与 Outdoor combat participant 继续由各自正向 Outdoor authority 在 materialization transaction 中覆盖自己的 presentation。
- 没有 Outdoor authority 的 stranded／incapacitated／corpse 不进入 Continuous materialization，其 Cave override 原样保留至重新进入该空间。
- `EntityViewSpawner` authored-location stack 仍仅是真实缺失 exact override 时的 fallback，不是 persistence authority。
- Continuous runtime 其余 cleanup 仍限定在自身 `_desiredMaterializedEntities` removal 或 `_continuousSitePopulation` release 集合，不扩展为 global spatial reset。

该行为已通过 Producer Acceptance。

## PartyWorld Mode（SPACE-01 Final Hardening）

Active Separate Space：

- `PartyWorld.Mode = InSeparateSpace`（additive enum value **5**）
- `PartyWorld.LocalMapId = ActiveMapLayoutId`
- `PartyWorld.SiteId = empty`

**既有 numeric values 保持不变**：`InEncounter=0`、`DepartingLocalMap=1`、`AtHex=2`、`AtSite=3`、`AtWorldPosition=4`。
Separate Space Enter／Restore／Host rehydrate **不再**写 `AtHex`。Leave 后按 Continuous Surface 恢复 `AtWorldPosition`。

## Snapshot local placement（SPACE-01 Final Hardening）

`LoadedLocalMapCharacterPlacements` 在 `SeparateSpaceSession.IsActive` 时捕获 **该 ActiveMapLayoutId 全部 persistent Character** local placement（Party／Cave NPC／enemy／stranded／incapacitated／corpse），不限 occupants。

- Capture authority：Domain `EntityLocationComponent` PresentationOverride（Save 前 Host 可从 View 回写）
- Restore：只回写 PresentationOverride；**不**因 placement 加入 PlayerParty／occupant／Active Character
- V1 不新增：瞬时 melee target／cooldown／projectile／transient aggro

## Snapshot Presentation Restore Priority

`RebuildPresentationAfterLoad` 严格优先级：

1. **Active Separate Space** → `RebuildSeparateSpacePresentationAfterLoad`（禁止 Outdoor Surface 抢先）
2. **Independent CharacterEncounter** → Encounter restore
3. **Continuous Outdoor** → `ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore`

Separate Space restore **不**重新调用 `Enter()`；沿用 Snapshot session／occupants／saved local placements。
`SnapshotActiveControlledLocalMapResolver` 第一优先返回 `SeparateSpaceSession` ActiveMapLayoutId。
`ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore` 在 LocalMap.IsActive 时直接 return false。

## Physical Exit Trigger

离开 Separate Space **不是**右键 Context Action，也不是 Action Menu「离开洞窟」。

正式路径：`HostSeparateSpaceExitTrigger`

- 仅 **Active Controlled Character** 的 View presentation 点进入 MapLayout authored Exit geometry（`HostCaveEntranceQuery.TryResolveInteriorExitAtPoint`／`IsInteriorExitStamp`）
- Follower／NPC 踩出口不触发
- Edge-trigger（`SeparateSpaceExitEdgeTrigger`）：spawn／Load 恰好在 Exit 内 → WaitForExit，走出后再 Armed，再次走入才 Leave
- 无确认窗；轻量 toast「离开洞府」
- 战斗中主动攻击／失能／modal 时不触发
- Leave 仍走 `SeparateSpaceTransitionService` + `ShouldMemberTransitionWithParty` exact Outdoor return
- Debug Force Leave 仅在 LevelTester 诊断页

已删除：ContextMenu 右键离开、`TryPickInteriorExitAtMouse`、Action Menu「离开洞窟」。

## 当前基本通过的功能（制作人自测记录）

以下流程当前基本可用，但**尚未完成正式人工验收**：

- **Discovery**：hidden entrance → 附近 strange-presence hint → Survey → reveal（reveal authority = PlayerParty knowledge，非全球 NPC knowledge）。
- **Approach**：Revealed + Far → 右键只走向 entrance；Revealed + Near → 出现 Enter。
- **Entry**：当前随队成员全部自动进入；无队员选择弹窗。
- **Combat**：洞内 direct in-place combat；followers 自动 assist；不入 BattleOffer／不建第二战场。
- **Save/Load**：洞内 Save/Load 已基本验证可工作，Load 后留在洞内。
- **Exit**：physical exit trigger → exact Outdoor return。

## Final Hardening（已完成并通过制作人人工验收）

> 状态：**Accepted / Sealed**。
> Producer Acceptance 已覆盖 discovery、approach、entry、in-place combat、physical exit、exact return、casualty ownership，以及 Active Cave／Outdoor Save-Load persistence。

本轮已落地：

| 项 | 结果 |
|---|---|
| A. Enter membership | 删除 all-Player fallback；空成员明确失败；Active 必须合法 transition member |
| B. Leave membership | 只撤离 `ShouldMemberTransitionWithParty` 的当前 Party 成员 |
| C. stranded／downed／corpse | Leave 不 teleport；保留 Interior ownership；再进可见 |
| D. `InSeparateSpace` | additive enum=5；既有 0–4 不变；Separate Space 停写 `AtHex` |
| E. Character local placement | active-map 全部 persistent Character Save／Load；restore 不污染 Party membership |
| Wrapper cleanup | 删除无 caller 的 `IssueEnterLocalMapWithParty` |

Producer Acceptance 已完成；本范围封板。后续只接受明确 regression fix，不在 MAP-04 中重新设计 Separate Space。

## MAP-04 暂停点

SPACE-01 stabilization dependency 已解除。MAP-04 可恢复 Final Physical Legacy Cleanup，但其改动继续保持 **Implementation Complete / Producer Acceptance Pending**，独立等待制作人人工验收。见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md)。

## 轻量验证（本轮）

- offline Roslyn compile：`XianXia.Core`／`XianXia.Data`／`XianXia.Unity`
- `git diff --check`
- PartyWorld／membership／placement static audit
- SeparateSpace snapshot serialize／deserialize sanity（若环境可跑）

未打开 Unity；未跑 PlayMode／Unity Test／batchmode。

## 制作人人工验收记录（2026-09-20 completed）

- [x] 正常两三人小队进洞／出洞
- [x] 洞内让一名同行 incapacitated／dying 后，主控单独离开；该角色不得被带出去
- [x] 再次进入同一洞府，留在里面的角色仍在那里
- [x] Cave enemy 移动到新位置 → Save → Load → 仍保持新位置
- [x] Cave NPC／enemy Load 后仍不是 PlayerParty member
- [x] 洞内 Save → Load → physical exit 再走一次，无主链回归
- [x] Outdoor Save → Load → re-enter Cave 后，stranded Character life state／ownership／exact local placement 保持
- [x] inactive Separate Space placement 不再被 Outdoor snapshot presentation rebuild 清除
