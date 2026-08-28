# 开发日志

> **倒序追加：最新的记录写在最上面。**
> 这是项目的历史记录，用于跨设备/跨时间恢复上下文，以及交接给他人时说明"为什么代码长这样"。
>
> 每次有实质进展就追加一条。宁可短，不可漏。

---

## 2026-08-28 — Phase 3 + Phase 4 文档封板收口

**做了什么**
- **Phase 3 = Accepted / Sealed**（用户正式确认；**非** Cursor 独立 Unity 人工验收）
  - 核心目标：FormalArmy 军事层；PlayerParty 独立旅行；Continuous WorldPosition／Travel／Presence／Save-Load／Authority 边界
  - 验证：LevelTester 持续使用 + Phase 4 开发／人工验收实际依赖
  - 原计划 166 F11 TEST 1–10／167 验收 1–12 逐条签字表未单独归档 → 不再阻塞
  - 真源：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md)／[167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)
- **Phase 4 = Accepted / Sealed**（LevelTester 人工验收通过）— 见同日较早条目
- 同步 [41-roadmap](41-roadmap.md)、[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)

**状态**
- Phase 0–4 **Accepted / Sealed** · Phase 5 **Not Started**

---

## 2026-08-28 — Phase 4 Accepted / Sealed（仅文档／Roadmap 收口）

**做了什么**
- 正式标记 **Phase 4 = Accepted / Sealed**；**未**扩功能、**未**启动 Phase 5
- 最终 Battle Authority 写入 [171](171-phase-4-battle-authority-2026-08-28.md) §1 为正式真源：
  - Trigger＝Initiator/Defender **共边相邻**（禁 WorldPosition 距离）
  - BattleArea＝Defender 当前 Hex（多 Hex Site＝全 Footprint）
  - SupportArea＝BattleArea ∪ 共边邻格；Participants／Manual 按 SupportArea + 交战方
- 记录 Hex topology Authority 修复（Odd-R↔axial；含 **CollectHexLine 已修**）；归类为 Hex 真源修复，非 Phase 4 特补丁
- **Deferred / Future Regression：** 敌军主动攻击 Retreat 人工验收；AI vs AI 主动接战人工验收（缺战略 AI，不阻塞封板）— 见 171 §8
- 附带体验：WorldMap 列表滚动收紧、Zoom In 扩大、Cheat Tools 与 F10 解耦（171 §6）
- 同步 [41-roadmap](41-roadmap.md)、[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)
- 唯一人工验收 Scene：`Assets/Scenes/LevelTester.unity`

**状态**
- Phase 4 **封板完成** · Phase 5 **Not Started**

---

## 2026-08-28 — Phase 4 Participant 来源追踪：PlayerHex Authority + 删除 Snapshot 旁路

**做了什么**
- **根因确认（LevelTester 实测）：** `BattleAreaHexes` / `SupportAreaHexes` **计算正确**；Player 误加入因 **PlayerHex Authority 与 WorldMap Marker 分裂**（Gathering 旧读 `WorldPresence`，Marker 读 `PlayerPartyTravel`）
- **Battle Trigger（A）：** 新增 `BattleEngagementTriggerService`；Initiator **已提交 Hex** ∈ Defender SupportArea；禁止 ContinuousWorldPosition 派生格提前接战
- **Player Hex Authority：** `BattleEngagementSpatialQuery` 优先 `PlayerPartyTravel`；idle 用 `WorldToHex(WorldPosition)` 与 Marker 对齐
- **Participant 旁路删除：** 移除 `ApplyLockedParticipantsToSnapshot` 内 **`seedMandatoryAttackers` 循环**（曾可在 `PlayerPartyIncluded` 时绕过 SupportArea 写 Snapshot）
- **逐成员 Gathering：** 每个 Party 成员独立 `MemberHex ∈ SupportArea`；Snapshot 写入二次校验
- **IncludedReason + 硬断言：** `BattleParticipantInclusionReason` / `BattleParticipantSpatialGuard`；Cheat Panel 输出判定链（Before/After Gathering/Snapshot）
- **WorldMap Debug：** `BattleEngagementWorldMapDebug` 橙框 BattleArea、蓝框 SupportArea
- **Tests：** Trigger 回归、Player 距 Defender 2 格、stale WorldPresence、`OfferPath_PlayerTwoHexFromDefender` 全路径集成测试
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) §4A–4B 根因与 IncludedReason
- **未**同步飞书；**未**人工验收；EditMode **待 Unity 跑通**（BatchMode 因 Editor 占用未执行）

**状态**
- Phase 4 Trigger + Gathering + Participant 追踪入仓 · push GitHub

---

## 2026-08-28 — Phase 4 SupportAreaHexes 集合规则（supersede 中心距离 ≤1）

**做了什么**
- **规则变更：** Participant Gathering 改用 **`BattleAreaHexes` + `SupportAreaHexes`** 冻结集合；资格判断为 `SupportAreaHexes.Contains(UnitHex)`
- **BattleArea：** 野外 = Defender 接战 Hex；多 Hex Site = 全 Footprint（非 Anchor 代替）
- **SupportArea：** BattleArea + 与其中任一 Hex 直接共边相邻的全部 Hex
- **Domain：** 新增 `BattleEngagementSupportArea`；重写 `BattleParticipantGatheringService` / `BattleEngagementAuthorityService` / Debug / Snapshot 持久化 SupportArea 列表
- **Bugfix：** `ApplyLockedParticipantsToSnapshot` 中 `seedMandatoryAttackers` 不再绕过 Gathering 空间规则
- **Tests：** T1–T10、LevelTester 回归、SeedMandatoryAttackers 快照测试
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) 更新；「BattleLocationHex 距离 ≤1」标记 superseded
- **未**同步飞书；**未**人工验收

**状态**
- Phase 4 SupportArea 规则入仓 · 待 EditMode 跑通

---

## 2026-08-28 — Phase 4 BattleLocationHex 规则修正（supersede Initiator 扫描）

**做了什么**
- **规则变更：** Participant Gathering 唯一空间 Authority 改回 **`BattleLocationHex`**（纯 Hex Graph Distance ≤1）
- **Initiator + Defender** 无条件加入；其他 Army 须同交战方 Faction + 距 BattleLocationHex ≤1 + 无 Battle Lock
- **PlayerParty** 强制加入条件改为距 BattleLocationHex ≤1（非 Initiator）
- **`InitiatorEngagementLocation`** 降为 Debug-only，不参与资格判断
- **Domain：** 重写 `BattleParticipantGatheringService`、`BattleEngagementHexDistance`（`HexDistanceToBattleLocation` / `ResolveBattleLocationHex`）
- **Tests：** `BattleAuthorityTests` T1–T9 按新规则重写（含 T6 防 Initiator 回归、T7 第三方、T9 不重新扫描）
- **Debug：** Cheat Panel 战斗页输出 BattleLocationHex / PlayerPartyHex / ManualEligible
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) 更新；旧 Initiator-centered 规则标记 superseded
- **未**同步飞书；**未**人工验收

**状态**
- Phase 4 规则修正入仓 · 待 EditMode 跑通

---

## 2026-08-28 — Phase 4 Battle Authority 入仓 + Initiator 扫描中心修正

**做了什么**
- **Phase 4 Domain：** `PendingEngagementRuntime`、`BattleEngagementAuthorityService`、`BattleParticipantGatheringService`、`BattleDecisionPolicy`、`BattleManualEntryPolicy`、`BattleRetreatService`
- **语义分离：** `BattleInitiator` vs `PlayerDecisionSubject`；Manual 仅 `PlayerPartyIncluded`；拒绝远程 FormalArmy Manual
- **Initiator 扫描中心修正：** 原 `GatherAndLock` 误用 `BattleLocation`（Defender Hex 优先）；改为 `InitiatorEngagementLocation` 唯一中心；`BattleLocation` 仅 Presentation / LocalMap 锚点
- **接线：** `BattleOfferService.ActivateOffer` → Authority；`HostStrategicInterruptPresenter` 按钮由 Policy 驱动；Snapshot Pending Engagement + Initiator 字段
- **EditMode：** `BattleAuthorityTests` T1–T9（含 T8 关键 Initiator≠接触点场景）
- **编译修复：** `CreateParty` 适配 `TryAddMember` 新签名
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md)
- **未**同步飞书；**未**人工验收；**未**跑全量 Unity Tests

**状态**
- Phase 4 **实现入仓 · 待 EditMode 跑通** — 产品指令：**暂停扩展与人工验收**
- **未做：** Legacy 战斗入口删除、Initiator=PlayerParty 路径、LevelTester 手操封板

---

## 2026-08-28 — WorldMap 选中真源、Attack Order Snapshot 与 Strategic UI 输入优先级

**做了什么**
- **Army Marker Load：** 玩家 FormalArmy 不再误走 ArmyStack Presentation；`ArmyWorldMapPresentation.ShouldDrawArmyStackMarker`；`ARMY_VIS07`
- **选中真源：** 新增 `HostWorldMapSelectionAuthority`；Marker / Army List / 右键 Dispatcher 统一读取；移除空 Id fallback PlayerParty
- **Attack Order Snapshot：** `AttackFormalArmy` + `OrderTargetArmyId` 持久化；`RestoreAttackOrderIfNeeded`；`FormalArmyOrderSnapshotTests`
- **Strategic UI Input Priority：** 修复 `HandleMapInput` 提前导致 Panel Block 未注册 + `e.Use()` 吞 Checkbox；`HostUiHitTest` 双通道；OnGUI 顺序收口
- 文档：[170](170-worldmap-selection-strategic-ui-input-2026-08-28.md)
- LevelTester CASE 1–6 **人工验收通过**

**状态**
- 已 commit + push；**未**同步飞书

---

## 2026-08-28 — Snapshot Faction / Test Entity 生命周期审计

**做了什么**
- 审计 LevelTester Save→Load：**主角团变 None** + **测试山贼消失** 为同一 Snapshot 链路类问题，非 Faction Registry 丢失
- 主角团：`base:faction_player` 为 Catalog ID（无 Faction JSON）；根因 `JsonSnapshotSerializer` 漏写 entity `factionId`
- 山贼：Snapshot **含** entity 19–26；根因 Restore 顺序（Membership 晚于 FormalArmy Apply）+ **ArmyStack 未从 FormalArmy 重建**
- 统一修法：`StrategicSnapshotHelper.FinalizeRuntimeLinks` + `ArmyStackAdapter.EnsurePresentationStacksFromFormalArmies` + Rehydration 后二次 Finalize
- 文档：[169](169-snapshot-faction-test-entity-lifecycle-audit-2026-08-28.md)
- **未** commit 代码改动（文档先行）；**未**人工验收封板

**状态**
- 待 LevelTester TEST 1–2 重验 + 新 Save 含 entity `factionId`

---

## 2026-08-27 — Phase 3 收口：A2 Authority 重验、PP-Follower、试炼三军与伤亡夹具

**做了什么**
- **A2 Authority 第二轮：** 禁止 FormalArmy 静默 `RemoveFromPlayerParty`；PlayerParty 成员须先 Leave Party 再组军；`ArmyAuthorityRules.TryValidateNotPlayerPartyMember`；`FormalArmyPhase3AuthorityTests` 扩充
- **G16–G18：** Moving Army `SyncNonLivingMembers`；F11 Debug Incap/Sync Casualties/Presence 显示
- **PP-Follower：** `PlayerPartyTransitionMembership` — Follower 跨 LocalMap/Hex 与 Active 同步；`PlayerPartyFollowerLocalMapTransitionTests`
- **主角营地：** 独立 `base:map_player_camp` + `player_camp_places.json`；不再复用荒村 LocalMap
- **试炼三军：** 荒村山匪 / 试炼弱匪（必胜）/ 试炼强匪（自动伤亡）；travel_mvp 小图西北放置出界修复
- **伤亡夹具：** 试炼强匪自动战必胜 + **必定 1 人弥留或阵亡**；`AutoBattleCasualtyFixtureTests`
- 文档：[167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)；166/roadmap 更新
- **未**人工验收封板；**未**跑全量 Unity Tests

**状态**
- Phase 3 **收口入仓** — 待 LevelTester 167 验收清单 + 166 F11 TEST 1–10
- **未做：** Phase 4 Battle Authority、Legacy 战斗入口删除

---

## 2026-08-27 — Phase 3 FormalArmy Continuous World 实现入仓（待验收）

**做了什么**
- **3A Authority：** `ArmyAuthorityRules`；Active 禁入军；Follower 入军原子退出 Party；Background Travel Cancel
- **3B–C World Motion + Travel：** `FormalArmyWorldMotion`／`FormalArmyContinuousTravelService`；Site Departure；Footprint → `AtWorldSite` canonicalize；`ArmyHexTravelService` 委托连续推进
- **3D Presence：** `FormalArmyMemberPresenceSync` 成员 Presence 从 Army 派生
- **3E–G ArmyService：** 仅 friendly Site 组军；Wilderness 禁 Disband；Snapshot Phase 3 字段 + `FormalArmySnapshotRestore`
- **PresenceHex 收口：** `EnsurePresenceHexValid()` 强制 `PresenceHex == AnchorHex`；Loader／Editor／Validation；30×15 测试 Content 调整
- **Host：** F11 `HostFormalArmyDebugPanel`（`PlayableHostBootstrap` 挂载）
- **EditMode：** `FormalArmyPhase3AuthorityTests`
- **编译修复：** Snapshot `armyMotion` 重名；`TryGetActiveSegmentWorld` 签名；DebugPanel `using`
- 文档：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md)；roadmap／163 更新
- **未**同步飞书；**未**人工验收封板

**状态**
- Phase 3 **未封板** — 待 LevelTester F11 人工 TEST 1–10 + EditMode 全绿
- **未做：** Battle Authority（Phase 4）、FormalArmy WorldMap Marker、Autonomous AI

---

## 2026-08-27 — Phase 2D Background Character World Travel 人工验收封板

**做了什么**
- **2D-A～D**（延续 720f585）：Background Simulation Scheduler、Travel Core、Save/Load、F12 Debug
- **2D-E Loaded LocalMap Materialization**：`LoadedDestinationArrivalMaterializer`（Initial Load + Runtime Arrival 共用）；Wilderness Hex Notify；Site Ingress `BackgroundTravelArrivalContext`；Belonging Query + Explain
- **2D-F Site Departure 语义**：禁止 BeginTravel 同步 instant arrival；`BeginSiteDepartureTravel` 保持 `AtWorldSite` 直至跨过 Boundary；真实 FootprintCenter→BoundaryEntry Travel
- **2D-G Destination Canonicalization**：Travel To Hex 命中 Footprint → `AtWorldSite(siteId)`；Travel To Site 真源 `WorldSiteId`；AnchorHex 仅 Presentation；Character 投影 PresenceHex
- **Bootstrap 修复**：同伴开局 `AtWorldSite(荒村)` + PresenceHex；`CaptureTravelingMembers` 仅主控
- **Development Trace**：`BGTRAVEL TRACE #N`（Core TraceSink + Host 注入）
- EditMode：`BackgroundCharacterWorldTravelPhase2DTests`／`BackgroundLoadedDestinationArrivalTests`／`BackgroundWildernessLocalMapMaterializationTests`
- 文档：[165](165-phase-2d-background-character-world-travel-2026-08-26.md) 封板更新；roadmap 2D 标记完成
- **未**同步飞书

**状态**
- Phase 2D **封板**；Deferred：Autonomous AI Travel、Background Combat UX、FormalArmy Continuous Marker

---

## 2026-08-26 — Phase 2C Continuous Player World Movement 人工验收封板

**做了什么**
- Continuous Player World Movement：WorldPosition 真源、LocalMap↔HexWorld 双向投影、Edge Transition
- **Ordinary Hex Actual Connections**：合法 Neighbor 数 = Exit Connection 数；真实世界方向 → LocalMap 周界投影；Overlap Resolution
- **WorldSite Full-Footprint Boundary Connections**：全 Footprint 外围唯一 Outside Hex；非固定 6 出口；FootprintWorldCenter 方向投影；ExitZone→Connection→DestinationHex Transition
- Edge Ping-Pong Guard、Canonical Exit Trigger Geometry、Host Presentation／Debug
- EditMode：`SurfaceExitConnectionTests`／`SurfaceExitZoneOverlapTests`／`WorldSiteFootprintExitConnectionTests`／`PlayerPartyContinuousWorldPhase2CTests`
- 文档：164／roadmap／2K §5.8.7 状态更新
- **未**同步飞书；**未 push**

**状态**
- Phase 2C **封板**；下一目标 Background Travel／Directional Site Entry Spawn 等 Deferred 项

---

## 2026-08-26 — Phase 2C Surface Exit Zone／Edge Transition 入仓

**做了什么**
- Surface LocalMap Edge Transition（Site Exit／Wilderness 跨 Hex；不 snap 邻格中心）
- Ping-Pong Guard：`PlayerPartySurfaceEdgeGate`（TransitionInProgress／Disarm／Rearm）；不改 Zone Geometry
- **Canonical Exit Trigger Geometry**：`ExitTriggerDepth` + PlayableBounds；Geometry∥Availability；Detection 与 Presentation 共用
- MapLayout 字段 `exitTriggerDepth`；Host `HostSurfaceExitZonePresenter` 半透明精确覆盖
- 文档：2K §5.8.7 Exit Trigger；[164](164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)；glossary／roadmap／163
- **未**同步飞书

**状态**
- Phase 2C 竖切入仓；已由后续封板 commit 取代

---

## 2026-08-26 — Phase 2B 人工验收封板

**做了什么**
- 制作人确认 Phase 2B 手操通过：PlayerParty Travel／Wilderness／Materialize／Background 留下
- EditMode 回归 PASS（含 Travel／Presence／Party／MultiHex／Snapshot）；补测具 `HexWorld.MapId` 与 MH05 Anchor 断言
- 代码真源：`c895d3d`
- 本条封板 commit：**不 push**；不混入 Phase 2C

**状态**
- Phase 2B **封板**；进入 Phase 2C（Continuous World Movement／双向投影／Edge Transition）

---

## 2026-08-25 — Phase 2B PlayerParty World Travel MVP

**做了什么**
- **独立测试世界** ase:hex_world_travel_mvp_30x15（30×15／6 Site）；正式 ase:hex_world_ch01 保留；开局 scenario 指向测试世界
- **PlayerParty Hex Travel**（非 Fake Army）：PlayerPartyWorldMotion／PlayerPartyHexTravelService；共用 Hex A*（HexTravelMode.Ground）与 per-Hex tick；可取消停当前格
- **WorldMap**：Active 头像 Marker；无军团时右键 Party Travel；路径预览；顶栏停止旅行
- **Wilderness Fallback**：Terrain→复用 LocalMap 模板（Plain→map_site_a／Forest→map_site_linjian／Mountain→map_site_kuangshan）
- **Materialize 闭环**：PlayerPartyLocalMapMaterializationService + ExpandLocalMapForCurrentPartyWorld；修复 AtHex 在非遭遇 LocalMap 被隐藏导致「进近景无 Party」
- **UX 注记**：「进入近景(调试)」仅为 Phase 2B Prototype；正式方向为关 WorldMap 自动 Expand
- **测试**：PlayerPartyWorldTravelPhase2BTests（含 Materialize／Background 不跟随）
- **未做**：Continuous LocalMap 边跨 Hex、Close→自动 Expand、Background Travel／Combat、正式 Wilderness 生成

**状态**
- Phase 2B 代码落地并 push；手操继续验证 Travel／Wilderness Materialize

---

## 2026-08-25 — Phase 2A 人工验收封板

**做了什么**
- 制作人确认 Phase 2A 手操通过：PresenceHex／Character World Presence／青石荒村 Multi-Hex／Background 不画 WorldMap 头像
- 代码真源：`18600af`（feat）／`8d49bf4`（docs note）
- 本条仅文档封板；**不 push**；不混入 Phase 2B

**状态**
- Phase 2A **封板**；下一阶段 Phase 2B（PlayerParty World Travel MVP + 30×15 测试世界 + Wilderness Fallback）

---

## 2026-08-25 — Phase 2A PresenceHex + Character World Presence（代码落地）

**做了什么**
- **PresenceHex**：Content `presenceQ/R`、Loader 缺省→Anchor 确定性迁移、Runtime `WorldSite.PresenceHex`、Validator／WorldGraphEditor 查看与设置（≠ Anchor 允许）
- **Character World Presence 查询**：`CharacterWorldPresenceQuery`（AtSite→Site.PresenceHex；Army 成员→Army.CurrentHex；AtHex 预留）
- **真源纪律**：AtSite 存 SiteId，不另存可漂移 Hex；Stop Follow 保留 Site Presence；Background 不画 WorldMap 个人头像
- **Snapshot**：可选 `characterWorldPresences`（v6 向后兼容）
- **验收 Content**：青石荒村 `base:site_huangcun` Footprint 扩为 4 格；Anchor `(80,52)`；Presence `(81,52)`
- **Camera 最终规则**（同日）：仅 WASD Hard Follow；RTS 不控镜头 — 见上条／[2K §1.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)
- **未做：** Background Travel／Combat／Continuous Exit／Wilderness／Party WorldMap Avatar／FormalArmy 重构
- **已知缺口：** Prototype **无** WorldSite LocalMap→卸载近景的正式 Exit；开「地图」仅叠 UI

**状态**
- 代码待制作人手操 Presence／Camera；本轮按指示 push GitHub；**不同步飞书**

---

## 2026-08-25 — LocalMap Camera 最终规则（仅 WASD Follow）

**做了什么**
- **Supersede**「RTS 默认 Follow／中键取消 Follow」
- 最终：仅 WASD → Snap＋Hard Follow；RTS／右键寻路完全不控镜头；切换 Active 一次性 Snap
- 真源写入 [2K §1.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、ADR-0026 补钉；Host 实现已对齐

**状态**
- 规则已定；待制作人 CAMERA-A～F 手操签收

---

## 2026-08-25 — Phase 1 PlayerParty 封板

**做了什么**
- 人工验收确认：Single Active／Party≤6／Follow／Stop Follow／Active Switch／Followers Move+Combat／Group Work／View≠Command／非 Active 右键不 fallback／Route Preview 仅 Active／**Camera：仅 WASD Hard Follow（RTS 完全不控镜头）**／Dying·Dead 不可 Active／FormalArmy 与 Party 互斥
- 代码已在 `aa1ebb9`／`e683aab`／`8770fb0`；本条仅文档封板，不 push

**状态**
- Phase 1 **封板**；进入 Phase 2A（PresenceHex + Character World Presence 真源）

---

## 2026-08-25 — RPG-First 架构真源（仅文档）

**做了什么**
- **新真源 [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)**：ActiveControlledCharacter / PlayerParty(≤6) / PresenceHex / 连续 HexWorld / FormalArmy 军事层 / Character Policy V1 / Succession
- **[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)**：从「多人 RTS + 移动必 Army」迁到 RPG-first
- **[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)**：代码冲突审计 + Phase 0–8 迁移计划（**未实现代码**）
- Supersede：2A 铁则 4、ADR-0024 部分、Glossary／Roadmap／Overview／AGENTS；RTS 过程文档加 Legacy 注记
- **禁止本轮：** 任何 C#／Prefab／Content／Snapshot／Editor 改动

**状态**
- 文档 Phase 0 **完成**；等待制作人审核后再开 Phase 1
- 飞书 **NOT SYNCED**

---

## 2026-08-24 — Hex WorldSite 准入/人口、Ch01 全 Site LocalMap、Manual Battle 可见性

**做了什么**
- **Hex 敌军 Pure 真源：** `HexActiveEnemyArmyQuery` 禁止 Node fallback；修复山匪 hex 误判荒村
- **WorldSite 进入：** `StrategicWorldSiteAccessService`、`WorldTravelService.EnterWorldSiteScene`、`HostWorldMapPanel` 右键菜单
- **Site 人口：** `StrategicWorldSitePopulationService`；`LocalMapVisibility` / `PlayableHostBootstrap` 不再用 FocusArmy 作白名单
- **Ch01 内容：** 28 Site 独立 map/places JSON；`ch01_hex_world.json` 全量 `localMapId`
- **Manual Battle：** `MarkPartyInEncounter` 主路径补全；遭遇图 `IsEngaged` 可见；Battle 前 `ClearSiteFocus`
- EditMode：SITE-ENTER、Population、Ch01 Mapping、ENCOUNTER_ASSEMBLY_03
- 收束文档：[160-hex-worldsite-localmap-and-manual-battle-fix-2026-08-24.md](160-hex-worldsite-localmap-and-manual-battle-fix-2026-08-24.md)（**未同步飞书**）

**验证**
- 制作人手操：Site 进入、人口、Manual Battle Members — **本轮口头 OK（几乎没有问题）**
- Unity EditMode 全套：**需关 Editor 后 batch**

**状态**
- 战略 WorldSite / Content / Manual Battle **COMMITTED + PUSHED**
- 飞书 **NOT SYNCED**

---

## 2026-08-24 — Encounter 作用域 Lingering + WeakBandit 参战名单 + 大地图路线预览

**做了什么**
- **多战场隔离：** `LingeringBattlefieldRegistry`、`BattlefieldSpawnScope`、冻结 Participant 的 Lingering 再进；MULTI-ENCOUNTER / LINGERING_PARTICIPANT EditMode 测试
- **WeakBandit 参战名单：** `EncounterAssemblyTrace`、Snapshot 统一刷怪、Hex 支援 1 格；`LocalMapVisibility` 遭遇图 Participant 过滤 + 荒村误判修复
- **大地图 UX：** `HostWorldMapPanel` 路线预览仅绑定当前选中我方 Moving Army（Presentation only）
- **Hex 右键 / 残留查询：** `HexRightClickResolver`、`StrategicResidualPresentationQuery` 等；多组 EditMode 测试
- **内容与 Editor：** `ch01_hex_world.json`、WorldGraphEditor / Shared.Tests 增量
- 收束文档：[159-encounter-scoped-lingering-and-worldmap-path-preview-2026-08-24.md](159-encounter-scoped-lingering-and-worldmap-path-preview-2026-08-24.md)（**未同步飞书**）

**验证**
- 制作人手操：荒村启动、路线预览、取消选择、Residual/Lingering — **本轮口头 OK**
- Unity EditMode 全套：**需关 Editor 后 batch**

**状态**
- 战略 Encounter / Lingering / WorldMap Path Preview **COMMITTED（本地）**
- 飞书 **NOT SYNCED**

---

## 2026-08-23 — Hex Battlefield Residual 聚合重构（PURE DERIVED）

**做了什么**
- 废弃 WorldMap 每 Character 散落头像 / 匿名 ArmyStack remnant 正式表现
- Residual 位置收口：`WorldAgentPresence.AtHex` + `HexCoord`；`StrategicResidualPresenceService`
- Presentation：`StrategicResidualPresentationQuery`（Hex × DynamicRelation × DEAD/DOWNED）
- Host：`DrawResidualMarkers`、左键详情、右键穿透；Active Army 优先
- Snapshot schema **v3 → v4**：`ResidualCharacterPresenceDto`（仅 CharacterId+Hex）
- EditMode：`ResidualGroupingPresentationTests`（ARMY/GROUP/REL/LIFE/SAVE）

**验证**
- EditMode Residual / Downed Vis：**需 Unity EditMode Runner**
- Manual Acceptance A–F：**NOT RUN**（制作人手操）

**状态**
- Domain + Host + Snapshot **IMPLEMENTED** · Manual Acceptance **PENDING**

---

## 2026-08-23 — WorldGraphEditor Performance Pass（200×100）

**做了什么**
- 审计：旧实现每次 Refresh 清空 Canvas + 最多 2 万 `Polygon`（per-Hex immediate）
- 改为 **16×16 Chunk DrawingVisual** 世界坐标缓存 + viewport `MatrixTransform`；visible chunk culling；dirty chunk rebuild
- Pan/Zoom/Hover **不** rebuild terrain geometry；Brush Stroke 单次 Undo；Validate 不再跟绘制绑定
- 状态栏 perf 指标；`Shared.Tests` WGE-PERF-01~08
- 文档：更新 [158](158-hex-world-content-authoring-pipeline-2026-08-23.md) Editor Performance Architecture

**验证**
- `dotnet test Shared.Tests`：11 passed
- WorldGraphEditor 手操 Idle/Pan/Zoom/Brush：**NOT RUN**（需制作人本地打开）

**状态**
- Editor Performance Pass **IMPLEMENTED · MANUAL ACCEPTANCE PENDING**

---

## 2026-08-23 — 158 Hex World Content Pipeline + WorldGraphEditor WYSIWYG

**做了什么**
- **HexWorld JSON Pipeline：** `Content/BaseGame/Data/Worlds/ch01_hex_world.json`；`HexWorldContentLoader`；Scenario `openingHexWorldId`
- **WorldGraphEditor 彻底 Hex 化：** 删除 Node/Route 编辑；Terrain/Road/Site WYSIWYG + Save JSON
- **WYSIWYG Layout 修复：** `HexWorldLayoutShared` 对齐 Runtime `HexWorldLayout`（Odd-R parity bug）
- **校验：** Road 连通性；Content schema `openingHexWorldId` / `hexWorld` reference
- 文档：[158-hex-world-content-authoring-pipeline-2026-08-23.md](158-hex-world-content-authoring-pipeline-2026-08-23.md)；更新 ADR-0025 / 155 / roadmap

**验证**
- EditMode：`HexWorldWysiwygLayoutTests`（5000 roundtrip）、`HexWorldContentPipelineTests`
- Unity PlayMode + Editor 手操：**DEFERRED**

**状态**
- Hex World Content Authoring **IMPLEMENTED · UNITY MANUAL VERIFICATION DEFERRED**

---

## 2026-08-23 — 154 Formal Army RTS 收束文档 + 追击暂缓

**做了什么**
- 收束文档：[154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)
- 记录 `f6eb844` 已交付：Formal Army 移动/攻击、青色路径预览、残留战场不双倍、角色名单弥留/尸体
- **追击（尤其追移动敌军）仍有问题** — 制作人决定暂缓，§3 记录静态分析与下轮修复方向
- 更新 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md) 已知问题、[62](62-project-status-2026-08-01.md)、[41-roadmap](41-roadmap.md)

**验证**
- 无新代码提交；文档收束 only

**状态**
- Formal Army RTS **PARTIAL ACCEPTANCE**（移动/攻击/预览/残留/名单 OK）
- 追击追移动敌 **KNOWN ISSUES · DEFERRED**

---

## 2026-08-22 — Strategic Host 双入口（角色／军队 · 移除 Node 组军）

**做了什么**
- 大地图工具栏并列 **「角色」** / **「军队」** 全局一级入口（不依赖 Node）
- **正式删除** Node 菜单「军团管理／节点组军」及 `OpenForNode` 玩家路径
- `HostArmyFormPanel` 仅作为军队列表 embedded 的 Detail / Creation UI
- 地图 Army portrait：单击 → 打开军队列表 + Detail；双击 → 镜头定位
- 角色列表：左侧多选未编组 + 底部「组建军队」；军队列表 empty state + 「组建军队」
- 验收清单修订：[153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)

**验证**
- EditMode：`HostStrategicRosterQueriesTests` — **STATIC REVIEW PASSED · UNITY VERIFICATION DEFERRED**

**状态**
- Host 双入口 **IMPLEMENTED · STATIC REVIEW PASSED · UNITY VERIFICATION DEFERRED**

---

## 2026-08-22 — Strategic Host 双入口（角色／军队列表 · 全战式）

**做了什么**
- 大地图工具栏并列 **「角色」** / **「军队」** 按钮；无军队时军队按钮 **不灰掉**，列表显示 empty state
- `HostStrategicCharacterListPanel`：全局角色列表；单击详情；双击跳 Army／Node；多选「组建军队 [ACCEPTANCE]」
- `HostStrategicArmyListPanel`：全局军队列表；单击详情；双击镜头定位；「新建军队」embedded 创建
- `HostStrategicRosterQueries`：只读角色／军队列表数据 + 战力摘要
- `HostArmyFormPanel`：`OpenGlobalDetail` / `OpenGlobalCreate` embedded 模式；仍只走 `ArmyUiCommands`
- 节点菜单「军团管理」→ **「节点组军（次级）」**；失败原因改 status 提示，不再整按钮灰掉
- 验收清单：[153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)

**验证**
- EditMode：`HostStrategicRosterQueriesTests` — **STATIC REVIEW PASSED · UNITY VERIFICATION DEFERRED**
- Host 手操 153 勾选表 — **DEFERRED**

**状态**
- A–K **IMPLEMENTED · FINAL STATIC CLOSURE PASSED · MANUAL ACCEPTANCE UI IMPLEMENTED**
- Host 双入口 **IMPLEMENTED · STATIC REVIEW PASSED · UNITY VERIFICATION DEFERRED**

---

## 2026-08-22 — Strategic Manual Acceptance UI（Unity 验证 DEFERRED）

**做了什么**
- 统一 Host 开发验收面板：`HostStrategicAcceptancePanel`（F8／大地图「战略验收」）；标注 DEVELOPMENT / ACCEPTANCE UI
- War / Alliance / Vassalage / Tribute hook / Node Owner / Retreating / Landless / Snapshot v2 最小可见与手操
- `HostArmyFormPanel` 补 AddMember / RemoveMember / ChangeLeader
- 战后 Aftermath 面板（Captured / Escaped / RetreatingArmy）；`ResolveLifeStateLabel` 显示「被俘」
- Core：`StrategicAcceptanceCommands` + `StrategicAcceptanceInspector`（薄 wrapper，不写 Board）

**验证**
- EditMode：`StrategicAcceptanceTests` — **PENDING — UNITY VERIFICATION DEFERRED**

**状态**
- A–K **IMPLEMENTED · FINAL STATIC CLOSURE PASSED · MANUAL ACCEPTANCE UI IMPLEMENTED · UNITY VERIFICATION DEFERRED**

---

## 2026-08-22 — Strategic Layer Final Closure（Unity 验证 DEFERRED）

**做了什么**
- Legacy anonymous ArmyStack：`StrategicDayHandler` → `EnsureBanditScoutArmy`（FormalArmy + 4 真实 Scout Character）
- 玩家 Character 战略入口：`WorldTravelPathService` + Host 全面拦截；仅 Formal Army 移动/追击
- Ch01 外交污染隔离：`Ch01ScenarioStrategicSetup` / `Ch01ScenarioProgressionHooks`；Generic Bootstrap 不再决定剧情 War
- Snapshot：**v1 explicit reject**；v2 required + strategic state mandatory
- 文档：2A §44 Ch01 Scenario 边界；152 §12 Final Closure

**验证**
- EditMode：`StrategicFinalClosureTests` — **PENDING — UNITY VERIFICATION DEFERRED**

**状态**
- A–K **IMPLEMENTED · FINAL STATIC CLOSURE PASSED · UNITY VERIFICATION DEFERRED**

---

## 2026-08-22 — Phase E–K 战略层 E–K（Unity 验证 DEFERRED）

**做了什么**
- **E** BattleOffer AttackerArmyId/DefenderArmyId；Army vs Army 追击 Adapter；BattleParticipantSnapshot 成员 ID  
- **F** AutoBattle 真实 Character 伤亡；ArmyStackAdapter 派生 downed 统计  
- **G** WarBoard/WarGateService DeclareWar/IsAtWar/CanAttack；Host/BattleOffer 军事门槛  
- **H** CaptureObjective + Node Owner 易主；ControlCore 接入；ArmyFormationNodePolicy 移除 presence 通用路径 → Ch01ScenarioArmyFormationPolicy  
- **I** Alliance/Vassalage/Tribute 占位  
- **J** Captured/Escaped/RetreatingArmy/Landless hook  
- **K** WorldSnapshot Schema v2 + StrategicSnapshotHelper + JsonSnapshotSerializer 战略字段  

**验证**
- EditMode：ArmyPhaseE–KTests 已编写 — **PENDING — UNITY VERIFICATION DEFERRED**  

**下一步**
- Unity Test Runner 全量回归（含 StrategicPhaseTests + 153 链）  

---

## 2026-08-22 — Phase B 最小组军 UI + WorldMap Army 投影（Unity 验证 DEFERRED）

**做了什么**
- HostArmyFormPanel + 节点菜单「军团管理」；ArmyUiCommands 薄层  
- ArmyWorldMapPresentation：FormalArmy @ NodeId + Leader 派生头像；AtNode 角色不重复正式显示  
- ArmyFormationNodePolicy：Ch01 无 Owner 时 presence-based 己方 Node  
- ArmyService：AddMember / RemoveMember / ChangeLeader / CollectResidentsAtNode  
- ArmyPhaseBTests（8 条）+ 152/roadmap 状态更新  

**验证**
- Unity Test Runner / Host：**DEFERRED**（制作人暂缓）  

**下一步**
- 恢复 Unity 后补跑 ArmyDomainTests + ArmyPhaseBTests + StrategicPhaseTests + Host 手操  
- 等待 **Phase C** 批准  

---

## 2026-08-22 — Phase A Formal Army Domain（Unity 验证 DEFERRED）

**做了什么**
- FormalArmy / ArmyService / ArmyMembership / ArmyDomainTests（11 条）  
- StrategicBootstrap Owner 保护；静态复核修复（单真源 / ForceDisband / AtNode-only）  

**验证**
- Unity：**DEFERRED**  

---

## 2026-08-22 — 152 审核后小修（仅文档，未编码）

**做了什么**
- 152 rev.2：War(G) 先于 Capture(H)；Legacy Character Travel B–D 过渡 + Phase D 正式退出；Phase A 收紧（自动化为主、Host 仅回归、禁 Army Debug UI）  
- roadmap／overview 状态同步：**Phase A 编码仍未批准**

**未做**
- 任何代码；Phase A 未开始

**下一步**
- 等待制作人 **「正式批准 Phase A 开工」**

---

## 2026-08-22 — 152 战略 Faction / Formal Army / Capture 实现分期计划（仅文档）

**做了什么**
- 只读代码审计结论 + 制作人拍板迁移方向 → 正式实现分期 [152](152-strategic-faction-army-capture-implementation-plan-2026-08-22.md)  
- 路线：Formal Army Domain + Compatibility Adapter（非 WorldPresence 大爆炸）；A–K 可停点；双真源退出表；第一刀推荐 **Phase A**（Domain + Membership，无移动／无战斗改动）  
- 最小更新 roadmap／overview 索引

**未做**
- 任何代码／Scene／测试／Snapshot 改动；**Phase A 未开始**

**下一步**
- 制作人审核 152 → 明确批准 **Phase A** 后方可编码

---

## 2026-08-22 — 153 弥留残留收束 + 自动战宏观头像 + 接战／追击修复（已编码）

**做了什么**
- 自动战胜后 `EnsureMacroRemnantSpawns`：宏观立刻刷弥留／尸体个体 + `WorldPresence`；隐藏聚合 `ArmyStack` 标记  
- 战损语义：`IncapacitatedMemberCount`／`CorpseMemberCount`；处决留尸体不 `Armies.Remove`  
- 接战强制名单：`CollectViewParty(mandatoryLiving)` 仅行动决定人 + 半径内弥留／尸体；探望记录派出名单  
- 接战窗撤退：`ClearPursuitForAgents`；自动战胜 `ClearPursuitForEngagedKeepEnRoute`  
- 再进 LocalMap 倒计时 preservation；`EstimateAutoWinPercent` 调整；EditMode 回归测例  
- 过程文档 [153](153-lingering-remnant-macro-presentation-2026-08-22.md)；GitHub／飞书同步

**未做**
- 2A 正式 Faction／Diplomacy／War／Capture 代码（仍为 Prototype `ArmyStack`）  
- 手操签收 153 清单

**下一步**
- 手操验 153 → 制作人批准 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 实现分期  
- 见 153 §6 与 devlog 外交条目

---

## 2026-08-22 — 战略势力层规则第二轮补充（仅文档，未编码）

**做了什么**
- 制作人拍板 9 条补充规则，写入 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) §1.4／§6／§10／§19.1／§20／§37
- 同步 [ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)、glossary（FactionId／ArmyMembership／GarrisonedArmy／RetreatingArmy 等）、`24`／`26`／`28` 注记

**关键规则**
- Army 编组仅能在己方 Node；禁止跨 Faction 混编；驻扎不自动解散
- 全系统统一 FactionId；无战后系统保护期；独立 Faction 最多 1 个 Alliance + 成员战争绑定
- Capture 全部完成后可「结束战斗」；残余守军 Captured／Escaped → RetreatingArmy

**未做**
- 任何代码改动；概率公式；RetreatingArmy AI

**飞书**
- 已 provision + 同步本轮战略文档（2A、ADR-0024、glossary、24/26/28、roadmap、devlog 等）；全量同步 28 篇历史文档因飞书权限 forBidden 失败（与本轮无关，需手动分享应用编辑权）

---

## 2026-08-22 — 战略势力层设计文档同步（仅文档，未编码）

**做了什么**
- 制作人与设计讨论完成战略 Faction / Army / Diplomacy / Vassalage / Alliance / War / Capture 基础框架
- 新增 [2A 势力、军队、外交与战略占领](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 系统设计真源
- 新增 [ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)：修士 = 持久 Character + Army 载体；部分 supersede ADR-0008
- 修订 `27`／`34`／`26`／`24`／`28`／`33` 注记；`113`／`138`／`139`／`140` 增加 Prototype vs target-model 区分
- 更新 glossary／roadmap／reading-guide／overview

**未做**
- 任何代码、JSON、Scene、测试改动
- 外交 UI、占点、Army 编组、AI、Snapshot 实现

**下一步**
- 制作人审核文档 → 明确「可以开工」后再进入实现

---

**做了什么**
- `base:map_world_node_stub` 扩为 150×80 空场；去掉歇脚树装饰  
- [151](151-encounter-stub-map-150x80-2026-08-21.md)

---

## 2026-08-21 — 150 批 3：残留再进走接战 Offer

**做了什么**
- `TryBuildOfferForLingeringBattlefield`；弥留菜单／残留栈再攻统一弹 Offer  
- 残留 Offer 使用 `LingeringLocalMapId`；补 `LingeringBattlefieldPartyService.cs.meta`  
- EditMode 测例；[150](150-lingering-battlefield-batch3-offer-2026-08-21.md)  
- 手操验收跳过（Unity 占用／环境未就绪）  
- 飞书／GitHub 同步

---

## 2026-08-21 — 149 批 2：Core 下沉 + 探望到站

**做了什么**
- 新增 `LingeringBattlefieldPartyService`；`EnterLingeringBattlefield` Core 校验  
- `PendingLingeringVisitIncapId` + 到站自动开进入菜单  
- [149](149-lingering-battlefield-batch2-2026-08-21.md)

---

## 2026-08-21 — 148 收束：批 1 + 接战一致性 + 删 JoinOngoing

**做了什么**
- `HostWorldMapPanel` 批 1；`ExecuteAttackStack` 仅 Pursuit 到站弹接战  
- `BattleOfferService.TryPromoteNextQueuedOffer` 人未到只追击  
- 删除 `JoinEngagedMembers`／JoinOngoing UI；测例更新  
- 收束 [148](148-worldmap-linger-incap-ux-2026-08-21.md)；飞书同步；提交（手操跳过）  
- 下一步：[149 批 2](149-lingering-battlefield-batch2-2026-08-21.md)

---

## 2026-08-21 — 148 大地图弥留交互与点击修补（待手操验）

**做了什么**
- `HostWorldMapPanel`：弥留左／右键分工、`CollectLingeringViewParty`、敌军吸附与命中优先级  
- 新建 [148](148-worldmap-linger-incap-ux-2026-08-21.md)；飞书 provision＋同步  

---

## 2026-08-21 — 收束 147＋飞书／GitHub（接战点／弥留残留）

**做了什么**
- 扩写 [147](147-battlefield-linger-no-teleport-2026-08-21.md) 收束全文（对齐 `eece220`）  
- 更新总览／通读／62／路线图／feishu-map；飞书 provision＋同步；推 GitHub  

---

## 2026-08-21 — 接战点无瞬移＋弥留残留战场

**做了什么**
- 战后参战者一律落 BattleAnchor，禁止瞬移回家  
- 有弥留则保留 Encounter；大地图可再攻击／查看进入  
- 未处决自动战＝全员弥留；再进刷弥留怪；修换路瞬移／再进跳荒村  
- 大地图支援半径滑块（默认 0.25）；ADR-0023 补丁：结束战斗≠销毁战场  
- 提交推送 `eece220`

---

## 2026-08-21 — ADR-0023／146 手操签收

**做了什么**
- 制作人手操确认 145／146 清单通过；文档状态改为已签收  
- 下一刀待选：占点／外交，或 Snapshot 纳入 Strategic／冻结态

---

## 2026-08-21 — 收束 146＋飞书／GitHub（ADR-0023 Host 打磨）

**做了什么**
- 新建 [146](146-adr0023-host-ux-polish-2026-08-21.md)：支援世界坐标半径、手动非强制结束、自动结算弹窗、山匪可见性、出队修复、CS0128
- 修订 [145](145-adr0023-phases-af-acceptance-2026-08-21.md)；更新总览／通读／62／glossary／devlog
- 飞书 provision＋同步；提交推送 `main`

---

## 2026-08-21 — ADR-0023 Phase A～F 连续落地

**做了什么**
- Phase A：ClockFreeze／Modal／禁 Tick  
- Phase B：ParticipantSnapshot＋ReinforcementRange  
- Phase C：Offer 可选支援勾选 UI  
- Phase D：PostBattle＋PreBattle 还原（防瞬移）  
- Phase E：BattleInterruptQueue 串行  
- Phase F：`Adr0023BattlePhasesTests`＋验收文档 [145](145-adr0023-phases-af-acceptance-2026-08-21.md)  
- 战中 JoinOngoing 改为排队（旧测已改期望）

---

## 2026-08-21 — ADR-0023：Manual Encounter 冻结战略 WorldTick

**做了什么**
- 新增 [ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)；影响审计与分期 [144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)
- 修订 `21`／`23`／`33` 补丁／138§3.1／139／140；[143](143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md) 标 superseded（废回战场／战斗中切图）
- 更新 glossary／总览／62／通读／roadmap／ADR 索引／0018 补充
- **Phase A：** `StrategicClockFreeze`；Offer／Manual／PostBattle；`SimulationLoop`／Host 禁 Tick；Modal 禁战略出行／进其他场景；薄「结束战斗」；EditMode 冻结断言

**废弃默认：** 战斗期间战略世界继续跑；FieldCleared 后挂起 InEncounter 再「回战场」

---

## 2026-08-20 — 文档 143：LocalMap／大地图进出交互行为方案

**做了什么**
- 新建 [143](143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md)：按 **1A＋2A** 写进出状态机、操作目录、决策表、§7.1「一人进村再回战场」泳道；**待确认后再改代码**
- 更新总览／62／通读／139／140 交叉引用；飞书 provision＋同步
- 说明：不做 Figma；可视化用行为树／Mermaid／ASCII

---

## 2026-08-20 — 收束文档 142＋飞书／GitHub

**做了什么**
- 收束 [142](142-auto-battle-incap-corpse-2026-08-20.md)（自动战结算＋弥留／尸体＋状态 UI 角标＋大地图菜单修复）；更新 23／62／总览；飞书 provision＋同步；提交推送
- **未手操验收**，仅 EditMode 测试

---

## 2026-08-20 — 自动战结算＋弥留／尸体

**做了什么**
- `CombatLifeStateService`：0 血→弥留；补刀→死亡+尸体；按修为 2／3／5 游戏日 decay
- `AutoBattleCasualtyService`：自动战胜／败伤亡；处决 vs 击溃；接战 checkbox
- LocalMap／Host 表现：弥留停战、补刀、角标；`HostFormalHud` 底栏左上角
- 大地图左键节点菜单锚定节点框（去掉镜头居中抽搐）

---

## 2026-08-18 — 收束文档 141＋飞书／GitHub

**做了什么**
- 收束 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md)（追击贴敌＋LocalMap 多选近战）；更新 140／139／62／总览／通读；飞书 provision＋同步；提交推送

---

## 2026-08-18 — 大地图攻击持续贴敌军

**做了什么**
- 追击每 tick `SyncPursuersToStack`：敌军挪位仍改道贴当前宏观位置，重合再弹接战
- `StartTravelToStackAnchor` 对道路行军中的栈也按显示进度追，不只追 Dest 节点

---

## 2026-08-18 — LocalMap 多选一起攻击

**原因**：`HostNpcMeleeAssault` 只记一名攻方；后到的 `Begin` 顶掉先前的人

**修复**：多名己方可同时攻击同一目标；右键攻击对当前选中全员下指令

---

## 2026-08-18 — 清大世界旧实现＋138 飞书换链

**做了什么**
- 删大地图「外交」面板／敌军「交谈」占位；删 `CaptureNodeForPlayer`、`TryResolveEncounterVictory`、未用 `CollectAtNodeParty`、`DepartingLocalMap` 运行时分支
- 旧 138 飞书无写权限 → 新建文档并换链：https://my.feishu.cn/docx/FSOcd9I2oosbBWx82CXcKMkZnod

---

## 2026-08-18 — 收束文档 140＋飞书／GitHub

**做了什么**
- 收束文档 [140](140-world-map-rts-battle-return-rollup-2026-08-18.md)；更新 139／62／总览／通读；飞书 provision＋同步；提交推送

---

## 2026-08-18 — 打完回不了青石荒村

**原因**：清场后仍 `InEncounter`，可达性用「较近端」当 BFS 起点；半路打完较近端常是荒村，点回荒村被当成「已在」→「无法沿宏观道路到达」。与外交无关。

**修复**
- 路中／InEncounter：当前道路两端可直达
- `ResolveAnchorNodeId` 覆盖 InEncounter
- 暂清节点 Owner、大地图不再按势力染色

---

## 2026-08-18 — 后到增援只弹加入战斗

**原因**：先到点「手动战斗」时 `ClearPursuit` 清掉全员（含路上第二人）标记 → 后到误弹到站查看

**修复**
- `ClearPursuitForEngagedKeepEnRoute`：只清进场者，按身上标记重建追击名单
- `BeginPursuit` 同栈合并名单
- 大地图点选头像时状态栏提示颜色含义（橘=接战／蓝=路上／灰绿=驻留）

---

## 2026-08-18 — 追击绝不弹到站 + 去掉战略敌对门槛

**做了什么**
- 角色挂 `CombatPursuitStackId`：攻击上路必标记；到站跳过「是否查看」，只弹接战
- `BeginPursuitToStackAnchor` 强制 `BeginPursuit`，到位立即尝试 BattleOffer
- 战略敌对不再挡进场／攻击确认；外交默认中立

---

**做了什么**
- 进场景：有我方在场即可进（含敌占荒村），不再被外交敌对挡住
- 攻击／追击：不弹到站「是否查看」；接战弹窗优先盖过到站提示

---

**原因**
- 进 Encounter 时清掉 TravelTicks，路进度变成 0 → 无法回荒村（误判已在出发端）
- 删敌军栈前未快照路点；路锚去非端点时误走瞬移逻辑

**修复**
- 删栈前 `SnapshotEngagedRouteFromStack`；`PreserveRouteProgressForEncounter`
- 释放进度丢失时用 0.5；路锚先走到端点再续走；短途至少 8 tick

---

**产品（1C）**
- 中途可开大地图查看、可派人增援；参战中不可宏观离开
- 敌清空：无结算弹窗、不自动开大地图；画面留战场 LocalMap；`FieldCleared` 后可大地图下令移动

---

## 2026-08-17 — 大地图选栈吸附 + 加入战斗真进场

**做了什么**
- 敌军栈点选：扩大吸附半径；已选己方时左键点栈保留选中并弹出攻击菜单
- `JoinEngagedMembers`／`RelocatePartyOnEncounterMap`：增援一律 `InEncounter`（修复到站后仍 AtNode／路锚导致看不见）

---

**做了什么**
- 遇敌：沿用 `BattleOffer`（自动／手动／撤退）；追击抵达仍由 `StrategicPursuitService` 弹出
- 到站：`ArrivalNotice` — 最终目的地才弹；文案「XXX 抵达「YY」」；**去查看**→开大地图并选中到站者；**暂不查看**关掉
- 接战优先于到站，不叠弹

---

## 2026-08-17 — 清残留 + 全员上路视线留在 LocalMap

**做了什么**
- 再删：`MarkDepartingLocalMap`／`CommitTravelAfterLocalExit`／`EnsureSpawned`／`TryUnloadActiveLocalMapIfNoFriendlyParty`／`NotifyPlayerOverride` stub／「未出行」头顶字
- 出行后**不再卸图、不 FrameCamera**：只 Despawn 上路者，画面留在当前 LocalMap
- `ApplyPartyWorldNodePresentation`：目标图暂无我方、或焦点图空但 ActiveMap 仍在 → return，禁止误卸把视线带走

---

## 2026-08-17 — 大地图重切：纯 RTS（删边缘离场）

**产品**
- 放弃「走到地图边缘再上路」；问题过多，整段 Host 离场链删除
- 大地图：选人下令 → **立刻** LocalMap 消失 → 宏观移动；路上再点别处 = 改目标打断
- 遇敌进 LocalMap／后到加入：之后再加细，本刀不动接战弹窗主路径

**做了什么**
- 重写 `HostWorldTravelDeparture`（约删光边缘／Force／Prepare）
- 删 `OrderEntityToWorldPointForDeparture`
- 确认窗不再传 `useLocalMapExit`
- 更新 139

---

## 2026-08-17 — 「有的能出行有的不能」：Departing 被滤掉 + 失败卡死

**原因**
1. `CanReceiveTravelOrder` 不含 `DepartingLocalMap` → 正在走边缘的人在大地图里**点不动／下不了新令**
2. 边缘失败后硬禁止上路 → 部分人既不走边缘也不上路，像「不能出行」

**修复**
- 可下令模式加入 `DepartingLocalMap`；改令先 `CancelDeparture`
- 边缘彻底失败时再宏观保底上路；状态栏区分「已出发／未能出行」人数

---

## 2026-08-17 — 根因：在场者离场失败会静默「直接上路」

**为什么会出现「有人走边缘、有人直接出发」**
- 旧循环：`TryStartEdgeExit` 一失败（无 View／Mark 被路权挡住／寻路失败）→ **立刻** `StartAgentTravel`+Hide
- 主角常成功，队友常失败 → 看起来像随机分裂

**这次硬规则**
- `MustEdgeExit`（在 ActiveMap 节点或场景里已有 View）→ **禁止**回退宏观上路
- 失败走 `TryEmergencyEdgeExit`（直线／贴边瞬移再回调）
- `MarkDepartingLocalMap` 不再因路权失败而挡离场标记

---

## 2026-08-17 — 离场链收束：RTS 大地图 + 保留边缘离场

**产品确认**
- 大地图 = RTS（随时下令／打断）；遇敌才进战斗 LocalMap；后到可加入
- 人在节点场景时：**仍要走到边缘再上路**（选项 2）

**做了什么**
- 重写 `HostWorldTravelDeparture`：删掉 Force／双路径／IssueOne Stop 旁路，只留 `TryStartEdgeExit`
- 在场全员：补刷坐标 + EnsureSpawn → 走边缘（寻路失败则直线）→ 上路
- 不在场：直接宏观移动；无人留下才卸图

---

## 2026-08-17 — 多人离场：禁止「寻路失败就直接上路」

**根因**
- 在 Active LocalMap 上的队友若无 View／坐标在墙外／寻路失败，旧逻辑会**静默回退** `StartAgentTravel`+Hide，看起来像瞬移上路；主角常有合法 View 所以仍走边缘

**做了什么**
- 在场者：`ShouldAttemptLocalMapExit` → **必须**走边缘；失败则直线离场，不再宏观跳过
- `OrderEntityToWorldPointForDeparture`：不发 Stop 打断同批；寻路失败改直线
- 离场前强制重刷表现坐标并贴格；整批 `_suppressOverride`

---

## 2026-08-17 — 多人离场：全员走边缘，不再只有主角

**做了什么**
- 焦点节点上 AtNode／Departing 的我方一律可见（不再因 LocationId 过期隐身）
- 离场前 `PrepareAgentsForLocalMapExit`：补表现坐标 + `EnsureSpawned`
- 多人离场边缘点加队形错开，降低寻路挤死回退成「直接上路」

**判断与理由**
- 旧逻辑 `NeedsLocalMapExit` 要求已有 EntityView；队友常无 View → 直接宏观移动

---

## 2026-08-17 — 卸图：仅当该 LocalMap 无我方角色

**做了什么**
- 离场只 Despawn 离开者；**当前 Active LocalMap 上仍有我方**时保持装载
- **无人留下**才 `UnloadActiveLocalMapPresentation`（清空 ActiveMap／视图／WalkGrid；空遭遇顺带清刷怪与 Engaged）
- `ApplyPartyWorldNodePresentation`：目标图上无我方且非即将刷遭遇 → 直接卸图，禁止空图挂载

**判断与理由**
- 击杀已不卸图；多人分遣时一人出门不应把还在村里的人连图一起卸掉

---

## 2026-08-17 — 遭遇战击杀不再自动胜利／弹大地图

**做了什么**
- 打死遭遇敌军只同步栈人数；不自动胜利结算、不卸图、不 `Open` 大地图
- 离场／回大地图改由玩家自行操作

---

## 2026-08-17 — 修复「出不了门」：离场失败必须回退宏观移动

**做了什么**
- `MarkDepartingLocalMap` 不再要求目标与当前节点**直达相邻**（多段路径可离场）
- 走边缘失败／标记失败时**回退** `StartAgentTravelToTarget`／追击上路，不再整单取消
- 边缘朝向用 BFS 下一跳，避免指错方向

**判断与理由**
- 强制 `useLocalMapExit` 后，非相邻目标或边缘寻路失败会 `continue` 且无上路 → 表现为完全出不去

---

## 2026-08-17 — 大地图攻击对齐普通派遣离场

**做了什么**
- 右键他方栈：敌对＝攻击／详情；非敌对＝攻击／交谈／详情（去掉跟随）
- 攻击：登记追击后直接 `BeginPursuitToStackAnchor`（场景走边缘离场→大地图追击），不再二次确认、不关大地图
- 普通节点／道路出行确认也强制 `useLocalMapExit: true`
- 先到 BattleOffer 手动战；后到「加入战斗」流程沿用

---

## 2026-08-17 — 重做路上遇敌（删同路自动接战）

**做了什么**
- 删除 `CheckBattleCollisions`／`CheckRouteCollisions`（同路即「行军遭遇」）
- 新增 `StrategicEngageRules`：须节点／道路进度真正重合才可接战
- 接战弹窗仅：**主动攻击**或**追击抵达**；普通赶路过敌不弹
- 更新 [138](138-world-strategic-battle-offer-plan-2026-08-17.md)／[139](139-world-map-rts-orders-2026-08-17.md)；补 EditMode 回归

**判断与理由**
- 旧逻辑「同 Route 非锚点栈即弹」+「行军中同路即 CanEngage」导致过路遇敌；与可自由移动、无暗雷产品定冲突

---

## 2026-08-17 — 删除 Route danger 暗雷

**做了什么**
- 删除 `RouteEncounterService`、`RouteEncounterPending`、路遇弹窗 UI 与相关 EditMode 测试
- 接战仅保留 ArmyStack + `BattleOffer`（同路碰撞／追击抵达）
- 更新 [113](113-world-graph-local-map-architecture-revision-v0.1.md)、[138](138-world-strategic-battle-offer-plan-2026-08-17.md)、SCHEMA；飞书同步

**判断与理由**
- 产品已定：战略遭遇必须对应大地图可见敌军，随机「路遇险情」与全战式接战语义冲突

## 2026-08-17 — 138 战略层 Phase 0～4 落地

**做了什么**
- Core：`StrategicBoard`、`BattleOffer`、外交四档、`ArmyStack`、日界 AI 派兵；`SimulationLoop` Travel 后接 `StrategicTravelDriver`
- Host：`HostStrategicInterruptPresenter`（接战弹窗）；`HostWorldMapPanel` 开 M 暂停、删 `DriveTravelWhilePaused`、Space／倍速、归属色、外交侧栏、Army 图标
- EditMode：`StrategicPhaseTests` 通过

**判断与理由**
- 按 138 分期验收；统一世界时间（§3.1）在 Host 层落地，避免战略层与 LocalMap 双时钟

**注：** 初版 Phase 0 曾含 Route danger roll（暗雷），已于同日后续删除。

## 2026-08-17 — 138 增补：统一世界时间＋无文明式复杂度

**做了什么**
- [138](138-world-strategic-battle-offer-plan-2026-08-17.md) §3.1：开大地图自动暂停；Space 全局继续；倍速与 LocalMap 一致；接战强制暂停
- §9 明确无科技树／兵种克制；飞书同步

**判断与理由**
- 大地图要看见派兵移动，时间须能流；开图先停给决策时间，与即时制不矛盾

## 2026-08-17 — 大地图战略层＋接战弹窗设计

**做了什么**
- 新建 [138 战略接战计划](138-world-strategic-battle-offer-plan-2026-08-17.md)：帮派／外交／占点分期、全战式 BattleOffer（战力对比／自动胜率／自动或手动 LocalMap）
- 更新 [113](113-world-graph-local-map-architecture-revision-v0.1.md) Phase G、[62 现状](62-project-status-2026-08-01.md)、总览；飞书同步

**判断与理由**
- WorldGraph 底座已有，下一刀应是「战略遭遇怎么打」而非再扩 LocalMap 战斗细节；接战弹窗统一自动／手动入口

## 2026-08-17 — 远程弹道文档＋飞书／GitHub

**做了什么**
- 补全 [134](134-spirit-veil-ranged-normal-attack-2026-08-16.md) 统一弹道专节；更新 [137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)／`23`
- 飞书同步相关页；推 GitHub（弹道代码＋文档）

---

## 2026-08-17 — 统一远程攻击弹道特效

**做了什么**
- `HostMeleeStrikeVfx.PlayRangedBetween`：青色光核沿攻→守飞行，抵达后爆闪＋受击闪白（纱衣普攻及日后远程共用）
- 程序化 `RangedProjectileSprite`；近战挥砍不变

---

## 2026-08-17 — 收束文档 137＋飞书／GitHub

**做了什么**
- 新建 [137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)（熟练／纱衣／田区／砍树＋冲击确认验收）
- 更新 [131](131-skill-mastery-study-ritual-2026-08-16.md)／[135](135-world-object-inspect-and-tree-chop-2026-08-16.md)／[136](136-farm-field-zone-labor-2026-08-16.md)／总览／通读／62
- 飞书 map 增补 130～137；provision＋同步；推 GitHub

---

## 2026-08-17 — 熟练冲击确认窗（成功率只在询问时显示）

**做了什么**
- 斗技／功法「冲击下一档」先确认：是否突破＋成功率＋材料；结果弹窗不再写成功率
- `EvaluateMasteryBreakthroughChance`＋`HostSkillMasteryPanelUi.DrawBreakthroughConfirm`

---

## 2026-08-17 — 砍树掉木修复＋中树10／大树40

**做了什么**
- 产量：中树 10、大树 40（小树 3）；伐倒先入包再销毁，避免读已毁对象
- 树不再注册 Work 热点（右键砍伐，不再误成林区劳动）
- 背包满时提示粗木未入包

---

## 2026-08-17 — 斗技／功法两级熟练度 UI（列表＋详情）

**做了什么**
- 斗技：一级竖列表（圆标＋名称摘要）；点进二级熟练度页（各档效果／冲击材料／进度）
- 功法：境界面板点当前功法卡片，进同款二级熟练度页（无多功法列表）
- 共用排布：`HostSkillMasteryPanelUi`

---

## 2026-08-17 — 斗技学习黄条对齐境界突破＋明确学习成功率

**做了什么**
- 删掉屏幕中上独立参悟框；学习／熟练冲击改用底栏状态板上方黄条（同突破）
- 文案与预估统一写成「学习成功率」；战斗释放不掷此骰
- 文档：[131](131-skill-mastery-study-ritual-2026-08-16.md)

---

## 2026-08-17 — 删掉绿草上的幽灵农田／药田工区

**做了什么**
- 去掉 `ResolveWorkBand` 旧大片农田／药田色带；Legacy 麦垄／药畦热点一并删
- 左键检视：farm／herb／grain 工区只点在耕种格上才出「工区·农田／药田」
- 地点圆心改为多块田的平均中心（`MapLayoutPresentationSync` + places JSON）
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — WorldGraphEditor 可视化节点拖动

**做了什么**
- 去掉纯表格编辑；画布拖节点／连线；右侧属性
- 外形与 `HostWorldMapPanel` 对齐（128×44 标签、棕线、Y 向上）
- 文档：[128](128-world-graph-editor-usage.md)

---

## 2026-08-16 — 农田只画一遍：物件页保留，分区去掉药田／农田区

**做了什么**
- MapEditor 分区板去掉 `zoneHerb`／`zoneGrain`；物件 `herbField`／`grainField` 即整片可耕作区
- 参考图删掉重复区标；Host 目录仍兼容旧图 overlay
- 文档：[112](112-map-editor-usage.md)

---

## 2026-08-16 — NPC 日程 Labor 接入田区走格农作

**做了什么**
- `HostFarmFieldLabor.SyncNpcScheduleFarmers`：`WorkAction`（Labor）＋ farm／herb／grain 工区＋有田格 → 自动走格
- NPC 收获进据点库存；玩家仍进背包；离开 Labor 自动停
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — 田区自动农作（整片区＋格上作物）

**做了什么**
- 药田／农田按 location 成区；交互后自动选格播种／照料／收获／清理
- 格状态着色；自然缓慢生长；停止／移动中断
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — 非玩家筑基交战自动开纱衣

**做了什么**
- `TryAutoActivateForNonPlayer`：Npc＋筑基＋灵力够 → 交战 `Begin` 自动开纱衣
- 杂役主管改为筑基、灵力 180（内容数据）
- 玩家仍仅 F2 手动；农田区域劳作下一刀

---

## 2026-08-16 — 世界物只读况栏＋砍树掉木

**做了什么**
- 左键点空统一只读物况栏（主管府／住房／工区／耕种格／树墙）；无指令球
- 树／墙带耐久；砍完树掉粗木并销毁；右键／F8 可砍拆
- 文档：[135](135-world-object-inspect-and-tree-chop-2026-08-16.md)

---

## 2026-08-16 — 斗气纱衣改 F2，并入人物面板指令横排

**做了什么**
- 去掉 R；选中筑基后底栏「战斗」旁出现 `F2 纱衣`
- 事件流水显隐改 Ctrl+F2，避免抢键

---

## 2026-08-16 — 斗气纱衣：筑基远程普攻姿态

**做了什么**
- Core：`SpiritVeil*`——筑基固定灵力召唤；开则普攻射程 7，伤害／攻速不变；空灵力／交战结束卸下
- Host：`R`／底栏纱衣；追击按姿态射程；远程青色外放特效
- 文档：[134](134-spirit-veil-ranged-normal-attack-2026-08-16.md)；更新 `22`／`23`

---

## 2026-08-16 — 功法／斗技轻量编辑器＋清理非正式斗技样例

**做了什么**
- 新增 `ManualArtEditor`（`启动-ManualArtEditor.cmd`）；Shared 认 `combatArt`／`mastery`
- 删无入口的 `art_spirit_strike`；内置斗技注册缩成 JSON 缺失时的薄保底
- 文档：[133](133-manual-art-editor-and-cleanup-2026-08-16.md)

---

## 2026-08-16 — 熟练度配置化：修 bug＋校验

**做了什么**
- 修：`ProgressRequiredToNext` 误删；熟练进度按配置表同步；斗技直授写入 profile；`teachesArtId`→`combatArt` 引用校验
- 增：`SkillMasteryAbsoluteTierTests`；JSON／引用静态检查通过

---

## 2026-08-16 — 熟练度配置化：每档绝对值（不连乘）

**做了什么**
- `mastery.tiers`／`breakthroughs` 进功法 JSON；新增 `combatArt`＋`combat_arts.json`
- Core 按档读绝对值（修为速度／伤害倍率）；去掉 EffectMult 连乘
- SCHEMA／[132](132-skill-mastery-config-absolute-tiers-2026-08-16.md)

---

## 2026-08-16 — 功法／斗技：蓄势研读＋熟练度（入门→小成）

**做了什么**
- Core：`SkillMastery*`；点学改蓄势掷骰入门；打坐／释放／灌注涨熟练；材料突破小成；效果倍率
- Host：`HostSkillStudyRitual`；境界面板／斗技面板灌注与冲击；Snapshot 读写
- 文档：[131](131-skill-mastery-study-ritual-2026-08-16.md)

---

## 2026-08-16 — WorldGraph Host 出行／进场景关大地图／场景隔离

**做了什么**
- 进场景：`Close()` 大地图 + `ApplyPartyWorldNodePresentation(closeWorldMap: true)`
- 场景隔离：Legacy 药畦／色带／灰盒回落仅限荒村；Preferred 缺失不刷荒村砖
- 收束文档 [129](129-world-graph-host-travel-scene-isolation-2026-08-16.md)；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — 宏观出行：确认／走到边缘／途中不可进场景

**做了什么**
- 右键目标节点 → 确认弹窗（打断当前行为）
- 确认后：人在当前 LocalMap 则走到地图边缘再消失，再上大地图慢移
- 途中 LocalMap 不现身；头像菜单可「查看信息」，到站后可「进入场景」

---

## 2026-08-16 — 大地图 30 节点＋组队移动（无通行令）

**做了什么**
- 去掉通行令／permit UI 与旅行门槛
- Ch01 WorldGraph 扩至 30 节点；仅荒村有 LocalMap
- `WorldPresenceBoard`：每人宏观位置；大地图勾选组队点相邻节点出发

---

## 2026-08-16 — WorldGraph D／F＋localPlaceSet

**做了什么**
- 换 Node：`ApplyPartyWorldNodePresentation` 卸／装 LocalMap；占位节点清空实体图
- Ch01 地点表迁 `localPlaceSet`（删 `ch01_reference_region`）；Scenario 改 `openingLocalPlaceSetId`
- WorldGraphEditor（节点／道路表）；`启动-WorldGraphEditor.cmd`

**下一步**
- 手操验：地图旅行换图；编辑器改点边保存

---

## 2026-08-16 — WorldGraph A～C：旅行＋地图按钮

**做了什么**
- Core：`WorldGraphBoard`／`PartyWorldPresence`／`WorldTravelService`（关隘 traversalRequirements）
- Bootstrap 灌图；Ch01 含青石关；`permit:guanai` 才能过关去矿山
- Host 顶栏「地图」＋M；废默认 Y 宏观 Travel；region 标明仅村内地点表
- EditMode：`WorldTravelPhaseBTests`

**下一步**
- D：换 Node 卸／装 LocalMap；F：WorldGraph 编辑器

---

## 2026-08-16 — WorldGraph 阶段 A（数据＋Loader）

**做了什么**
- `type=worldGraph`：SCHEMA／Definition／Loader／引用校验；`WorldGraphs/ch01_world_graph.json`（荒村绑现有 map，矿山／林间／渡口占位）
- EditMode `WorldGraphPhaseATests`；旧 `worldRegion` 并行不改 Demo Host

**判断与理由**
- 战略层（含未来宗门运营）需要节点图底座；A 只立数据，旅行／大地图 UI／卸图另开 B～D

**下一步**
- 阶段 B：`StartTravel`／时间推进／到站 EnterNode

---

## 2026-08-16 — 收束文档 127＋飞书／GitHub

**做了什么**
- 新建 [127](127-defeat-teleport-cave-camera-spawn-table-gui-2026-08-16.md)；更新总览／通读／62／126；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — 击败瞬移／进出洞相机＋刷怪表 GUI

**做了什么**
- 击败后只 Despawn 尸体，不再 Rebuild 整图（根因：Rebuild 把交战坐标重置成地点中心）
- 走动写 PresentationOverride；进出洞换地点清 override；镜头优先对准己方
- MapEditor「编辑／新建刷怪表…」GUI；重发 MapEditor

**判断与理由**
- 生命／背包等 Core 状态进出洞本就同一世界，不靠存档；缺的是表现坐标继承

---

## 2026-08-16 — 收束文档 126＋飞书／GitHub

**做了什么**
- 新建 [126](126-control-core-chase-spawn-zone-rollup-2026-08-16.md)：府近战／追击／刷怪区表
- 更新总览／通读／62／feishu-map；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — MapEditor 刷怪区＋刷怪表（无敌人编辑器／无钉点）

**做了什么**
- `type=spawnTable`＋`placement.kind=spawnZone`；开局 `SpawnZoneApplier` 引用角色定义刷人
- 洞府残影改走刷怪区；名册／residentNpc 拿掉；MapEditor 分区页可画刷怪区
- 近战追击：到位待命不再误脱战（并行）

**判断与理由**
- 敌人＝角色数据；工具只编「哪里出／出哪张表」，避免双 schema

---

## 2026-08-16 — 近战追击不再因到位待命而脱战

**做了什么**
- 根因：出距追击到位后 `HoldStandby`→`Stop`→`Disengage`，追一下就停战
- 交战攻方到位跳过待命；追击重寻路更快并保持定住目标

**判断与理由**
- 近战应对移动目标持续贴身；玩家显式 Stop／右键地面仍脱离

---

## 2026-08-16 — 主管府突击对齐正式近战

**做了什么**
- 删除 `TestMeleeDamagePerHit`／固定每秒伤；`ControlCoreService.ApplyStrikeFromAttacker`＝攻−防/2
- `HostControlCoreAssault`：近战间隔＋挥砍特效；破门站占不变
- 主管 NPC 仍走 `HostNpcMeleeAssault`（右键攻击）；文案／[121]／[125] 同步

**判断与理由**
- 地图互砍已正式化后，府建筑不应再留临时常量伤

---

## 2026-08-16 — 彻底去掉机缘点静默学功法

**做了什么**
- `CultivationAttemptGate`：入定不再 Learn（已在 `f71063c`）
- `OpeningScenarioApplier`：修士 NPC 开局也不再 `offeredManualId`→LearnManual（仅 Discover 地点）

**来历**
- 该机制来自 **2026-08-01 VS0.3**（`8893906`）：Gate「已知可修炼 Site → 顺带学点上功法」，当时验收文档写明「青云诀经机会获得」
- 不是本轮偷偷加的，是旧竖切保底；与后来「秘籍显式学习」冲突，故清掉

---

## 2026-08-16 — 战斗／斗技／体魄验收收束＋功法不再保底

**做了什么**
- 未学功法统一「还没有学功法」；`CultivationAttemptGate` 去掉入定静默学青云诀
- 收束文档 [125](125-combat-arts-physique-acceptance-rollup-2026-08-16.md)；更新总览／通读／62；飞书 map＋同步；推 GitHub

**判断与理由**
- 「青云诀残篇」来自入定顺带 Learn，不是单纯 UI 占位；与秘籍显式学习冲突

---

## 2026-08-16 — 选中面板右侧斗技栏（1–6 可点放）

**做了什么**
- 左键展开角色底栏：主面板右侧竖列显示装备斗技，角标 1–6，标题「斗技·已学N」
- 己方左键点击主动格＝释放（与快捷键共用 `HostCombatSkillBar`）；底栏横向技能条去掉以免叠层

**判断与理由**
- 对齐修仙模拟器「状态板右侧技能位」；键位与技能名同格可见

---

## 2026-08-15 — 体魄≠血条：拆开 Physique／生命

**做了什么**
- 新增 `AttributeId.Physique`（体魄）；`MaxHp` 显示一律改「生命」
- 概况／人物／境界／突破／属性列表统一走 `HostAttributeLabels`
- 角色 JSON 补 Physique；突破阶梯附带少量体魄；SCHEMA／术语表对齐

**判断与理由**
- 术语表本就写体魄＝肉身属性；此前把 MaxHp 标成体魄是误用

---

## 2026-08-15 — 修互砍：战斗组件未进 Entity 白名单

**做了什么**
- `Entity` 白名单补上 `CombatVitals`／`CombatArts`／`EncounterLink`／`CharacterBio`
- 根因：`AddComponent` 失败被静默忽略 → 无生命池 → `ApplyStrike` 失败 → **无掉血、无攻击特效**

**判断与理由**
- 特效绑在命中成功路径上；组件挂不上时两边一起哑火

---

## 2026-08-15 — 1–6 专供斗技；卸掉其它数字快捷键

**做了什么**
- `HostCommandBridge`：劳动／休息／观察／修炼／社交／分工等不再绑 0–9（默认 None；场景序列化一并清零）
- `DemoPrototypeHud`：去掉 1／2／5 调倍速（改顶栏按钮）
- 斗技释放仍由 `HostCombatSkillBar` 独占数字键 1–6

**判断与理由**
- 数字键与技能栏抢键；菜单／V／顶栏已够用

---

## 2026-08-15 — 主动斗技：裂爪击／开山拳＋1–6 技能栏

**做了什么**
- 装备栏 6 格；选中角色按 1–6 释放主动斗技
- 洞府掉落「裂爪击」（200%×3）；将老任务奖励「开山拳」（500%×1）＋原功法秘籍

**判断与理由**
- 先做可手操的主动技竖切，半自动／技能树另开

## 2026-08-15 — 敌对姿态：免确认／可见标识

**做了什么**
- 明确：`hostile` 标签＝敌对姿态（洞府威胁）；无标＝中立／未宣战（主管等）攻击仍双确认
- 敌对：头顶名「·敌对」、偏红、菜单「（敌对）」且一点即打；主动寻仇留后

**判断与理由**
- 与宣战前中立单位分流，符合「洞府里就是敌人」

## 2026-08-15 — 斗技学习／装配面板

**做了什么**
- 脚下状态板右侧新增「斗技」入口（与人物／境界／关系并列）
- `HostCombatArtsPanel`：已学列表、装到键 1–6、从背包秘本学习（不消耗）

**判断与理由**
- 斗技装配与人物面板同一入口区，符合「点人再开详情」手操

## 2026-08-15 — 洞府残影不应出现在地表

**做了什么**
- 洞内地点（`interior`／`localMapId`）地表强制不可见
- 洞府怪无驻点时不落到开局地表点；Level Tester 默认剧本改 `scenario_ch01_reference`

**判断与理由**
- 残影出现在外面＝驻点／可见性过滤断了，不是「怪该刷在村口」

## 2026-08-15 — 战斗 A–C：近战体感／指令层／血条

**做了什么**
- A：挥砍特效＋交战中标签；敌人交战被 Hold，不乱跑
- B：换目标；右键地面脱离；S／Stop／行动菜单「脱离战斗」；顶栏交战提示
- C：头顶体魄／灵力护盾条；底栏「况」显示当前 HP／护盾（非仅上限）

**判断与理由**
- 按约定一次做完 A→C，再进入 D 斗技加深／E 差异化

## 2026-08-15 — 通用近战挥砍特效＋战斗路线拆分

**做了什么**
- `HostMeleeStrikeVfx`：程序化挥砍弧＋受击闪白；普攻互砍／主动斗技共用
- 回复中给出相对完整战斗的分阶段拆分（近战体感 → 指令层 → 斗技／姿态 → 境界差异）

**判断与理由**
- 设计文档（23）要 RTS 当场打；先把「看得见打」做稳，再扩系统面

## 2026-08-15 — 洞府残影战斗修复

**做了什么**
- 名册开局时 `WorldRegionBootstrap` 用 roster entries 挂驻点（残影终于在 `loc_cave_chamber`）
- 敌对 NPC：菜单只显示攻击、跳过双重确认；近距直接开打
- 追击不再每帧 `issueStop`；灵力护盾不再被 `EnsureVitals` 回满
- 应用 `initialRealmPlaceholder`；敌对单位偏红着色

**判断与理由**
- 「斗技对、战斗不对」主因是残影没挂上洞内地点，外加互砍手操脆弱

## 2026-08-15 — 战斗 Alpha／洞内残影／斗技 v0／秘籍不消耗

**做了什么**
- 秘籍／斗技秘本**不消耗**；换功法覆盖确认；旧功法修饰卸除
- 地图内 `HostNpcMeleeAssault` 自动互砍；击倒 NPC 写 `encounter:*`
- 洞府残影 `character_cave_shade`；洞内掉落斗技秘本；斗技装 1 门进普攻

**判断与理由**
- 遭遇依赖战斗；斗技挂伤害，功法仍一人一本

## 2026-08-15 — 将老／秘籍／洞府进出收束（文档＋推送）

**做了什么**
- 收束文档 [124](124-jiang-lao-cave-manual-rollup-2026-08-15.md)；更新总览／通读／62／devlog；飞书 map＋同步；推 GitHub
- 功能已含：将老井字棋、秘籍道具、勘查（神识×2／多圈）、进洞选人、出口离开、洞府秘诀拾取（攻+6%）

**判断与理由**
- 炼气后功法来源先竖切可玩，再开洞内遭遇／多功法并存

## 2026-08-15 — 洞窟 LocalMap 进出竖切

**做了什么**
- 地点字段 `localMapId`／`enterLocalMapId`／`enterSpawnLocationId`；Core `EnterLocalMap`／`LeaveLocalMap`
- 后续迭代：勘查显形、右键进／出、选人弹窗、洞内秘籍拾取（详见 [124](124-jiang-lao-cave-manual-rollup-2026-08-15.md)）

**判断与理由**
- 对齐 [113] 最小「发现→进另一张图」，不做完整 WorldGraph

## 2026-08-15 — 学功法：秘籍道具＋选人研读

**做了什么**
- 功法字段 `grade`／`effectSummary`；打坐修为改跟 `cultivationSpeed`
- 道具 `teachesManualId`；背包使用 → 选炼气队员学习并消耗；将老任务奖励改发秘籍

**判断与理由**
- 传承≠立刻学会；功法按角色学、需炼气；品阶／效果先做展示闭环

## 2026-08-15 — 将老对弈竖切（井字棋＋三胜传承）

**做了什么**
- NPC `character_jiang_lao`（泉边踱步）、任务 `quest_jiang_lao_chess`、功法 `cultivation_jiang_lao_legacy`、对话链＋`startMinigame`
- Host `HostTicTacToePanel`：真井字棋；每日一局（`daily:jiang_lao_chess`）；累计胜 3 次领残谱

**判断与理由**
- 对弈先于洞窟；日限＝可来才下，非每日必到；传承走既有 `learnManual` 领奖

## 2026-08-15 — 功法任务条件／奖励接口（内容另填）

**做了什么**
- 条件：`counterAtLeast`／`missingDailyFlag`／`hasDailyFlag`／`encounterCleared`
- 奖励：`addCounter`／`setCounter`／`setDailyFlag`／`clearDailyFlag`／`learnManual`／`setEncounterCleared`
- SCHEMA＋QuestEditor 字段；测 `ContentQuestApiSliceTests`；说明 [123](123-quest-manual-api-interfaces-2026-08-15.md)

**判断与理由**
- 将老对弈／洞窟探索先定契约，避免内容 JSON 绑死假库存代币；日访与计数进会话板、不升 Snapshot

## 2026-08-15 — 境界阶梯／打坐／突破仪式 Host 闭环

**做了什么**
- 人物／境界／关系暂停窗（底栏右侧入口）；F6 就地打坐；修为每 5 游戏分 +5
- `realmLadder` 内容阶梯：感应→炼气→筑基；手动突破；感应境不需功法
- 突破约 10s 黄条蓄势；可取消；移动／受伤／其他指令打断失败；暂停结果弹窗（境界＋属性差分）
- 炼气起灵力护盾；IMGUI 悬停墨色锁定
- 新建 [122](122-cultivation-breakthrough-host-ritual-2026-08-15.md)；更新总览／通读／62／SCHEMA；飞书同步；推 GitHub

**判断与理由**
- 突破不能是瞬升按钮；蓄势＋结果窗才能形成可读反馈；感应期不卡功法避免开局假门槛

## 2026-08-15 — 住房分配／主管府占领／Import 清旧

**做了什么**
- 住房区 vs 主管府拆分；`zoneHousing`／`controlCore`；三住房样例＋`homeWorkAreaId`
- 左键住房／府面板；右键府攻击；靠近近战 20／秒；破门站满 occupyHoldSeconds 占领
- `SettlementAuthority`（manageHousing／manageSchedules）；课表可改
- Level Tester Import 清空 mapRoot 子物体（修叠旧建筑）
- 新建 [121](121-housing-assignment-and-control-core-2026-08-15.md)；更新总览／通读／62／SCHEMA／114；飞书同步；推 GitHub

**判断与理由**
- 府不是住房；权限走内容 `grantsPrivileges` 而非硬编码建筑类型；攻击入口对齐 NPC 右键菜单

## 2026-08-15 — 人物／工区编辑器、名册刷人、倍速、对话发任务可见性

**做了什么**
- 废弃职业式 Job；WorkAreaEditor／CharacterNpcEditor；characterRoster 试玩刷人
- 人物编辑器保存出场／导出名册：深拷贝修复崩溃＋另存为默认当前路径
- Host 倍速：`PresentationDeltaTime` 驱动移动；顶栏统一 `SetSpeedMultiplier`（工作／休息／吃饭靠 Tick 已随倍率）
- 事件编辑器补 `npcDefinitionId`；人物页只读关联 onTalk；说明对话发任务非硬编码
- 新建 [120](120-character-roster-editors-and-timescale-rollup-2026-08-15.md)；更新 111／118／119／总览／通读／62；飞书同步；推 GitHub

**判断与理由**
- 制作人必须能在编辑器里看见「谁能对话发任务」；倍速必须覆盖表现层移动，否则时钟与行为脱节

## 2026-08-14 — 对话／任务 UX polish＋主管 startQuest 样例

**做了什么**
- `stockAtLeast` 任务进度显示 x/目标（右栏／J）
- 对话 `startQuest` 后自动追踪 + 顶部 toast
- 主管不语 → 惩罚任务 + 再对话切催促事件；EditMode 测
- 更新 [117](117-npc-dialogue-host-ux-rollup-2026-08-14.md)；飞书同步；推 GitHub

**判断与理由**
- 对话接任务需要即时反馈与可读进度，否则制作人难以验证 content 管线

## 2026-08-14 — NPC 对话框＋任务失败＋时间流速＋文档收束

**做了什么**
- NPC 右键对话／攻击；走近停下；onTalk → UGUI 居中对话框（打字机／立绘占位／可换皮）
- 非 onTalk 仍走中央打断；对话时隐藏 ACS 底栏
- 任务 `deadlineDays` 失败、`failResults`、`relationDelta` 多目标／`@party`；日志「已失败」
- 时间：1x＝1 现实秒 5 游戏分；5x＝25 游戏分／秒
- 新建 [117 收束](117-npc-dialogue-host-ux-rollup-2026-08-14.md)；更新总览／通读指南／62；飞书同步；推 GitHub

**判断与理由**
- 对话要先定 Host 框架再换美术；失败惩罚与流速是手操可感的基础规则

## 2026-08-14 — Ch01 三环手操＋小队背包＋文档收束

**做了什么**
- Ch01 裁成三任务（手动接取）：uniqueHarvest／背包 stockAtLeast／三人集合
- 劳动约 10s/份＠1x、自动续采；`LocationLaborProgressBoard` 计 harvest
- 小队共用 50 槽背包（B／顶栏）；任务库存读背包
- 任务日志 J、ReadyToClaim 领奖；QuestEditor 补劳动类条件
- 新建 [116 收束](116-recent-updates-rollup-2026-08-14.md)；更新 SCHEMA／110／总览／通读指南；飞书同步；推 GitHub

**判断与理由**
- 制作人要可手操的劳役→凑物资→集合闭环，且物资必须进可见背包而非隐性聚落库存

## 2026-08-10 — 重写「编辑器工具」文档（106）

**做了什么**
- [106](106-content-authoring-editors-plan-v0.1.md) 重写：点名 4 个一期编辑器＋二期 3 个；每个写清界面／操作／实现；工程放 ExternalTools、读写 Data JSON、Phase 验收

**判断与理由**
- 旧稿只停在原则层，制作人看不出「要做哪些、屏幕长什么样、怎么保存」

## 2026-08-10 — 文档补录＋编辑器工具计划确认

**做了什么**
- 新建 [106 编辑器工具](106-content-authoring-editors-plan-v0.1.md)：ExternalTools、A～D 模块、读写 Data JSON、加载路径说明
- 新建 [107 近期里程碑收束](107-recent-milestones-rollup-2026-08-10.md)：导航／NPC／Demo0.1／WorkAction 热修
- 更新总览／[62 现状](62-project-status-2026-08-01.md)／通读指南；飞书映射与同步

**判断与理由**
- 制作人明确要用编辑器做关卡；计划先落盘再开工，避免范围漂移
- 总览／现状仍停在 RTS 页时，后续三块底座对制作人不可见，故补收束页

**下一步：** 确认后开工 ExternalTools 第一期（校验台＋地点＋任务＋事件）。

## 2026-08-10 — 热修 WorkAction OrderId 编译

**做了什么**
- `WorkAction.cs` 补回 `using XianXia.Core.Orders`（commit `65f39a5`）

**判断与理由**
- 清理 unused using 时误删，导致 Unity CS0246／CS0738

## 2026-08-03 — Demo 0.1 Production · Chapter 01 Playable Arc

**做了什么**
- 计划 [103]；验收 [104]；制作人手操 [105]
- 新任务「调度·三人分派」；砍柴老人与矿工老倔拆角；矿洞氛围事件
- 开局／任务／功法／洞府文案对齐 30 分钟可玩弧；HUD 提示含分派三人

**判断与理由**
- 不扩系统：弧骨架已在 [97]，Production 补「可签收体验」缺口（分派节点＋机缘一致性＋手操脚本）
- 矿工与老人必须拆角，否则 NPC Job 样例会毁掉主线机缘

**下一步：** 制作人按 [105] 手操签收；正式长文案可按 [94] 换皮。

## 2026-08-03 — NPC Simulation Foundation Milestone

**做了什么**
- Location：`tags`／`allowedActivities`；WorkArea／Job 内容包；样例药农／矿工／巡卫／管事
- Core：`ActivityResolver`＋`NpcActivityDriver`；`MoveAction`／`WorkAction`（Schedule 不直接产 Labor）
- Host：`MovementIntent` 寻路，去掉硬编码地点启发式
- 计划 [101](101-npc-simulation-foundation-milestone-plan-v0.1.md)；验收 [102](102-npc-simulation-foundation-acceptance-report.md)

**判断与理由**
- 用配置 Job×WorkArea 而不是行为树：满足「策划定义谁／做什么／在哪／每天怎么动」，避免假 AI
- Move／Work 拆段才能接统一导航，且不把坐标写进 C#

**下一步：** 手操签收四类样例走动；无 Job NPC 可逐步迁到 Job。

## 2026-08-03 — Navigation Foundation Milestone

**做了什么**
- Core：`WalkGrid`／A*／`Ch01ReferenceWalkGrid`（可行走＋障碍）
- Host：移动沿航点；`HostNpcScheduleMover` 按课表走地点；多单位软分离
- 计划 [99](99-navigation-foundation-milestone-plan-v0.1.md)；验收 [100](100-navigation-foundation-acceptance-report.md)

**下一步：** 手操签收绕障／NPC 走动；障碍烘焙可后补。

## 2026-08-03 — RTS 手动控制＋HUD／多交互点

**做了什么**
- 己方不跟课表自动行动（`ScheduleDriver` 跳过 Character）；指令改为移动／停止／交互／战斗占位／修炼点选
- HUD：时钟显示分钟；暴露／主管压顶栏全局条＋条内数值；角色面板交差／功法
- 各地多交互点＋地面标记；走到具体点再劳动／修炼
- 交付记录 [98](98-rts-manual-control-and-hud-pass-2026-08-03.md)；飞书同步

**下一步：** NPC／守卫表现巡逻；守卫数据修正；手操签收。

## 2026-08-02 — 第一章可玩弧＋打断＋RTS 引导（交付总结）

**做了什么**
- 内容打断 CIF：`HostContentInterruptPresenter`（事件选项＋任务提醒、强制暂停）；计划／验收 [95]／[96]
- 样例关 Data 拉齐 2G 觉醒弧至炼气→隐藏→权力伏笔（无战斗）；制作对照 [94]
- RTS：首次入区自动勘察；开局／操作条引导；F11 调试／G 敛息；焦点一人下令
- 场景：`DemoParityHost`＝样例关；`PlayableHost`＝框架调试
- 交付总结 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)；EditMode **194/194**
- 文档收束＋飞书 map 增补并同步

**下一步：** 制作人手操签收 DemoParityHost；正式第一章文案；战斗／夺权另开。

## 2026-08-02 — Demo 手感对齐关验收

**做了什么**
- [91] 缺口审计→按文档补齐；HostDemoTileMap（Demo 布局 Prefab 铺砖）；全 NPC 可见；验收 [93]
- EditMode **182/182**

**下一步：** 制作人手操签收；stride=1 可选加密；日课三资源任务数值再对齐。

## 2026-08-02 — Demo 手感对齐（正式 Host）进行中

**做了什么**
- 缺口审计 [91]；PlayableHost 改 Sprite＋XY；Stop／W／C／X／G；工区产资源；暴露／愤怒；课表／头顶字／村民标签／守卫商人
- 进度 [92](92-demo-parity-progress-2026-08-02.md)

**下一步：** 见上条验收。

## 2026-08-02 — Chapter 01 Reference Level 验收

**做了什么**
- 模板关：8 区灰盒地图／RTS 移动＋行动菜单／FormalHud／三类 AI／参考 Scenario＋Data
- 制作流程 [88](88-chapter-01-reference-level-production-guide.md)；验收 [89](89-chapter-01-reference-level-acceptance-report.md)；EditMode **175/175**

**下一步：** 制作人手操参考关；按 88 生产真实章节内容。

## 2026-08-01 — 文档收束＋飞书同步（VS0.7～1.0）

**做了什么**
- 新增交付总结 [75](75-vs0.7-to-1.0-delivery-summary-2026-08-01.md)；更新总览／现状／SCHEMA
- feishu-map 增补 VS0.7～1.0 计划／验收／SCHEMA；`--provision`＋全量同步

**下一步：** 制作人手操 Demo；Snapshot 入档前硬停。

## 2026-08-01 — VS1.0 Demo 0.1 Vertical Slice 验收

**做了什么**
- Demo 闭环验收测：探索→学法→突破→据点日产→关系
- Host Demo 路径提示；EditMode **169/169**；验收 [74](74-vertical-slice-1.0-acceptance-report.md)
- **VS0.7～1.0 长期路线自动化收束**

**下一步：** 产品定 Snapshot 入档／正式 UI／内容扩量；开发可停。

## 2026-08-01 — VS0.9 World Interaction Layer 验收

**做了什么**
- WorldRegion／Travel／Explore；四地点内容；Host 俯视布局＋T／Y
- EditMode **168/168**；地点不入 Snapshot；验收 [72](72-vertical-slice-0.9-acceptance-report.md)

**下一步：** VS1.0 Demo Vertical Slice。

## 2026-08-01 — VS0.8 Cultivation & Settlement Simulation 验收

**做了什么**
- Resource／Facility／Settlement 内容化；SettlementBoard＋日终生产；WorkRole／AssignWork
- 开局青石洞府＋三人分工；Host HUD／键 8–0
- EditMode **165/165**；据点不入 Snapshot；验收 [70](70-vertical-slice-0.8-acceptance-report.md)

**下一步：** VS0.9 世界互动层。

## 2026-08-01 — VS0.7 Character & Content Foundation 验收

**做了什么**
- Character：`personalityTags`／`backgroundTags`／`talentTags`；Scenario 驱动开局 spawn／NPC／势力／关系
- 移除 PlayableDay 软编码 CreateNpc「村内可招者」
- 数据样例：`character_village_recruit`／`character_herb_gatherer`／`cultivation_wood_whisper`／`scenarios.json`
- EditMode **161/161**；验收 [68](68-vertical-slice-0.7-acceptance-report.md)；**未**改 Freeze／Snapshot schema

**下一步：** VS0.8 据点／资源／经营循环（另开计划）。

## 2026-08-01 — 文档收束＋飞书同步（VS0.6）

**做了什么**
- 固化 VS0.6 计划／验收／制作人试玩清单（64／65／66）
- 更新现状 62、路线图、总览；飞书 map 增补并全量同步
- 开发停止；状态＝制作人人工验收

**下一步：** 按 66 试玩签收；再定 Snapshot 入档或下一切片。

## 2026-08-01 — VS0.6 Phase E：Playable Social Host 验收

**做了什么**
- SocialHostAcceptancePhaseETests 闭环
- 验收报告 [65](65-vertical-slice-0.6-acceptance-report.md)
- EditMode 157 全绿；Snapshot 未升版

**下一步：** 停止编码；待产品定 Snapshot 入档或下一切片。

## 2026-08-01 — VS0.6 Phase B～D：社会 HUD／命令／事件

**做了什么**
- HUD：Personality／Relation／Faction／FocusKind
- Port：Help／Slight／Recruit → 既有 Social／RecruitService（非 Order）
- Bridge：键 5／6／7；Actor＝首个 Character，Target＝首个非 Actor
- EventFeed 优先 RelationshipChanged／FactionMembershipChanged
- **未**升 Snapshot schema

**下一步：** V6-E 整合验收报告。

## 2026-08-01 — VS0.6 Phase A：Recruitable NPC 表现

**做了什么**
- ViewableEntityIds：三角色 + 可招 Npc
- EntityViewSpawner 生成／绑定 Npc 槽位（可点选）
- 计划 [64](64-vertical-slice-0.6-playable-social-host-plan-v0.1.md)

**下一步：** V6-B 社会 HUD 薄信息。

## 2026-08-01 — 文档收束＋飞书同步（VS0.5 完成后）

**做了什么**
- 更新 [62 现状](62-project-status-2026-08-01.md)：测试门禁 151、下一步、验收入口
- 飞书 map 增加 VS0.5 验收报告 [63](63-vertical-slice-0.5-alpha-acceptance.md)；provision／全量同步

**下一步：** 人工验收 VS0.4 Host 手操 + VS0.5 Core 社会闭环；关系入档前硬停。

## 2026-08-01 — VS0.5 Phase G：Alpha 整合验收

**做了什么**
- `SocialAlphaAcceptancePhaseGTests`：人格→开局关系→Help→招募→日程偏置→社会漂移→Player Override；断言 Snapshot schema=v1
- 验收报告 [63](63-vertical-slice-0.5-alpha-acceptance.md)；更新计划／现状／路线图

**下一步：** 关系入档前硬停确认 schema；否则另开切片。

## 2026-08-01 — VS0.5 Phase F：社会 Tick

**做了什么**
- `SocialTickDriver`：每 N tick 对 Character／Npc 抽一对，低频 Help／Slight → Ledger（人格加权）
- `SimulationLoop` 可选启用（默认关）；`PlayableDayBootstrap` 开启漂移
- 固定 seed 可复现；**未**改 Snapshot／无地图邻近
- EditMode 150 全绿

**下一步：** V5-G Alpha 整合验收。

## 2026-08-01 — VS0.5 Phase E：人格日程偏置

**做了什么**
- `PersonalityScheduleBias`：bold／cautious／curious 微调 Schedule 活动与时长；bold 可将 Rest 块转为 Labor；bold+cautious 互相抵消
- `ScheduleDriver` 注入偏置；Player Override 仍优先；可招 Npc 挂 Schedule（无 quota）
- EditMode 147 全绿；**未**改 Snapshot／Freeze

**下一步：** V5-F 社会 Tick 漂移。

## 2026-08-01 — VS0.5 Phase D：薄招募

**做了什么**
- `FactionMembershipComponent`／`RecruitService`（门槛＝target→recruiter Score ≥ RecruitMinScore）；离开清隶属、保留 Ledger
- `EntityTag.Npc` + `CreateNpc`：可招者为 Npc，不进 DirectControl／CharacterIds
- PlayableDay：三人劳役隶属 `base:sect_huangcun_labor`；额外 1 名无势力「村内可招者」
- `EventType.FactionMembershipChanged`；**未**改 Snapshot schema
- **Content Authoring Tool 需求（记录）：** 可招 NPC／势力角色若继续手写 spawn，应改为 Content 表＋编辑器；当前 Alpha 仅 1 个软编码实例
- EditMode 143 全绿

**下一步：** V5-E 人格日程偏置。

## 2026-08-01 — VS0.5 Phase C：开局关系

**做了什么**
- `OpeningRelationsSeeder`：三人互惠 `opening_companion` 分
- `SocialInteractionService`：Help／Slight → RelationshipService
- `PlayableDayBootstrap` 启动时播种；EditMode 覆盖种子／互动／可玩日

**下一步：** V5-D 薄招募／FactionMembership。

## 2026-08-01 — VS0.5 Phase B：RelationshipLedger

**做了什么**
- `RelationshipEvent`／`RelationshipLedger`／`RelationshipComponent`／`RelationshipService`
- `SimulationWorld.Relationships`；`EventType.RelationshipChanged`
- 唯一写路径：Service → Ledger Append → 缓存刷新 → DomainEvent；**未**改 Snapshot schema
- `SocialAlphaConstants` 预置试玩常量（供 C～D）

**下一步：** V5-C 开局关系种子 + Help／Slight。

## 2026-08-01 — 文档现状总表＋飞书同步

**做了什么**
- 新增 [62 项目现状](62-project-status-2026-08-01.md)：VS0.1～0.5 进度、V4 交付／commit、V5-A 状态、硬停与下一步
- 更新总览／路线图／VS0.4·0.5 计划头状态／`AGENTS.md`；飞书 map 补 VS 验收与现状文档并全量同步

## 2026-08-01 — VS0.5 Phase A：人格档案

**做了什么**
- `PersonalityProfileComponent`；`CreateCharacter` 默认挂载；`GameStartBootstrap` 写入 Spawn tags
- Content 三人差异标签可查询；34／术语表登记；**未**改 Snapshot

**下一步：** V5-B RelationshipLedger。

## 2026-08-01 — VS0.4 自主推进：先完成 D～H，再进 VS0.5

**决定**
- 不搁置 V4-D～H；按 Phase 独立实现／测试／commit 做完 Host 可玩切片后，再开 VS0.5 社会 Alpha。

## 2026-08-01 — VS0.4 Phase D：命令桥

**做了什么**
- `HostCommandBridge`：选中集合 → `PlayerCommandRequest` → `IPlayerInputPort.Submit`
- 键位 1–4／调试按钮：Labor／Rest／Observe／Cultivate；多选逐个下令、失败不中断
- EditMode／PlayMode：ActiveAction、Schedule Override、Observe→Cultivate

**下一步：** V4-E 时间控制＋最小 HUD。

## 2026-08-01 — VS0.4 Phase E：HUD 与时钟

**做了什么**
- `HostHudSnapshot`／`HostDebugHud`：Day／Hour／Action／Schedule／Quota／Risk／Realm 只读
- 键位：Space 暂停、`.`／N 单步、`[`／`]` 倍速 1→2→5；自动 Tick 按倍速推进
- EditMode：HUD 与 DayClock／组件一致

**下一步：** V4-F DomainEvent 调试反馈。

## 2026-08-01 — VS0.4 Phase F：事件反馈

**做了什么**
- `HostEventFeed`：Tick／Init 后 Drain DomainEvent 到环形缓冲；IMGUI 调试列表
- 优先标记 Day／Override／Observe／Reject／Quota／Action 等事件
- EditMode：发现／ScheduleInterrupted 可见

**下一步：** V4-G Snapshot 存读 UI。

## 2026-08-01 — VS0.4 Phase G：Snapshot UI

**做了什么**
- `PlayableHostSession.CaptureSnapshotJson`／`RestoreSnapshotJson`（既有 SnapshotService，不改 schema）
- `HostSnapshotPanel`：F5／F9＋按钮；Load 后 `RebuildPresentationAfterLoad`
- EditMode：存读后 Tick／Quota／KnownSites／Views 一致

**下一步：** V4-H 一日可玩验收。

## 2026-08-01 — VS0.4 Phase H：一日可玩验收

**做了什么**
- `HostPlayableDayPhaseHTests`：选中→Schedule→Observe→Cultivate→日界→Snapshot
- 验收报告 `61-vertical-slice-0.4-acceptance-report.md`
- VS0.4 Host 切片闭环完成

**下一步：** 开 VS0.5 社会／人格／关系 Alpha（Core）。

## 2026-08-01 — VS0.4 Phase C：RTS Selection

**做了什么**
- `HostSelectionState`／`HostSelectionController`：点选替换、Shift 点选 Toggle、框选覆盖
- `EntityView.SetHighlight` 驱动；Rebuild 清空选择
- EditMode／PlayMode 选择烟测；**未做** PlayerCommand／Port 命令／HUD／事件日志

**下一步：** 等确认后 V4-D 命令桥。

## 2026-08-01 — VS0.4 Phase B：EntityView 表现绑定

**做了什么**
- `EntityView`／`EntityViewRegistry`／`EntityViewSpawner`：三人胶囊槽位＋Collider＋只读同步
- `PlayableHostCameraRig`：固定观察＋WASD／滚轮极简
- Host 初始化／Rebuild 时生成与清理 View；EditMode＋PlayMode 烟测
- **未做：** 点选／框选／命令／HUD／事件日志／存档／地图／Demo 迁移

**下一步：** 等 V4-B 验收后再进 V4-C。

## 2026-08-01 — VS0.4 Phase A：Playable Host Bootstrap

**做了什么**
- Data：`PlayableDayBootstrap`（Load BaseGame → 三人 → Schedule／Site／Manual／DailyTask／Risk；Loop＋Port；默认 QuotaConsequence）
- Unity：`PlayableHostSession`／`PlayableHostBootstrap`；场景 `Assets/Scenes/PlayableHost.unity`
- Editor：Space 单步 Tick、P 暂停／继续；Content 路径失败清晰报错
- EditMode：`PlayableDayBootstrapPhaseATests`；全量 **107/107**
- **未做：** EntityView／点选／命令／HUD／事件日志／存档 UI／Demo 迁移／V4-B+

**下一步：** 等 V4-A 验收后再进 V4-B。

## 2026-08-01 — Vertical Slice 0.4 Unity Host 规划（不编码）

新增 [`59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md`](59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md)：VS0.3 规则闭环 → 最小 Unity 可玩场景；Content／World、EntityView、RTS 选择、PlayerCommandRequest、HUD、事件、Snapshot；Demo 只读禁迁；V4-A～H 独立 commit。确认前不编码。

## 2026-08-01 — VS0.3 最终验收报告（不编码）

新增 [`58-vertical-slice-0.3-acceptance-report.md`](58-vertical-slice-0.3-acceptance-report.md)：汇总 A–D、完整玩家循环、可玩边界、架构观察／ADR 挂账、下一阶段仅规划；**特别记录**后续每 Phase 必须单独测／单独 commit。不自动开工下一阶段。

## 2026-08-01 — VS0.3 Phase B–D：Observe／偷修／日终 Quota

**做了什么**
- B：`ObserveAction`、`OpportunitySite`／`KnownSites`、`sites.json`（NameKey＋短描述）
- C：`CultivationAttemptGate`（发现 Site 后学青云诀再 Cultivate）；`PersonalConcealmentRisk` 0–100
- D：`QuotaConsequenceHandler` 挂 `DayEnded`；`PendingReprimand`＋日切重置
- 整合测：`Vs03PhaseBcdIntegrationTests`（命令序列，非章节脚本）
- **未做：** 主管视线／潜行／目击者、地图、战斗、产品 UI、第一章导演

**下一步：** 等 VS0.3 验收。

## 2026-08-01 — VS0.3 Phase A：DayClock／日循环

**做了什么**
- `DayClock`：由 `WorldTick` 派生 dayIndex／tickInDay／hourOfDay（不另存）
- 跨日：`DayEnded` 先于 `DayStarted`；`IDayBoundaryHandler` 空钩子供 D 接入
- EditMode：`DayClockPhaseATests`（派生、95→96 顺序、同日不重复、Snapshot）
- **未做：** Quota 结算／Reset／Risk／Observe／Site／Narrative／Schedule 改动

**下一步：** 等确认后 Phase B。

## 2026-08-01 — VS0.3 计划设计确认修订（不编码）

完善 [`57`](57-vertical-slice-0.3-plan-v0.1.md)：非剧情／非章节脚本／RTS／Schedule 默认／Override 优先；范围 A–D；Core／Data／Narrative 分层；禁止战斗地图寻路／NPC AI／主管 Boss／完整关系；阶段 V3-A～E。与 `2I` 阶段叙事口径对齐。确认前不编码。

## 2026-08-01 — 叙事：荒村杂役阶段框架重定位（不编码）

**做了什么**
- 删除线性稿 `2I-chapter-1-huangcun-labor-v0.1.md`
- 新建 [`2I-huangcun-labor-phase-narrative-v0.1.md`](../20-systems/2I-huangcun-labor-phase-narrative-v0.1.md)：定位为**阶段叙事**（状态／触发／反馈／可重复事件），非固定章节脚本
- 废除：固定第几天偷修、固定结束点、写死现实／理想伙伴、姓名性别锁定
- 主管改为长期压迫源；倒台声明为玩家筑基后（与 `20` 冲突已记入 2I §11）
- 更新 `20-systems/README`、`00-overview`、`2G` 链与语感说明

**为什么**
- 游戏非章节制／非线性剧情 RPG；原 v0.1 易被读成强制主线日程

**仍待确认**
- 主管倒台门槛（筑基 vs 炼气掀桌／外交分流）；告密是否允许；开局关系权重；`2G` 是否改名；默认灵地包装

## 2026-08-01 — 叙事：第一章《荒村杂役篇》v0.1（不编码）

> 已被上条重定位取代；原文件已删除。保留本条仅作历史痕迹。

## 2026-08-01 — Vertical Slice 0.3 规划草案（不编码）

新增 [`57-vertical-slice-0.3-plan-v0.1.md`](57-vertical-slice-0.3-plan-v0.1.md)：第一天完整体验闭环——Day/Hr/Tick、Observe、OpportunitySite、偷修接 Cultivate、QuotaDeviation 日终薄后果、Exposure 建议一并、V3-A～H 与 Cursor 任务。VS0.2 已验收；确认前不编码。

## 2026-08-01 — VS0.2 Phase C：Player Override + Quota 偏差

**做了什么**
- PlayerOrder 打断进行中的 Schedule Action，原因 `OverrideByPlayer`；事件 `ScheduleInterrupted`
- `DailyTask`：RequiredAmount／CompletedAmount／Deviation；未完成 Schedule Labor → `QuotaDeviationCreated`
- Snapshot 恢复 Deviation／ActiveOrderSource；EditMode `PlayerOverridePhaseCTests`
- **未做：** 主管／惩罚／关系／Exposure／UI／地图／移动／战斗

**下一步：** 等 Phase C 验收。

## 2026-08-01 — VS0.2 Phase B：ScheduleDefinition + ScheduleDriver

**做了什么**
- `ScheduleDefinition`／`ScheduleBlock`／`ScheduleActivity`（Labor／Rest）
- `ScheduleComponent` 绑定；`ScheduleDriver` 空闲且无 Player Order 时注入 `OrderSource.Schedule`
- `OrderQueue`：Player 入队优先于 Schedule
- Snapshot 含 schedules + 实体绑定；EditMode **84/84**
- **未做：** Override 中断、Quota 惩罚、Observe、Exposure、AI／UI／地图

**下一步：** 等确认后 Phase C。

## 2026-08-01 — VS0.2 Phase A：PlayerInput → Order → Labor

**批准：** `56` 已审；RTS／Schedule 语义／报告 B 范围冻结。

**做了什么**
- `IPlayerInputPort`／`PlayerInputPort`／`PlayerOrderFactory`／`PlayerCommandRequest`
- `LaborAction`／`RestAction`／`DailyTaskComponent`；`OrderType.Labor|Rest`
- EditMode：`PlayerInputPhaseATests`；全量 **78/78**
- Commit：`548a095` `feat(core): vs0.2 phase a player input bridge`
- **未做：** Schedule、Override、Observe Action、Exposure、UI

**下一步：** 等确认后 Phase B（ScheduleDefinition／Driver）。

## 2026-08-01 — VS0.2 开工前确认报告（不编码）

新增 [`56`](56-vertical-slice-0.2-pre-dev-confirmation.md)：RTS 控制冻结；目标改为 PlayerOrder+Schedule+Override+规则惩罚；第一阶段仅 Port／Factory／Schedule／Labor·Rest·Observe／最小 Override；Exposure／OpportunitySite 后置；列出 ADR D1–D6。等确认。

## 2026-08-01 — VS0.2 计划范围收紧修订（不编码）

按补充要求重写 [`55`](55-vertical-slice-0.2-plan-v0.1.md)：明确≠第一章；六条核心体验；PlayerInput→Order→Action；Schedule=计划非 AI；Override 三维代价；偷修仅 CultivationAttempt 接口；ExposureRisk 0–100 进入；延期表与风险点；阶段 V2-A～H。

## 2026-08-01 — 文档同步（VS 0.1 完成链）

将已完成的 Data Pipeline M1／Bootstrap／Cultivation／验收状态写回过程文档：`53` 完成标准勾选与命名落地说明、`AGENTS`／通读指南／`34` 组件实现注记；入库未跟踪的 `55` VS0.2 计划草案；补本条之前缺失的专条（见下）。**不编码。**

## 2026-08-01 — Vertical Slice 0.2 规划草案（不编码）

新增 [`55-vertical-slice-0.2-plan-v0.1.md`](55-vertical-slice-0.2-plan-v0.1.md)：杂役第一天闭环——输入→Order、最小 Schedule、Player Override、三角色、日流程、偷修接入、暴露是否进切片、不做清单、V2-A～G 与 Cursor 任务模板。

## 2026-08-01 — Vertical Slice 0.1 验收

**判断：** Core／Data／Bootstrap／Cultivation 闭环已足以作为下一「可感杂役日」切片的底座；产品可玩性仍缺输入／工作／日程／地图。

**做了什么**
- 验收报告 [`54-vertical-slice-0.1-acceptance-report.md`](54-vertical-slice-0.1-acceptance-report.md)
- 架构观察：Cultivation vs Manual 命名、WorldLayout 存档、境界／Progress 语义待 ADR
- EditMode 门禁（Cultivation 完成后）：**73/73**

**下一步：** VS 0.2 规划确认前不编码。

## 2026-08-01 — Cultivation Slice 0.1：凡人→炼气闭环

**判断：** 修炼必须走 Order→Action→ActionClock；突破事件可观测；Snapshot 须恢复 Progress／Realm。

**做了什么**
- `CultivationComponent`／`CultivationService`／`CultivateAction`／`EventType.Breakthrough`
- `RealmStage`：仅 Mortal／QiRefining
- 青云诀 `base:cultivation_qingyun_manual`（Speed／BreakthroughProgress／Modifiers）
- 学法 → 修炼 → 突破 → Snapshot 中途／完成后一致
- Commit：`64cb3ab` `feat(core): cultivation vertical slice`

**不做：** 多境界、天劫、丹药、洞府、战斗。

## 2026-08-01 — Vertical Slice 0.1 Bootstrap

**判断：** 开局只需数据结构 + 三角色 Entity + 初始化事件；不是地图／工作玩法。

**做了什么**
- `WorldInitData`（Region／LocalMap／Settlement 占位）
- `GameStartBootstrap`／`ContentGameStart`
- 角色 Definition：protagonist／companion_a／companion_b（性格 Tag、灵根／境界占位）
- Commit：`6897807` `feat(core): prepare vertical slice 0.1 bootstrap`

## 2026-08-01 — Data Pipeline M1-B：CSV→JSON 校验导入

**判断：** CSV 只作 Authoring；失败必须阻断写盘并给出 ValidationReport。

**做了什么**
- `CsvDefinitionImporter`／`SimpleCsv`（无 Excel 库）
- 校验：重复 ID、非法 ID、缺必填、`requiredRealm` 形如 DefinitionId 时引用必须存在
- Authoring：`Content/BaseGame/Authoring/Csv/*.csv`
- Commit：`90f89ea` `feat(data): complete data pipeline m1-b import validation`

## 2026-08-01 — Data Pipeline M1-A：三类 Definition 加载

**目标：** BaseGame JSON → ContentPackageLoader → Registry 可查询（无玩法结算）。

**做了什么**
- `CharacterDefinition`／`CultivationDefinition`／`ItemDefinition` + Registry 扩展  
- 严格未知字段／重复 ID／非法 DefinitionId  
- `characters.json`／`cultivation.json`／`items.json` + SCHEMA.md  
- EditMode：`ContentPackageTests` 覆盖加载与阻断路径  
- Commit：`3ee16e1` `feat(data): complete data pipeline m1-a definitions`

**说明：** 实现文件名为 `cultivation.json`／`CultivationDefinition`（与计划草案中的 Manual／manuals 用词并存；以代码与 SCHEMA 为准，待 ADR 统一术语）。

## 2026-08-01 — Data Pipeline M1 计划批准（v0.2）

**确认：** JSON 运行时为主、CSV 辅助、Excel 仅编辑源；严格未知字段／重复 ID／非法引用；不实现完整 Localization（预留 NameKey）；Modifier 规则与计算在 Core，Data 只提供配置。

**后续：** 编码已完成（见上 M1-A／M1-B 专条）；计划正文完成标准已勾选。

## 2026-08-01 — Core Milestone 1 验收完成（收尾）

**判断：** 统一 Core 骨架已证明可承载后续系统；下一优先是真实数据接入，而非继续扩模拟功能。

**做了什么（Phase 1～10）**
- 程序集：`XianXia.Core`／`Data`／`Unity`／`Tests`（Core／Data 无 UnityEngine）
- Id／WorldTick／ActionClock／Result／IRandomSource（完整 PRNG 状态）
- ContentPackage 基础（显式 BaseGame）／Entity／AttributeModifier（`2C` 公式）
- DomainEvent／Order→Action／SimulationLoop／Snapshot JSON
- EditMode **54/54**；`CoreM1IntegrationTests` **PASS**（单 Region：加载定义→实体→Wait→存档续跑）

**Git 范围**
- `1688187` `feat: scaffold Core M1 phase 1…`
- … 阶段 2～10 与整合测 …
- `e8340da` `chore: add JsonSnapshotSerializer meta`
- 共 12 个提交（`1688187`‥`e8340da`）

**关键架构成果**
- 双时间职责可测；Modifier 可溯源；业务失败走 Result；存档可恢复 Tick／Action／PRNG／Final
- Demo Runtime 未迁入、未扩展

**下一步（当时）**
- Data Pipeline M1 → 其后 VS Bootstrap／Cultivation（均已完成，见上方专条）

## 2026-08-01 — Core M1 阶段 10 Snapshot＋整合烟测完成

**判断：** JSON 往返必须恢复 WorldTick／ActionClock／PRNG／Modifier Final；内容版本不匹配走 Result。

**做了什么**
- `WorldSnapshot`／`SnapshotService`／`JsonSnapshotSerializer`
- 黄金：Wait 中途存档 → 新 World 续跑同 Tick 完成；Modifier／PRNG 一致；版本不匹配失败
- `CoreM1IntegrationTests` 单 Region 闭环

**下一步**
- Core M1 编码完成，待人工总验收

## 2026-08-01 — Core M1 阶段 9 Order／Action／SimulationLoop 完成

**判断：** WorldTick++ 只能由 SimulationLoop 拥有；Wait(4) 在第 4 次推进完成。

**做了什么**
- Order／OrderQueue／DefaultOrderTranslator；WaitAction／ApplyModifierAction
- `SimulationWorld`＋`SimulationLoop`；CanStart 失败 → OrderRejected／ActionFailed 事件

**下一步**
- 阶段 10 Snapshot JSON 往返

## 2026-08-01 — Core M1 阶段 8 DomainEvent 完成

**判断：** 失败路径也必须留下事实事件，避免用异常代替世界记录。

**做了什么**
- `DomainEvent`／`DomainEventQueue`／`EventType`（EntityCreated／Modifier*／Action*／OrderRejected）
- Peek／Drain；稳定顺序；cursor 供 Snapshot

**下一步**
- 阶段 9 Order／Action／SimulationLoop

## 2026-08-01 — Core M1 阶段 7 AttributeModifier 完成

**判断：** Final 只能经管道计算；黄金例锁定 (100+10)×(1+0.5)=165。

**做了什么**
- `AttributeId` 小枚举；`AttributeModifier`／`ModifierOperation`／`AttributePipe`／`ModifierIdFactory`
- `AttributesComponent` 正式挂载 Add／RemoveBySource／GetFinal／Explain；无公开 SetFinal

**下一步**
- 阶段 8 DomainEvent

## 2026-08-01 — Core M1 阶段 6 Entity 基础完成

**判断：** 组合组件 + 白名单，避免配置反射创建任意组件；Dead／Removed／Incapacitated 语义先锁死。

**做了什么**
- `Entity`／`EntityStore`／`EntityIdFactory`／`EntityQuery`
- 四核心组件：Identity／Attributes（入口）／Lifecycle／ActionState
- Character 标签；白名单拒收未知组件

**下一步**
- 阶段 7 AttributeModifier 管道

## 2026-08-01 — Core M1 阶段 5 ContentPackage 基础完成

**判断：** 官方内容也必须走 ContentPackage，且 M1 只显式加载、不扫 Mods/。

**做了什么**
- `ContentManifest`／`ContentPackageLoader`／`DefinitionRegistry`／`CharacterDefinition`
- `AssetId`／`DataVersion`；`SimpleJson`（Data 层，零 UnityEngine）
- BaseGame `Data/characters.json` 样本；重复／非法 ID／缺字段校验

**下一步**
- 阶段 6 Entity 基础

## 2026-08-01 — Core M1 阶段 4 随机系统完成

**判断：** 存档一致性要求完整 PRNG 状态，而非仅 seed＋计数。

**做了什么**
- `IRandomSource`／`DeterministicRandom`（XorShift128+）／`RandomState`／`RandomStreamId`
- 单测：同 seed 序列一致；Capture／Restore 后续抽取一致

**下一步**
- 阶段 5 ContentPackage 基础

## 2026-08-01 — Core M1 阶段 3 Result／Validation 完成

**判断：** 业务失败必须与异常路径分离，否则 Tick 模拟会被异常控制流污染。

**做了什么**
- `ErrorCode`／`GameError`／`Result`／`Result<T>`／`ValidationReport`／`IValidator`
- `DefinitionId.Parse` → `Result<DefinitionId>`；保留 `TryParse` 兼容
- EditMode 增补 Result 测试

**下一步**
- 阶段 4 随机系统

## 2026-08-01 — Core M1 阶段 2 基础类型完成（待确认）

**判断：** 跨系统共享原语必须先于 Result／Entity／Modifier 落地；DefinitionId 与 EntityId 在类型层隔离，避免后期存档污染。

**做了什么**
- 清理检查：删除探测残留 `Logs/phase1-offline-boundary-check.txt`；无探测脚本／临时工程改动进正式程序集
- 实现 `EntityId`／`DefinitionId`／`SourceRef`＋`SourceKind`／`ModifierId`（最小句柄）／`ActionId`／`EventId`／`SnapshotId`／`RegionId` 占位
- 实现 `WorldTick`（1 tick=15 分，96／日；加减 checked 溢出抛 `OverflowException`）与 `ActionClock`（Duration 剩余，钳制≥0，不改 WorldTick）
- DefinitionId 非法解析过渡：`TryParse` 返回 bool（不做 Phase 3 Result）
- EditMode 全量 **24/24** 通过（含原 5 个边界测试）

**下一步**
- 人工确认后进入阶段 3 Result／Validation

## 2026-08-01 — Core M1 阶段 1 工程结构完成（待确认）

**判断：** 正式分层必须在编译期不可破坏；Demo Runtime 保持不动。

**做了什么**
- 确认／补齐 `XianXia.Core`／`Data`／`Unity`／`Tests` 四 asmdef（Core／Data `noEngineReferences`）
- 补 `Unity/Presentation/`；Tests 按计划落在 `Assets/Tests/EditMode/`
- `DataAssemblyMarker` 用 `typeof(CoreAssemblyMarker)` 保留真实程序集引用
- 修正 Tests asmdef 重复 TestRunner 引用
- batchmode 编译成功；EditMode 边界测试 5/5 通过

**下一步**
- 人工确认阶段 1 后进入阶段 2 基础类型

## 2026-08-01 — Core M1 补充执行规则

写入 Plan v0.2 §0.2／AGENTS／ADR-0022：Demo 冻结；阶段门禁与独立提交；禁擅自改 ProjectSettings／Packages／冻结 ADR／Demo；设计冲突停码。

## 2026-08-01 — Core M1 计划批准并进入编码（阶段 1）

**判断：** Implementation Plan 人工确认 5 项（Domain 不拆 asmdef、Snapshot＝JSON、Random＝完整状态、AttributeId＝小枚举、EditMode 为完成标准）。

**做了什么**
- 发布 Plan **v0.2**；修订 ADR-0022 实施确认节  
- 开始阶段 1：正式 asmdef 工程结构（不扩 Demo）

**下一步**
- 阶段 1 完成并确认后进入阶段 2 基础类型

## 2026-08-01 — AI 多会话协作规范

新增 [`52-ai-collaboration-protocol.md`](52-ai-collaboration-protocol.md)（`46` 号已被 Demo 美术占用）。同步更新 `README.md`、`AGENTS.md` 入口。不改架构／不写代码。

## 2026-08-01 — Core M1 实施计划草案（不编码）

新增 [`51-core-milestone-1-implementation-plan-v0.1.md`](51-core-milestone-1-implementation-plan-v0.1.md)：十阶段工程／类型／Result／Random／ContentPackage／Entity／Modifier／Event／Order-Action／Snapshot；附风险与人工确认项。**确认前不写 Core 代码。**

## 2026-08-01 — 文档可读性整理与飞书一一对应

1. 新增 [通读指南](../00-project/04-reading-guide.md)、[ADR 决策索引](43-decisions/README.md)  
2. 总览入口改为可点链接；飞书 map 补齐 `34`／`35`／`36`／`2E`／审计／全部 ADR  
3. 同步脚本导航分组改为 00／10／20／30／43／40  
4. 原则：**本地 MD 真源 ↔ 飞书阅读层结构与链接一致**；不重写已冻结规则正文  

## 2026-07-31 — Architecture Freeze v0.2 修补

根据审计报告与人工确认，写入 v0.2（仍不编码）：

1. **RelationshipLedger** 唯一真源；Component 只缓存（ADR-0017）  
2. **WorldTick** 唯一世界时间轴；**ActionClock** = Action Duration（ADR-0018）  
3. **Dead ≠ Removed**；Incapacitated 非死；Recovered→Alive（ADR-0019）  
4. **FocusCharacterUnavailable**；DirectControl≠Focus≠Leader≠Identity（ADR-0020）  
5. 开局三人隶属压迫宗门劳役；主管同宗管理者（`2G`／`34`）  
6. 地图 **World／Region／LocalMap**；修订 `24`（ADR-0021）  
7. **Core M1** 范围冻结（ADR-0022）  
主契约文件：`33-architecture-core-rules-freeze-v0.2.md`；v0.1 改为指向 stub。

---

## 2026-07-31 — 架构契约一致性审计报告 v0.1

完成只读审计，产出 `50-architecture-freeze-review-report-v0.1.md`。主轴数据流一致；主要冲突：`24` 地图正文过时、隐匿字段异名、Relationship 双写未定权威、FocusCharacter 失能规则缺失。建议小修补后再开 Core 第一阶段。不编码。

---

## 2026-07-31 — 架构冻结增量：死亡／控制权／Mod Ready

在上一轮文档包之上补三类不可后补的契约（仍不编码、不扩 Demo）。

**为什么补：**

1. **永久死亡默认 + TemporaryProtection**：若默认“剧情重要=不死”，选择与因果差异化会被架空；保护必须显式、阶段性。  
2. **Membership／Role／Relationship／ControlAuthority + PlayerAgency**：单一 IsPlayer／FactionId 无法表达失势领袖、客卿、离开后再敌对；势力领导权必须随职位动态得失，旧势力转 AI 继续。  
3. **Mod Ready／ContentPackage**：把“暂不承诺 Mod”改为正式长期目标但分阶段；官方必须与社区同管线，否则日后拆硬编码极贵。当前只冻结构，不写加载器。

**本轮：** `33` §19～21、`34` 生命周期与势力控制、`36` ContentPackage、`2E` 存档／事件扩展、`27`／`28` 对齐、术语表、路线图阶段 A～E、ADR-0010～0016；飞书说明改号 `37`。

**故意后置：** FocusCharacter 死亡接管细则、TemporaryProtection 事件模板库、阶段 B 加载器、Workshop。

---

## 2026-07-31 — 架构冻结文档包收口（不编码）

**为什么现在冻结这些边界：** Demo 已验证「下令→行动→劳动／偷修→世界在动」的手感；若直接开写 Core，会在 ECS vs 组合、事件溯源 vs 快照、Intent 层、多乘区、私有倒计时等分叉上反复返工。先把可实现、可测试、可维护的契约写死，正式开发才有唯一依据。

**本轮写入：**

- 主契约扩写 `33`：总架构、双层时间、实体、Order/Action、事件账本、地图四类、多队离屏、战斗、AI、军队边界、永久保存、校验
- 新／完善：`34` 实体与组件、`35` Order/Action、`2C` Modifier 公式、`2E` 事件与 WorldLedger
- 桥接 `32`：Demo 类映射；公开概念去掉 Intent
- ADR-0002～0008；总览 v0.6、术语表、路线图 M2.5「架构冻结文档包」、AGENTS 必读
- 原 `35-feishu-sync` 改号为 `36`，把 `35` 留给 Order 系统

**故意后置：** 第一次突破事件细则、炼气术法清单、`24` 正文全面改写、AttributeId 全表、Knowledge 传播公式、正式 UI（ADR-0009）、任何 Core／Demo 代码。

**为什么不采用：**

- **Unity ECS：** 高精度实体规模在 30～50 + 群体层，组合模型更利单人+AI 与序列化；见 ADR-0002
- **完整事件溯源／回放：** 成本高且拖垮开发期规则迭代；快照+ScheduledEvent+必要账本足够「世界记得」；见 ADR-0005
- **完整 GOAP／每类巨型行为树：** 用时间表+效用+统一 Action 更可解释、可维护

**下一步：** 人工审核本包 → 通过后再写突破规格 → 再开 Core asmdef。

---

## 2026-07-31 — Milestone 3.5：统一角色行动与交互框架

建立 `CharacterAction` / `CharacterActionController`，工作与修炼复用同一套「移动→交互→进度→产出／中断」。

- 右键地面=移动；右键工位=采集木材／草药／耕作；右键灵地=开始修炼
- 自动走近交互距离后进入 Working／Cultivating；暂停与倍速影响进度
- 新命令中断旧行动；超时无法到达则回 Idle 并提示原因
- 选中栏显示行动／目标／进度；头顶短状态文字；产出飘字
- 数值来自 `WorkZone` / `ActionSettings`，不写死在移动脚本

---

## 2026-07-31 — 选中单位路线预览

选中角色有移动／赴工／追击目标时，画预览线到落点：绿=移动、黄=赴工、红=追击；入定中不画。

---

## 2026-07-31 — 修炼语义：停下入定，非选目标

明确修炼与工作／攻击的交互差异：工作／攻击是 RTS 选目标；修炼是收敛打坐。

- 按 C：先停掉当前工作／移动／交战，再尝试入定
- 仅在灵地内才真正入定涨修为；站在外会提示「已停下·需在灵地入定」
- 入定有压扁打坐姿势 +「入定」飘字；移动／离开灵地／X → 出定
- 按钮文案改为「入定／出定」

---

## 2026-07-31 — 补基础 RTS 体验交互

补齐此前缺失、但很影响试玩可读性的交互：

- **攻击(A)**：红指针选 NPC → 追击进入交战（闪红、头顶标）；离开／移动则停（伤害待战斗系统）
- **移动落点 X**、**工作产出飘字**、**头顶活动图标**（移／工／修／攻）
- **双击己方单位** = 全选三人

---

## 2026-07-31 — 工作指令改成 RTS 选目标

试玩反馈：应是「点工作 → 指针变样式 → 再点工位寻路开工」，不是到了工位旁再点工作直接开工。

- `工作(W)` 一律进入选目标模式（黄圈指针 + 工位高亮）
- 左键点工位／工作区：寻路过去并开工
- 右键／Esc：取消选目标；平常右键移动会停止当前工作
- 离开工位且未在赶往工位：自动清工作状态

---

## 2026-07-31 — 交互验收迭代汇总（可玩性包）

试玩驱动的一揽子 RTS／经营可读性改动，方便验收压迫感与分工偷修循环。

**操控**：框选／点查；中键拖地图；底部常驻状态栏；点 UI 不丢选中；S 停止  
**查看**：可控三人数值栏；NPC 只读身份栏；主管红三角／守卫黄三角  
**工作**：黄色多工位；`工作(W)`→点工位才开工；右键只移动  
**劳役表**：全村一张村规（右侧竖栏）；村民按表走动；被发现才涨愤怒  
**氛围**：主管／守卫巡逻；村民按表干活  

明细见本文件下方各条与 `44-session-handoff-2026-07-31.md`「本轮改动汇总」。

---

## 2026-07-31 — 显式工作指令 + 多工位

试玩反馈：到区自动开工不合理；大片农田应有多处可操作点。

- 工作区生成多个黄色工位圈（农田5／森林4／草药3）
- 选人 → **工作(W)** → 再点工位才开工；若已在工位旁则直接开工
- 右键工位／空地只移动，到达不自动工作
- Esc 取消「选工位」模式

---

## 2026-07-31 — 全村劳役表 + 被发现才涨愤怒

按设计语义重构课表：一张主管规定的全村表，不再 PA/PB/PC 分别排班。

- `ScheduleService` 改为单一 `villageSchedule`；村民 NPC 与愤怒判定共用
- 课表 UI 改为一行「村规」；文案说明不直接指挥三人
- 愤怒：工时内未工作 **且** 处于主管/守卫检测范围内才累计
- 威胁头顶色标（上一条）保留

---

## 2026-07-31 — 威胁头顶色标

- 主管头顶红色三角；守卫头顶黄色三角（村民／商人无）
- 运行时按 `ThreatLevel` 自动挂载，未重建场景也可显示

---

## 2026-07-31 — NPC 只读状态栏 + 移除左侧「状态」面板

试玩反馈：需区分主管／村民；NPC 也应底部查看；左侧「状态」与底部重复。

- 点 NPC 显示底部只读状态栏（身份／威胁／当前活动），不可操作
- 可控三人仍用底部操作栏；点 NPC 会取消选中
- 删除左侧「状态」面板（三人汇总与底部重复）
- 左侧「详情」保留建筑／工作区／灵地；人物信息改看底部栏

---

## 2026-07-31 — 角色状态栏常驻 + 氛围 NPC

试玩反馈：底部应显示修为／灵气等数值；点操作不应取消选中；世界太死板。

- 单人选中底部状态栏：境界、修为条、暴露条、灵气环境／吸收、课表与指令、操作按钮
- 点状态栏／侧栏／顶栏不再触发世界取消选中；仅点空白或其他单位／建筑时切换
- 主管、守卫、商人简易巡逻；生成 4 名氛围劳工按课表去农田／森林／吃饭／睡觉
- 未重建场景时 `AmbientWorldBootstrap` 运行时自动补挂

---

## 2026-07-31 — 单位操作条 + 停止指令 + 区域／灵地标记

试玩反馈：进农田后无法取消工作；找不到灵地；缺少修仙模拟器式「选中后给操作」。

- 选中单位时底部显示操作条：停止(S) / 修炼(C) / 停修(X) / 敛息(G)，并显示当前指令
- `S` 或「停止」取消移动、工作与修炼
- 工作区在地图上显示名称边框；东南灵地有青色菱形标记与屏幕标签
- 未重建场景时运行时自动补挂标记组件

---

## 2026-07-31 — 中键拖拽移动地图

试玩反馈：80×50 地图无法平移，验收受阻。补齐 RTS 基础镜头操控。

- `CameraController`：鼠标中键按住拖拽平移（抓取地图）；仍受 `WorldBounds` 限制
- 滚轮缩放逻辑不变；HUD 帮助文案补充中键说明

---

## 2026-07-31 — 基础 RTS 交互：框选 + 点选查看

试玩反馈：没有框选、不能点建筑／农田看状态，会严重干扰对压迫／分工循环的验收。先补最小操控，不做夺府指令、不做正式 UGUI。

- 左键拖拽框选可控制角色；单击点选；Shift 追加／取消
- 左键点民宅／仓库／主管府／工作区／灵地 → 左侧「详情」面板（只读）
- 工作区详情显示产出、区内人数、正在工作人数；灵地显示可修炼与修炼人数
- 建筑点选只查看，不下指令；右键工作／移动逻辑不变
- 场景生成器写入 `StructureInspectable`；未重建场景时运行时按对象名补挂

---

## 2026-07-31 — 补齐跨设备交接文档

此前有 `42-devlog` 与 GitHub 提交，但最新 `44-session-handoff` 仍停在 7/30 策划日，换设备无法 30 秒恢复原型进度。

- 新增 `44-session-handoff-2026-07-31.md`：30 秒摘要、开工 5 步、操作、入口路径、约束、建议下一步
- `README` 补远端链接与交接入口；`41-roadmap` 勾选已落地的 M3／M3.5／M4／课表项
- 旧交接 `44-…-07-30.md` 顶部标明以 7/31 与 devlog 为准

---

## 2026-07-31 — Milestone 3.5：基础工作交互

补齐 RTS 式工作指派缺口：选中角色后右键工作区下达持续工作，右键空地自由移动。

- 角色状态：`Idle`／`Moving`／`Working`
- 仅 `Working`（已指派且位于对应工作区）产出木材／草药／粮食
- HUD「状态」面板显示正在工作／移动中／空闲与目标工作区
- 不再因“人站在区内”自动打工

---

## 2026-07-31 — 时间表网格（测试可改）+ 地块悬停灵气

- 时间表改为环世界式 **24 小时 × 三角色** 网格；点击格子循环 睡→起→工→饭→闲
- 正式设计仍应锁定；当前单机测试 `allowEditForTesting=true`，可「重置」回默认劳工课表
- 遵守检测改为按**每角色当前小时**活动判断是否该工作
- 新增地图格环境数据（属性能量／灵气／是否浓郁），确定性随机，灵地偏高；鼠标悬停浮层显示

---

## 2026-07-31 — 进入架构冻结阶段

Demo 功能开发结束。新增并冻结：

- `33-architecture-core-rules-freeze-v0.1.md`：Modifier 管道、Tick、四层模拟、炼气能力、突破事件、隐匿三层
- `32-prototype-to-product-bridge.md`：Demo 已验证语义 → 正式接口；重构换实现不改语义
- `2C-attributes-and-modifier-pipeline.md`：系统展开入口

同步更新：总览阶段、`AGENTS.md`、`31` 架构、`20-systems` 索引、`27`／`2F`／`21`、术语表、路线图 M2.5、handoff／`49`。

**本阶段不编码**；等待下一轮规则确认（2C 数据、2E、第一次突破细则）。

---

## 2026-07-31 — 文档：Demo v0.1 原型现状快照

新增 `49-demo-v0.1-prototype-status.md` 作为当前可玩版本单一入口；同步更新 `45-demo-v0.1` 实现进度表、`41-roadmap` M3～M5 勾选、`44-session-handoff` 代码索引；飞书全量同步。

---

## 2026-07-31 — Milestone 5：NPC 基础日程系统

让荒村按日程自主运转，为后续偷修／潜行提供基础。不做战斗、发现玩家、追捕、潜行判定。

- 新增可配置 `NpcScheduleConfig`（24 小时相位：工作／休息／巡逻）
- 守卫：巡逻点列表 + 路线 + 速度；状态 `Patrol`／`Rest`（夜间回休息点）
- 主管：白天巡视检查，晚上返回住所
- 村民：不逐人全模拟；`VillageCrowdPresenter` 按日程显示群体「工作中／休息中」
- NPC 头顶显示当前状态（巡视中／工作中／休息中）

验收入口：Play 后观察守卫沿路线巡逻，倍速到夜晚应回休息点；主管夜间归府；住宅旁可见村民群体状态。

---

## 2026-07-31 — Milestone 4 接入统一行动（暴露风险补齐）

在 M3.5 行动框架之上，补完秘密修炼验收环，仍不进入战斗／突破／功法／灵根／占领。

- 灵地增加可交互占用态：进入飘字「可修炼」，地图标签显示交互状态
- 行动制修炼同步 `Cultivating`；修为进度 0～1000，数值走 `CultivationConfig`
- **修复**：行动制修炼时补回暴露风险（夜低／昼高／主管附近额外）；只显示不惩罚
- 敛息草（初始 3，G 键）降低暴露；未入定时仍可在灵地缓慢采集
- HUD：修为／暴露／修炼状态／当前暴露增速；支持一人修炼、其他人继续工作

验收入口：Play 后右键灵地令角色 A 修炼，B／C 派往农田或森林；切白天看暴露上升，按 G 用敛息草。

---

## 2026-07-31 — Milestone 4：秘密修炼系统

验证核心循环：白天劳动 → 夜晚偷修 → 修为增长 → 隐藏身份。不进入战斗／突破／功法／灵根／占领。

- 隐藏灵地新增 `SpiritSiteZone`：进入后提示可修炼；未修炼时缓慢采集敛息草
- 角色新增 `UnitCultivation`：`Cultivating` 状态与修为 `0～1000`
- `CultivationSystem` 支持选中角色开始／停止修炼；移动会中断修炼；其他角色可继续工作移动
- 暴露风险：夜晚低、白天高、靠近主管额外增加；本阶段只显示不惩罚
- 新增资源 `ConcealGrass`（初始 3），使用后降低暴露风险
- HUD 新增修为、修炼状态、暴露风险与修炼操作按钮（C／X／G）

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity`，将一名角色移到东南隐藏灵地按 C 修炼，其余角色继续派往工作区。

---

## 2026-07-31 — Milestone 3：基础荒村生活循环

只实现任务、工作、资源与主管愤怒，不进入战斗／修炼／突破／占领。

- 新增独立 `DailyTaskConfig`：每天 06:00 发布“木材 20、草药 5”，次日 06:00 结算；进度只统计发布后的新增资源
- 新增木材、粮食、草药数值库存与 HUD 资源面板
- 新增森林／草药区／农田三类工作区；角色进入后按游戏时间自动产出对应资源
- 新增 `SupervisorAngerSystem`（0～100）：任务未完成增加、工作时段每游戏小时检查未工作角色、任务完成减少；本阶段不施加处罚
- HUD 新增任务进度／剩余时间、资源和主管愤怒面板
- 场景生成器新增任务与愤怒配置资产、草药区占位地块和三个可配置工作区

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity`，进入 Play Mode 后将三名角色分别派往森林、草药区和农田。

---

## 2026-07-31 — 灰盒尺度修正 + 基础时间表验证

按 Demo v0.1 下一里程碑落地，不进入战斗／修炼／突破／占领。

- 地图由 28×18 扩到 **80×50**，分区为住宅、主管府／仓库、农田工作区、森林、隐藏灵地，并拉开移动距离
- 角色根节点逻辑尺寸不变；`Visual` 独立缩放默认 **0.6**；碰撞体与选中圈独立
- 移动速度改为 **1.5**，使横向穿越约 **53 秒**（落在 45～60 秒目标）
- 新增滚轮镜头缩放，并用 `WorldBounds` 限制不超出地图
- 新增独立 `GameClock`：暂停／1x／2x／5x；默认 1 游戏日＝现实 8 分钟，可配 5～10
- 新增只读时间表配置 `DaySchedule_Laborer` 与面板；玩家命令优先，时间表不自动控制角色
- 工作时段检测是否在 `WorkZone`；违反仅调试显示，并预留 `ISupervisorAngerSink`（暂不处罚）

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity` 进入 Play Mode。

---

## 2026-07-31 — Demo v0.1 原型正式开工

用户明确结束“只做策划”的等待阶段，要求不等待最终美术，先以可替换 Sprite 架构建立 Unity 原型。

- 锁定 Unity 2022.3.6f1 + Built-in，并更新 ADR-0001
- 建立 `Assets/`、`Packages/`、`ProjectSettings/` 工程结构
- 新增 `ReplaceableSprite`：角色、NPC、地块、建筑、UI 均通过独立 Sprite 引用
- 新增三人选择与移动：左键单选、Shift 多选、右键编队移动
- 新增荒村场景生成器：28×18 地块、住宅／主管府／仓库、主管／商人／守卫与玩家三人
- 新增 15 项占位 PNG 规格与 Prefab 生成规则；已有同名 PNG 不会被生成器覆盖
- 新增 `48-demo-v0.1-minimum-art-integration.md`

Unity 2022.3.6f1 个人版许可证随后已激活。批处理导入与生成验证通过，实际产出 15 个 Prefab、`Demo_v0_1.unity`，并将场景加入 Build Settings；二次无修改打开验证退出码为 0。

---

## 2026-07-31 — 新建 Demo v0.1 AI 美术生成批次计划

新增 `docs/40-process/47-demo-v0.1-ai-art-batches.md`，把美术需求拆成可执行的 AI 生成批次，**不立即生成全部素材**。

- 第一批 ≤10：只验风格（草地／路／灵地／树／敛息草／民宅／主管府／玩家A／妖兽／灵力特效）
- 第二批：服务探索→修炼→战斗→击败主管→占领
- 第三批：增强体验；城市／宗门／大地图／高阶技能／8方向明确排除
- 每项含顺序、数量、用途、Prompt、Unity处理、角色一致性
- 明确 AI 出静态／方向参考，行走逐帧人工二次制作

---

## 2026-07-31 — 新建 Demo v0.1 美术资源需求表

新增 `docs/40-process/46-demo-v0.1-art-assets.md`，将 Demo 范围转成可供 AI 生成、人工整理与后续 Unity 导入参考的资源规划。

- 统一分为 A 必须拥有／B 可占位／C 正式版替换
- Prototype 推荐 2D 3/4 俯视、32px Tile、64px 角色帧、64px 图标
- 第一批只要求 4 方向最小动画；8 方向完整动画延后
- 列出玩家三人、主管、商人、守卫、村民、人才候选、妖兽的资源与动画需求
- 列出地形、环境物件、建筑、战斗特效、UI、图标和目录命名规范
- 提供统一 AI Prompt、负面词与生成验收清单
- 明确 AI 不适合直接稳定交付多方向逐帧 Sprite Sheet，需人工统一

仍处纯策划阶段，不开始制作素材或搭建 Unity 工程。

---

## 2026-07-31 — 新建 Demo v0.1 设计文档

新增 `docs/40-process/45-demo-v0.1.md`，冻结第一个可验证闭环，明确不做完整游戏。

- 目标：验证玩家是否喜欢「凡人 → 修士 → 势力建立」
- 地图：一张荒村（住宅、主管府、工作区、农田、森林、隐藏灵地、妖兽区）
- 六阶段：入村 → 发现机缘 → 一人突破／两人护法 → 隐藏修炼与敛息 → 夺控制核心／击败主管 → 管理（时间表／人口／人才）
- 最小系统：三人分控、暂停／2x／5x、感应境／炼气／灵力、RTS＋一技能＋一妖兽、夺权后管理
- 完成标准：能完整体验 凡人→修炼→突破→隐藏→反抗→占领→管理

仍处纯策划：先补突破与夺府体验脚本／区域草图，不进入 Unity。

---

## 2026-07-31 — 前期节奏压缩、敛息、夺取控制权与学校人才

在不覆盖既有设计的前提下，整理近期讨论进对应章节。

**前期节奏（`20`／`2G`／`2F`）**

- 凡人阶段不是长期玩法；约 **40 分钟～1 小时**内从感应境进入炼气
- 主题改为：从被压迫的凡人，成为隐藏的修士
- 两段循环：凡人觉醒 → 隐藏修士（炼气后不能立刻公开）
- 离开荒村高风险而非禁止
- 新增敛息资源（如敛息草）：短时隐藏修为，需持续采集
- 三人分工：修炼／采隐藏资源／探情报

**据点夺取与管理（`26`／`21`）**

- 占领=夺取控制权，不是杀光；控制核心建筑（主管府／城主府）
- 三种方式：斩首夺权／逐步瓦解／外交接管
- 占领后第一项重要权限：修改时间表（影响生产、幸福度、人才）

**人才与成长（`27`／`26`）**

- 学校约每 2～3 月刷新人才候选
- 可收为弟子（需修炼潜力）或任命管事（不需修炼天赋）
- 成长循环：治理→人才→弟子→增强→扩地→再治理

下一步：第一次突破事件，或细化隐藏修士／势力发展。

---

## 2026-07-31 — 突破系统通用规则定型

在 `25-cultivation-and-breakthrough.md` 中补齐突破规则层，并同步第一章草案、术语、总览和交接文档。

- 修为达标只获得**突破资格**，不会自动升级；所有突破由玩家主动开始
- 小境界突破流程轻、风险低；大境界突破改变生命层次，需要环境、身体、心境、资源与护法
- 突破过程进入事件，可出现灵气不足、心魔、妖兽、敌袭与机缘
- 结果统一为普通／完美／瑕疵／失败，不再使用“优秀突破”旧称
- 突破会产生随境界增强的异象，并可能引发敌对或友好势力介入
- 同伴、宗门与地点可提供护法；加入势力的保护与资源成为实际价值
- 渡劫不覆盖所有突破，高境界逐渐出现；起始境界与因果影响待定
- 保留十境界顺序；“筑基→金丹／金丹→元婴”仅为机制举例，不删除结晶与具灵

下一步：设计第一次突破事件（感应境 → 炼气），或继续势力发展。

---

## 2026-07-31 — 世界三级结构、四层模拟、时间尺度与领地建设

在保留既有设计前提下，补充世界／人口／领地／修炼节奏，并复核战斗框架（不覆盖已定规则）。

**世界与据点（`24` 重写）**

- 结构定为：大陆 → 城市区域 → 格子地图
- 城市区域是连续地图（约 10 屏），不是单独城池；大陆约 100 屏
- 区域出口由地图数据决定
- 小格子：角色约 1 格，房屋／酒馆等约 4×5；未来地图编辑器编辑
- 地图数据方向：地形、资源、灵气（含属性）、建筑、人口群体、关键 NPC
- 废止早前“村庄 1.5 屏／城市 2–3 屏／短道路连接”的尺度描述

**人口（`27`）**

- 四层模拟：修士高精度／关键 NPC 实体／势力战略／普通凡人群体统计
- 凡人不逐人模拟；地图用代表性群体单位；关键凡人实体化

**领地（`26`）**

- 据点是可发展区域；可建洞府、生产、灵气、防御建筑
- 灵气来源：地形天然 + 建筑 + 灵物 + 多据点汇聚

**修炼时间尺度（`25`）**

- 开局→元婴约 60–100 小时游玩目标
- 正常资质（灵根 15–20／30）+ 黄阶高级：炼气→筑基约 100 游戏天
- 玄阶约 70 天、地阶约 20 天（示意）；低境界差距小、高境界差距扩大

**战斗（`23`）**

- 本次内容与既有小队框架一致，仅强化击杀进入因果／道德体系的表述

**后续讨论方向：** 突破系统、境界突破事件、势力发展。

---

## 2026-07-30 — 境界差异补充、小队战斗框架与灵气汇聚

在不覆盖既有世界观、时间、主管、属性、灵根、功法、江湖关系设计的前提下，补充三块规则。

**境界能力差异（`22`）**

- 明确每个大境界应改变移动、战斗、世界交互三类体验
- 成长闭环：境界改变能力 → 能力改变玩法 → 势力改变资源 → 资源推动修炼 → 修炼推动更高境界
- 筑基：控制外物与灵气（法器／附武／驭器／远程）
- 金丹：力量外放；内丹风格示例改为灼烧／恢复／治疗／麻痹／反伤／护盾；真正飞行方向倾向金丹，仍待最终确认
- 元婴：生命形式改变；补神魂战斗、神识压制、出窍风险
- 化神：补神识领域方向；保留原分神／化身讨论
- 新增**踏空**与普通飞行分层
- 空间能力：悟道原有虫洞讨论保留；羽化本次出现空间通道方向，归属冲突标待确定，不擅自迁移

**战斗框架（`23` 重写扩写）**

- 核心小队级修士战斗；明确不做数千人实时微操
- 大战用算法／战报表现
- 操作：左键选择，右键移动／攻击／互动，1–6 放技能
- 可学很多技能，战斗最多装备 6 个
- 手动／半自动／自动三种释放模式；AI 细则以后设计
- 普通攻击随境界改变方式
- 生命归零＝重伤非即死；默认不攻击重伤；可求饶／威胁／交易；放过产生后续关系
- 战利品按敌人实际携带结算
- 神识影响控制与压制；元婴后可神魂战斗

**领地灵气（`26`）**

- 占领地盘是为了修炼优势，不是扩图
- 每据点有灵气；多据点可把灵气汇聚到主洞府
- 不做复杂摆放；汇聚损耗／容量／可争夺性待确定

同步修订 `25`、`2D`、术语表、总览、索引、交接文档。

---

## 2026-07-30 — 功法系统规则层独立成章

新增 `2H-manual-system-rules.md`，只确定功法底层规则，不设计具体功法与数值。

- 功法是修仙成长核心，但不是职业限制；角色原则上可学习任何功法
- 灵根不构成禁学，只影响学习、掌握、修炼与发挥效率
- 功法可有境界、身体、前置知识、传承等客观条件；“可学”不等于低境界可完整发挥
- 黄／玄／地／天代表完整程度与成长潜力，影响修为获取、最大灵力、灵力恢复、灵力质量、续航与成长上限
- 高阶功法前期优势可以有限，随境界提高和修为需求增加而逐渐放大
- 修为与灵力继续严格分离：修为用于境界成长，灵力用于技能、护体与法宝
- 大部分功法不可升级品阶，角色通过寻找和更换功法成长；少数特殊机缘功法可成长
- 掌握程度提升不等于功法品阶提升
- 功法决定“如何修炼”，技能／斗技决定“如何战斗”
- 新增策划规则字段清单，但明确未进入数据库或技术实现

同步修订 `2D`、`2B`、`25`、总览、系统索引、术语表与交接文档。

新增未决问题：更换功法的转修成本、主修／辅修关系、灵力质量是否独立显示、功法成长上限的具体含义。

---

## 2026-07-30 — 统一属性体系，感应境不再等于特殊视觉

本次确定底层规则，不做数值与具体功法内容。

**统一属性结构（`2B` 重写为《角色属性与修仙成长体系》）**

- 玩家、NPC、凡人、修士、敌人**共用同一套属性**，区别只是开发程度
- 分四类：肉身；灵魂（神识／悟性）；修仙（灵根／灵力／修为／境界）；社会（性格、背景、家乡、关系、声望、目标、欲望）；另加心境
- 修为是成长资源、灵力是战斗资源，两套系统不混用
- 神识：社交洞察、探索感知、可控制的法宝／法术数量与上限、精神抗性；**明确不替代关系系统**
- 悟性：学得更快、更易领悟，**不直接提升技能威力**

**感应境重新定义（影响 `22`、`20`、`2G`、`25`、`28`、`27`、`23`）**

- 感应境**只是修炼境界，不是特殊视觉能力**
- 能力仅三项：感受天地灵气、基础吸收、进入正式修炼准备阶段
- 玩家能看见隐藏灵物、妖气、隐藏修士，来源改为**神识、天赋与经历**
- 因此一个神识出众的普通凡人也可能察觉异常，为"不起眼的 NPC 知道很多"提供了自然解释
- 已从上述文档中替换掉「感应境能力」作为感知手段的旧表述

**灵根与掌握程度**

- 灵根与「属性亲和」**合并为同一概念**，术语表原待确定项关闭
- 灵根以数值表示（示例：火灵根 15/30）；可跨属性学习但速度、熟练与效果下降
- 属性名单改为：火、金、土、木、雷、风、冰、毒；仍不做传统五行相克
- 技能与功法有**独立掌握程度**：初学→入门→小成→大成→圆满→化境（命名待定）
- 战力排序中的「属性天赋」改为「角色属性」，涵盖神识、悟性、心境

**新增未决问题**

- 新名单不含「水」，但 `22` 内丹风格表、`25` 与 `2G` 的属性环境表仍有水条目。已就地标注待确定，未擅自删除，需用户定案水是并入冰还是保留。
- 神识的社交优势上限如何设才不架空关系系统。
- 掌握程度六档的最终命名与各档实际差异。

---

## 2026-07-30 — 时间表、控制优先级与主管愤怒

补充时间与劳役系统的社会自由主题，不改动世界观、感应境、修炼与因果既定方向。

**时间系统（`21`）**

- 采用类似《环世界》的时间表：时间代表修仙社会中的自由程度
- 核心理念：低阶没有自己的时间，高阶与势力掌控者拥有更多时间自由
- 不同身份不同时间表；前期只可查看，后期可制定；Demo 可临时开放修改做验证
- 流速改为暂停／2x／5x；暂定现实约 5～10 分钟 = 游戏一天，待 Demo 调
- 角色控制：手动 + 自动跟随／按时间表；无命令默认待机；第一阶段无复杂自主 AI
- 行为优先级：玩家命令 > 紧急事件 > 时间表 > 待机；战斗中不强制切回时间表

**劳役与主管（`2F`）**

- 明确主题：玩家不是一开始没有力量，而是没有自由
- 新增主管愤怒值与低／中／高／极高阶段后果方向；数值待定
- 愤怒来源含违反时间表、未完成任务、私离区域、偷练／藏物被发现
- 主管系统服务于"逐渐获得自由"：前期守规则 → 中期用关系与能力降低风险 → 后期变成管理者
- 怀疑度与愤怒是否合并为一条风险反馈：倾向合并展示，标为待确定
- 待设计项按用户清单原样登记，不自行定稿

---

## 2026-07-30 — 术语统一：境界名一律写作「炼气」

此前文档里「炼气」与「练气」两种写法混用（正文多为"练气"，第一章标题与口头讨论多为"炼气"），违反术语表「禁止同义词混用」这条硬规则。

已全库统一为**炼气**，覆盖 17 份文档共 90 处，包括竞品拆解里对《鬼谷八荒》境界名的引用。术语表新增一行 `炼气 / QiRefining` 并注明不要写回「练气」，避免后续会话反复。

选择「炼气」而非「练气」的理由：这是讨论时的自然用法；趁尚未进入实现、代码与配置表里还没出现标识符时统一，成本最低。

---

## 2026-07-30 — 第一章体验流程草案：从凡人到炼气

新增 `2G-first-chapter-flow.md`。这是一份体验流程草案，不锁定人物、地点、名称与具体事件。

节奏骨架：劳役入场 → 感应探索 → NPC 机缘 → 多路线取得功法 → 秘密准备 → 引气入体进入炼气 → 低级妖兽教学战 → 管理者与制度矛盾伏笔。

关键补充：

- 初始地点暂称“青云宗外围资源点”；宗门保护地方但底层资源体系压迫劳役者
- 第一次炼气需要感应积累、功法、合适环境与不被打断的时间
- 结果方向为普通成功、优秀突破、失败反噬，具体判定待确定
- 炼气后获得灵气池、恢复与基础防御；战斗先耗灵气，耗尽后才直接损伤生命
- 突破后的低级妖兽战是首场修仙教学战，不等于第一次 Boss 战

---

## 2026-07-30 — 修炼／属性／功法体系定方向；世界观合并整理

**第一部分：世界观**

`29` 按《世界观哲学：天道、因果与修仙社会结构》合并整理，补强灵气分布与阶层表述、对抗邪修不计严重业障、元婴／化神滥用力量的示例。不重复开新平行章节。

**第二部分：修炼相关**

- `22`：炼气定为"正式进入修仙体系"而非巨大跃迁；感应境明确不能真正运转灵力、不能使用修士能力
- `25`：扩写第一份功法多路线、自然环境灵气／属性环境、灵物不叠放、阵法宗门期
- 新建 `2B-attributes-and-affinity.md`：七属性亲和；**明确不做传统五行相克**
- 新建 `2D-manuals-arts-and-equipment.md`：黄玄地天功法／斗技；战力排序境界>功法>斗技>属性>装备；低境界可获高阶功法但受容量限制

术语表同步：属性亲和、品阶、灵物、阵法；生克标为明确不做。本阶段仍无数值定稿。

---

## 2026-07-30 — 世界观哲学：天道因果与修仙伦理

把 `29-karma-and-consequence.md` 从简短的力量约束草稿扩成世界观／伦理专章。

核心方向：

- 世界不是正道善良、魔道邪恶的二分，而是围绕资源、寿命、修炼机会与境界突破运转的复杂社会
- 力量越强，因果限制越多；影响道心、气运、突破、渡劫与修行道路
- 杀戮按情境判定，不做"杀人 = 罪恶值"
- 凡人与修士存在阶层与利益交换；宗门压迫常是结构性结果，宗门不做脸谱化反派
- 玩家长期主题：从被压迫者成长为体系一环后，是改变体系，还是成为新的维护者

本阶段只记设计方向，不进数值与平衡。总览新增第 6 条设计支柱；术语表补功德、心魔、气运、天道等词。

---

## 2026-07-30 — 修正感应境：核心角色开局即具备

感应境不再是游戏过程中触发解锁的阶段。三名核心角色开局默认已经处于感应境，代表拥有修仙潜质，能够隐约感知并极低效吸收天地灵气；世界中的大多数普通人仍是真正凡人，不能感知灵气。

感应境主要用于感知灵气浓度、隐藏资源、灵物、异常区域、部分隐藏修士、妖气与特殊环境。它不能学习或运转正式功法，也不算真正修士。

玩家需通过探索、NPC 互动或机缘获得第一份功法；首次成功按功法正式运转灵气后进入炼气。设计目标也随之明确：玩家从开局就在逐渐发现一个隐藏的修仙世界，而不是通过事件突然获知自己可以修仙。

已从开局、境界、修炼、总览、术语表和交接文档中删除「感应境如何触发」的旧描述。

---

## 2026-07-30 — 开局章节展开：感应境、半固定背景与 NPC 隐藏经历

**做了什么**

围绕"开局体验／第一章流程"补了三块新设计，并同步到相关系统文档。

**1. 凡人与炼气之间插入感应境**

> **后续修正（同日）：** 感应境不是后续触发的过渡阶段；三名核心角色开局默认已经处于感应境。以下记录保留为设计演变历史，以最新一条为准。

这是本次最有结构性影响的一条。玩家不再直接从凡人进炼气，中间加一个过渡阶段：

- 感应境只给**认知**不给力量：能感知灵气、发现灵物与妖气痕迹，世界的样子变了
- **感应境不能学习正式功法**，必须进炼气才能修习并运行修炼体系
- 因此"获得功法"与"突破炼气"被拆成两个独立目标，可以互相牵引

判断：这一层解决了一个此前含糊的问题——玩家凭什么找得到秘密灵地、凭什么看出某个砍柴人不普通。原来只能靠事件推送，现在有了角色自身的能力来源。它也让开局多出一级可感知的进展，不必等到炼气才有正反馈。

**2. 三名初始角色改为半固定背景 + 玩家自定义**

要素：家乡、身世标签、性格标签、天赋倾向、外貌。固定部分负责产出剧情（回故乡、遇旧人、家族关系），自定义部分负责代入感。

约束写进文档：背景要素必须真的对应地点、NPC 或事件，不能只是角色卡上的文字；玩家改了背景，被引用的剧情也要跟着改。

**3. NPC 拥有可挖掘的隐藏经历**

NPC 不只是任务发布器。样板案例：砍柴人曾是低资质修士，修炼失败落回凡人，想把功法传出去。玩家通过聊天、观察、感应境能力三条途径挖掘。

这条让感应境第一次作用于**社交层面**而不只是探索：同一个 NPC，凡人眼中和感应境眼中不是同一个人。

**4. 开局玩法循环成文**

接受劳役 → 完成任务 → 获得生存资源 → 与 NPC 交流 → 获取信息和机会 → 偷偷探索 → 寻找修炼资源 → 提升实力 → 获得功法 → 进入炼气。

关键点写清楚了：劳役不是终点，而是换取信息与自由时间的成本。玩家在生存、任务、修炼三者间分配时间。

**改了哪些文档**

`20-opening-experience.md` 大幅扩写（新增初始角色、感应境、玩法循环、NPC 隐藏经历、待设计清单五节）；`22` 加入感应境并重排章节；`25` 新增"入门的两级门槛"；`28` 新增 NPC 隐藏经历一节；`27` 补初始角色与凡人的秘密；术语表补 7 个词。

**待解决（本次确认要做但未展开的五项）**

- 宗门劳役制度
- 第一次战斗流程
- 第一次突破炼气流程
- 第一处秘密修炼地点
- 三个初始角色之间的关系

另外新增的待确定项：感应境的感知范围、精度与极低效率吸收如何表达；感应境的"感知灵气"与炼气候选能力里的"灵气感知"是同一能力的两档还是两个东西。

---

## 2026-07-30 — 策划方向统一整理，多项结论回退为「待确定」

**背景**

明确当前处于**纯策划阶段**：不进入 Unity、不做技术架构、不写代码。本次按讨论内容统一整理策划案，并把此前由 AI 提前拍板、但实际尚未确认的设计**回退为「待确定」**。

**做了什么**

- `AGENTS.md` 与总览新增「当前阶段：纯策划」，规定遇到未定设计标注待确定，不得自行定稿
- 定位改写为：以修仙成长为核心的实时暂停式战略 RPG，融合个人修仙 RPG、RTS 式世界与战斗、领地经营、宗门经营、人物关系与江湖系统
- 开局补全：约 3 名角色、被迫进入宗门体系底层劳役、白天完成管理者任务、可同时给不同角色下达 RTS 式实时任务、**每天没有命令次数上限**、基础饭食只维持生存
- 前期核心体验写实：白天服从、晚上偷偷寻找修仙机会，形成风险／收益选择
- **战斗形态修正**：统一为"直接在世界地图中进行，不切换独立副本"。此前写的"据点战役进入独立战斗场景"已删除，大规模战争的承载方式改列为待确定
- 世界地图写入规模目标：3 块大陆、约 30 座城市，村庄约 1.5 屏、城市约 2–3 屏，自由缩放与连续无缝视觉切换
- **凡人系统回退**：不再断言"凡人按人口组管理、不逐个模拟"。改为策划目标"凡人真实存在于世界中"，分层模拟深度列为待确定；重要凡人可有姓名、关系、性格与故事；凡人军队要能参战但不必生成上千独立单位
- 正魔大战与宗门使者写入世界与关系文档，作为"江湖关系影响宏观势力发展"的载体

**回退的具体条目**

| 此前写法 | 现状 |
|---|---|
| 炼气解锁周天调息／灵气锻体／灵气感知（已定） | 炼气能力**暂定**，候选：灵气感知、灵力攻击、自然恢复增强、灵气护体 |
| 筑基使用「法器」，法宝留到金丹 | 按讨论改为筑基即使用**法宝**并学习**斗技**；是否分两级待确定 |
| 金丹解锁自主飞行（已定） | 飞行解锁于**金丹还是元婴，待确定** |
| 飞行不给通用闪避／增伤加成 | 恢复为"更高闪避、更高伤害、无视大量地形"的设计方向，**具体数值待确定** |
| 战斗分两档，战役进独立场景 | 统一在世界地图内进行 |
| 凡人不逐个模拟（明确不做） | 放宽为**待确定**，策划目标是凡人真实存在 |

**判断与理由**

- 之前几轮为了推进速度，把我的建议直接写成了已定稿，这会让后续设计建立在未经确认的前提上。本次统一改回讨论中／待确定，代价是文档"看起来没那么确定"，但避免了错误前提被继承
- 术语表移除了未确认的提案词（周天调息、灵气锻体、灵力灌注、丹相），改为在待定名词区登记，等能力定稿后再统一命名

**待解决**

- 炼气最终能力清单
- 飞行归属金丹还是元婴
- 凡人分层模拟的深度
- 凡人军队的参战表现方式
- 大规模战争在不切副本前提下如何承载

---

## 2026-07-30 — 十境界权限骨架与悟道空间网络

> **后续修正（同日）：** 本条中的炼气三能力、筑基法器、金丹丹相与自主飞行，均属当时的提案，未经确认。已在上一条中回退为「待确定」。境界名称与"每境界必须带来机制新能力"这两条仍然成立。

**做了什么**
- 境界名称定为：炼气、筑基、结晶、金丹、具灵、元婴、化神、悟道、羽化、登仙
- 修正炼气与筑基边界：炼气只负责体内循环（调息、锻体、感知）；灵力离体、法器、术法、御器与短距离御器飞行移到筑基
- 金丹定为丹相、属性本源、灵力外放、自主飞行与法宝；元婴定为出窍和死亡逃生；同时操纵第二躯体延后到化神
- 悟道加入空间规则：战术挪移／空间锁，以及可连接远方据点的空间虫洞网络
- 空间虫洞定为双端空间锚点基础设施，有建造与维护成本、通行容量，可被封锁、破坏与反向入侵

**判断与理由**
- 如果炼气已经御器，筑基相对炼气的规则优势不够明显；移动后，筑基管事弟子的第一卡点更可信
- 金丹解锁自主飞行比等到元婴合适：中期就能改变地图规则，元婴则专注第二生命
- 空间虫洞若能随手连接任意地点，会抹掉物流、地形和战争前线；把它做成可争夺的基础设施，反而能生成后期战略玩法

**下一步**
- 用户提到后期希望加入“两种”能力，目前只描述了空间力量，需要确认第二种
- 继续定义结晶、具灵、化神、羽化、登仙各自不可替代的新操作

---

## 2026-07-30 — 炼气能力、RTS 操作与宗门审查定型

> 后续修正：御器与灵力灌注已移至筑基；见上一条“十境界权限骨架”。

**做了什么**
- 参考《凡人修仙传》与《斗破苍穹》的能力边界，把凡人 → 炼气定为“首次获得灵力资源与操作权限”
- 炼气基础能力定为一套灵力系统的三种用法：周天调息、灵力灌注、御器；通过炼气小阶段逐步展开，防止一次塞三个按钮
- 明确炼气恢复不能治疗重伤，御器受距离／重量／灵力限制，持续御剑飞行留给筑基
- 战斗定为 RTS 操作语法 + 连续即时暂停：框选、编队、右键指令、Shift 队列、技能指定目标
- 明确筑基管事弟子不能被刚入炼气的三人正面数值碾过，必须先破坏飞行法器、消耗灵力、布置陷阱或争取内应
- 新增宗门使者审查：正魔大战提供暂缓处理窗口，使者报告综合据点产出、地方稳定、旧管事证据与私人关系；投其所好的礼物可以推动有利报告

**判断与理由**
- 单纯回血不够表达境界质变；“凡人只能卧床，炼气可以主动调息，但要付出时间与暴露风险”才是操作规则改变
- 炼气即可基础御器符合类型认知，但如果同时获得持续飞行，会提前透支筑基的辨识度
- 使者不能只因一件礼物无条件洗掉杀死宗门弟子的事实；礼物负责改变解释意愿，产出、稳定与旧管事证据负责让解释站得住
- RTS 指的是选取与下令语法，不是高 APM；暂停、指令队列和自动基础攻击继续负责降低操作负担

**下一步**
- 排前 5 天具体配额与教学节奏
- 设计宗门使者的身份、偏好、礼物来源与审查倒计时

---

## 2026-07-30 — 确认外门劳役身份与第一卡点，并约束心智负担

**做了什么**
- 确认身份：三人家贫被迫接受宗门外门劳役，本是凡人但有天赋
- 确认第一卡点：宗门派驻的管事弟子（约筑基初期），手下有凡人监工
- 在开局与义务文档中写入「按天数分层解锁」与「同时只盯一条怀疑度」的防过载约束
- 术语表区分管事弟子（StewardDisciple）与监工（Overseer）

**判断与理由**
- 外门劳役比「被强征的苦力」更贴合后续宗门关系：掀桌后要面对的是宗门态度，不是单纯地方豪强
- 筑基初期作为第一卡点合理：凡人阶段够压迫，炼气后有挑战但不至于无解
- 心智过载的真正解法不是砍掉明暗双轨，而是**不要第一天全开**；玩家每天主动决策约 5 次封顶

**下一步**
- 设计凡人 → 炼气能力矩阵（须服务战斗／跑图／隐匿）
- 排前 5 天具体配额与教学节奏表

---

## 2026-07-30 — 前期主玩法定型：明面配额与暗面积累

**做了什么**
- 新建 `2F-obligation-and-concealment.md`：把前期定为劳役处境，玩家白天完成监工配额换口粮，夜间偷偷修炼、狩猎、采药、炼制
- 定义怀疑度（Suspicion）作为暗面行动的统一代价，分五档后果，最高档是剧情分支而非失败结算
- 定义掩护、藏匿点、贿赂三个机制，让三人分头行动产生真实的策略取舍
- 完善 `21-core-loop-and-time.md`：确定指令模型（下达意图、角色自动执行、三人独立队列）、1 Tick = 15 游戏分钟、每 Tick 六步结算顺序、自动暂停规则、战斗与世界时间的两档处理
- 用一整天的时段表做了纸面推演，确认一天约 5–8 次重要决定
- 改写 `20-opening-experience.md`，把阶段一二从抽象的"求生与机缘"落成可玩的配额日常与偷练日常
- 术语表新增两组：时间与指令（Tick、Order、OrderQueue、AutoPause 等）、义务与隐匿（DailyQuota、Suspicion、Contraband、Stash、Cover 等）

**判断与理由**
- 前期一直缺一个持续的压力来源。配额加口粮解决了这个问题：官粮能活命但对修炼无益，"老实干活"这个消极解法被直接堵死，玩家必须冒险
- 明面与暗面的对立同时制造了时间张力（白天被占用）、风险张力（偷练会暴露）和资源张力（私藏会被没收），三者叠加让"挤出来的每一刻钟都有分量"，这正是"修炼要挑时间挑地点"能成立的前提
- 明确不做实时潜行（视野锥、噪音、蹲伏躲避）。它是另一个游戏类型，成本高且只服务前期一小段；抽象化的怀疑度则能平滑演化成后期的势力猜忌、情报与外交
- 指令模型定为环世界式的"指派 + 自动执行"，不是全战式直控。既然过程自动完成，玩家的乐趣就必须来自取舍，这一条决定了前期所有系统的设计取向
- 修正了此前"1 Tick = 一个时辰"的设想。那个粒度无法表达移动、采集和遭遇战，改为 15 分钟一 Tick、一日 96 Tick，表现层仍用时辰与时段
- 战斗与世界时间分两档：小队遭遇战不切场景、世界时间照常流逝，保留"A 在打架时 B 还在采药"的同时性；据点争夺这类战役才进独立场景并冻结世界
- 整套结构不是一次性的开场关卡。配额演化为宗门贡献与岁贡，藏匿点演化为暗桩，贿赂守卫演化为收买敌方要员，玩家开局学会的心智模型后期可直接复用

**风险与约束**
- 最大风险是这段变成"打工模拟器"。已写入节奏约束：劳役阶段控制在游戏内 15–30 天、实际游玩 1–1.5 小时，配额内容必须随天数变化，结束由玩家推动而非熬够天数

**待解决**
- 怀疑度按角色算还是按小队算
- 修炼气息外泄如何表达，是否需要"掩息"作为第一个功法
- 睡眠与体力如何建模（夜间修炼必然挤占睡眠，代价要在数值上体现）
- 1 Tick = 15 分钟的手感需要原型验证

**下一步**
- 设计"凡人 → 炼气"的能力矩阵，要求在战斗、跑图、隐匿三个场景都有用
- 把前 5 天的配额内容与秘密灵地的发现节点排成具体关卡表

---

## 2026-07-30 — 拆分文档结构：总览只留大纲

**做了什么**
- 把总览页的细节全部拆出，新建 10 份系统文档（20 开局体验、21 核心循环与时间、22 境界与机制能力、23 战斗、24 世界与据点、25 修炼与突破、26 领地经营、27 角色与人口、28 江湖关系、29 因果与业力）
- `00-overview.md` 精简为最高层大纲：定位、身份五阶段、五支柱、三层玩法、文档索引、依赖顺序、范围控制、跨系统未决问题
- 更新 `20-systems/README.md` 清单与依赖图，补齐尚未开始的基础层系统（五行、属性管道、功法词条、事件与世界状态）
- 更新 `AGENTS.md`：入口改为总览，并新增"总览只放大纲"的硬性规则
- `feishu-map.json` 增加 10 个系统文档条目，待用户建好飞书空文档后填 ID

**判断与理由**
- 单页塞满细节会导致两个问题：阅读时找不到重点，修改时容易冲突。分层后总览负责导航，系统文档负责深度
- 每份系统文档末尾都保留自己的未决问题清单，跨系统的问题才留在总览，避免同一个问题写在多处
- 21 核心循环与时间被标为下一份要完善的，因为其他系统都挂在它定义的时间轴上

**待解决（阻塞后续）**
- 21 的时间模型未定：暂停时能下什么命令、同一 Tick 内各系统如何排序结算、何时切手动战斗
- 三个跨系统分叉未定：战斗是连续即时暂停还是短回合、领地是自由布局还是槽位面板、境界命名体系

**下一步**
- 完善 `21-core-loop-and-time.md`，纸面推演"一天怎么过"

---

## 2026-07-30 — 核心游戏概念第一次收敛

**做了什么**
- 将用户关于“境界机制质变、个人到势力、领地扩张、江湖关系和真实修炼”的口述去重并整理为正式策划框架
- 确认玩家身份是从凡人小队逐步成长为修仙势力，不是纯个人 RPG 或开局宗门模拟
- 确认当前战斗方向为 2D 暂停即时小队战术，战力悬殊时允许自动结算
- 明确凡人人口群体化管理，核心修士逐个养成，长期目标上限约 30–50 人

**判断与理由**
- 项目真正的核心差异化是“大境界解锁实际机制能力”，而非普通数值成长
- 个人修炼与领地扩张必须互相供养，否则角色 RPG 与经营系统会成为两个割裂的游戏
- 完整概念已接近中型策略游戏，必须先验证“三人、一个村庄、两个据点、一次机制质变”的最小闭环
- 自由建造、完整天下关系网和后期大规模战争暂不进入第一阶段

**待解决（阻塞后续）**
- 暂停即时战斗的具体时间与行动模型尚未确定
- 领地采用自由布局还是据点槽位尚未确定
- 境界名称、能力矩阵和最终胜利条件尚未确定

**下一步**
- 编写《核心循环与统一时间系统》，先纸面推演角色行动、修炼、领地产出和战斗切换

---

## 2026-07-30 — 飞书云文档同步打通

**做了什么**
- 复用 ChatCCC 已有飞书应用凭据，完成 Markdown → 飞书 docx 的单向同步脚本
- 修复清空接口方法（DELETE）、表格只读字段剥离、按 children 切批等坑
- 首次成功将 `01-vision.md` 同步到飞书文档《攻城略地 修仙 策划案》

**判断与理由**
- 本地 Markdown 仍是唯一真源；飞书只作阅读与分享层，避免双向编辑冲突
- 不新建应用，复用现有机器人凭据，减少运维面

**待解决（阻塞后续）**
- 其余策划页的飞书文档 ID 尚未创建/填入 `tools/feishu-map.json`
- `01-vision.md` 的 Q1–Q5 仍未回答，方向尚未收敛

**下一步**
- 用户可继续新建飞书空文档并填入映射；或先回答愿景分叉问题

---

## 2026-07-29 — 项目启动，建立文档体系

**做了什么**
- 建立文档仓库 `D:\UnityProjects\XianXia`，初始化 Git
- 完成两款竞品的系统拆解：鬼谷八荒、了不起的修仙模拟器
- 产出横向对照，明确两者本质区别是"玩家身份"（修仙者 vs 宗门天意）
- 起草借鉴项 / 差异化假设 / 明确不做清单
- 确定架构原则：逻辑与表现分离、数据驱动（文本为真源）、数值可溯源、随机可复现

**判断与理由**
- 竞品的成本大头是内容量而非系统复杂度，单人项目只能走"系统做深、内容做窄、美术做省"
- 即时动作战斗列入"明确不做"，动画与手感是个人开发者投入产出比最差的部分
- 配置表用 CSV/JSON 而非 ScriptableObject，理由是可 diff、可 Git 合并、可被 AI 批量修改

**待解决（阻塞后续）**
- `01-vision.md` 的 Q1–Q5 未回答，尤其 Q1（玩家身份）不定则所有系统设计无法收敛
- Unity 版本与 UI 方案未定（ADR-0001、ADR-0002）

**下一步**
- 回答 Q1–Q5 → 定稿差异化 → 进入 M1 纸上原型
