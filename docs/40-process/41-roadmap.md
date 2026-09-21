# 路线图

> **2026-09-19 当前状态（Handoff）：** [MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)／[MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)／[MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) 均已 **Producer Accepted / Sealed**。
> **当前 WIP（2026-09-20）：LEGACY-FINAL-A — Unified NPC Squad World Motion / FormalArmy Runtime Retirement，Implementation Complete / Producer Acceptance Pending。** 实施记录见 [248](248-legacy-final-a-unified-npc-squad-world-motion-2026-09-20.md)。MAP-01～MAP-04、SPACE-01、CW-10 与 CW-10.5 均已 **Producer Accepted / Sealed**。A 验收封板后依次进入 LEGACY-FINAL-B、LEGACY-FINAL-C；A/B/C 全部完成前不得宣称 Legacy migration complete。
> 未来新会话请从 [247 Project Handoff — Continuous World Current State](247-project-handoff-current-state-2026-09-18.md) 开始（Milestone 表、Current Architecture、Known Issues、Do Not Regress、Resume Order、可复制上下文）。

> **2026-09-17 地图进度（历史）：** [MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md) 与 [MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) 均已 **Producer Accepted / Sealed**。主 Continuous Surface 的正常 Gameplay 使用精确世界位置与 Surface authority；Hex/Outdoor LocalMap 旧路径仍保留为 legacy/derived compatibility。MAP-04 物理清理正在实施；当前状态见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md)。

> **Editor 工具链／旧 Content 迁移：** [ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 已锁定方向，MAP-01/MAP-02/MAP-03 已完成；下列为后续边界，而非本轮开工授权：
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
> 现状（2026-09-15 实测，未变）：External Editor 仍是 10 个独立工程、`publish.ps1` 硬编码发布列表、输出 `Apps/<Editor>/<Editor>.exe`；Content 侧仍是 37 mapLayout／35 localPlaceSet／2 hexWorld／1 worldRegion，主 Surface 646 个 chunk 的 source 全部是 wilderness fallback 图。**不得把 ADR-0037 读作「工具链或 Content 已迁移」。**


> **当前唯一主线（2026-09-20）：** 已封板 CW-U0～CW-10.5、MAP-01～MAP-04 与 SPACE-01；当前实施 LEGACY-FINAL-A，随后为 LEGACY-FINAL-B／C。

> 状态：CW-10 与 CW-10.5 均为 **Producer Accepted / Sealed（2026-09-20）**。保持既有编号，不重排 CW-06 / CW-07。
>
> **工具链与地图方向：** MAP-01/MAP-02/MAP-03 已验收并封板；MAP-04（legacy content/editor retirement）正在实施。WorldGraphEditor／RegionEditor 已删除；MapEditor／LocalPlaceEditor 只维护独立 LocalMap。

## 当前阶段说明

- **当前产品：** SiteId 公库已替代旧 Settlement 原型；NPC 日程农作逐格消费实时行政授权，真实收获进入当前管理 Site 公库。固定接管、公库保留、可拆旗失效、同势力管理接续与存读档已贯通。
- **下一步：** 制作人按 237／239 的正常玩法路线验收青石荒村接管前后 NPC 劳作、公库变化、战略物资访问与储藏室，以及 Save/Load。后续只记录更完整仓储物流、税赋、跨 Site 运输和离屏生产设计，不在本轮预实现。
- **地图后续：** MAP-03 已由制作人人工验收；MAP-04 物理清理目前 **Paused**，Chunk 的 MapLayout 桥和两套旧 Editor 已移除，Scenario、Site opening、Hex WorldMap 等仍待迁。**恢复顺序：SPACE-01 final hardening → SPACE-01 验收/封板 → MAP-04 final consumer audit → 物理删除 → 验收 → seal。** 当前审计、剩余 consumer 与未通过 gate 见 [247](247-project-handoff-current-state-2026-09-18.md) §13／§15 与 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md)。方向边界见 [ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

- **不阻塞 CW-05 的 backlog：** FormalArmy／BattleOffer／Hex support 深层清理；NPC Squad macro movement 去 FormalArmyWorldMotion；Level 2／3；Encounter 介入参数调优；飞舟；NPC 自动攻城与普通建筑战争。

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

### RPG-First 迁移分期

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
- [x] **Phase 5** Continuous LocalMap ↔ HexWorld Transition — **In Progress**（5A/5B Accepted / Sealed；5C 未开始）
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

## M2 — 技术骨架（当前阶段）

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
