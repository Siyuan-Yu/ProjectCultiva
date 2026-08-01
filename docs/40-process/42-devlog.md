# 开发日志

> **倒序追加：最新的记录写在最上面。**
> 这是项目的历史记录，用于跨设备/跨时间恢复上下文，以及交接给他人时说明"为什么代码长这样"。
>
> 每次有实质进展就追加一条。宁可短，不可漏。

---

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
