# 路线图

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

当前 SOCIAL-QUEST-01：Producer Accepted / Sealed。下一阶段 Full Trading（本轮不启动）；之后 Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。Knowledge / Rumor / Information Propagation：Future / Only if gameplay later requires it。


> 2026-09-25 SOCIAL-QUEST-01 最终 P1：制作人主流程人工验收通过。临时同行仅参与移动/空间/战斗，不授予 Active 或玩家手动战斗控制；统一 authority、UI/backend guard、restore reconcile 已实施。Snapshot v11 不变；最终封板授权已由制作人给出。后续方向不自动开始。

> **2026-09-24 DYNAMIC-DISCOVERY-01＋MAP-COORD-01：Producer Accepted / Sealed。** WorldOpportunity 动态物体、三种 discovery mode、稳定 onInspect、显式 resolve、Activity 生命周期、Snapshot v9、Producer authoring、LevelTester 验收入口及 WorldMap Surface 坐标读数已完成人工验收并封板；见 [261](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md)／[262](262-map-coord-01-worldmap-world-coordinate-readout-2026-09-25.md)。后续路线已由下述 2026-09-25 DELAYED-EVENT-01 决定替代。

> **QUEST-INSTANCE-01（2026-09-24）：Producer Accepted / Sealed。** Quest 模板与运行实例已分离；人物委托按真实发布者生成稳定实例，互动接取／结构化交付、关系方向、生命周期、Journal/HUD 与 Snapshot v8 已贯通。双同模板行商的独立接取、交付、领奖、失效与 Save/Load 已由制作人验收。后续 `ContentIntent`、可取消延迟反应等只保持 Planned。见 [258](258-quest-instance-01-dynamic-character-commissions-v1-2026-09-24.md)。

> **VASSAL-WORK-01＋P1（2026-09-24）：Producer Accepted / Sealed。** 直接附庸可在宗主药田／粮田劳作，但不共享仓储、建造或管理权；解除附庸即时失权。首次 New Game Active/View/Selection 初始化与 empty-selection Active fallback 已由制作人验收。见 [260](260-vassal-work-01-direct-vassal-labor-access-2026-09-24.md)。

> **SAVE-01＋STRATEGIC-STOCK-01（2026-09-24）：Producer Accepted / Sealed。** Snapshot v7 持久保存 Quest／Flags／Event fired／Chapter／Counter／Daily／LocationLabor；对话进行中禁止保存，v1～v6 明确拒绝。临时行商与受伤散修两条 prototype 已完成 World Opportunity → Event Choice `startQuest` → Player Accessible Stock hand-in → ReadyToClaim → Journal Claim → Save/Load。resource 在可访问己方战略物资网络时聚合 PartyInventory＋eligible WorldSitePublicStock，非 resource 始终 bag-only；势力仓库 V1 只支持战略资源取出。见 [257](257-save-01-content-progress-persistence-v1-2026-09-23.md)。

> **EVENT-02A（2026-09-23）：Producer Accepted / Sealed。** publicNotice 已从一次性 Toast 升级为持久 WorldActivity Active/History；原 v6 additive 字段在当前 Snapshot v7 中保持原 shape。左侧活动栏支持 unread、详情、最近 100 条历史与同 Surface 镜头定位。制作人已验收 publicNotice、详情、精确位置定位、Save/Load、无重复恢复通知及 expiry→history。见 [256](256-event-02a-persistent-world-activity-feed-2026-09-23.md)。

> **EVENT-02（2026-09-23）：Producer Accepted / Sealed。** `WorldOpportunityDirector` 的 NPC Opportunity 生成、EVENT-01 Template interaction、合法世界落点、同日 density/refill、Save/Load 与 expiry 已由制作人验收。V1 仅支持 `worldVisible`／`publicNotice`；hidden、动态 WorldObject 与后续扩展不在封板范围。见 [255](255-event-02-world-opportunity-director-v1-2026-09-23.md)。

> **Continuous Surface Streaming（2026-09-23）：Radius 2。** Player-centered active neighborhood 已由共享 Core policy 统一为 5×5；Runtime initial/transition 与 Startup Preflight 使用同一 radius。普通相邻 crossing 仍逐帧最多 Build 1 chunk，新增列由 3 增至 5；hard activation 最多同步 Build 25。下一阶段使用 EVENT-01＋EVENT-02 制作真实荒村内容，不继续扩 Opportunity Director。

> **EVENT-EDITOR-V2（2026-09-23）：Producer Accepted / Sealed。** Graph-first Event/Dialogue authoring、Step/Choice 连线、可读 Speaker、Conditions/Outcomes、Priority/Topic/Repeat、显式保底、dirty/undo/redo、editor-only layout、对象/事件 Browser、全局人物来源、可读 Character Picker、按 Package 记忆上次来源，以及 NPC onTalk／WorldObject onInspect authoring已由制作人验收。EVENT-02 后续只扩展了 onTalk binding 表单与通用模板投影，不重开 V2 封板。

> **EVENT-01 FINAL（2026-09-22）：Producer Accepted / Sealed。** 固定世界 NPC onTalk＋WorldObject onInspect、统一稳定目标键、接近后复核、EventEditor 绑定与 Load definitions-only 已接通；见 [253](253-event-01-final-fixed-world-interaction-acceptance-2026-09-22.md)。EVENT-02 Opportunity Director 和剧情 runtime 持久化不在本轮。

> **当前状态（2026-09-22）：LEGACY-FINAL-SEAL、Hex／Army 正式运行依赖退役与 021915 统一收尾均已正式 Sealed。** MAP-01～04、SPACE-01 与 LEGACY-FINAL-A／B／C 同样均已 **Producer Accepted / Sealed**；最终冻结矩阵见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。后续 EVENT-01 Final 已单独授权并实施，等待制作人人工验收；其余方向仍未批准。
> 未来新会话请从 [247 Project Handoff — Continuous World Current State](247-project-handoff-current-state-2026-09-18.md) 开始（Milestone 表、Current Architecture、Known Issues、Do Not Regress、Resume Order、可复制上下文）。
> **EVENT-02 封板时点的后续说明：** 当时 Hidden Opportunity 与动态 WorldObject Opportunity 不在范围；二者现已由 DYNAMIC-DISCOVERY-01 实施。程序化人物、ContentIntent、NPC 主动找玩家、Opportunity Chain、导航 waypoint 与 camera preload 仍未批准实施。

> **2026-09-17 地图进度（历史）：** [MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md) 与 [MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) 均已 **Producer Accepted / Sealed**。主 Continuous Surface 的正常 Gameplay 使用精确世界位置与 Surface authority；Hex/Outdoor LocalMap 旧路径仍保留为 legacy/derived compatibility。MAP-04 物理清理正在实施；当前状态见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md)。

> **Editor 工具链／旧 Content 迁移：** [ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 的 MAP-01～04 已完成；下列分期图是历史实施顺序，不是当前任务：
>
> ```
> Editor Toolchain Cleanup          // 唯一 Build All、Apps/ 平铺输出、staging all-or-nothing、
>   ↓                               //   stale 清理、Editor manifest、README/启动器生命周期分区
> MAP-01 WorldComposer / FineEditor foundation + Huangcun pilot
>   ↓                               // 新 Authoring schema + 青石荒村 Blueprint pilot；旧 source 暂留
> MAP-02 WorldMap Surface LOD       // WorldMap 改为 Final Surface LOD / exact world projection
>   ↓
> MAP-03 product de-Hex / de-LocalMap consumers
>   ↓                               // Scenario / PlayerTravel / WorldSite 退出 Hex/LocalMap authority
> MAP-04 legacy content / editor retirement
> ```
>
> **Historical / Superseded（2026-09-15 当时实测）：** 当时 External Editor、旧 Content 数量与 fallback 状态如下文原记录。后续 MAP-01～04 与 2026-09-22 runtime retirement 已改变正式边界：WorldComposer／FineEditor V1 已落地，Runtime Loader 拒绝 `hexWorld`／`formalArmy`。不得把这段历史数字当作当前运行输入清单。


> **当前唯一状态（2026-09-22）：** CW-U0～CW-10.5、MAP-01～MAP-04、SPACE-01、LEGACY-FINAL-A／B／C 与 LEGACY-FINAL-SEAL 已封板。该时点无获批实施主线；2026-09-25 已授权 DELAYED-EVENT-01。

> 状态：CW-10 与 CW-10.5 均为 **Producer Accepted / Sealed（2026-09-20）**。保持既有编号，不重排 CW-06 / CW-07。
>
> **工具链与地图方向：** MAP-01～04 已验收并封板。WorldGraphEditor／RegionEditor 已删除；MapEditor／LocalPlaceEditor 只维护合法独立空间。

## 当前阶段说明

- **当前产品：** SiteId 公库已替代旧 Settlement 原型；NPC 日程农作逐格消费实时行政授权，真实收获进入当前管理 Site 公库。固定接管、公库保留、可拆旗失效、同势力管理接续与存读档已贯通。
- **前轮：** DELAYED-EVENT-01，Implementation Complete / Producer Acceptance Pending；见 [263](263-delayed-event-01-content-event-scheduling-2026-09-25.md)。
- **当前：** SOCIAL-QUEST-01 — Secret-Realm Quest Social Topic + Temporary Quest Companion，Producer Accepted / Sealed；见 [264](264-social-quest-01-secret-realm-social-topic-and-temporary-companion-2026-09-25.md)。
- **Knowledge / Rumor / Information Propagation：** Future / Only if gameplay later proves it necessary，非近期必做、非 Delayed Event 依赖。
- **当前待定缺口：** WorldMap Player／NPC marker world-space scaling 仍未授权。Separate Space JSON wire 和 restore 后 Quest／Event／Chapter definitions shell 继续沿用既有实现。
- **未来范围：** 更完整仓储物流、税赋、跨 Site 运输、离屏生产、飞舟、自动攻城、NPC 对 NPC 战斗等继续作为 Future / Not Implemented；不属于 Final Seal。

- **Future backlog：** Level 2／3、Encounter 介入参数调优、飞舟、NPC 自动攻城与普通建筑战争。FormalArmy／BattleOffer／Hex 字样若属于 ADR-0038 的合法兼容边界，不再仅凭名称进入清理 backlog。
- **路线（当前已实施，后续 Planned）：** SOCIAL-QUEST-01 → Full Trading → Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。小游戏中途保存作为独立正确性待办，不并入本轮。

- **2026-09-13 历史状态：** CW-U0 的设计收口与多人落点修复已由制作人验收并封板；范围见 [220](220-cw-u0-design-and-manual-entry-placement-2026-09-13.md)。CW-U1 当时已完成统一 Squad 成员权威、正常加入／离队、现有共同移动适配、近场观察与正式存读档接线。

- **2026-09-13 历史状态：** CW-02 为 **Producer Accepted — 当前交付范围**，范围及明确延期见 [218](218-cw-02-pause-ownership-and-party-incapacitation-safety-exit-2026-09-12.md)。CW-03 新 Site／唯一旗核心闭环当时为 **Implementation Completed / Producer Acceptance Pending**；交接与正常玩法验收路线见 [219](219-cw-03-new-worldsite-and-flag-core-closure-2026-09-13.md)。
- **2026-09-12 Documentation only：** Continuous World 最终设计已由 [ADR-0032](43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)～[0034](43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) 确认并同步系统正文、Freeze 补丁、术语和索引；文档对齐见 [216](216-continuous-world-final-design-documentation-alignment-2026-09-12.md)。**Design: Confirmed；Documentation: Updated；Implementation: Not migrated / Partially present / Needs verification；Producer Acceptance: Pending。**
- 后续依赖顺序：稳定身份／动态资产存档／战前锚点／暂停与控制生命周期核查 → SiteCore 范围与建筑归属 → 同源遭遇与人物战 → 战内建筑战争、接管、OR 胜利和收尾 → 有限援军、顺序接替／全队死亡继承、预警 → 飞舟运输与统一 WorldMap 观察／下令。此顺序不是一次性编码授权。
- 玩家势力无人终局、舰战／甲板战、完整俘虏／赎金、无限远援和同城多核心明确延期。Outdoor 动态破坏物存档失败仍是未通过技术债，是后续战场往返依赖。

- **2026-09-10：** W1C **Accepted / Sealed**；当前阶段为 **W1D — Default Wilderness Continuous Surface Cutover**。W1C acceptance Surface 保留为诊断，不参与 normal runtime authority。

- **2026-09-09 历史记录：** [ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md) 当时将 Continuous Outdoor 记录为 Future；该“仅 Future／未授权迁移”的状态已由 2026-09-12 制作人最终设计决定替代。历史实现边界仍保留用于迁移核对。
- **2026-08-29：** Phase 5B **WorldMap↔LocalMap Travel View Takeover — Accepted / Sealed**：基线 dev_1 @ 47b3f89；AutoTravel 关图 **不再 Cancel**；LocalVisible 时 World Advance 停止；再开 WorldMap 从同一 Continuous Position 继续；多次开关无漂移 / Route / Destination 异常；人工验收 Assets/Scenes/LevelTester.unity；真源 [173](173-phase-5b-worldmap-localmap-travel-view-takeover-2026-08-29.md)。**未开始 Phase 5C。**
- **2026-08-29：** Phase 5A **Travel 代码清理 / Authority 收口**：行为基线 `dev_1 @ 47b3f89`；真源 [172](172-phase-5a-travel-cleanup-authority-consolidation-2026-08-29.md)。
- **2026-08-28：** Phase 3 **Accepted / Sealed**（用户正式确认）：FormalArmy 军事层收敛；PlayerParty 独立旅行 Authority；Continuous WorldPosition／Travel／Presence／Save-Load；已在 LevelTester 持续使用及 Phase 4 验收中实际验证；真源 [166](166-phase-3-formal-army-continuous-world-2026-08-27.md)／[167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)。
- **2026-08-28：** Phase 4 **Accepted / Sealed**：Battle Authority；真源 [171](171-phase-4-battle-authority-2026-08-28.md)。**未开始 Phase 5。**
- **2026-08-27：** LevelTester **Cheat Tools 统一整理入仓**：`HostLevelTesterCheatPanel` 替代 F3/F4/F8/F11/F12 等分散 Debug Panel；实现索引 [168](168-level-tester-cheat-tools-consolidation-2026-08-27.md)；操作真源 [114](114-level-tester.md)。
- **2026-08-27：** Phase 3 **收口入仓**：A2 Authority 第二轮；PP-Follower 跨图；主角营地独立 LocalMap；三支试炼山匪 + 伤亡夹具；实现索引 [167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)。
- **2026-08-27：** Phase 3 **主体入仓**：FormalArmy Continuous World + RPG-First Authority；F11 Debug；EditMode `FormalArmyPhase3AuthorityTests`；`PresenceHex==AnchorHex` 兼容收口；实现索引 [166](166-phase-3-formal-army-continuous-world-2026-08-27.md)。
- **2026-08-27：** Phase 2D **人工验收通过并封板**：Background Character World Travel Core；Loaded LocalMap Materialization；Site Departure 真实 Travel；Destination Canonicalization；F12 Debug + BGTRAVEL Trace；实现索引 [165](165-phase-2d-background-character-world-travel-2026-08-26.md)。
- **2026-08-26：** Phase 2C **人工验收通过并封板**：Continuous Player World Movement；Ordinary Hex Actual Connections；WorldSite Full-Footprint Boundary Connections；Surface Exit Trigger／Edge Transition／Overlap Resolution；实现索引 [164](164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)。
- **2026-08-26：** Phase 2B **人工验收通过并封板**（`c895d3d`）：PlayerParty Hex Travel／30×15 测试世界／Wilderness Fallback／Materialize；下一目标 Phase 2C。
- **2026-08-25：** Phase 2B 落地：PlayerParty Hex Travel（非 Fake Army）＋30×15 测试世界＋Wilderness Fallback＋LocalMap Materialize 闭环；「进入近景」为 Prototype／Debug UX。
- **2026-08-25：** Phase 2A **人工验收通过并封板**（`18600af`／`8d49bf4`／`61bca9a`）：PresenceHex／Character World Presence；**不含** Travel。
- **2026-08-25：** LocalMap Camera **最终规则**：仅 WASD Hard Follow；RTS／右键寻路不控镜头（[2K §1.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)）。
- **2026-08-25：** Phase 2A 代码落地：`WorldSite.PresenceHex`、Character World Presence 查询、Editor／Validation／Snapshot 最小扩展；青石荒村 Content 改为 4-Hex 且 Anchor≠Presence（验收用）。**不含** Background Travel／Combat。
- **2026-08-25：** Phase 1 人工验收通过并封板（`aa1ebb9`／`e683aab`／`8770fb0`／`961d0d2`）：Single Active／PlayerParty≤6／Follow／Switch／View≠Command。
- **2026-08-25：** [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) + [163 迁移计划](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) — RPG-First 文档真源。
- Architecture Freeze **v0.2**＋**ADR-0023**＋**ADR-0024（部分 superseded）**＋**ADR-0025**＋**ADR-0026**。
- **2026-08-24：** Multi-Hex footprint Runtime／Editor／Ch01（`0a40a86`）；Snapshot v6 JSON（`ff112cd`）；Purge 审计（162）。
- **2026-08-23：** [158](158-hex-world-content-authoring-pipeline-2026-08-23.md) HexWorld Pipeline；[155](155-hex-strategic-worldmap-migration-2026-08-23.md) Hex 迁移。
- Demo Runtime 继续冻结。旧 WorldMap **纯 RTS** 路径（139／152／154）视为 **Legacy Prototype**，迁移见 163。

### RPG-First 迁移分期（历史）

- [x] **Phase 0** 文档 + 架构审计 + Supersede — ✅
- [x] **Phase 1** Single Active Character／PlayerParty 控制模型 — **Accepted / Sealed**（2026-08-25）
- [x] **Phase 2** Background Character World Presence／Simulation + PresenceHex — **Accepted / Sealed**
  - **2A（已封板）：** PresenceHex Content／Runtime／Editor；`GetCharacterWorldHex`；AtSite 存 SiteId；Stop Follow 保 Presence；Snapshot `characterWorldPresences`；Background 不画 WorldMap 头像
  - **2B（已封板）：** PlayerParty World Travel MVP＋30×15 测试世界＋Minimal Wilderness Fallback＋LocalMap Materialize；**非** Background Travel
  - **2C（已封板）：** Continuous WorldPosition；Actual Surface Exit Connections（Ordinary Hex + WorldSite Full-Footprint）；WorldMap↔Wilderness 双向投影；LocalMap Edge→Neighbor；Canonical Exit Trigger Zone + Overlap Resolution；Close WorldMap = Cancel＋Expand；**无** PreciseWorldDestination；见 [164](164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)
  - **2D（已封板）：** Background Character Travel Core；Scheduler；Loaded LocalMap Materialization；Site Departure；Destination Canonicalization；Save/Load；F12 Debug；见 [165](165-phase-2d-background-character-world-travel-2026-08-26.md)
  - **后续 Backlog：** Background Combat／Autonomous AI Travel／Policy；Directional Site Entry 等
- [x] **Phase 3** FormalArmy 职责迁移 — **Accepted / Sealed（2026-08-28）**
  - 真源：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md) + [167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)
  - FormalArmy 军事层；PlayerParty 独立旅行；Continuous WorldPosition／Travel／Presence／Save-Load／Authority 边界
  - 验证：LevelTester 持续使用 + Phase 4 实际依赖；用户 2026-08-28 正式确认封板
  - **Backlog / Deferred：** FormalArmy WorldMap Marker 连续表现、Autonomous AI Order、更复杂 Army AI／主动战争／Army Capacity 等
- [x] **Phase 4** Manual Battle Permission — **Accepted / Sealed（2026-08-28）**
  - 正式真源：[171](171-phase-4-battle-authority-2026-08-28.md) §1
  - Battle Trigger＝Initiator/Defender **共边相邻**；BattleArea／SupportArea／Participants／Manual
  - Hex topology Authority（Odd-R↔axial；含 CollectHexLine）已修
  - **Deferred / Future Regression：** 敌军主动攻击 Retreat 人工验收；AI vs AI 主动接战人工验收（缺战略 AI）
  - **Deferred（原）：** Legacy 战斗入口删除、PlayerParty 作 Initiator
  - 附带验收：WorldMap 列表滚动收紧、Zoom In 扩大、Cheat Tools 与 F10 解耦
- [x] **Phase 5** Continuous LocalMap ↔ HexWorld Transition — **历史阶段已由 MAP／LEGACY-FINAL 后续决策替代并封板**
- [ ] **Phase 6** WorldMap Auto Travel — **Not Started**
- [ ] **Phase 7** Wilderness LocalMap — **Not Started**
- [ ] **Phase 8** Character Policy V1 — **Not Started**
- [ ] **Future** Flight／Sect Mission Board／Advanced AI／Territory Tint／Dynamic Bandit

### 战略战斗时间纪律（2026-08-21）

- [x] ADR-0023 采纳；冲突文档修订；影响审计 [144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)
- [x] Phase A～F：[145](145-adr0023-phases-af-acceptance-2026-08-21.md)（自动化断言已补）
- [x] Host UX 打磨：[146](146-adr0023-host-ux-polish-2026-08-21.md)（支援半径／战后条／自动结算弹窗／山匪可见）
- [x] Host 手操签收 145／146（2026-08-21）
- [x] 接战点无瞬移＋弥留残留战场＋支援半径滑块：[147](147-battlefield-linger-no-teleport-2026-08-21.md)
- [x] 大地图弥留批 1～3：[148](148-worldmap-linger-incap-ux-2026-08-21.md)／[149](149-lingering-battlefield-batch2-2026-08-21.md)／[150](150-lingering-battlefield-batch3-offer-2026-08-21.md)
- [x] RTS 左右键纪律：[152](152-worldmap-rts-click-discipline-2026-08-22.md)（手操待签）
- [x] 弥留／自动战宏观 + 接战名单 + 追击撤退：[153](153-lingering-remnant-macro-presentation-2026-08-22.md)（手操待签）
- [x] Formal Army RTS 收束 + 追击 backlog：[154](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)（**追移动敌 延期**）
- [x] Hex World Content Pipeline + WorldGraphEditor Hex 化：[158](158-hex-world-content-authoring-pipeline-2026-08-23.md)（**手操 延期**）
- [x] 战略层 Host 双入口（角色／军队列表；**Node 组军已删除**）：[153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)（Unity 手操 延期）

### 样例关可玩弧（2026-08-02）— **自动化 Completed／手操签收中**

- [x] Demo 手感对齐关 [93](93-demo-parity-level-acceptance-report.md)
- [x] 内容打断系统 [95](95-content-interrupt-system-plan-v0.1.md)／[96](96-content-interrupt-system-acceptance-report.md)
- [x] 章节制作指南合并 [94](94-chapter-full-production-and-sample-guide.md)
- [x] 2G 觉醒弧 Data＋UX 引导交付 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)
- [ ] 制作人手操签收 `DemoParityHost`
- [ ] 用 `LevelTester` 换 mapLayout／scenario 做节点逻辑试玩（见 [114](114-level-tester.md)）
- [ ] 正式第一章文案／战斗夺权切片（另开）

## M2.5 — 架构冻结

### 文档包

- [x] Freeze v0.1 文档包 + 审计报告 `50`
- [x] **Freeze v0.2 修补**
- [x] ADR-0017～0022
- [x] ADR-0023 Manual Encounter 冻结 WorldTick（2026-08-21）
- [x] 通读指南＋ADR 索引；飞书映射
- [x] Freeze v0.2 已作为 Core M1 依据落地（验收完成）

### Core Milestone 1（ADR-0022）— **Completed**

- [x] 实施计划 v0.2 批准并执行（[51…v0.2](51-core-milestone-1-implementation-plan-v0.2.md)）
- [x] 阶段 1～10 全部完成
- [x] EditMode **54/54**；Integration Test **PASS**
- [x] Git：`1688187` … `e8340da`（含整合测与 meta）
- [x] **未做（按范围）：** 跨 Region 离屏、完整势力领导、真战斗、完整 NPC AI、Mods/ 加载、大地图战争、扩 Demo

### Data Pipeline Milestone 1 — **Completed**

- [x] 规划 v0.2 已批准并编码（[53…v0.2](53-data-pipeline-milestone-1-plan-v0.2.md)）
- [x] M1-A Definitions：`3ee16e1`
- [x] M1-B CSV 导入校验：`90f89ea`
- [x] 完成标准已在 `53` §9 勾选

### Vertical Slice 0.1 — **Completed**

- [x] Bootstrap：`6897807`
- [x] Cultivation Slice：`64cb3ab`
- [x] 验收报告 [54](54-vertical-slice-0.1-acceptance-report.md)；EditMode **73/73**（Cultivation 完成后）

### Vertical Slice 0.2 — **Completed**

- [x] 计划／确认／Phase A–C；验收 [56-acceptance](56-vertical-slice-0.2-acceptance-report.md)；EditMode **89/89**

### Vertical Slice 0.3 — **Completed**

- [x] 计划 `57`；验收 [58](58-vertical-slice-0.3-acceptance-report.md)；EditMode **100/100**

### Vertical Slice 0.4（Unity Playable Host）— **Completed**

- [x] 规划：[59](59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md)
- [x] V4-A～H 独立 commit；验收 [61](61-vertical-slice-0.4-acceptance-report.md)
- [x] EditMode 全绿（含一日可玩整合测）

### Vertical Slice 0.5（社会／人格 Alpha）— **In Progress**

- [x] 计划：[60](60-vertical-slice-0.5-social-alpha-plan-v0.1.md)；现状总表：[62](62-project-status-2026-08-01.md)
- [x] V5-A 人格档案：`e443eee`（`PersonalityProfileComponent`）
- [ ] V5-B～G：Ledger → 开局关系 → 招募 → NPC 自主 → 社会 Tick → Alpha 验收
- [ ] 禁止无计划扩战斗／地图／正式 UI；Snapshot 含关系前先确认 schema


## M0 — 定方向

目标：把不确定性砍掉，让后面的工作不再摇摆。

- [x] 竞品系统拆解（鬼谷八荒、了不起的修仙模拟器）
- [x] 文档仓库与维护规范建立
- [ ] 回答 `01-vision.md` 的 Q1–Q5
- [ ] 定稿差异化决策（`14-borrow-and-differentiate.md` 第 2 节）
- [x] 确定 Unity 版本与渲染管线（2022.3.6f1 Built-in，ADR-0001）
- [ ] 确定正式 UI 方案（ADR-0009；原型暂用最简 GUI）

完成标准：能用三句话说清"这是什么游戏、玩家在干什么、和竞品哪里不一样"，且自己一周后看还认同。

## M1 — 纸上原型／Demo 范围

目标：不写代码就验证核心循环是否有趣；并冻结第一个可玩 Demo 的范围。

- [x] 核心循环与时间设计文档（`20-systems/21-core-loop-and-time.md`）
- [x] Demo v0.1 范围文档（`40-process/45-demo-v0.1.md`）
- [x] Demo v0.1 美术资源需求表（`40-process/46-demo-v0.1-art-assets.md`）
- [x] Demo v0.1 AI 美术生成批次计划（`40-process/47-demo-v0.1-ai-art-batches.md`）
- [x] 第一批最小可用素材接入清单（`40-process/48-demo-v0.1-minimum-art-integration.md`）
- [ ] Demo 荒村区域草图与六阶段体验脚本
- [ ] 第一次突破事件最小脚本
- [ ] 主管战／夺府体验脚本
- [ ] 修炼与境界数值模型（Excel/表格算一遍，看成长曲线）
- [ ] 战斗规则手推（纸面模拟 3 场，看构筑是否有意义）

完成标准：能向别人口述一局 Demo 的完整过程（凡人→修炼→突破→隐藏→反抗→占领→管理），对方听完想玩。

## M2 — 技术骨架（历史阶段）

目标：把架构约束落成可运行的空壳。

- [x] Unity 工程目录与 Demo 原型生成器
- [x] 可替换 Sprite、玩家／NPC／地块／建筑 Prefab 结构
- [x] 三人选择与移动、荒村灰盒场景
- [x] 灰盒尺度修正（80×50）+ 镜头缩放 + Visual 0.6
- [x] GameClock（暂停／1x／2x／5x）与只读时间表验证
- [x] 基础荒村生活循环（任务／资源／工作区／主管愤怒显示）
- [x] 统一角色行动框架（M3.5：右键下令、走近、工作/修炼、中断）
- [x] RTS 工作指派（Idle／Moving／Working／Cultivating）
- [x] 秘密修炼（M4：灵地、修为、暴露、敛息草；无突破惩罚）
- [x] NPC 基础日程（M5：守卫 Patrol/Rest、主管昼夜、村民群体状态）
- [x] 24 小时时间表网格（测试可改）+ 地块悬停灵气
- [ ] 程序集边界（Core / Data / Unity / Tests）
- [ ] 配置表加载管线（CSV → 运行时对象，含报错定位）
- [ ] 属性与 Modifier 管道（含来源溯源）
- [ ] Tick 时间系统
- [ ] 存档读写 + 版本号
- [ ] Core 层单元测试跑通

完成标准：能在没有任何美术的情况下，用 Console/最简 UI 跑通"修炼 → 突破 → 存档 → 读档"。

## M2.5 — 架构冻结（当前）

- [x] Freeze v0.1 文档包 + 审计 `50`
- [x] **Freeze v0.2 修补** + ADR-0017～0022
- [x] `24` 对齐 World／Region／LocalMap
- [ ] 人工审核通过 v0.2
- [ ] 第一次突破事件规格；炼气术法清单

### Core Milestone 1（ADR-0022，审核后另开实现任务）

**做：** Id、WorldTick、IRandomSource、ContentPackage 基础、Entity 基础、AttributeModifier、DomainEvent、Order／Action、Snapshot、单 Region 验证。

**不做：** 跨 Region 离屏、完整势力领导、真战斗、完整 NPC AI、Mods/ 加载、大地图战争。

### Mod Ready（见 `36`）

- [x] 阶段 A 契约
- [ ] 阶段 B～E（M1 后）

完成标准：按 v0.2 契约即可搭 Core M1，无需再问关系写哪、时间几套、Focus 失能怎么办、开局隶属谁、地图几层。

## M3 — 垂直切片

目标：一个小而完整、真的好玩的闭环。

- [ ] 一条修行路线（炼气 → 筑基）
- [ ] 一套战斗（含构筑选择）
- [ ] 一个区域 + 10 个事件
- [ ] 最小可用 UI
- [ ] 一次死亡/传承

完成标准：**你自己愿意连续玩 90 分钟**，且能说出至少一个"这局发生的故事"。

### 2026-09-24 当前推进点

- [x] QUEST-INSTANCE-01：制作人人工验收通过，状态 **Producer Accepted / Sealed**。
- [x] VASSAL-WORK-01：直接附庸劳动、仓储／建造隔离与解除附庸失权已由制作人人工验收，状态 **Producer Accepted / Sealed**。
- [x] VASSAL-WORK-01-P1：首次 New Game 右键、empty selection fallback 与合法多人农作已由制作人人工验收，状态 **Producer Accepted / Sealed**。
- [x] SUCCESSION-01：全 Party 真死亡后的玩家势力继承、精确位置接管、Continuous Surface 重锚与无候选等待态已于 2026-09-24 **Producer Accepted / Sealed**。
- [x] CONTROL-HANDOFF-01：全员弥留时的 Emergency Takeover、Recovery Squad、Separate Space 释放与 v8 恢复已于 2026-09-24 **Producer Accepted / Sealed**。
- [x] CONTROL-HANDOFF-01-P1：Committed report-only spatial boundary、战后即时目的地 materialization 与 Camera／Selection barrier 已于 2026-09-24 **Producer Accepted / Sealed**。
- [x] **DYNAMIC-DISCOVERY-01 — Dynamic WorldObject + Discovery Foundation**：2026-09-24 **Producer Accepted / Sealed**。
- [x] **MAP-COORD-01 — WorldMap World Coordinate Readout**：2026-09-24 **Producer Accepted / Sealed**。
- [ ] 前轮：**DELAYED-EVENT-01**（Implementation Complete / Producer Acceptance Pending）。
- [ ] 当前：**SOCIAL-QUEST-01**（Producer Accepted / Sealed）。
- [ ] 下一阶段 3：Full trading。
- [ ] 下一阶段 4：Equipment / crafting。
- [ ] 下一阶段 5：Production / logistics。
- [ ] NPC AI 最后统一规划；在上述阶段之前不提前展开。

## M4 — 横向扩展

前提：M3 验证通过。内容量扩充、系统补齐、美术升级。此阶段任务待 M3 后再拆。

---

## 阶段推进纪律

- 上一个里程碑的完成标准没达到，不进下一个。
- 每完成一项，在 `42-devlog.md` 追加一条记录。
- 里程碑本身可以改，但改动要写进 devlog 并说明原因。

## 2026-09-15 — Recovery Spot + Player Party Combat Cheats

- [x] 新增 2×2、粗木 5 的正式恢复处建筑，并在荒村 authored Content 预放三处。
- [x] 恢复处建造复用 Actual Control footprint authority、Continuous materialization 与通用室外建造资产快照。
- [x] 新增独立 `RecoveryAction`：30 分钟后恢复当前生命/灵力到现有上限；普通 Rest 与突破不回满。
- [x] LevelTester 战斗页增加 PlayerParty 攻击 +10、最大生命 +50 并回满、生命/灵力回满。
- [ ] 制作人 Unity 人工验收建造预览、左右键交互、角色接近、6 ticks 进度、Save/Load 与 HUD 即时刷新。
