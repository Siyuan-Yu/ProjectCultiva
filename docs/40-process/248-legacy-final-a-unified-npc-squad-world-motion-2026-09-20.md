# LEGACY-FINAL-A — Unified NPC Squad World Motion / FormalArmy Runtime Retirement

> 日期：2026-09-20  
> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 前置封板：MAP-04、CW-10、CW-10.5、Cave Loot persistence 已 Accepted / Sealed  
> 后续：LEGACY-FINAL-B（PlayerParty movement legacy）→ LEGACY-FINAL-C（TerritoryRegion 等剩余边界）

## 1. 本轮结论

正常 New Game 与 Continuous Runtime 的 NPC 多人组统一为：

- `SquadState`：唯一成员名单与 Leader authority；
- `SquadWorldMotionState`：唯一正常 NPC group 连续世界位置与移动 authority；
- `SurfaceId + exact WorldPosition`：正式空间坐标；
- `SquadContinuousFormationResolver`：由 group anchor 与稳定成员序号得到人物展示位置。

正常启动不再创建 `FormalArmy`、`ArmyStack` 或现代 `ArmyMembershipComponent`。旧 FormalArmy 内容和旧 FormalArmy snapshot 只允许单向转换为 `Squad + SquadWorldMotion`，转换完成后不保留旧 runtime authority。

## 2. Current consumer audit

| 分类 | 处理结果 |
|---|---|
| A — normal modern New Game / Continuous runtime | Content bootstrap、Host presenter、Continuous materialization、WorldMap marker、空间解析、NPC schedule ownership、CharacterEncounter 与 snapshot authority 均迁到 Squad/SquadWorldMotion。 |
| B — old save/content migration | 保留 `FormalArmyDefinition`、旧 JSON parser、`InitialFormalArmyIds`、FormalArmy/ArmyMembership snapshot DTO 与旧字段解析；只在 load/import 边界读取并立即升级。 |
| C — retired Army battle compatibility | ArmyStack、Hex army travel/pursuit、army-vs-army pending engagement 与对应 active services 已删除；旧 snapshot DTO 仅在 load boundary 单向迁移为真实 Character + Squad identity 的 `CharacterEncounter`。无法唯一恢复真实人物与精确 Surface 位置时明确 `SnapshotInvalid`。 |
| D — dead / no real caller | 删除旧军队创建/列表 UI、对应 `ArmyUiCommands`、无 caller 的 `SiteDefenseService`，并删除 normal `HostFormalArmyContinuousPresenter`。 |

审计目标是 normal runtime authority 为零，而不是仓库中字符串 `FormalArmy` 为零。

## 3. SquadWorldMotion domain

`SquadWorldMotionState` 以 `SquadId` 为唯一 group identity，保存：

- `SurfaceId`、精确 `WorldX/WorldY`、可选 `SiteId`；
- moving/idle、精确 destination；
- continuous route、waypoint 与 progress；
- revision/hash 等恢复所需信息。

`SquadWorldMotionBoard` 只按 `SquadId` 索引。`SquadWorldMotionService` 负责初始化、下令和推进。正常路径只使用 `SurfaceGroundNavigation`；不以 CurrentHex、DestinationHex、Outdoor LocalMap 或 WorldRegion 作为位置真源。速度沿用既有 continuous travel 参数，未调整平衡。

`SquadCommandKind` 以 additive 方式增加 `SquadWorldMotion = 3`；旧数值不重排。`FormalArmyWorldMotion = 2` 仅供旧 snapshot 迁移读取，新 runtime 会升级为现代 command。

## 4. Content 与 New Game

新增 `NpcSquadDefinition`，正式 authored 字段包括稳定 SquadId、名称、FactionId、成员、Leader、Surface 部署、精确位置或 authored core offset，以及可选 SiteId。

Current BaseGame 的六个原 FormalArmy authored group 已改为 `npcSquad`，OpeningScenario 改用 `InitialNpcSquadIds`。人数、名称、Faction、Leader 与 authored Surface 部署保持原设计。正常 bootstrap 先执行 `NpcSquadContentBootstrap`；只有旧场景确实存在 `InitialFormalArmyIds` 时才调用 `FormalArmyContentBootstrap` 单向 adapter。adapter 直接产生 Squad 与 SquadWorldMotion，不创建 FormalArmy 或 ArmyStack。

## 5. Formation、travel 与 Host

- `SquadContinuousFormationResolver` 按 EntityId ordinal 稳定排序成员，并从 Squad anchor 推导稳定 slot；不依赖 FormalArmyId/ArmyStackId。
- `StrategicTravelDriver` 推进现代 SquadWorldMotion、现代 background simulation 与仍属 LEGACY-FINAL-B 的 PlayerParty Hex compatibility；不再执行 FormalArmy/ArmyStack travel、sync 或 pursuit tick。
- `HostNpcSquadContinuousPresenter` 取代旧 FormalArmy presenter；Host bootstrap 只创建新 presenter。
- `ContinuousOutdoorSurfaceRuntime` 从 active SquadWorldMotion 物化 living Squad members；群组掌权时忽略 stale personal presence，角色失能、死亡或脱队后交回真实 Character lifecycle/personal spatial authority。
- `HostWorldMapPanel` 的“NPC 小队” marker 与选择 identity 直接来自 SquadId + exact SquadWorldMotion position。
- `HostNpcScheduleMover` 使用通用 Squad group-motion ownership，避免 individual schedule 与 group movement 竞争。

## 6. CharacterEncounter 与残留人物

现代 CharacterEncounter 的 roster 来自 `SquadState.MemberCharacterIds`，空间来源记录 `SquadId + SurfaceId + exact position`，不需要 FormalArmyId 或 ArmyStackId。Encounter snapshot format additive 升级；旧 FormalArmy owner 字段仍可读并迁移。

进入 Incapacitated/Captured/Dead 前，如角色仍受 SquadWorldMotion 掌权，会先把其确定性 formation point 写入个人精确世界位置。战后仍存活且仍在组内的人继续由 Squad 掌权；倒地、死亡或脱队人物保留自己的战术落点。残留人物由真实 Character lifecycle 与 spatial persistence 表达，不依赖 `ArmyStack.IsBattlefieldRemnant`。

旧 Army-vs-Army pending runtime 没有现代 producer，因此没有改名重建成 Squad RTS。旧 active pending snapshot 只在读取边界从 frozen Character records 解析真实 SquadId 与精确 Surface 位置，随后直接生成 `CharacterEncounter`；缺少唯一人物或位置证据时拒绝加载。现代 `CharacterEncounter` 绑定出的 `BattleParticipantRecord` 写入 `CharacterId + SquadId`，旧 `ArmyStackId/FormalArmyId` 仅保留反序列化字段。

## 7. Snapshot authority 与单向迁移

现代 snapshot 新增：

- `HasSquadWorldMotionSnapshotAuthority`；
- 按 SquadId ordinal 稳定排序的 `SquadWorldMotions[]`；
- Squad 的 DisplayName/FactionId；
- modern Squad 的 `LegacyArmyId` 固定为空。

新 Save 的 group truth 只有 `Squads + SquadWorldMotions`；FormalArmies 与 ArmyMemberships 兼容数组保持为空，不双写旧 authority。

旧 snapshot 在缺少现代 motion authority 且带有 FormalArmy DTO 时，一次性恢复/复用 Squad roster，迁移 exact Surface motion/route/progress，清空 LegacyArmyId、FormalArmyBoard、ArmyStackBoard 与旧 component id。旧 ArmyMembership DTO 只作为 migration input。

## 8. Compatibility boundary

特意保留：

- `FormalArmyDefinition` 与 `formalArmy` JSON parser；
- `OpeningScenarioDefinition.InitialFormalArmyIds` parser；
- FormalArmy/ArmyMembership snapshot DTO 与旧格式字段；
- `FormalArmySnapshotDto`、`ArmyMembershipSnapshotDto`、旧 PendingEngagement DTO 与对应 JSON 字段，仅作为旧存档输入。

这些类型不代表现代产品 authority。`ArmyMembershipComponent` 和 `SquadState.LegacyArmyId` 已明确为 LegacyMigrationOnly 语义。禁止现代路径由 Squad 反向创建 FormalArmy 或 ArmyStack。

## 9. 验收诊断

LevelTester 现显示：

`NPC Squad Runtime: Squads=X ActiveNpcSquadWorldMotions=Y LegacyArmyRuntime=RETIRED`

战斗作弊页提供“让所选 NPC 小队移动到主控附近测试点”。它要求单选现代非玩家 Squad 的 NPC，且至少两名 living member；目标是主控附近的安全固定偏移，并只调用正式 `SquadWorldMotionService.MoveToWorldPosition`。它不 teleport、不写 Transform、不使用 Hex destination。

## 10. 明确未做

- 未改 PlayerParty movement/follow、速度、repath、arrival epsilon 或 formation timing；
- 未做 TerritoryRegion 最终清理；
- 未重造 NPC-vs-NPC background combat；
- 未打开 Unity，未运行 Unity Test/PlayMode/batchmode；
- 未提交 LEGACY-FINAL-A。

因此 LEGACY-FINAL-B 与 LEGACY-FINAL-C 仍明确延期；A/B/C 全部完成前不得写“Legacy migration complete”。

EditMode 编译补查发现四类测试仍引用已退役的 `SiteDefenseService`、`ArmyUiCommands`、旧军队 roster DTO 与军队创建选择字段。只验证 dead UI/service 的断言已删除；仍验证旧战略 compatibility domain 的 acceptance test 改为直接调用 `ArmyService`。未恢复任何已删 production API。EditMode 测试程序集离线编译 0 error，未运行测试。

## 11. 制作人人工验收重点

1. New Game 后诊断为 FormalArmies=0、ArmyStacks=0、ModernArmyMemberships=0，且 authored NPC Squads 正常存在。
2. WorldMap 能看到“NPC 小队”，选择与定位使用精确 continuous position。
3. 使用 LevelTester 指令让选中的 NPC 小队向主控附近移动；观察连续路线、队形、到达和 marker 同步。
4. 移动中 Save/Load 后路线、进度、位置、成员与 Leader 保持一致。
5. NPC Squad 与玩家发生 CharacterEncounter，参战 roster 正确；战后存活成员归队，失能/死亡人物保留真实落点。
6. 读取一份旧 FormalArmy 存档/内容时能单向迁移，随后诊断仍为 FormalArmies=0、ArmyStacks=0。
7. PlayerParty 移动、跟随、到达与 Separate Space 行为无回归。

## 12. Producer Acceptance regression：PlayerParty authority isolation

人工验收发现 WorldMap 下达连续移动后主控停在原地。根因是 `SquadWorldMotion` 缺少严格的 NPC-only authority isolation：迁移或旧开发存档可为 PlayerParty ControlledSquad 留下一条 motion，随后 NPC presenter 每帧把玩家 Transform 写回 stale Squad anchor，覆盖 `PlayerPartyTravel` 的 LocalVisible 表现移动。

本次修复冻结以下 invariant：

- `PlayerPartyTravel` 永远拥有 Controlled Squad 的世界移动 authority；
- `SquadWorldMotion` 只允许非玩家 NPC Squad 使用；
- PlayerParty spatial authority 永远高于 Squad 与 legacy FormalArmy authority；
- modern snapshot 永不保存 ControlledSquad 的 `SquadWorldMotion`，且其 Squad command 规范化为 `FollowLeader + Active Character`；
- bug build 产生的 modern transitional snapshot 会丢弃 ControlledSquad motion，并保留独立 `PlayerPartyTravel`；
- legacy FormalArmy migration 永不把 ControlledSquad 转成 `SquadWorldMotion`；
- PlayerParty bind/restore 会幂等移除 stale motion，并通过 `SquadCommandService` 恢复玩家 command；
- Host NPC Squad presenter、Continuous materialization、WorldMap marker、roster 与诊断统一使用 active NPC Squad authority gate，不能写 PlayerParty Transform 或将玩家误标为 NPC group。

未修改 `PlayerPartySurfaceTravelService`、`HostPlayerPartyController` 的路线、速度、arrival、repath 或 LocalVisible 参数，也未回滚 `StrategicTravelDriver` 的现代 NPC 推进位置。状态保持 **Implementation Complete / Producer Acceptance Pending**，等待制作人重新验收后再封板。

## 13. Producer Acceptance regression：NPC SiteArrival 与战斗初始落点

第二次人工验收发现两项相连的 authority 错误：

1. 无显式 deployment 的旧 FormalArmy 原本通过正式 `SiteArrival` 初始化；迁成现代 NPC Squad 后却把 `controlCore` 几何中心当成 group anchor。Core center 可能位于议政厅等 blocking building 内，不能作为隐式 physical spawn authority。
2. CharacterEncounter 把基于 Core `SurfaceGround` 计算的 formation point 直接当成最终独立战场 Tactical 点，没有在包含 Site、建造物、destructible 与旗帜 blocker 的 prepared Composite grid 上重新验证。

本次修复冻结以下 invariant：

- AtSite NPC Squad anchor 永远来自 `SurfaceGround.TryResolveSiteArrival`；`controlCore` 中心不再是无显式部署时的 group spawn；
- modern bug-build snapshot 中 stationary + non-empty SiteId 的 motion 会归一化到当前 SiteArrival；旧 FormalArmy AtSite migration 复用同一现代入口；
- field NPC Squad 的无 SiteId 精确位置保持原值；
- `OriginX/Y` 只记录普通世界 source authority；`TacticalX/Y` 是最终独立战场中的合法初始位置；
- 当前已 materialize 且严格合法的 EntityView 可原位提供 Tactical；否则从 Origin 开始，在 final prepared grid 内做半径 8 格、四向连通、确定性且不重叠的 correction；找不到同侧连通位置时明确失败；
- formation candidate 在 SurfaceGround 层必须与 anchor 线段连通；最终物理 blocker 仍由 Composite grid 判定；
- `SquadWorldMotion` owner 不进入普通 personal-placement capture，只有脱队、倒地或死亡后才由既有 handoff 产生个人精确位置。

战斗 preparation 只改 Tactical，不写 Origin、SquadWorldMotion、PlayerPartyTravel、WorldPresence、EntityLocation 或成员关系。正常 runtime 仍为 `Squad + SquadWorldMotion`，没有恢复 FormalArmy/ArmyStack。状态继续保持 **Implementation Complete / Producer Acceptance Pending**。
## 14. LEGACY-FINAL-A2 — Runtime Zero-State（2026-09-21）

A2 完成最后一层 runtime 退役，状态保持 **Implementation Complete / Producer Acceptance Pending**：

- `StrategicBoard` 已移除 FormalArmyBoard 与 ArmyStackBoard；Entity runtime 不再允许 ArmyMembershipComponent。
- `LegacyFormalArmySnapshotMigration` 直接把旧 FormalArmy/ArmyMembership DTO 转成 Squad 与 SquadWorldMotion，不创建中间 FormalArmy/ArmyStack/component。AtSite 使用 canonical SiteArrival；exact Surface 与 route/progress 原样迁移；仅 Hex 的 genuine old save 在 migration boundary 解析为精确 Surface 位置。
- modern capture 的 FormalArmies 与 ArmyMemberships 始终为空；BattleParticipant 增加 additive SquadId，CharacterEncounter normal bind 写入 SquadId。
- 旧 CharacterEncounter FormalArmy owner 在 validate 前改写为 Squad owner；旧 army-vs-army PendingEngagement runtime 已删除，旧 active snapshot 通过 `LegacyPendingEngagementSnapshotMigration` 直接转为真实 Character + Squad 的 CharacterEncounter，证据不足则 SnapshotInvalid。
- FormalArmy/ArmyStack 创建、变更、旅行、追击、战斗 roster、残留和 Host presentation services 已删除。normal spatial resolver、Host/UI、opening/rehydration 和 snapshot fingerprint 只读取 Squad/SquadWorldMotion/Character。
- `StrategicTravelDriver` 不再执行 army travel、ArmyStack sync 或 army pursuit；PlayerParty Hex compatibility 留给 LEGACY-FINAL-B，TerritoryRegion 留给 LEGACY-FINAL-C。
- 旧 Content schema/parser/validator、FormalArmyDefinition、FormalArmy content adapter、FormalArmy/ArmyMembership/Pending snapshot DTO 与 JSON reader 继续保留，只允许在兼容输入边界使用。

本阶段不声明 Accepted / Sealed，等待制作人人工验收。

## 15. A2 制作人验收修复：Continuous 启动 ActiveCharacter 物化

`PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty` 曾把旧“排除 FormalArmy 成员”的条件机械替换成“排除任何 Squad 成员”，误伤了本来就由 `ControlledSquadId` 表达的玩家队伍。结果是 ActiveCharacter 未进入首次 Continuous materialization，启动 invariant 报 `NotContinuousMaterialized`。

现改为只排除不属于 PlayerParty controlled/player Squad 的其它 Squad；玩家 ActiveCharacter 继续由 PlayerParty motion 驱动物化，NPC Squad 仍由 SquadWorldMotion 驱动。状态保持 **Implementation Complete / Producer Acceptance Pending**。

## 16. A2 制作人验收修复：恢复现代 CharacterEncounter presentation

`HostStrategicInterruptPresenter` 在退役旧 Army battle infrastructure 时被缩成 no-op，但该序列化组件仍承担现代 `CharacterEncounter` 的完整表现入口。结果是 Domain 已进入 Pending、`CharacterEncounterUI` 已持有 modal pause 与输入锁，却没有窗口，也没有调用 `BeginConfirmed`、`StartBattle`、`TryFinishBattle` 或 `CloseReport` 的用户操作。

本次只恢复现代 presentation pipeline：

- 类名仅为现有场景序列化兼容而保留；runtime 责任限定为 CharacterEncounter offer、准备进度、失败重试、开始门、结束门、`ManualBattleReport` 与 transient toast；
- Encounter 预览和最终参战名单继续来自真实 Character／Squad roster 与 `CharacterEncounterState.Participants`；
- `CharacterEncounter` 继续掌管确认、独立战场准备、Start、ReadyToEnd、CommitAndReturn 与 CloseReport；`ManualBattleReport` 继续是战果展示权威；
- 战报使用独立 `ManualBattleReport` pause/input owner，关闭时必须成功调用 `HostCharacterEncounter.CloseReport` 后才释放；
- Active encounter snapshot 恢复后仍停在 `ReadyToStart` 等待玩家开始，恢复的 `ReadyToEnd` 仍显示结束入口；
- 删除无 caller 的第二层 `LocalAttackConfirm` residue，保留据点／势力旗外交用途的 `StrategicAggressionConfirm`。

旧 `BattleOfferService`、`BattleDecisionPolicy`、`PendingEngagement`、ArmyStack／FormalArmy 战斗 UI 与战略 Army battle workflow 继续保持退役和删除状态，没有因本次表现修复恢复。状态保持 **Implementation Complete / Producer Acceptance Pending**。

