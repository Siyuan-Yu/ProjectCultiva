# CW-02 暂停归属与小队失能安全出口交接（2026-09-12）

## 状态与范围

- **CW-02：Producer Accepted — 当前交付范围**。验收依据为制作人反馈：当前已能找到敌人、从 Continuous 地面发起并完成手动战斗、结束并显示战报；非参战人物隔离、战报名单、伤亡区分、失败／暂停和存读档在本轮范围内“基本是对的”，制作人同意进入下一阶段。本记录不伪造逐项测试日志。
- **Known Deferred Items**：最终临时独立同源战场、按真实战场裁切范围确定初始参战资格、有限远援在途／到场、完整势力继承及大地图攻击入口退役均未纳入本次通过范围。
- CW-01 的 Outdoor 动态物件、逐格通行、Continuous Surface hard rebind 与普通 Chunk 增量加载保持原实现。本轮复用 CW-01 的 `RebuildPresentationAfterLoad`，没有新增第二套读档重建流程。
- 本轮没有实现新独立战场、战前锚点快照、战后精确回位、SiteCore、建筑宣战／接管／援军、完整势力继承或飞舟。

## 2026-09-13 Fixup：Continuous Outdoor 手动战斗入场闭环

制作人确认试炼弱匪已经能在地面显示并产生 FormalArmy BattleOffer，但“手动战斗”仍被 `LoadedLocalMapBelongingQuery` 拒绝。现在 Local-origin 入口会先识别当前物理表现是否由 `ContinuousOutdoorSurfaceRuntime` 主导；Continuous 分支不再解析、填写或加载 Legacy `LocalMapId`，也不使用 Wilderness fallback。

入场新增一份绑定当前 `SimulationWorld`、`OfferId` 与 `SurfaceId` 的只读准备结果。宣战前会一次性核对：冻结 BattleAnchorHex、当前 Surface coverage、目标所在 loaded Chunk、CompositeWalkGrid、全部已选参战实体，以及每个实体可用的稳定合法落点。现有合法 View／override 保持；缺失落点按 FormalArmy `WorldMotion`、PlayerParty 连续位置或精确 `WorldPresence` 在当前 composite grid 上做有界连通布置。任一真实实体、Chunk 或落点缺失都会保留 Offer 并明确失败，不生成 synthetic bandit，也不提交半套 materialization。

成功装配后，`StrategicBoard.ContinuousManualCombat` 记录本场表现空间所有权和冻结参战者集合。`ContinuousOutdoorSurfaceRuntime.Update` 在 ManualEncounter／PostBattle 期间保留当前 Surface、Chunk、动态物件和导航；普通 field-army reconcile 即使跳过 engaged Army，也会由该集合继续保留同一 EntityId 的 materialization/View。`LocalMapVisibility` 使用同一状态在 legacy Site／Location 门禁前放行本场 Continuous participant。敌对判定、参战快照、战争与结算仍复用现有领域链，Continuous 消费器只复用真实冻结实体，绝不执行旧 `WorldRegion.StartLocationId` 摆位或 fallback spawn。

入场装配现在返回 `Result`；只有成功后才清 BattleOffer／PendingEngagement、提升冻结阶段并释放 Offer modal。每次 Continuous 入场尝试只写一条 `[ContinuousManualBattleEntry]` 汇总，包含 Offer、Origin、Surface、ActiveMap、战略/连续锚点、Chunk readiness、预期/实际人数和失败阶段。战后在 `ResolveAndEnd` 完成伤亡与 Army 同步后，Host 以原 OfferId 只清本场 Continuous context，提交当前主控的合法连续位置并执行普通 Continuous entity reconcile；不重载旧房间，不清别场状态，不重复生成角色。Manual/PostBattle 中途存档仍沿用既有“不支持”边界。

## 2026-09-13 Fixup：field FormalArmy 表现与 Modal ownership 收口

### Fixup #2：Main Surface coverage 与 W2A geography 解耦

制作人第二次复验确认“试炼弱匪”大地图存在但靠近后地面仍无人。根因是 field-army eligibility 曾要求 `SurfaceGroundNavigation.Contains(WorldPosition)`；该 navigation 只是 5×5 Chunk 的 W2A 精细 geography，而 Main Continuous Surface 的 authored coverage 有 646 Chunk。试炼弱匪 `(10,6)` 的世界点约 `(17.32,9.0)` 属于 Main Surface，却在 W2A bounds 外，因此被稳定过滤。

现改为两级判定：`OutdoorSurfaceCoverageResolver.ContainsWorldPosition(activeSurface, ...)` 决定 Army 是否属于 Main Surface，`_loaded` 决定是否进入 near-field materialization。`SurfaceGroundNavigation` 不再参与实体存在资格；它只在确实覆盖 Army anchor 时辅助精细编队。

首次和后续编队统一通过 Continuous runtime：W2A 内优先复用 geography/route；W2A 外使用当前 `_compositeWalkGrid` 修正 blocked anchor，并以确定性有界搜索选择 walkable、与 anchor 直线连通的 presentation slot。所有 offset 只写 `EntityLocationComponent.PresentationOverride`，不写回 `FormalArmy.WorldMotion` 或独立 `WorldPresence`。

上一轮定向核对确认：拥有 `WorldPresence.AtWorldPosition` 的成员可由 `ContinuousOutdoorMaterialization.IsMaterialized(id)` 放行，不要求 `LocalMap.ContainsOccupant` 或 legacy LoadedLocalMap；但该结论没有覆盖“派生 Presence 已丢失”的 NewGame 成员。下方 follow-up 已把 Continuous predicate 移到 Presence 分支之外。现有 `DescribeDiagnostics()` 的 `FormalArmyNearField` 汇总仍保留。

### Fixup #2 follow-up：NewGame Presence、visibility 与落点提交闭环

对照 `Scripts(20260913-052515)` 继续核对后确认：`StrategicContentBootstrap` 先创建 Army，随后 `HexStrategicSessionBootstrap.ApplyOpening` 清空 `WorldPresence`；opening spawn 只恢复 authored spawn，普通 Army member 的派生 Presence 因此丢失。现于该开局边界在 authored spawn 取得优先权之后，只为缺失 Presence、仍由对应 Army 管理且满足现有 macro-living 规则的成员调用 `FormalArmyMemberPresenceSync.SyncMember`。已有 authored Presence 不覆盖；交战 Army 跳过；脱队弥留者和尸体不被重新收回 Army；驻军／AtWorldSite 仍按 Army `WorldMotion` 派生为 Site Presence。正式读档继续只走既有 `FinalizeRuntimeLinks`。

`LocalMapVisibility` 新增并复用 `EvaluateContinuousMaterializedVisibility`：有效 Continuous scope、非 Interior／Encounter／真实 LocalMap battle、未 Removed、当前 materialized 且有合法 `PresentationOverride` 时，在任何 legacy `WorldPresence`／`LocationId` 门禁之前放行。诊断与 gameplay 调用同一个 predicate，并报告具体拒绝原因。

field Army reconcile 现在先解析或修复 composite-grid 合法落点，再把成员加入 desired materialization；首次落点失败不会伪装为 materialized。已经 materialized 但 override 缺失／越出 composite grid／落在 blocked cell 的成员会在同一次正式 reconcile 尝试修复。所有有效 field Army living member 都先标记为 Army authority，防止后面的通用 `WorldPresence` fallback 用派生旧记录绕过 Surface／Chunk／落点拒绝；AtWorldSite／驻军仍交给 Site population。

开发工具入口：反引号打开 `LevelTester 开发工具` → `诊断` → `复制军队显示诊断（只读）`。优先读取大地图当前选中的 FormalArmy；未选中时默认查询试炼弱匪。剪贴板报告包含 Army/Entity、地图 scope、WorldMotion、coverage/chunk、Presence、Location/override、materialized、实际 visibility 与共享 predicate 原因、Registry View；View 存在时追加 transform、Renderer、sprite、sorting、主相机 viewport 与 culling mask。

### Continuous Outdoor field FormalArmy

`ContinuousOutdoorSurfaceRuntime.ReconcileOutdoorEntityMaterialization()` 现在把当前主 Surface、已加载 Chunk 内的 field FormalArmy living members 纳入 desired entity 集合。living 判定复用 `LingeringBattlefieldPartyService.IsLivingForMacroOrder`，PlayerParty 成员排除；驻军仍只走 `StrategicWorldSitePopulationService` 的 Site population 路径。

首次生成以 `FormalArmy.WorldMotion.WorldPosition` 为编队锚点，并与 `HostFormalArmyContinuousPresenter` 共用稳定的 route-behind／connected-slot helper。后续 WorldTick 移动仍由原 Presenter 更新同一 EntityId 的同一 View。`SurfaceId` 非空时必须与当前 Surface 相同；idle motion 的 `SurfaceId` 为空时，仅在其 WorldPosition 经当前 mapper 落入实际 loaded Chunk 时显示。

field army 单独记录 presentation ownership。离开 Chunk、Surface deactivate 和正式读档 hard rebind 只清它的 View/materialized state 与 presentation override，绝不调用 Site NPC 的 `TryCaptureAtSiteAnchor`，不写 `WorldPresence.AtSite`，也不改 Army membership、`WorldMotion` 或大地图图标。

制作人首次复验发现荒村 `杂役主管` 缺失（Expected 17 / Materialized 16）。原因是防双写分流曾错误要求军队成员必须处于 `FormalArmyState.Garrisoned` 才能走 Site population，而正式 `StrategicWorldSitePopulationService` 按军队实际 Hex 是否属于 Site 判定，开局主管所属军队仍为 `Idle`。分流现改为：只有已被本次 field-army pass 实际认领的 EntityId 才跳过 Site path；否则继续服从正式 Site population 与 baked opening anchor。Army state 不再被当作 Site presentation 的替代判据。

### Modal pause ownership

附件列出的剩余临时窗口均改为具名 `AcquireModalPause`／`ReleaseModalPause`：洞府勘查、LocalMap 进入确认、人物档案、斗技学习与斗技面板、打坐确认与修炼面板、功法学习、突破结果、Content interrupt、对话、任务日志、技能研读、井字棋，以及 NPC 菜单中的拆旗确认。Close／Cancel／`ClearSessionState`／`OnDisable` 只释放本组件 owner；背包与建造补齐 `OnDisable` 释放。NPC 菜单的普通 `CloseAll` 不再恢复时间；Dialogue／ContentEvent 分别由自己的 Presenter 持有。

仍保留的直接 `ManualPaused`／`IsPaused` writer 均是明确玩家或 gameplay command：HUD／Bootstrap 的 Pause、Resume、Space toggle 与初始化暂停；人物移动、工作目标、HUD action、NPC 已确认行动的 Resume；手动遭遇入场只恢复入场前记录的 `ManualPaused`。它们不是临时 Modal close。

## 已修复行为

### 制作人首轮复验修正：Continuous → 手动战 LocalMap 落点

制作人首次按 Route 1 进入手动战时，PlayerParty View 仍短暂保留 Continuous presentation 坐标 `(625,375)`，而当前战斗 LocalMap WalkGrid 是局部坐标，旧 safety repair 只在该远端点周围搜索 16 格，最终报 `PlayerParty materialized placement still unsafe after repair`。

现在最终落点屏障先读取 PlayerParty materializer 已写入的 `EntityLocationComponent.PresentationOverride`，该局部点合法时直接把残留 View 对齐过去；局部 authored 点被 blocker 占用时，才在当前 WalkGrid 内执行一次完整、确定性的最近安全格搜索。该搜索只发生在 materialization finalize，不进入 Update／WorldTick。手动遭遇中的局部安全修正不会反写 `PlayerPartyWorldMotion` canonical 世界坐标。

### 1. 玩家意图、Host 模态限制与领域冻结分层

`PlayableHostSession.ManualPaused` 只表达玩家手动暂停。Host 模态面板使用具名 owner 持有 `ModalHardPaused`；关闭时只释放自己的 owner，不把 `ManualPaused` 无条件写成 `false`。Fixup 后正式战斗打断、Snapshot restore 临时保护及现有临时 UI 都已接入该 ownership。大地图只是输入／规划 overlay，开关都不再改写玩家手动暂停。

领域层的 `StrategicClockFreezeState` 继续冻结 `WorldTick`。`SimulationLoop.TickOnce` 原本已在入口检查 `IsWorldTickFrozen`，所以日程、生产、修炼、旅行和战略 AI 不会在 BattleOffer／ManualEncounter／PostBattle 期间偷跑；该正确部分保留。Host 战斗表现仍由 `ManualPaused + ModalHardPaused` 决定，没有使用 `Time.timeScale=0`，也没有新增世界日历。

### 2. BattleOffer → ManualEncounter → PostBattle → Exit 连续归属

- BattleOffer 出现时，领域冻结进入 `BattleOffer`，战略弹窗持有 `StrategicInterrupt` 模态 owner。
- 进入手动战斗时，领域冻结只提升到 `ManualEncounter`，中间不清空；弹窗释放自己的 Host owner。开战前玩家未手动暂停则战斗可运行，玩家原本手动暂停则保持该意图，可自行解除。
- 全场结束或我方全失能时，领域冻结提升到 `PostBattle`，不会出现短暂 WorldTick 放行。
- 正常结算和撤退只允许释放自己预期的冻结阶段。旧回调重复关闭时为幂等；若冻结已属于另一阶段或下一场 Offer，则拒绝清除。
- 撤退路径原本同时在 `BattleRetreatService` 和 Presenter 各结束一次，现在只由 Core 服务完成一次结算与释放，避免二次清理。
- 结束、撤退和下一场排队都不再恢复／覆盖 `ManualPaused`；只恢复本场保存的倍速。

### 3. 正式读档的临时保护与重绑

正式 LevelTester Snapshot 读取在替换／恢复 World 前取得 `SnapshotRestore` 模态 owner，并在完整 domain restore、rehydrate、Continuous hard rebind、表现重建之后用 `finally` 只释放该 owner。读档失败也会释放本次保护。

重建时会清掉旧 Presenter／面板各自的瞬态 owner，再从新 World 的 PendingEngagement／BattleOffer 数据重建有效领域冻结。读档结束不再执行 `Session.IsPaused=false/true`，也不使用读档前缓存值覆盖新 World 的有效状态。当前正式 Snapshot 已支持 PendingEngagement／BattleOffer；正在进行的 ManualEncounter／PostBattle 完整生命周期持久化仍属于后续完整遭遇闭环，不在 CW-02 扩张。

### 4. 失能、恢复与真正死亡分开

`PlayerPartyRuntime.ControlState` 现在明确区分：

| 状态 | 判定 | 当前行为 |
|---|---|---|
| `Active` | 当前 Active 可控，或固定 Party 顺序中找到下一名合格者 | 保持已有 Active；失能后按成员顺序接替，不按战力排序 |
| `TemporarilyUnavailable` | 当前没有合格 Active，但至少一名成员仍未真正死亡 | `Active=None`，禁止人物移动／攻击；保留状态提示、战斗结算／退出、读档与每帧恢复重判 |
| `AllMembersDead` | 每名 Party 成员都是 `Dead` 或 `Removed` | 进入已有死亡交接边界；完整势力最强角色继承仍待后续实现 |

`HostPlayerPartyController` 不再因旧的 `IsAwaitingSuccession` 或 `Active=None` 在生命状态刷新前永久 return。它每帧先校验现有 Active；若队员通过既有规则恢复，重新按固定顺序选出合法 Active，并重绑输入、选择和相机，不改成员顺序、不重复生成、不传送。

HUD 在暂时全失能和全员真正死亡时仍显示小队与明确文字。手动战斗中全员失能仍由既有 `StrategicEncounterResolveService.TryEnterPostBattleFromManual` 进入 PostBattle，右侧“结束战斗”入口不依赖 Active 存在，退出后按本场 owner 释放冻结。

Snapshot 捕获不再以 `HasActive` 作为保存 Party membership 的前提。`Active=None` 时仍保存有序成员；读取实体生命状态后重新推导控制状态，旧档不会靠遗留的等待继承状态把控制永久锁死。没有新增可从生命数据安全推导的持久字段。

## 空间锚点实施前核查

本表只记录当前实现，供 CW-06 接入战前锚点与战后回位；本轮没有新增 DTO 或空壳系统。

| 情境 | 当前实际字段／服务 | 当前精度与兼容缺口 |
|---|---|---|
| Outdoor PlayerParty | `PlayerPartyWorldMotion.WorldPosition`；成员同步到 `WorldPresenceBoard` 的 `WorldPosX/Y + HasContinuousWorldPosition`；Continuous Host 负责 presentation↔canonical 同步 | Continuous 路径有精确世界坐标；`CurrentHex` 是上下文／派生值，不能替代精确点 |
| Outdoor 普通人物／残留者 | `WorldAgentPresence`：`AtSiteWithAnchor`、`AtWorldPosition` 或 `SetAtResidualWorldPosition` | 新 Continuous 数据可有精确点；旧 `SetAtSite`／`SetAtHex` 只有 Site/Hex，无法恢复精确战前位置 |
| 真正 Interior | `PartyWorld.LocalMapId` + `LocalMapSession.ActiveMapLayoutId/OccupantIds`；个体局部点由 `EntityLocationComponent.PresentationOverrideX/Z`，Snapshot 由 `LoadedLocalMapCharacterPlacements` 保存 | 能保存当前已加载 Interior 的局部点；空间实例身份仍主要依赖 MapLayoutId，CW-06 需核对同模板多实例需求 |
| FormalArmy 成员 | `ArmyMembershipBoard` 解析所属军队；军队位置由 `FormalArmyWorldMotion.WorldPosition/CurrentHex/SiteId`，`FormalArmyMemberPresenceSync` 写成员 `WorldPresence` | 新军队 motion 有世界坐标；旧档或旧会话可能只有 `FormalArmy.CurrentHex`，`FormalArmyHexWorldPositionResolver` 只能给格心级兼容位置 |
| 运输中的人物 | 当前没有飞舟／通用载具运行时位置持有者 | **未实现**；不得把 Army membership 或 FollowStack 冒充运输状态 |
| 只有 Hex 的旧会话 | `WorldAgentPresence.Mode=AtHex` 且 `HasContinuousWorldPosition=false`，或 FormalArmy 只有 `CurrentHex` | 只能恢复 Hex 级位置；CW-06 必须显式记录兼容降级，不能假称精确回位 |

## 关键修改入口

- 暂停分层与 owner：`Assets/Scripts/Unity/Host/PlayableHostSession.cs`
- WorldTick 冻结归属：`Assets/Scripts/Core/World/Strategic/StrategicClockFreeze.cs`
- 结算／撤退单一释放：`BattleOfferService.cs`、`BattleRetreatService.cs`
- 战斗弹窗阶段与 Host 暂停：`HostStrategicInterruptPresenter.cs`
- 正式读取保护与 CW-01 hard rebind：`HostLevelTesterSnapshotOps.cs`、`PlayableHostBootstrap.cs`
- 大地图、背包、建造面板边界：`HostWorldMapPanel.cs`、`HostInventoryPanel.cs`、`HostConstructionPanel.cs`
- 小队控制状态和存读档：`PlayerPartyRuntime.cs`、`PlayerPartySnapshotRestore.cs`
- 自动接替／恢复重绑与 HUD：`HostPlayerPartyController.cs`、`HostFormalHud.cs`
- 人工触发和读档诊断：`HostLevelTesterCheatPanel.cs`、`HostLevelTesterSnapshotSummary.cs`、`HostSnapshotActiveControlTrace.cs`

## 2026-09-13 补充：战场参战范围收口与手动战报 V1

状态：**Implementation Completed / Producer Acceptance Pending**。本节记录本轮实现接线，不代表制作人已经验收。

- `ActualBattleParticipantQuery` 现在是本场实际参战名单的统一判定：按冻结记录顺序收集 `MandatoryFriendly`、已勾选 `OptionalFriendly`、`EnemyPrimary` 与 `EnemyReinforcement`，并按 `EntityId` 稳定去重、保留敌我阵营。Continuous 入场成功后把这份名单冻结到 Offer／Surface scoped presentation state；表现、敌对、选靶、控制接替、最终伤害与胜负均使用同一名单。
- Continuous `ManualEncounter`／`PostBattle` 激活期间，角色 materialization 只保留实际参战者；普通 PlayerParty、field Army、Site population 与 residual pass 暂停。非参战角色只隐藏 View，既有实体、军队关系、`FormalArmy.WorldMotion` 和位置记录不删除；本场结束、回滚或 World 替换后按 Offer ownership 清除隔离并恢复普通 reconcile。
- `LocalMapVisibility` 在所有 legacy Site／Location 条件之前执行战场硬门禁。交互路由、NPC 敌对、斗技自动选靶与 `MeleeCombatService` 最终伤害入口共同拒绝非参战第三方；最终伤害门禁位于生命值初始化、伤害、事件与关系副作用之前。战中 Active 失能仍按 Party 固定顺序选择下一名实际参战且可控的队员。
- 手动战正式入场后、首次伤害前只读捕获进场状态；点击“结束战斗”时再捕获最终状态。结构化报告只复制实际参战者的 `EntityId`、姓名、阵营、进场／最终生命状态与已有 HP，不调用 `EnsureVitals`。最终类别互斥：完好、受伤、重伤（HP 比例 ≤ `ManualBattleReportBuilder.SeriousInjuryHpRatio` 30%）、弥留、阵亡、被俘、已移除、状态不可用；进场已有伤势与本场变化分开显示。
- 结算成功后显示独立手动战报 modal。战报 owner 在释放 Encounter freeze 前取得；“继续”只关闭战报并释放自己的 owner，不重复结算。若已有下一场排队接战，战报优先显示，关闭后才显示下一场；旧 Continuous 战场隔离仍立即清理。战报打开时 LevelTester Save 明确要求先关闭战报。

### Fixup：结算身份被快照重建清空

制作人复验发现：敌军已经全部失去战斗能力，但点击“结束战斗”提示 `Battle settlement has no frozen participant identity.`。实际条件不是参战名单为空，而是 `BuildSnapshotFromEngagement` 先写 `Participants.OfferId`／ArmyId，随后 `ApplyLockedParticipantsToSnapshot` 内部执行 `snap.Clear()`；该调用又重建了参战记录，因此形成“人物名单存在、场次身份为空”的不一致快照。现已把场次身份绑定移到快照重建之后。

手动入场还会在允许首份战术伤害前建立 `ManualBattleSettlementState`：它独立保存本场可靠 OfferId、实际参战 EntityId／阵营副本和战报进场状态，不引用随后会被清理的可变集合。PostBattle 先校验当前快照、Continuous context 与该副本属于同一场且名单一致，再捕获点击结束时的最终伤况、执行一次结算并保存已提交战报；之后才允许清本场运行上下文。快速重复结束只恢复同一已提交报告，“继续”只释放 `ManualBattleReport` modal owner，不再结算或修改 `ManualPaused`。

对修复前已经进入战斗的同一运行会话，只在快照或 Continuous context 仍提供可靠场次 ID、且现有实际名单完全匹配时建立兼容副本；进场 HP／状态显示未知。若连这两项也不可靠，则保留现场并输出一次 `[ManualBattleSettlementFailure]`，不会扫描附近人口、伪造 ID 或跳过校验。

### Fixup 人工验收

1. 正常 New Game，从荒村走到 Hex `(10,6)` 的“试炼弱匪（自动必胜）”，地面右键攻击并选择手动战斗。把 WeakBandit 打至弥留；若需要确认最终伤况，可在 PostBattle 补刀后点击“结束战斗”。预期不再出现冻结身份错误，战报只列本场实际参战者并显示点击结束时的最终状态；点击“继续”后回到普通 Continuous World，原有手动暂停意图保持。
2. 敌方全部失能后快速重复点击“结束战斗”。预期只提交一次战果并只显示一份本场报告，无重复奖励、重复 Army 同步或永久冻结。
3. 点击第一份战报“继续”，在同一次运行中再次完成一场手动战斗。预期第二份报告使用新的 OfferId，只列第二场人物，不沿用上一场身份或伤况。
4. 可用既有“全队进入弥留”手动入口补验失败结算。预期仍能进入合法 PostBattle 并生成失败战报；本 Fixup 没有新增测试按钮。

### 本轮制作人人工验收

1. **参战范围与胜负**：正常 New Game，从荒村走到 Hex `(10,6)` 的“试炼弱匪（自动必胜）”，地面右键攻击并在 BattleOffer 选择手动战斗。入场后应只看到本次实际敌我参战角色；附近村民、未勾选可选友军及未参加的军队成员不出现。敌方增援若列入 Offer，必须全部失能后才进入战后；未参战旁观者不阻止胜利。
2. **第三方门禁与固定接替**：战中尝试用点击／技能选取附近未参战角色，预期不能选为攻击目标、不能产生新 BattleOffer、不能受伤或产生攻击关系事件。用现有 LevelTester 战斗工具令 Active 弥留，预期只在本场实际参战的 Party 成员中按原列表顺序接替；弥留／尸体仍留在战场表现与最终战报中。
3. **一次结算与战报**：允许一名参战者带伤入场，战中再造成受伤、弥留或阵亡，敌清空后可补刀，再点击“结束战斗”。预期只结算一次并打开战报；每行显示进场状态→点击结束时的最终状态，进场旧伤不会被写成新伤。战报不含 Acceptance/Debug 区块；打开期间主世界不推进，Save 提示先关闭。点击“继续”后恢复普通 Continuous 人口，非参战角色回到原表现位置，无 clone。

本轮仍未迁移独立同源战场、战前精确锚点／战后回位、建筑宣战与接管、关系／守备援军完整生命周期、完整势力继承及飞舟；不进入 CW-03。

## 验证结果与未迁移边界

- 现有 `tools/offline-compile.ps1` 编译 Core／Data／Unity Host／Editor／Tests／PlayModeTests／Assembly-CSharp：通过，0 error；只有既有 warning。这里只执行编译，没有运行任何测试。
- 静态核对：所有 `StrategicClockFreezeService.EndFreeze` 调用均提供预期 owner 阶段；撤退只剩一次 `FinishOfferResolution`；正式读取的保护为 `try/finally`；Party Snapshot 在 Active=None 时仍捕获成员。
- 未启动 Unity；未运行 EditMode、PlayMode、Test Runner、batchmode 或其他自动测试；未执行 Bake。
- ManualEncounter／PostBattle 的完整中途存档、战前精确锚点、战后精确回位、完整势力继承和空势力终局仍待后续。空势力终局按 ADR-0034 明确延期。

## 制作人人工验收路线

### Continuous 手动战斗 Fixup（本轮必须先验）

1. **正常入场**：正常 New Game／LevelTester `travel_mvp`，从荒村沿 Continuous Outdoor 走到 Hex `(10,6)`、WorldPosition 约 `(17.32,9.00)` 的“试炼弱匪（自动必胜）”；地面出现 `WeakBandit` 后用现有右键攻击，BattleOffer 中点击“手动战斗”。预期不再出现“无法解析当前已加载的 LocalMap surface”；地图、桥路、建筑与动态破坏状态保持当前 Continuous 现场，PlayerParty 与该 FormalArmy 的同一批真实 EntityView 进入战斗，没有 fallback 地图、clone 或瞬移到 `(0,0)`。
2. **战中运行与暂停**：进入后先确认 WorldTick／日期／生产不推进，再用现有 WASD／点击移动和攻击完成战斗；分别测试玩家原本未手动暂停，以及开战前已按 Space 手动暂停后自行解除。预期 BattleOffer modal 已释放，未手动暂停时战术操作可运行；手动暂停意图保留；主世界始终冻结，Surface/Chunk 不卸载。
3. **结束并继续世界**：敌方失能后在 PostBattle 点击“结束战斗”。预期伤亡和消耗保留，弱匪 Army/成员按既有结算同步；画面留在同一 Continuous Outdoor，没有旧 LocalMap 重载、StartLocation 跳转、人物消失或重复 View；本场冻结释放后世界时间按原手动暂停状态恢复，随后可继续移动并正常保存/读取。
4. **失败可重试**：若第 1 步仍失败，保持现场并保留 BattleOffer，只记录 Toast 与当次唯一一条 `[ContinuousManualBattleEntry]`。该日志应明确 `Stage=Preflight/Assembly`、Surface、连续锚点、Chunk readiness、Expected/Actual 和 Failure；失败不得已经宣战、清 Offer、卸 Surface 或留下半套 Encounter。将这一条日志交回，不反复走路。

### Fixup Route A：试炼弱匪 Continuous presence、卸载回载与地面攻击

1. 正常 New Game／LevelTester `travel_mvp` 从荒村出发，打开大地图找到 Hex `(10,6)`、WorldPosition 约 `(17.32,9.00)` 的“试炼弱匪（自动必胜）”（正式军队 `army:formal_bandit_patrol_weak`，当前内容为 1 名 living member），先停在其 3×3 loaded neighborhood 外。该位置由荒村 `(3,7)` 加 `(Q+7,R-1)` 得到，明确位于 Main Surface、W2A geography 外。预期：大地图军队标记仍在原 Hex，地面不提前生成人物。
2. 关闭地图，沿正常 Continuous Outdoor 移动靠近标记真实位置。预期：进入 loaded neighborhood 后出现 1 名 `WeakBandit`；人数等于该 Army living member 数，位置围绕军队真实 WorldPosition，没有出生 Site 偏移、重复人物或第二个 View；大地图标记仍在。
   - 若本项失败，立即保持现场，不再反复走路：反引号 → `诊断` → `复制军队显示诊断（只读）`。若刚才在大地图选中过该 Army，按钮读取当前选择；否则默认读取试炼弱匪。粘贴一次完整剪贴板报告即可。
3. 走远到该军队 Chunk 离开 loaded neighborhood，再返回。预期：走远只清地面表现，Army／图标／成员关系不变；回来仍是同一 EntityId 的 1 人，无 clone，位置继续服从当前 `FormalArmy.WorldMotion`。
4. 在 WeakBandit 已显示且未开战时使用开发工具正式保存并读取。预期：读后 Army 位置、成员 EntityId、人数与 marker 不变；重新进入 loaded neighborhood 时同一角色显示。读档不能成为它第一次出现的必要条件。
5. 在地面对该 `WeakBandit` 使用现有敌对右键攻击。预期：沿 FormalArmy hostile routing 产生 BattleOffer；不降级为普通 LocalCombat，不创建第二支 Army或 clone participant。该 Offer 同时用于下方暂停验收。

### Fixup Route B：临时 UI 不污染 ManualPaused

分别从“世界正在运行”和“玩家先按 Space 手动暂停”两种初态，各完成一轮：

1. 选中开局主角，打开人物档案；再按 `J` 打开任务日志。每个窗口打开时 Effective Pause，关闭后运行轮恢复运行、手动暂停轮仍保持暂停。
2. 与荒村现有 NPC 对话；再靠近荒村可进入的真正 Interior/洞府入口，打开 LocalMap Enter Prompt 后取消。预期同上，取消不清玩家手动暂停；对话切入将老井字棋时全程无一帧主世界偷跑。
3. 按 `B` 打开背包，并打开建造面板各一次。预期作为已迁移回归，关闭任何一个窗口都只释放自己的 owner，不能解除另一个窗口或玩家手动暂停。

### Fixup Route C：CW-02 原失能流程无回归

继续使用反引号开发工具“战斗”页已有三个按钮：依次令当前 Active 弥留、令全队弥留、恢复首位弥留队员。预期仍按开局主角／同伴甲／同伴乙固定顺序接替；全队弥留显示暂时不可控且能合法完成战斗收尾；恢复后重新得到唯一 Active。不要把此路线当作完整势力继承验收。

### Fixup #2 Route D：W2A 外多成员军队

LevelTester `travel_mvp` 优先找“试炼强匪（自动伤亡）”（`army:formal_bandit_casualty_test`，3 名 authored member）；当前小图西北 fallback 位置为 Hex `(0,5)`、WorldPosition 约 `(0.00,7.50)`，在 W2A geography 外。先在大地图确认该档中的实际位置和 living member 数，再步行进入其 loaded neighborhood。预期：全部 living members 出现，采用稳定分散且不跨当前 composite blocker，每个 EntityId 只有一个 View。若该 Army 已因当前档进度移动、减员或不再满足条件，改找其他 W2A 外多成员 field Army；确实没有方便对象时记录本项未人工覆盖，不得为验收移动 Content 或创建测试军队。

### Route 1：手动暂停、接战与结束归属

1. 正常 New Game 进入荒村 Continuous Outdoor，用 Space 解除暂停，观察日期／NPC 日程会推进；再用 Space 手动暂停。
2. 按 `M` 打开大地图并关闭两次。预期：开关地图都不改变手动暂停；暂停图标保持原选择，关闭地图不让 WorldTick 偷跑。
3. 在大地图选择 PlayerParty，右键现有“试炼弱匪”军队并下达攻击；接近后出现 BattleOffer。分别做两轮：一轮进入前手动暂停，一轮进入前运行。
4. Offer 出现时等待数秒，预期日期、旅行、生产、NPC 日程不推进。选择“手动战斗”：运行轮应可战斗，手动暂停轮仍保持暂停且可用 Space 解除；阶段切换期间主世界均不推进。
   - 入场时 PlayerParty 应出现在当前战斗 LocalMap 的可走区域；Console 不再出现 `PlayerParty materialized placement still unsafe after repair: (625.00, 375.00, 0.00)`。
5. 清空敌方后点击右侧“结束战斗”。预期只结算一次、无二次奖励／重复残留，主世界冻结释放；玩家在开战前的手动暂停选择保持不变。另开一轮在 Offer 选择“撤退”，预期同样不污染手动暂停且不会留下冻结。

### Route 2：地图、面板与正式读档不污染暂停

1. 正常荒村中先设为运行，依次打开／关闭 `M` 大地图、`B` 背包和建造面板；预期面板打开时按其现有规则限制输入，关闭后恢复运行，但 `ManualPaused` 没被改写。
2. 再用 Space 设为手动暂停，重复上述开关；预期所有面板关闭后仍是手动暂停。
3. 按反引号打开“LevelTester 开发工具”→“存档”，保存当前档；分别在运行和手动暂停状态读取。预期读取期间不能操作旧 World，完成后只移除读档保护，保持读取时玩家当前手动选择；荒村 Surface、人物、CW-01 已毁物件状态与 WalkGrid 按恢复数据 hard rebind。
4. 在 BattleOffer 弹窗时保存并读取现有正式 Snapshot。预期读取后同一 PendingEngagement／BattleOffer 重新出现且 WorldTick 仍冻结；关闭／处理旧弹窗不会解除新 World 的有效冻结。

### Route 3：固定顺序接替、全员弥留与恢复

1. 正常 New Game 使用开局 PlayerParty（主角、同伴甲、同伴乙），记下左上小队栏顺序。按反引号→“战斗”→“CW-02：当前主控进入弥留”。预期 Active 从当前主控切到列表中第一名仍可控成员；再次点击时继续按原顺序跳过弥留者，不按战力重排，任意时刻只有一个 Active。
2. 读取一个三人均存活的档恢复夹具。攻击现有“试炼弱匪”，进入手动战斗后点击“CW-02：全队进入弥留”。预期 HUD 明示“全队暂时无法操作，但仍有生者”，角色移动／攻击不可用，且自动进入或能够完成既有 PostBattle；右侧“结束战斗”仍存在并可点击，退出后不残留战略冻结。
3. 在正常 Outdoor 状态再次点“全队进入弥留”，转到“存档”页保存并读取。预期读后成员顺序仍在、Active 为 None、状态仍是暂时不可控，而不是全队死亡或永久等待继承。
4. 回“战斗”页点击“CW-02：恢复首位弥留队员”。预期按既有恢复规则使第一名合格队员重新成为唯一 Active，恢复输入、选择和相机绑定；人物不重复、不换队列、不传送。再保存／读取一次，预期同样能绑定该 Active。

若 Route 3 使用当前战斗内容无法自然让三人同时弥留，使用上述 Development Cheat 按钮是本轮提供的最短触发方法。真正全队死亡只核对能进入 `AllMembersDead` 交接边界；“从玩家势力选择最强合格角色”和空势力终局不属于本轮验收。
