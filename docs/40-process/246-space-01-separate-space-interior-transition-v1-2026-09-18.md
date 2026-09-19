# SPACE-01 — Separate Space / Interior Transition V1

> 日期：2026-09-18  
> 状态：**Implementation Complete / Producer Acceptance Pending**（**不是 Accepted／未封板**）  
> 第一份正式样板：**废弃洞府（Cave）**  
> 提交 checkpoint：`c05a3d2`（2026-09-19 00:32:36，制作人提交并推送，非开发会话所为）  
> 新会话入口：[247 Project Handoff — Current State](247-project-handoff-current-state-2026-09-18.md)  
> 制作人约束：不 `git add` / `commit` / `push`（除非授权 checkpoint）；不打开 Unity；不运行 PlayMode／Unity Test／batchmode。

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

另沿用 `LoadedLocalMapCharacterPlacements` 保存 party interior local positions。

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
- 进入：Active + living current companions
- 不进入：incapacitated／corpse／FormalArmy-controlled／已脱队
- 已删除 `HostLocalMapEnterPrompt` 队员选择弹窗
- Host API：`IssueEnterSeparateSpace(leader, entranceId)`；旧 `IssueEnterLocalMapWithParty` 仅 legacy wrapper（忽略 party 数组）

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

## Deferred Final Hardening（封板前必须完成）

> **以下为制作人明确暂缓（不在实现轮内修复）的项目。完成前不得把 SPACE-01 写成 Accepted / Sealed。**

### A. Enter transition membership fallback

`SeparateSpaceTransitionService.CollectTransitionMembers()` 在按 `ShouldMemberTransitionWithParty` 收集结果为空时会 **fallback 抓取所有 Player characters**，可能把失能／尸体／已脱队／FormalArmy 控制的人物一起带进洞。建议未来删除该 fallback。

### B. Leave evacuation ownership

`SeparateSpaceTransitionService.EvacuateSeparateSpaceParty()` 迁移范围 = session occupants ＋ 扫描所有仍挂在 interior location 的 Player characters，**未严格使用** `PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty()`。应严格只迁移随队成员。

### C. Incapacitated / corpse / stranded party member

主控出洞时，失能（incapacitated）、尸体（corpse）、detached、stranded 队员行为**尚未系统验证**；必须明确这些状态不应被错误 teleport（对照 ADR-0019 与 CW-02 安全出口规则）。

### D. PartyWorld Mode

Separate Space 当前仍复用 `PartyWorldPresenceMode.AtHex`（`SeparateSpaceTransitionService.Enter`、`SeparateSpaceSessionSnapshotRestore`、`HostSnapshotSessionRehydration`、`PlayableHostBootstrap`）。未来应增加 `InSeparateSpace`，避免 Hex compatibility semantic 污染。

### E. Cave NPC moved-position persistence

`LoadedLocalMapPlacementSnapshotRestore.Capture()` 目前**只捕获 active map 的 occupants**，洞内 resident／enemy 移动后的 Local position 不落点。未来需确保这些位置在 Save/Load 后保留。

### F. 未 Producer Accepted / Sealed

**Deferred hardening required before final sealing.** 封板前需完整验收：discover → reveal → enter → combat → save/load → physical exit → downed edge case。

## MAP-04 暂停点

本轮 **不**继续 MAP-04 Legacy 删除（Hex／WorldRegion／WorldGraphEditor 等）。  
MAP-04 保持 **Paused / Producer Acceptance Pending**，待 SPACE-01 验收后再续 Final Cleanup。见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md)。

## 轻量验证（本轮）

- `tools/offline-compile.ps1 -Only XianXia.Core,XianXia.Data,XianXia.Unity`
- `git diff --check`
- BaseGame Content load + `ContentReferenceValidator`
- SeparateSpace snapshot serialize／deserialize sanity
- Transition state-machine static sanity
- SeparateSpaceExitEdgeTrigger arming sanity
- Presentation restore priority static audit

未打开 Unity；未跑 PlayMode／Unity Test／batchmode。

## 制作人人工验收 checklist

见本轮最终汇报。
