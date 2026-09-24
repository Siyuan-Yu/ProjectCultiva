# Project Handoff — Continuous World Current State

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

当前 SOCIAL-QUEST-01：Producer Accepted / Sealed。下一阶段 Full Trading（本轮不启动）；之后 Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。Knowledge / Rumor / Information Propagation：Future / Only if gameplay later requires it。


## 2026-09-25 最新接续摘要：DYNAMIC-DISCOVERY-01＋MAP-COORD-01 FINAL SEAL

[DYNAMIC-DISCOVERY-01](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md) 与 [MAP-COORD-01](262-map-coord-01-worldmap-world-coordinate-readout-2026-09-25.md) 已于 2026-09-24 完成制作人人工验收，状态均为 **Producer Accepted / Sealed**。`WorldOpportunity` 现同时支持既有 NPC 与不创建 Character Entity 的动态 WorldObject；动态物体以 Opportunity Instance 派生稳定 `WorldObjectInstanceId`，直接保存 `SurfaceId + exact WorldPosition`，由独立 Host transient registry 呈现与拾取，不进入导航阻挡。

发现模式固定为 `worldVisible`、`publicNotice`、`hiddenUntilDiscovered`。隐藏物体在 Domain 中先存在，任一存活 PlayerParty 成员进入 authored radius 后永久揭示；发现前不创建 Presentation、Interaction、Activity、Toast 或定位入口。onInspect 通过 `worldObjectKind=opportunityObject + worldOpportunityId` 绑定模板，`resolveCurrentOpportunity` 显式终结当前实例并与 Activity 变更共同参与 Outcome transaction；Inspect 本身不等于解决。

Snapshot 当前为 **v9**，持久保存 SpawnKind、稳定物体身份、精确坐标与发现状态；v8 及更早版本明确不兼容，不猜测迁移。OpportunityEditor、EventEditor 与 LevelTester 三条稳定验收物体已接通。制作人确认 Hidden 石碑的未发现隐藏、距离发现、调查保留、显式解决与 Activity 主链通过。WorldMap 另以同一 Surface 投影显示 pointer／player exact coordinate 与自适应 major ticks，不泄漏隐藏 Opportunity。下一里程碑为 `KNOWLEDGE-DELAY-01 — Character/Faction Knowledge + Delayed Content Event Foundation`，状态 **Planned**。

## 2026-09-24 已封板基线：PLAYER CONTROL CONTINUITY / SUCCESSION FINAL SEAL

制作人已完成人工验收并确认“这一轮很完美”。SUCCESSION-01、CONTROL-HANDOFF-01 与 CONTROL-HANDOFF-01-P1 均为 **Producer Accepted / Sealed**。现行控制连续性分三级：当前 Party 内仍有合法成员时按固定顺序接替；无可控成员但仍有生者时走 Emergency External Handoff，旧生者留在原地 idle Recovery Squad；全员 Dead／Removed 时走 True-Death Succession，旧成员保持 corpse／singleton authority。外部候选共用统一 policy，以 CombatPower 最高、稳定 EntityId tie-break，并在 membership mutation 前捕获真实 `SurfaceId + exact WorldPosition`。

接管者若来自 NPC Squad，只拆本人并保留其余 roster／motion。跨 Surface 或远出旧 loaded 5×5 时以接管者 chunk hard re-anchor；目的地 Surface、邻域 Site／Squad／Population 与 successor EntityView 同步就绪后才切 Camera／Selection。CharacterEncounter `Preparing / Active / ReadyToEnd` 持有 spatial authority；`Committed` 仅保留战报，不阻断 ordinary world，`CloseReport` 不承担世界重载。Separate Space handoff 不执行正常 Leave，旧伤员／尸体保持洞内持久位置。该封板时点使用 Snapshot v8；当前全局 schema 已由 DYNAMIC-DISCOVERY-01 提升为 v9，控制连续性字段 shape 未变。

旧“只有全员真正死亡才允许切到势力其它人物；全员弥留保持 TemporarilyUnavailable”的规则已标记 **Superseded on 2026-09-24**，现行真源为 [ADR-0039](43-decisions/ADR-0039-external-faction-control-handoff.md)、[2K §4](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[259](259-succession-01-player-faction-succession-2026-09-24.md) 与 [260](260-control-handoff-01-emergency-faction-control-transfer-2026-09-24.md)。DYNAMIC-DISCOVERY-01 的当前状态见本页顶部；NPC AI 继续最后。

## 2026-09-24 实施时点历史：CONTROL-HANDOFF-01（状态已由顶部 Final Seal 取代）

制作人修订 SUCCESSION-01 最终产品规则：[ADR-0039](43-decisions/ADR-0039-external-faction-control-handoff.md) 将外部势力控制转移统一为 Emergency Takeover 与 Succession。当前 Party 无任何可控成员但仍有生者时，如果玩家势力存在外部合法人物，也会在战斗正式提交后接管；旧生者保留在原地 idle Recovery Squad，Dead／Removed 继续 singleton/corpse retirement。无候选时不拆旧 Party，等待恢复。

实现记录见 [260](260-control-handoff-01-emergency-faction-control-transfer-2026-09-24.md)。候选筛选、CombatPower/EntityId 排序、精确位置、Surface re-anchor 与 Separate Space presentation release 只有一套；Emergency 使用独立 DomainEvent/Toast。Snapshot 仍为 v8。LevelTester “战斗”新增“CONTROL-HANDOFF-01：使当前 Party 全员弥留”。**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新为 Producer Accepted / Sealed。

P1 已修正战后即时目的地物化：CharacterEncounter `Committed` 仅保留战报，不再阻断普通 Continuous Surface；独立战场退出在等待外部接管时跳过旧 Party Surface 重建，并由唯一 Host 呈现门槛同步完成 C 的 Surface、邻域人口与 EntityView 后再切 Camera／Selection。真正死亡旧成员 singleton 注册保持每人一次，双死亡回归已覆盖。无需关闭战报或点击 HUD 头像触发刷新。两项状态仍为 **Implementation Complete / Producer Acceptance Pending**。

## 2026-09-24 实施时点历史：SEAL-20260924 / SUCCESSION Final Audit（状态已由顶部 Final Seal 取代）

QUEST-INSTANCE-01、VASSAL-WORK-01 与 VASSAL-WORK-01-P1 已由制作人人工验收并正式 **Producer Accepted / Sealed**。P1 的最终 opening context-menu 问题属于 Host Active/View/Selection 初始化时机，并非 Direct Vassal Work Authorization 失败。

SUCCESSION-01 主体审计通过，未重建系统。仅修正来源 NPC squad 在 successor 同时为 command target 时的悬挂目标；无候选扫描从逐帧改为 0.75 秒 throttle，首次进入／显式刷新／Snapshot restore 仍立即；LevelTester 护卫限定玩家势力。候选、精确位置、远距无捕获 Surface re-anchor、物化后 Camera/Selection、Separate Space wipe 与 Snapshot v8 行为保持。**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新。

## 2026-09-24 最新接续摘要：VASSAL-WORK-01-P1

制作人实际验收证明 Direct Vassal Labor Authorization 已正确生效；残余故障是 Host opening selection 时机。Continuous New Game 曾在 Active EntityView materialize 前选中，Registry gate 令其失败，最终物化后未补选；Context Gate 接受空 selection，但农田旧入口没有 Worker。

P1 已在最终 opening population Refresh/Prune/Spawn 后仅为空 selection 补选已物化 Active，并把 party-work actor 语义集中到 `HostPlayerMoveCommandGate`：合法非空选择使用所选 Party members，空选择隐式使用可行动 Active，非法非空选择不回退。农田开工为 0 时会显示明确反馈。Snapshot restore 既有 rebind 未改，Vassal Work Authorization／仓储／建造／收获归属均未改，Snapshot 仍为 v8。状态 **Implementation Complete / Producer Acceptance Pending**；离线编译 `ALL_OK`，未运行自动测试或 Unity，未 stage／commit／push。

## 2026-09-24 最新接续摘要：VASSAL-WORK-01

[VASSAL-WORK-01](260-vassal-work-01-direct-vassal-labor-access-2026-09-24.md) 已完成实施，状态 **Implementation Complete / Producer Acceptance Pending**。严格 `WorldAdministrativeAssetAuthorizationService` 未放宽；新增 Work Authorization 只允许实际管理势力自己或其直接附庸劳作，并明确区分 `AllowedAsManager`／`AllowedAsVassalWorker`。玩家开工、逐格复核、Host Hover、NPC WorkArea 与 NPC schedule 收获均已统一接线。

Ch01 真实 opening 已有 `base:faction_player → base:sect_huangcun_labor` 直接附庸，故现有荒村宗主粮田／药田会自然开放劳动，不含 ID hardcode。玩家手控产物仍进背包，NPC schedule 产物仍进宗主实际 Site 公库；宗主仓储、建造、拆除、管理、住房、时间表和 ControlCore 继续隔离。解除附庸或 War 后权限即时失效。Snapshot 仍为 v8；离线编译 `ALL_OK`，未运行自动测试或 Unity，未 stage／commit／push。完成本项人工验收后继续既有 SUCCESSION-01 验收。

## 2026-09-24 实施时点历史：SUCCESSION-01（状态已由顶部 Final Seal 取代）

QUEST-INSTANCE-01 已由制作人人工验收通过并封板，正式状态为 **Producer Accepted / Sealed**。旧文档中的 Pending 是实施时点历史，不回写。当前最新实现是 [SUCCESSION-01](259-succession-01-player-faction-succession-2026-09-24.md)：只有旧 PlayerParty 全员真正 Dead/Removed 才从玩家势力其它真实人物中选择继承者；战力最高优先、同战力较小 EntityId 优先，普通队内 Active 接替仍保持固定 Party 顺序。

继承者在改变 Squad 前捕获精确 Surface/WorldPosition/Site/来源 Squad；旧死亡 Party 退为 singleton，新 `squad:player` 仅含继承者。NPC squad 只分离继承者。Host 对跨 Surface 或超出 loaded 5×5 的位置执行无旧位置捕获的 hard re-anchor，并在 EntityView 就绪后镜头/选中。Separate Space 全灭只释放玩家表现 ownership，不把尸体送回洞口。无候选继续保持 AwaitingSuccession，可存档并在 world shell 恢复后重试。Snapshot schema 仍为 v8。

LevelTester 反引号开发工具 →“战斗”提供 SUCCESSION-01 A/B 候选准备与正式全灭按钮。该段记录的**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新。后续顺序为 Dynamic WorldObject / Discovery → Knowledge + delayed event foundation → Full trading → Equipment/crafting → Production/logistics → NPC AI last。

## 新会话恢复摘要（2026-09-25；最新状态以顶部 Final Seal 摘要为准）

### 项目与阶段

PJCultiva／XianXia 是一款以**具体角色的修仙成长**为核心，结合同行小队、连续探索、人物关系、实时暂停战斗和领地经营的单机 2D RPG。已封板基线保持不变；QUEST-INSTANCE-01、SUCCESSION-01、CONTROL-HANDOFF-01、DYNAMIC-DISCOVERY-01 与 MAP-COORD-01 均已 **Producer Accepted / Sealed**。当前 Snapshot schema 为 v9；下一里程碑 KNOWLEDGE-DELAY-01 为 Planned。

### DYNAMIC-DISCOVERY-01 当前实现（2026-09-24）

[261](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md) 增加动态 Opportunity WorldObject、三种发现模式、稳定模板 onInspect、显式 transaction-safe resolve、Activity 生命周期和 Snapshot v9。隐藏物体发现前不泄漏任何 Host 表现或 Activity，发现后永久可见并可定位；worldVisible 不入 Activity，publicNotice 保持即时公开。OpportunityEditor 与 EventEditor 已覆盖全部新字段，LevelTester 提供三条稳定验收实例与清理入口。状态 **Producer Accepted / Sealed**。

### QUEST-INSTANCE-01 当前实现（2026-09-24）

[258](258-quest-instance-01-dynamic-character-commissions-v1-2026-09-24.md) 将人物委托从“模板 ID＋全局 flag”升级为稳定 `QuestInstanceId`。同模板 NPC A／B 可分别接取、交付、领奖、放弃、失效和存读档；`@actor`／`@target`／`@issuer` 精确指向真实 Entity，关系继续写入 `RelationshipLedger`。当前 Snapshot schema 为 v8，v1～v7 要求新开局。固定任务保留既有模板单份语义。LevelTester 内容页可确定生成两名同模板临时行商。制作人已于 2026-09-24 完成双实例完整路线人工验收，状态 **Producer Accepted / Sealed**。

### SAVE-01＋STRATEGIC-STOCK-01 当前封板（2026-09-24）

[257](257-save-01-content-progress-persistence-v1-2026-09-23.md) 的 Content Progress 与战略库存封板继续有效；QUEST-INSTANCE-01 已把当时两条 prototype flag 流替换为结构化人物委托。当前 Snapshot 为 v9，v8 及更早版本明确拒绝；ContentProgress 与资源访问 shape 未变，仍保持 PartyInventory＋可访问战略库存的既有语义。

### Continuous Surface Streaming Radius 2（2026-09-23）

Core `ContinuousSurfaceStreamingPolicy` 是唯一 active-neighborhood policy：radius 2、diameter 5。`ContinuousOutdoorSurfaceRuntime.InitializeNeighborhood`、`RequestNeighborhoodTransition` 与 `ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood` 共用该值；完整邻域 25 chunks，authored edge 可少于 25。相邻 crossing 仍 staged 且每 Update 最多 Build 1，典型新增 5 chunks；initial activation／Snapshot presentation rebuild 最多同步 Build 25。Chunk metric、player-centered authority 与 GridPathfinder 算法未改变；当前 Snapshot schema 为 9。

### EVENT-02A 当前封板（2026-09-23）

[256](256-event-02a-persistent-world-activity-feed-2026-09-23.md) 将 `publicNotice` 从 Toast-only 改为持久 `WorldActivityBoard` authority：Active／History、unread、最近 100 条与严格 Opportunity source 一致性。其原字段 shape 在当前 Snapshot v9 中保持；DYNAMIC-DISCOVERY-01 另使已发现 `hiddenUntilDiscovered` 进入 Activity，并让动态物体定位使用实例精确坐标。worldVisible 仍不入栏，Toast 仍只在 Activity 首次创建时播放一次。EVENT-02A 原范围已 **Producer Accepted / Sealed**。

### EVENT-02 当前封板（2026-09-23）

[255](255-event-02-world-opportunity-director-v1-2026-09-23.md) 建立独立于旧 `OpportunitySite` 的动态 NPC 机会链：当前 Surface 每日低频 refill、SpawnTable 单抽、合法可走精确坐标、真实 NPC/WorldPresence、通用 Continuous materialization、EVENT-01 onTalk 与到期 Removed。该 V1 范围已 **Producer Accepted / Sealed**；“不支持 hidden 或动态 WorldObject”是当时范围，现已由 DYNAMIC-DISCOVERY-01 的 additive 扩展取代。当前 Snapshot v9 同时保存 NPC 与动态物体实例。

### EVENT-EDITOR-V2 当前增补（2026-09-23）

[254](254-event-editor-v2-visual-flow-authoring-2026-09-23.md) 将 EventEditor 改为 Browser＋Visual Steps Graph＋Inspector。Event 仍是独立 Template；按对象只是 binding projection。Working copy、dirty、undo/redo、自动布局、错误定位与 `Authoring/EventEditor/layouts.v1.json` 已接通。EVENT-02 后续增加 tag／Opportunity onTalk binding 与通用人物模板投影；Graph authority 未变。当前 narrative runtime 已进入 Snapshot v8。

制作人已完成 Graph、Step/Choice 连线、inline authoring、Priority/Topic/Repeat、保底与 Once、dirty、全局人物来源、可读 Character Picker、NPC／WorldObject authoring 的实际验收。最后剩余的“重启后不恢复人物来源”已由 final patch 以 editor-local、按 Package Root 的 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json` 关闭；当前状态为 **Producer Accepted / Sealed**，不继续拆 V2.7/V2.8。

V2.1 在原 Graph 上补齐 input/output port、Bezier 箭头连接、连接选择/删除、拖空白选择 Speaker 后创建、节点内 Speaker/正文/Choice 编辑、按内容估算间距与专注模式。仍只写同一 Steps working copy 和 editor-only layout；Runtime、ContentEvent schema、Priority/repeat/Host dialogue 均未改。状态仍为 **Implementation Complete / Producer Acceptance Pending**。

V2.2 修复打开/切换 Event 时旧 Inspector 控件误写新 Session 的 dirty 根因，并把 dirty 改为 Working JSON＋layout 相对 clean baseline 的差异判断；Undo/Redo 或手工改回 baseline 会恢复 clean，新建草稿首次保存前保持 dirty。Browser fallback 分类收紧为无条件、可重复、Priority 0 的 onTalk；五条将老条件状态对话统一 Priority 10。未改 Runtime resolution 或 Snapshot。

V2.3 进一步确认 sparse JSON 默认字段才是残余 false dirty 根因：新增 editor-only document normalizer，在 Existing/New/Legacy Convert 的 Session baseline 之前 canonicalize Event/Step/Choice 默认字段；optional binding 仍保持 sparse。Graph refresh 改为只读 layout，不再在 render 阶段补节点或 prune。Runtime Loader、schema 与磁盘源内容的自动写回规则未改。

V2.4 删除“状态对话/特殊对话/普通对话”等伪类型，只保留由现有字段组合表达的显式保底：onTalk、具体 NPC、P0、repeatable、无 Event Conditions。Inspector toggle 负责约束字段，同 NPC 唯一性在创建/切换与保存验证两层检查；Browser 改为“★ 保底”加 Priority/Conditions/Repeat factual badges。EVENT-01 acceptance 内容经引用审计仍自包含，但专项 Pending，故未删除。

### EVENT-01 Final 当前增补（2026-09-22）

[253](253-event-01-final-fixed-world-interaction-acceptance-2026-09-22.md) 是固定世界 NPC onTalk＋WorldObject onInspect、统一稳定 TargetKey、对象接近后复核、EventEditor 绑定及 Load definitions-only 的实施记录与人工验收入口。状态 **Implementation Complete / Producer Acceptance Pending**。沿用既有 ContentEvent／Outcome transaction／HostDialogue；旧 body／choices 兼容；Priority／Topic、整组 party-aware 条件、Step 与 once scope 均为 session runtime。EVENT-02 已在其上增加动态 NPC binding；ContentIntent 与通用剧情 runtime persistence 仍未实现。

### 基线、验收与 Git

- 当前对话审查基线：`Scripts(20260922-042034).zip`。制作人明确说明：042034 与 040154 解包后的文件内容一致，**不是**又实施一轮代码修改。
- 021915 完成主要 Hex／Army 正式运行依赖删除；040154 完成旅行契约、Host 物化刷新、序列化字段映射、当前数据分阶段恢复和孤立链删除等统一收尾；042034 复核未发现需要重开专项清理的实质问题。
- 状态分层：设计边界 **Design Confirmed**；上述代码 **Implemented**；制作人人工验收明确通过，故为 **Producer Accepted**；封板提交为 `9b32fe0f06d9e838e1e0be6ea40f5be50b239e01`（`chore: seal Hex/Army runtime retirement and cleanup`），故专项 **Committed / Sealed**。
- 当前 branch：`dev_openworld`。本轮文档收口开始前，封板提交后工作区只保留两份无关未提交改动：`Assets/Scripts/Unity/Host/HostBreakthroughRitual.cs`、`Assets/Scripts/Unity/Host/HostSkillStudyRitual.cs`。当前工作区还包含本轮文档／协作规范更新；两份 C# 不属于本轮，不得回滚、覆盖或顺带提交。
- 此前 82/82、离线编译与 converter 5/5 是实施阶段的历史执行记录，不是本次文档收口重跑，也不能代替制作人人工验收。

### 已封板的运行边界

- 普通 Outdoor 位置与旅行 authority：`SurfaceId + exact WorldPosition + Continuous Surface`；当前 movement budget scale 来自 opening `outdoorSurface.movementScale`，不读取 `LegacyHexWorld`，也不等同于 `cellSize`。
- 正常活动人物组织：`Squad`；玩家投影：`PlayerParty + ActiveControlledCharacter`；NPC group 运动：`SquadWorldMotion`。singleton Squad 身份不自动等于物理移动 ownership。
- 现代战斗：`CharacterEncounter` + 真实 Character participants + 独立战术坐标 + source／return world anchors。世界时间与战斗时间按已冻结规则分离。
- 据点与行政：`WorldSite + SiteCore + TerritoryClaim + Actual Administrative Control`；Surface 范围查询不以 Hex footprint 兜底。
- 真正独立空间：`LocalMapSession`／Separate Space（当前样板为洞府）；普通城市／户外 Site 不因此切换 LocalMap。
- 旧 Hex 世界、旧执行器和旧 Army runtime migration 不再承载正式 Gameplay。残留的旧格式识别／拒绝、稳定 JSON key／ID／数值、`FormerlySerializedAs`、独立转换工具和历史文档不等于旧运行系统仍在。

### 不应重新开启

没有“继续找 Legacy／Hex／Army”的默认任务。不得因历史文档、协议键、保留枚举值、旧序列化字段名或离线转换器仍含历史词语而重开扫描删除；不得恢复旧 Hex／Army runtime adapter。下方 Historical Resume Snapshot 与旧里程碑只记录当时事实。

### 当前限制与未核实事项

- 本封板不表示全项目完成、所有历史存档均可直接加载、所有历史字符串消失或项目无缺陷。
- 系统现状以本文下方“当前系统现状总表”为准；表内 `Proposed / Not Implemented` 不构成开发授权。
- SAVE-01 已选择“Active Event 期间禁止保存”，并实现通用任务／剧情状态磁盘持久化；当前只等待制作人人工验收，不是未授权 Proposal。
- Snapshot restore 的 Quest／ContentEvent／Chapter definitions shell 不重放 opening；SAVE-01 在 definitions rehydrate 后校验恢复引用。Separate Space Session JSON wire 继续沿用既有 authority。WorldMap 的 Site／Flag marker 随 world projection 缩放，而 Player／NPC Squad marker 仍固定像素，仍是未授权差异。

### 新会话短阅读顺序

`AGENTS.md` → [00 overview](../00-project/00-overview.md) → 本节与[当前系统现状总表](#当前系统现状总表2026-09-22) → [31 architecture](../30-tech/31-architecture.md) → 下一议题对应的少数 `20-systems` 正文与源码入口。无需顺序重读全部历史 devlog。

### 执行与验收规则

遵守 `AGENTS.md` 与 [52 协作规范](52-ai-collaboration-protocol.md)：一轮一份明确指令；不自行运行测试或启动 Unity；代码只做基础编译与最少静态检查；编译、静态核对、人工验收和提交状态分开；未经制作人确认不自动编码；普通实施不自动提交，“封板”才授权选择性 commit，默认不 push／tag／amend／reset／clean。

> **当前边界：** Runtime Loader 只接受当前 `outdoorSurface`／`npcSquad`；旧 `formalArmy`／`hexWorld` 明确拒绝。`LegacyRuntimeConverter` 只读输入并写不同且尚不存在的独立副本，但只无损转换 FormalArmy 与 current authority 完整的 hybrid Snapshot；`hexWorld`／`openingHexWorldId` 只检测并拒绝，须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。旧 Content 的 `squad:migrated:`／`squad:legacy:` 与旧 Snapshot 的 `squad:army:` 三规则保持分离；稳定 wire key、数值空洞与 ID 只用于检测／离线转换。

## Current implementation and verification — 2026-09-22

- **021915 统一处置表：**

| ID | 当前消费者／数据 | 决定 | 验证证据 |
|---|---|---|---|
| PP-PLAN | Surface Host 路径缓存、取消／到达／恢复 | 恢复单次逻辑操作一次 `TravelPlanVersion` 失效；逐帧更新不增；重放置清旧 Site context；取消保留当前位置；到达保留目的 Site；零 consumer 的全清 `Clear` 删除 | `PlayerPartyWorldMotionCurrentContractTests` 5/5 |
| HOST-SCOPE | chunk／到达／成员／生命状态／读档／Encounter／Separate Space | `None`／`RefreshViewsOnly`／`ReconcileAndRefresh` 三态合并；generation 已完成 Runtime reconcile 时只同步 Host Views；dirty 的先后顺序有独立 generation 证据 | `OutdoorEntityReconcileGateTests` 6/6；静止 tick 回归 |
| SNAP-CURRENT | exact presence、PlayerParty、Squad、Background、Flag／Site／Claim、Encounter、Separate Space | Continuous Outdoor 必须有精确 Surface；合法非物理 Site 可无坐标；idle background 空 Surface 只可由现成 exact personal authority 归一；Interior／Encounter 不强行户外化 | current Snapshot／opening／Site／Encounter 定向矩阵；本机 `vs04_slot0.json` 完成 Core + content-dependent phase-two restore |
| DEAD-CHAIN | placement trace、旧 Site access／constants／grid wrapper、playable bounds、旧 map stub／catalog／visibility尾部 | 全仓零正式 root 后连 `.meta` 删除；洞府 placement、当前可见性、独立战与 `LogSnapshot` 保留 | Core／Data／Unity／Tests／Player assemblies 全量离线编译；符号与 GUID 扫描 |
| API-NAME | 空 residual stop、无效 out／参数、CoPresence enum、诊断文字 | 删除空壳和参数；`LocalMapOccupants = 2` 保持数值；稳定 wire／ID／DTO 不变 | 编译 + 直接调用扫描 |
| CONTENT-ASSET | BaseGame Surface／NpcSquad、movement scale、旧转换、SerializeField | `movementScale 1.0` 与 `cellSize 0.028` 分离；opening Surface 唯一注入；converter fail-closed；镜头字段以 `FormerlySerializedAs` 保值 | Content tests、预算公式测试、converter 5/5、Scene／Prefab／GUID 静态检查 |

- **核心入口：** `Assets/Scripts/Core/Simulation/SimulationWorld.cs` 仅持有 `ContinuousWorldMovementScale`；`Assets/Scripts/Core/World/ContinuousWorldMovementScale.cs` 只读该字段。`Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs` 从当前 opening Surface 的 `movementScale` 唯一注入。
- **Content 边界：** `Assets/Scripts/Data/Content/ContentPackageLoader.cs` 拒绝 `hexWorld`／`formalArmy`；`Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` 显式 `movementScale: 1.0`。该值不是同文件的 `cellSize: 0.028`。
- **离线入口：** `ExternalTools/ContentAuthoring/LegacyRuntimeConverter/` 只转换 FormalArmy Content 与 current authority 完整的 hybrid Snapshot；不会覆盖输入，也不生成同路径输出。`hexWorld`／`openingHexWorldId` 在写文件前拒绝并转介 WorldComposer／SurfaceAuthoring Legacy migration 路径。
- **已删除链：** Core Hex 目录、旧执行器、Outdoor LocalMap／footprint／SurfaceExit／Host 接线，以及 Site／Flag／Encounter／SeparateSpace current 链的 Hex 参数／缓存均已退出。
- **此前实施阶段实际执行记录（不是本次封板重跑）：** Core／Data／Unity／Unity.Editor／Tests／PlayModeTests／Assembly-CSharp／Assembly-CSharp-Editor 离线编译 `ALL_OK`；021915 当前行为矩阵 82/82；ContentAuthoring solution 0 warning／0 error，converter 5/5。本机 `vs04_slot0.json`（36 entities、28 Squads、6 motions、20 exact presences、11 flags、11 runtime sites、17 claims）完成 serializer、Core restore、Content shell、Surface Site、PlayerParty／Background route、政治状态与 Squad motion 二阶段恢复。
- **封板：** 制作人已确认本版人工验收通过。本次封板只做规范与状态文档更新及选择性 Git 提交；不重跑编译或任何测试，不启动 Unity。本专项 **Producer Accepted / Sealed**。不宣称全项目无缺陷，也不宣称历史 Hex／Army 字符串已清零。

<a id="当前系统现状总表2026-09-22"></a>
## 当前系统现状总表（2026-09-22）

本表只写当前可用能力与明确边界。状态列中的 `Accepted / Sealed` 仅引用已有人工验收和 Git 记录；未列明者不得自动提升为已验收。源码路径用于恢复上下文，不表示需要重新审计全仓。

### A. 世界、位置与地图

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| Continuous Surface 与 Runtime Chunk | `OutdoorSurfaceSpatialAuthority`、`SurfaceGroundNavigation` 与 exact `WorldPosition` 承载户外地形、通行、道路／桥／森林输入和 chunk materialization；`SimulationWorld.ContinuousWorldMovementScale` 由 opening `outdoorSurface.movementScale` 注入 | `Assets/Scripts/Core/World/Surface/`；`Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`；动态状态进 `WorldSnapshot`，静态地理留在 Content | Implemented / Accepted / Sealed。自动水文、道路 A*、detail scatter、完整大陆内容覆盖与 Runtime Chunk profiling 仍是 Future |
| WorldMap | `HostWorldMapPanel` 查看同源 Surface、exact 选点、缩放／平移、Site／Flag／Party／NPC Squad／控制范围；Site／Flag 图标和标签随 world projection 缩放，Header／Flyout 固定屏幕空间；打开时只规划，关闭后恢复 Surface travel | `Assets/Scripts/Unity/Host/HostWorldMapPanel.cs`；不保存第二套地图位置，只提交 `PlayerPartyWorldMotion` 旅行计划 | MAP-02/03 Accepted / Sealed；但当前 Player 为固定 20×20 px、NPC Squad 为固定 14×14 px，与“图标／标签随地图缩放”的确认规则不一致，须另行定范围修正 |
| PlayerParty 移动与同行 | 单 `ActiveControlledCharacter`；PlayerParty 上限 6；Followers 为 AI；`PlayerPartySurfaceTravelService`／`PlayerPartyWorldMotion` 以 Surface route 移动；取消保留位置，重放置清旧 Site，到达保留目的 Site | `Assets/Scripts/Core/World/Strategic/PlayerPartyWorldMotion.cs`、`PlayerPartySurfaceTravelService.cs`；Snapshot 保存位置、目的地、计划语义并在 Surface shell 后恢复 route | Implemented / Accepted / Sealed。飞舟、跨大陆新能力与完整继承终局未实现 |
| NPC Squad 与独立人物后台移动 | 正常 group = `Squad + SquadWorldMotion`；独立人物可由 background travel authority 移动；loaded/unloaded 只改变表现与 LOD，不改身份／位置 authority | `SquadState.cs`、`SquadWorldMotion.cs`、`BackgroundCharacterTravelService.cs`；Snapshot + Content-dependent phase-two restore | Hex／Army retirement seal 已覆盖。完整战略 AI／NPC 主动战争仍 Future |
| 表现物化与刷新 | Host 只根据 presentation scope materialize；`OutdoorEntityReconcileGate` 合并为 `None`／`RefreshViewsOnly`／`ReconcileAndRefresh`，避免静止 tick 重扫并保留真实 scope 变化 | `Assets/Scripts/Unity/Host/OutdoorEntityReconcileGate.cs`、`PlayableHostBootstrap.cs`；运行 generation 不作为磁盘 authority | Implemented；021915 统一收尾 Accepted / Sealed。性能 profiling 未核实，不作为当前 bug |
| 世界／Interior／Encounter 位置恢复顺序 | `EntityLocationSnapshotAuthorityComponent`、character presence、PlayerParty／Squad motion、Separate Space／CharacterEncounter 分层恢复；Host presentation 优先级为 Separate Space → Encounter → Outdoor | `WorldSnapshot`、`SnapshotService`、`StrategicSnapshotHelper`、`HostSnapshotSessionRehydration` | Outdoor／Encounter spatial 主线已封板；Separate Space session JSON wire 缺口见 D 表。不保证任意旧档直接兼容 |

### B. 小队、战斗与人物状态

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| Squad／PlayerParty／主控 | 每个正常活动人物属于唯一 Squad，单人也是 singleton Squad；PlayerParty 是玩家 Squad + Active 的控制投影；切换 Active 不改 Faction／HomeSite／关系 | `SquadBoard`、`PlayerPartyState`、`PlayerPartyTransitionMembership`；Snapshot 保存 membership 与 active identity | Unified Squad 主线 Accepted / Sealed。singleton Squad 不自动拥有某个共用物理位置 |
| CharacterEncounter | 第一击前统一确认；真实初始双方、有限候选、临时独立战术坐标、世界停表／战术时间、ReadyToStart／Active／ReadyToEnd／结算／返回；只恢复各自战前 world anchor，保留战果 | `CharacterEncounter.cs`、`HostCharacterEncounter.cs`；`WorldSnapshot.CharacterEncounter` + participant origin／return／tactical fields | Current 主线 Accepted / Sealed。NPC 对 NPC 大型战争、无限远援与大军实时战不在当前范围 |
| 独立战场与战报 | 使用接战地点同源地形／关键建筑 materialize；正式参与者决定控制、胜负与战报；最终结算唯一 | `ContinuousOutdoorEncounterField`、CharacterEncounter settlement/report services；Snapshot 在可保存 phase 保存，Preparing 明确禁止存档 | 已实现当前范围。完整俘虏／赎金、全面战争为 Future |
| 弥留、死亡与遗体 | `Incapacitated`、`Dead`、`Removed` 分离；Dead ≠ Removed；失能主控优先按 Party 顺序接替；residual／corpse 保留人物真实 Surface／空间责任 | `CombatLifeStateService`、entity lifecycle、world presence／residual services；Snapshot 保存 lifecycle、位置与相关 Encounter 状态 | 当前链已实现并纳入封板；玩家势力无人终局、完整继承／丧葬等未实现 |
| 私人冲突、关系介入与 War | 攻击人物不自动宣战；关系只决定有限介入资格；攻击势力有效拥有的建筑才走对应战争授权；永久关系 authority 是 `RelationshipLedger` | `CharacterEncounterHostilityService`、relationship services、Faction／Claim boards；关系事件已进 Snapshot | 设计与当前主线已确认。更完整外交、战争升级和 NPC 自动战争仍 Future |

### C. 据点、建设与生活

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| WorldSite／SiteCore／控制 | Permanent Site 使用固定 Council Hall core；FactionFlag 可创建可拆／可毁 runtime Site；Owner、TerritoryClaim 与 Actual Administrative Control 分离；范围改变不移动人物 | `WorldSite`、`WorldSiteBoard`、`FactionFlagService`、`WorldSitePhysicalRegionQuery`；Snapshot 保存 runtime sites、flags、claims | Implemented / Accepted / Sealed。SiteCore、Owner、Actual Control、建设权限与资产状态不得混写 |
| 建设与范围显示 | `ConstructionCatalog` + `ConstructionService` 事务性消费 PartyInventory，当前支持势力旗、FarmField、RecoverySpot、StorageRoom 等真实 outdoor constructed assets；footprint 必须落在发起势力 Actual Control；可主动拆除返料 | `Assets/Scripts/Core/Construction/`、`HostConstructionPanel` 与各 placement presenter；constructed assets／farm plots／Inventory 进入 Snapshot | 2L Implemented / Producer Accepted / Sealed。当前不含完整建筑产权、Core upgrade 产品 UI、通用建筑战争或通用 house／workshop 建造 |
| 农田、劳动与 Site 经济 | WorkArea／Job／Schedule、农作逐 tick 消费当前行政授权，收获进入 Site inventory／treasury 相关 current board | `Assets/Scripts/Core/Labor/`、Strategic economy services；Content 位于 `WorkAreas/`、`Jobs/`、`Schedules/`、`SiteEconomies/` | 当前样例链已接通；更完整仓储物流、税赋、跨 Site 运输、离屏生产未实现 |
| 接管与资产保持 | 接管改变政治／行政 authority，不传送人物、不自动删除建筑／库存／日程；拆可移除旗不等于清空 Site 资产 | Site warfare／capture services、FactionFlag／Claim restore | Current 主线 Accepted / Sealed。产权、继承、战争升级和普通建筑战争扩展仍需另议 |

### D. 探索与独立空间

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| 洞府发现与显形 | 当前 cave 样板支持靠近提示、神识 Survey、`KnownSites` reveal；Survey 只更新知识和表现，不提前加载 Interior 或移动 Party | `ExplorationService`、`OpportunityEntranceRules`、`KnownSitesComponent`；KnownSites 作为实体组件随当前 entity Snapshot 保存 | SPACE-01 cave 流程 Accepted / Sealed。其他秘境／遗迹不因设计存在而视为已有正式内容 |
| 进入、同行与内部状态 | `SeparateSpaceTransitionService.Enter` 验证入口并捕获 exact outdoor return；当前真正随队成员全部 transition；Interior 只 materialize occupants／当地居民；内部战斗原地进行 | `LocalMapSession`／SeparateSpace session、`PlayerPartyTransitionMembership`、`SeparateSpaceCombatPolicy` | Implemented / Accepted / Sealed。普通 Outdoor Site 不使用这条链 |
| 离开、回程与存读档 | authored exit edge 由 Active 触发；Leave 恢复 exact `ReturnSurfaceId + WorldPosition`；DTO／helper capture／restore SeparateSpace session，load 不重新调用 Enter；`JsonSnapshotSerializer` 已读写 `strategic.separateSpace` | `SeparateSpaceExitEdgeTrigger`、`SeparateSpaceSessionSnapshotRestore`、`HostSeparateSpaceExitTrigger` | SPACE-01 历史主线 Accepted / Sealed；SPACE-01-P1 JSON wire 修复状态不变，当前总 Snapshot schema 为 7 |
| 拾取物保持 | `WorldLootPickupService` 记录稳定 loot spot id；`HasTakenWorldLootSnapshotAuthority` + `TakenWorldLootSpotIds` 保存已取状态 | `WorldSnapshot`／`SnapshotService` | 当前专用链已接线；它不是通用 Story Flag／Quest 持久化 |

### E. 人物、成长与内容

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| 属性、境界与修炼 | AttributeModifier 管道、Realm／Cultivation、学习／熟练度／突破 ritual 与 WorldTick／ActionClock 基础链已存在；当前两份 Ritual Host UI 改动仍是无关未提交工作区内容 | `Core/Attributes`、`Core/Cultivation`、`HostBreakthroughRitual`、`HostSkillStudyRitual`；Snapshot 已保存 Cultivation、已学功法、功法／斗技 mastery 和 manual specs | 当前 slice 已实现；更完整境界内容、突破事件与 AI 日程不是因底层类型存在就视为完成。两份 Ritual 改动尚未确认／提交 |
| 行动、Order 与日程 | `ActiveActions`、Order queues、Schedule definitions／entity binding 与 `DailyTaskComponent` 已用于当前劳动、修炼、恢复和日程链 | Snapshot 已保存 active actions、orders、schedules、entity schedule id 与 daily task fields；Host ritual channel 自身不是 Snapshot authority | 当前基础链已实现；不得从可序列化目标推断所有 Action／Host UI 中间态均可无损继续 |
| 关系、Bond、态度与档案 | `SocialBondBoard` 保存客观 Bond；五维单向态度由 `RelationshipLedger` 事件聚合；人物档案供 UI／内容查询 | `RelationshipService`、`SocialBondBoard`、profile components；Snapshot 已保存 Social Bonds、RelationshipLedger 与 PersonalityProfile tags | 2M 主线 Accepted / Sealed。关系 ≠ 私人冲突 ≠ Faction War |
| 背包、装备、资源与掉落 | 非 resource 只使用 PartyInventory；resource 在可访问己方战略物资网络时统一使用 PartyInventory＋eligible WorldSitePublicStock，离开网络退回 bag-only。`stockAtLeast`、Quest progress／Journal 与 `removeStock` 共用该语义 | `PlayerStrategicResourceService` 是访问与稳定扣除顺序 authority；`HostInventoryPanel` 同窗提供小队背包／势力仓库，只允许战略资源从公库取到背包；PartyInventory、WorldSitePublicStock 与 taken loot 均沿用既有 Snapshot 字段 | STRATEGIC-STOCK-01 **Producer Accepted / Sealed**；势力仓库不是通用 Item／装备仓库，不含容量与物流 |
| Quest | 固定任务与人物委托共用 `QuestBoard`／`QuestService`；Journal/HUD 按实例操作 | 当前 Snapshot v9 保存实例、发布者、来源 Opportunity、交付、期限与序列；Quest shape 自 v8 后未变，definitions 仍来自 Content | QUEST-INSTANCE-01 **Producer Accepted / Sealed** |
| Flags／ContentEvents／Chapters | `WorldFlagBoard`／`StoryFlagService`、`ContentEventBoard`、`ChapterBoard`／day handler 支持条件、触发与推进；固定与动态 WorldObject interaction event 已接通 | 当前 v9 保存 Flags／History、fired keys 与 Chapter runtime；restore shell definitions-only 重注册并校验，不重放 opening；Active dialogue 不保存 | SAVE-01／DYNAMIC-DISCOVERY-01 **Producer Accepted / Sealed** |
| WorldOpportunity／WorldActivity | NPC 与动态 WorldObject 共用 Opportunity lifecycle；动态物体有稳定 object identity、三种发现模式、模板 onInspect 与显式 resolve | v9 保存 SpawnKind、NPC／物体互斥身份、精确坐标、发现状态及 Activity；hidden 发现前不泄漏 | DYNAMIC-DISCOVERY-01 **Producer Accepted / Sealed** |
| ContentCounters／ContentDaily／LocationLabor | runtime board 支持计数、每日限制与采收／劳动 Quest facts；角色 `DailyTaskComponent` 是另一条实体日程链 | 当前 v9 保存全部 Counter、marked day index 及 opaque labor tick／harvest key；`DailyTaskComponent` 仍按实体字段保存 | SAVE-01 **Producer Accepted / Sealed**；两类 Daily authority 不混用 |

### F. 工具链与工程

| 范围 | 当前能做什么／authority | 入口与输出 | 状态／边界 |
|---|---|---|---|
| ContentAuthoring | `EditorManifest.json` 是编辑器清单 authority；`WorldComposer`／`FineEditor` 维护 Continuous Surface；OpportunityEditor 已覆盖 NPC／动态 WorldObject、发现规则，EventEditor 已覆盖 Opportunity Object onInspect 与显式 resolve | `ContentAuthoring.sln`、`编译-所有编辑器.cmd`、`启动-*.cmd`；发布到 generated `Apps/*.exe` | MAP-01／DYNAMIC-DISCOVERY-01 编辑器能力均已封板 |
| Authoring → Runtime | Authoring Source 只由 baker 消费；Preview／Bake 共用 `CompositionEngine`；候选导出不改 Runtime，`CompatibilityPublisher` 可 staging／backup／rollback 原子替换 Surface、Geography、WorldMap cache 三文件 | `Content/BaseGame/Authoring/ContinuousSurface/` → candidate 或 `Content/BaseGame/Data/Worlds/` | Authoring Source ≠ Runtime Content；不得让编辑源成为 runtime DefinitionRegistry authority。发布后 Content Validation 入口本轮未重新核实 |
| Runtime / converter / prototype 边界 | Core／Data／Unity 正式程序集不反向依赖工具；`LegacyRuntimeConverter` 只做有证据的无损 FormalArmy／hybrid Snapshot 转换，hexWorld fail-closed；Demo Runtime 只读参考 | `ExternalTools/ContentAuthoring/LegacyRuntimeConverter/`；输入只读，输出必须是不同且不存在的文件 | Converter 自测记录仅为历史；本轮不重跑。不得把工具接回正常 Loader |
| Content／Snapshot 版本策略 | JSON ContentPackage + namespaced DefinitionId；Loader 严格校验；当前 Snapshot v9 由版本化 DTO／serializer 恢复；v8 及更早 authority 明确报错，不静默猜 | `Content/BaseGame/manifest.json`、`ContentPackageLoader`、`WorldSnapshot`、`JsonSnapshotSerializer` | 当前正式 BaseGame 可用；完整外部 Mods 产品能力、复杂 migration 平台未实现 |
| 启动、构建与诊断 | Unity 版本 2022.3.6f1 Built-in；LevelTester 是主要逻辑试玩入口但不在 Build Settings；Build Settings 当前启用 DemoParityHost／Demo_v0_1；工具 Build All 日志为 `build-all.log`，WorldComposer／FineEditor crash log 在 `Apps/` | 根 `README.md`、`docs/40-process/114-level-tester.md`、`ExternalTools/ContentAuthoring/README.md`、`tools/offline-compile.ps1` | 本轮未编译、未启动 Unity。generated `Apps/` 静态观察仍有已移出 manifest 的旧 exe，须以后 Build All 刷新后再确认；不影响 manifest 作为工具清单真源 |

## 已确认且不随换会话重议的设计边界

1. 游戏首先是具体角色的修仙 RPG；同行小队、连续探索、关系、战斗与领地经营服务角色体验，不把玩家本体替换为 RTS／4X 组织。
2. `SurfaceCell` 是 1×1 逻辑地理单位；`RuntimeChunk` 是流式技术分区；`WorldEditorCell` 是宏观 authoring 单位；`WorldSiteBlueprint` 与 `DetailPatch` 是 bake 输入。它们不能互相替代；1×1 逻辑格也不等于可任意改写 movement scale 的 Unity world unit。
3. 普通城市／村镇／户外 Site 均属于同一 Continuous Surface；Cave／Interior／Dungeon 与临时 Encounter 才是真正 Separate Space。
4. 玩家与 NPC 使用统一 Squad 语义；每名 Character 保有真实位置，singleton Squad 不自动产生共享物理移动 ownership。
5. 战斗必须在第一击前经过统一确认；只以真实参与者结算；主世界停表而本场战术时间按自身 owner 推进；战后回各自 pre-encounter anchor，战果不回滚。
6. SiteCore、政治 Owner／Claim、Actual Administrative Control、建设许可与资产状态分离。控制改变不自动移动人物、删除资产或重置日程。
7. WorldMap 是同一 Surface 的 planning view；选择目标后关闭地图再恢复旅行。图标／标签按 world-space 投影随 zoom 缩放，不恢复固定屏幕像素方案。
8. RelationshipLedger 是态度真源；Social Bond 是客观关系事实；人物冲突、关系介入与 Faction War 是不同状态，不得互相隐式升级。
9. Separate Space transition 带当前 PlayerParty occupants、保存内部状态与 exact outdoor return；普通 Outdoor 不借此恢复一 Site 一 LocalMap。
10. 编辑器生产 authoring source，baker 生产 runtime output，游戏 Loader 只消费 current runtime Content。工具输出、历史原型和正常 gameplay authority 不得混层。

依据：[00 overview](../00-project/00-overview.md)、[03 glossary](../00-project/03-glossary.md)、[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[23](../20-systems/23-combat.md)、[24](../20-systems/24-world-and-settlements.md)、[26](../20-systems/26-territory-management.md)、[28](../20-systems/28-jianghu-relations.md)、[ADR-0032～0038](43-decisions/README.md)。

<a id="save01通用内容状态磁盘持久化"></a>
## SAVE-01：通用内容状态磁盘持久化

- 当前 Snapshot v9 的 `ContentProgress` 是 Quest／Story／Event／Chapter 内容进度磁盘 authority；其 shape 自 v8 后未变。`hasAuthority=true` 用于区分合法空状态与缺失／损坏数据，Quest 实例身份与序列同属 authority。
- Flags／History、全部 Quest runtime、fired event keys、Chapter runtime、Counters／Daily、LocationLabor ticks／harvests 完整 round-trip。Quest／Event／Chapter definitions 仍来自当前 Content Package。
- Active Event 期间禁止保存，不持久化 pending dialogue。v1～v6 无法可靠推导上述状态，统一拒绝。
- New Game／Load 使用同一 `PlayableSimulationLoopFactory`，避免 Quest deadline 与 Chapter day beat 在读档后丢失或重复注册。
- 原 Proposal 已由制作人授权并实现；当前状态 **Implementation Complete / Producer Acceptance Pending**。完整范围、验证和人工验收见 [257](257-save-01-content-progress-persistence-v1-2026-09-23.md)。

## Historical Resume Snapshot — 2026-09-18～21（已归档）
> **2026-09-21 FINAL LEGACY CLOSEOUT：** Continuous World / Legacy Runtime Migration 已完成最终 Scripts-only 实现清理，状态为 **Implementation Complete / Producer Acceptance Pending**。PlayerParty authority 为 `SurfaceId + WorldPosition + SurfaceVisible / ContinuousSurfaceRoute`；NPC group 为 `Squad + SquadWorldMotion`；角色空间为 `AtWorldPosition / AtSite(background-resident) / InSeparateSpace / InEncounter`；领土为 `WorldSite + TerritoryClaim + Actual Administrative Control`；战斗为 `CharacterEncounter`；独立空间为 Interior LocalMap / `EntityLocation`。旧 Hex／FormalArmy／TerritoryRegion／Outdoor LocalMap 只保留 old input、migration、derived metadata、显式 compatibility 与 Demo/test contract。以后不得因搜索到 Legacy 名字自动开启 cleanup；仅在它重新成为现代 authority，或产品明确终止旧兼容时再处理。
>
> **本轮最终 smoke：** 复验 New Game、WorldMap／PlayerParty movement、行进 Save/Load、洞府进入离开、CharacterEncounter／战报／Return、NPC Squad marker，以及旧档 Outdoor LocalMap／Hex compatibility fixture。通过前不 git seal。
>
> **2026-09-21 Skill Mastery acceptance fix：** 功法／斗技灌注统一以真实 `mastery.breakthroughs` 路径计算资格与有效门槛，并在确认 `actualGain > 0` 后原子扣修为／写熟练。制作人随后要求当前全部功法／斗技显式配置到化境：四段门槛依次 20/30/40/50，灵草与粗木分别各需 1/2/3/4。显式 profile 缺失当前 tier 路径仍不回落成默认路径。
>
> **本补丁人工 smoke：** 分别查看洞府秘诀与一门斗技的小成／大成／圆满；核对灌注、冲击按钮、10 修为固定扣费、四档材料、近上限实际增量，以及关闭详情页后选中角色面板上方的仪式读条。通过前保持 Pending。
>
> **2026-09-21 FINAL-SEAL Non-World follow-through：** 非世界修复与清理已达到 **Implementation Complete / Producer Acceptance Pending**。四类学习／熟练判定统一到可保存 `world.Random`；失能自主行动即时且逐 tick fail-closed、释放本人预约；`TryAddAll` 与整组 outcome／领奖／事件 choice 形成原子提交；Normal Outdoor 公库改用 exact position 的 Actual Control。已删除旧即时学习 wrapper、空 UI、无入口关系种子／派生 helper 和 runtime 假 LocalMap／Settlement 布局；Prototype Demo 因 Build Scene、Prefab 与 GUID 契约整体保留。特殊空间公库政策与通用剧情 Boards 持久化仍待独立定义，不得宣称全游戏状态均已完整 Save/Load。全程序集 offline compile `ALL_OK`；集中纯 C# sanity（含上一轮回归）14/14；Current BaseGame Content reference validation 通过；`git diff --check` 通过且 staged 为空。
>
> **本轮制作人集中验收：** 1) 用背包现有功法秘籍／斗技秘本开始研读，中途令固定 subject 弥留，确认不授予且恢复后不会自动续上；2) 让角色执行采集／劳动或打坐，运行中令其弥留，确认进度停止、工位释放、下一条旧自主命令不启动；3) 满包时领取含多项奖励的现有任务／事件，确认无前项奖励、flag、关系或计数残留，腾出空间后仅领取一次；4) 在己方核心 Actual Control 内外移动并观察公库可用量／消费，陈旧地点上下文不得授权；5) 底栏人物入口直接打开统一人物档案的人际页；6) New Game 核对灵根、初始境界、RegionId 与 authored opening relations。通过前保持 Pending。
>
> **2026-09-21 FINAL-SEAL consolidated repair：** 已完成 correctness repair 与 dead-dependency closure，状态为 **Implementation Complete / Producer Acceptance Pending**。组织 membership 与实际移动／命令 ownership 已分离；casualty handoff 冻结首次精确位置；精确位置查询、显式 Surface 路由及后台 Snapshot 两阶段恢复已收口；零 caller battle/arrival/旧 runtime snapshot/stack/Host residue 已删除，旧 DTO/readers 与真实工具／测试 caller 保留。架构冻结详见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。本轮未打开 Unity、未暂存、未提交、未推送。
>
> **制作人人工 smoke（仍待完成）：** 1) 单人 Squad 成员个人旅行／到达／Save-Load；2) 多人 Squad 成员只随 Squad、不可同时个人旅行；3) A 点失能、Squad 移到 B、再死亡仍留 A；4) WorldMap 精确位置与 `(0,0)` 聚焦；5) 重叠 Surface 上 PlayerParty／Squad／后台个人均留在显式 Surface，跨 Surface 目的地拒绝；6) moving/idle 后台旅行 Save-Load，idle 不重启；7) Encounter 与 Separate Space 的弥留／死亡／返回不被户外 handoff 污染；8) Host roster 显示组织归属但只对真实 group 聚焦 Squad；9) New Game、旧档 migration、WorldSite/economy 与基础内容 smoke。全部通过前不得改为 Accepted / Sealed。

> **2026-09-21 LEGACY-FINAL-SEAL implementation：** Compatibility quarantine、zero-caller API cleanup、runtime naming cleanup、Development boundary guards 与最终 Architecture Freeze 已达到 **Implementation Complete / Producer Acceptance Pending**。冻结矩阵见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。LEGACY-FINAL-C 已 Sealed；最终 smoke 通过前暂不宣称 Migration Complete。下一阶段为 Gameplay / Content development，具体玩法尚未指定。

> **2026-09-21 LEGACY-FINAL-C Seal：** Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement 已由制作人完成人工验收并正式 **Producer Accepted / Sealed**。正常 runtime authority 已收口为 Continuous Surface、Squad、WorldSite／Claim、CharacterEncounter 与 Separate Space；当前只进行 **LEGACY-FINAL-SEAL — Compatibility Quarantine / Dead API Cleanup / Architecture Freeze**，完成最终 smoke 前暂不宣称 Migration Complete。

> **2026-09-21 LEGACY-FINAL-C implementation：** Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement 已达到 **Implementation Complete / Producer Acceptance Pending**。TerritoryRegion、StrategicEncounter、RetreatingArmy 与 LingeringBattlefield runtime boards 已退出；现代 residual 使用 `AtWorldPosition + SurfaceId + exact WorldPosition`；旧 Hex／Outdoor LocalMap 与旧 DTO 仅留明确 compatibility／one-way migration 边界。正式记录见 [250](250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。C 尚未获制作人验收，不得宣称 Legacy Finalization Complete。

> **2026-09-21 LEGACY-FINAL-B Seal：** PlayerParty Continuous Surface travel authority cutover 已由制作人完成人工验收并正式 **Producer Accepted / Sealed**。正常玩家户外位置与旅行使用 `SurfaceId + exact WorldPosition + SurfaceVisible`；Hex／Outdoor LocalMap executor 仅留兼容边界。当前主线切换为 **LEGACY-FINAL-C — Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement**。C 完成并验收前不得宣称 Legacy Finalization Complete。

> **2026-09-21 LEGACY-FINAL-B implementation：** 已完成 PlayerParty Continuous Surface travel authority cutover，状态为 **Implementation Complete / Producer Acceptance Pending**。正常 New Game／WorldMap／WASD 链使用 `SurfaceId + exact WorldPosition + SurfaceVisible`；正常 WorldSite 保持 AtWorldPosition，Hex／LocalVisible／Wilderness transition 仅留旧档、旧内容与旧 Outdoor LocalMap 兼容。正式记录见 [249](249-legacy-final-b-playerparty-continuous-surface-travel-authority-cutover-2026-09-21.md)。LEGACY-FINAL-C 为 Next；B 未经制作人验收且 C 未完成前不得宣称 Legacy Finalization Complete。

> **2026-09-21 current handoff：** LEGACY-FINAL-A 已由制作人完整人工验收并正式 **Accepted / Sealed**；已验收实现 checkpoint 为 `e97f53f`。当前主线为 **LEGACY-FINAL-B — PlayerParty Continuous Surface Travel Authority Cutover**，目标是把正常玩家旅行收口为 `SurfaceId + exact WorldPosition + SurfaceVisible`，并把 Hex／Outdoor LocalMap executor 限定为兼容路径。LEGACY-FINAL-C 为下一阶段；B/C 完成前不得宣称 Legacy Finalization Complete。

> **2026-09-20 Seal / Legacy Finalization superseding state：** MAP-04、CW-10、CW-10.5 与 Cave Loot persistence 已由制作人人工验收并正式 **Accepted / Sealed**。当前主线切换为 **LEGACY-FINAL-A — Unified NPC Squad World Motion / FormalArmy Runtime Retirement**；随后依次为 LEGACY-FINAL-B、LEGACY-FINAL-C。A/B/C 全部完成前不得宣称 Legacy migration complete。本文后续关于 MAP-04／CW-10／CW-10.5 pending 的段落均为历史恢复快照。

> **2026-09-20 LEGACY-FINAL-A implementation state：** A 已达到 **Implementation Complete / Producer Acceptance Pending**，正式记录见 [248 LEGACY-FINAL-A](248-legacy-final-a-unified-npc-squad-world-motion-2026-09-20.md)。正常 New Game 的 NPC group authority 已收口为 `Squad + SquadWorldMotion + exact Surface position`；Current BaseGame 不再创建 FormalArmy/ArmyStack，旧 content/save 仅作单向 migration input。待制作人验收 A 后才进入 B，TerritoryRegion 等 C 边界仍未完成。

> **2026-09-21 LEGACY-FINAL-A2 runtime zero-state（已封板）：** FormalArmyBoard、ArmyStackBoard、ArmyMembershipComponent 与旧 Army runtime/services 已物理退出 Simulation；旧 Content/Snapshot DTO 只在兼容边界直接单向迁移到 Squad + SquadWorldMotion，旧 active battle identity 迁到 SquadId / CharacterEncounter。制作人已完成人工验收，A 状态为 **Accepted / Sealed**。

> **2026-09-20 Current State supersedes the older snapshot below:** SPACE-01 已由制作人验收并以 `49f8650`（`Seal SPACE-01 separate-space ownership and persistence`）提交、推送，状态为 **Accepted / Sealed**。MAP-04 已恢复并完成 final physical cleanup 实施：删除 `ContinuousWildernessPair` runtime、清理 W1C acceptance logs / force-leave cheat、Normal Outdoor Surface sync 不再回退 Hex terrain legality、正常 `PartyWorld` summary 不再写 `AtHex`。MAP-04 当前为 **Implementation Complete / Producer Acceptance Pending**；本轮 MAP-04 文件全部保持未暂存、未提交、未推送。后文 2026-09-18 的 WIP、暂停与旧 consumer 数量是历史恢复快照，不再代表当前状态。

> **2026-09-20 Final Seal Preparation：** 最终 caller audit 发现并修复三处 modern AtHex producer：Continuous movement member presence、Snapshot active Continuous focus、Continuous AutoResolve battle commit。W1B primary-context dead APIs 已删除。现存 AtHex 只服务 old-save restore、旧 Outdoor LocalMap／Hex travel、non-continuous battle 与 legacy residual；WorldRegion 只保留 Data schema、旧包验证和 migration/import。Separate Space LocalMap 是正式独立空间 authority，不属于 Legacy。完整 Residual Matrix 见 [245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md#final-seal-preparation--legacy-residual-matrix2026-09-20)。状态仍为 **Implementation Complete / Producer Acceptance Pending**，不得提前 Sealed。

> **2026-09-20 Cave Loot acceptance patch：** MAP-04 其余人工验收项已由制作人确认正常；最后阻断是洞府 loot 的 `loot:*` runtime flag 未进入 Snapshot，导致 Load 后重新物化。现增加窄范围 taken-loot Snapshot authority，并将 LocalMap loot identity 统一为 `MapLayoutId + PlacementId`；PartyInventory 沿用既有 authority，未扩展通用 StoryFlag persistence。纯 C# round-trip 覆盖已取、空 authority、跨 MapLayout 同名 placement 和背包满失败。MAP-04 仍待制作人复验后封板。CW-10／CW-10.5 current-code readiness audit 未发现 blocker，二者仍为 **Implementation Completed / Producer Acceptance Pending**。

> **本文是未来新会话（Codex / 新 ChatGPT / 新 Cursor 会话）接手本项目的正式入口。**
>
> - 轮次性质：**Documentation / Recovery Snapshot**（本轮禁止任何实现修改）
> - 请求日期：2026-09-18
> - 实际生成时间：**2026-09-19 00:33～00:45（本地）**，即制作人 09-18 夜场跨过午夜之后
> - 生成时 HEAD：`c05a3d26651693d0346db09e1b86220c3965d757`（branch `dev_openworld`）
> - 生成时工作树：**clean**（SPACE-01 的全部改动已由制作人在 2026-09-19 00:32:36 提交为 `c05a3d2`）
> - 关联：[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)｜[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)｜[244 MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)｜[243 MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)｜[242 MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)｜[240 Editor 工具链](240-editor-toolchain-cleanup-2026-09-15.md)｜[2N 系统真源](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)｜[ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)｜[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)
>
> 本文件**不是**新的架构真源；它是对当前仓库真实状态的登记与索引。系统正文仍在 `docs/20-systems/**`，决策仍在 `docs/40-process/43-decisions/**`。

---

# Archived Recovery Snapshot（2026-09-18～19，不可执行）

以下 §1～22 保留当时事实与审计过程；所有“当前／下一步／恢复顺序”只描述当时状态。

# 1. Executive Summary（历史）

本项目是 **Unity 2D top-down 仙侠 RPG**（修仙世界模拟 + 角色扮演 + 战略层）。

当前阶段的真实结论：

- **Continuous Surface（连续地表世界）已经成为正常的 Outdoor 世界。** 一个大陆一张 Final Continuous Surface，正常户外 gameplay 使用 exact `WorldPosition` + `SurfaceId` + `SurfaceGroundNavigation`，**没有** Outdoor LocalMap 切换。
- **Hex / Outdoor LocalMap 正在退役。** Hex 字段与旧 LocalMap 代码仍大量存在于仓库中，但已被降级为 **Legacy / Derived Compatibility**，不再是正常产品的 authority。
- **WorldComposer / FineEditor 已经建立**（MAP-01，Producer Accepted / Sealed）。
- **Surface WorldMap 已经建立**（MAP-02，Producer Accepted / Sealed）。
- **Normal Gameplay Surface authority 已完成**（MAP-03，2026-09-17 Producer Accepted / Sealed；封板提交 `b04920b`）。
- **MAP-04（物理删除 Legacy）尚未结束**：已完成第一批大清理（外层 Editor、旧 Content 地图集、旧 WorldRegion 管线），剩余 consumer 仍多。
- **Separate Space / Cave 系统正在 SPACE-01 WIP**：主体实现已完成并已提交（`c05a3d2`），但存在明确的 deferred hardening，**尚未 Producer Accepted / Sealed**。

**必须明确：当前不是一个“坏掉的中间版本”。**

- 绝大部分 Continuous Outdoor 正常玩法（New Game 开局、精确地表旅行、旅行存读档、WorldSite 连续归属、FactionFlag 世界坐标放置与存读、NPC/FormalArmy 连续移动、Outdoor CharacterEncounter → 独立战场 → 战后精确回归、农田/储藏室/恢复处等 Continuous asset）已经可以正常运行，并已由制作人在 MAP-03 人工验收。
- 本轮离线验证（2026-09-19）：`XianXia.Core`（470 sources）、`XianXia.Data`（80）、`XianXia.Unity`（147）离线 C# 编译 **ALL_OK，0 error**（4 条既有 warning）。

**当前未完成主要集中在三处：**

1. **MAP-04 最终 Legacy cleanup**（Hex / WorldRegion / Outdoor LocalMap / 历史测试 fixture）；
2. **SPACE-01 的边缘 ownership / persistence hardening**（transition membership、逃离所有权、失能/尸体/落单、`PartyWorld` 语义、洞内 NPC 落点持久化）；
3. **Cave / Separate Space 的最终封板**（制作人人工验收 + 正式 seal）。

---

# 2. 本轮 Snapshot Audit 依据

本轮**不是**根据旧聊天或旧文档推断，而是同时对照：当前 git 状态 + 当前未提交/已提交代码 + 当前正式 Content + 当前 process docs。

实际读取/核对：

| 类别 | 实际核对对象 |
|---|---|
| Git | `git status --short`、`git diff --stat`、`git diff`、`git log -10 --oneline`、`git branch --show-current`、`git remote -v`、`git reflog`、`git show --stat`（`c05a3d2` / `54141d1` / `596d9c9`） |
| 正式文档 | [2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[41-roadmap](41-roadmap.md)、[42-devlog](42-devlog.md)、[240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)、[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)、[246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md) |
| ADR | ADR-0036（地图方向）、ADR-0037（Editor 工具链／旧 Content 迁移）、ADR-0031／0032／0033／0035／0026／0023（Continuous World / SiteCore / Encounter / Squad / PlayerParty / Battle 时间） |
| 代码 | `SeparateSpaceTransitionService`、`SeparateSpaceResolver`、`SeparateSpaceExitEdgeTrigger`、`SeparateSpaceSessionSnapshotRestore`、`SeparateSpaceCombatPolicy`、`LocalMapSession`、`LoadedLocalMapPlacementSnapshotRestore`、`HostSeparateSpaceExitTrigger`、`HostCommandBridge`、`HostNpcContextMenu`、`PlayableHostBootstrap`、`HostSnapshotSessionRehydration`、`LocalMapVisibility`、`SnapshotActiveControlledLocalMapResolver`、`WorldSnapshot` |
| Content | `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`（解析：6 SiteRegion / 11 FactionFlag / 76 SitePlacement（含 6 controlCore）/ 21 SitePlace / 646 chunk / 24 openingEntityAnchor；cellSize 0.028、origin (-1.4,-1.4)、1900×850 Surface Cells）、`Sites/sites.json`、`Maps/ch01_cave_map.json`、`Maps/strategic_encounter_arena.json`、`LocalPlaces/ch01_cave_places.json`、`LocalPlaces/strategic_encounter_arena_places.json` |
| 工具链 | `ExternalTools/ContentAuthoring/EditorManifest.json`（当前 10 项）、`Apps/*.exe` 时间戳、`build-all.log` 末次记录 |
| 检索证据 | `findstr` 全仓库统计（见 §16）：Hex 字段名消费者 113 文件；`WorldRegion` 15 文件；LocalMap compat token 35 文件；Hex 命名文件 36 个 |
| 离线验证 | `tools/offline-compile.ps1`（Core/Data/Unity，2026-09-19，ALL_OK） |

### ⚠ 本轮审计期间发生的并发提交（重要）

审计开始时工作树有 46 项未提交改动（SPACE-01）。**审计进行中，制作人在 2026-09-19 00:32:36 从外部（非本会话）提交了 `c05a3d2`「先传一版洞府也好了」**，把当时全部 SPACE-01 代码 + 文档 + `tools/space-01-sanity.ps1` 提交并推送到 `origin/dev_openworld`。因此：

- 审计开始时的工作树快照仍完整保留在本文件 §22；
- 审计结束时工作树为 clean，HEAD = `c05a3d2`；
- **本会话没有执行任何 `git add` / `commit` / `push`**，也没有修改任何 SPACE-01 代码。

---

# 3. Milestone Status Table

| # | Milestone | Status | Producer Acceptance | Git checkpoint | Current meaning | Remaining work |
|---|---|---|---|---|---|---|
| 0 | **Editor Toolchain Cleanup** | **Accepted / Sealed** | 已验收（[240](240-editor-toolchain-cleanup-2026-09-15.md)） | 已提交 | 唯一 manifest（`EditorManifest.json`）为 Editor metadata 真源；Build All 为 staging all-or-nothing；正式输出平铺在 `Apps/<Editor>.exe` | 最后一次成功 Build All 产物**陈旧**（见 §17 问题 6）：`Apps/` 仍含已删除的 `RegionEditor.exe`／`WorldGraphEditor.exe` |
| 1 | **MAP-01 — WorldComposer / FineEditor Production V1** | **Accepted / Sealed（2026-09-16）** | 已验收（[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)） | 已提交 | 大陆级 macro authoring + 逐 Surface Cell 精修 + CompositionEngine/Bake + Legacy Migration Bridge + 荒村（青石荒村）迁移 | 自动水文、道路 A*、detail scatter、minor POI、terrain compatibility matrix、Runtime Chunk profiling 仍属后续范围 |
| 2 | **MAP-02 — Continuous Surface WorldMap** | **Accepted / Sealed（2026-09-16）** | 已验收（[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)） | 已提交 | WorldMap 由 exact `WorldPosition` 投影/反投影；Surface terrain/forest cache；固定 Header + 地图视口 + 按需 Flyout；Site/Flag marker 世界空间缩放 | 无（该 milestone 范围已封板）；不表示“完全去 Hex” |
| 3 | **MAP-03 — Normal Gameplay Surface Authority Cutover** | **Accepted / Sealed（2026-09-17）** | 已验收（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)） | `b04920b`（`docs: seal MAP-03 continuous surface cutover`） | 正常玩法 authority = exact WorldPosition / SurfaceGroundNavigation / Continuous SiteCore / Actual Control；Hex 与 Outdoor LocalMap 降级为 Legacy / Derived Compatibility | MAP-04 负责物理清理；MAP-03 本身不删除兼容实现 |
| 4 | **MAP-04 — Physical Legacy Cleanup** | **In Progress / Paused / Not Accepted** | **未验收**（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)） | 部分已提交：`596d9c9`（第一批大清理）+ `54141d1`（第二批 + 回归修复）；**无 seal 提交** | 逐 consumer 删除 Hex / WorldRegion / Outdoor LocalMap / 旧 Editor / 旧 Content | 见 §15「Still remaining」；另有未通过的 Completion Gate（Build All 切换失败、Legacy Hex gameplay 源码面仍广、WorldMap 视觉未 Unity 验收） |
| 5 | **SPACE-01 — Separate Space / Interior Transition V1** | **Implementation Complete / Producer Acceptance Pending**（**不是 Accepted**） | **未验收 / 未封板** | `c05a3d2`（2026-09-19 00:32:36，已 push） | 统一 Separate Space 模型（Cave／Interior／Dungeon／SeparateMap）；Cave 为第一份样板；Enter/Leave Domain 收口到 `SeparateSpaceTransitionService`；in-place combat；physical exit；additive snapshot | **Deferred Final Hardening A–F**（§14）+ 制作人人工验收 checklist；封板前必须先做 hardening |

**说明：** `c05a3d2` 的提交信息「先传一版洞府也好了」表明制作人自测认为 Cave 基本好用；但**本仓库没有任何 Producer Acceptance / Seal 记录**，245 与 246 都明确写着未验收。**不要把它写成 Accepted。**

编辑工具链当前状态（`EditorManifest.json`）：Active 8 项 = `PackageBrowser`、`CharacterNpcEditor`、`ManualArtEditor`、`QuestEditor`、`EventEditor`、`WorkAreaEditor`、`WorldComposer`、`FineEditor`；Legacy Compatibility 2 项 = `LocalPlaceEditor`（仅室内/洞穴/独立地图）、`MapEditor`（仅室内/洞穴/独立战斗地图）。`WorldGraphEditor` 与 `RegionEditor` **已不存在**（项目、启动器、manifest 项均删除）。

---

# 4. 当前 Authoring 架构（MAP-01 已建立，固定术语）

## 4.1 比例与术语（**冻结，不得重新争论**）

| 术语 | 比例 / 尺寸 | 产品职责 | 非职责 |
|---|---|---|---|
| **Surface Cell** | 1×1 | **最小 gameplay terrain unit**：terrain / walkability / footprint / water / road / farm / fine edit | 不是运行块 |
| **World Editor Cell** | 10×10 Surface Cells | WorldComposer macro terrain authoring | runtime 不读取 |
| **Runtime Chunk** | 50×50 Surface Cells | **只用于 streaming / materialization / navigation cache** | **不是 authoring authority**、不是 Site 尺寸、不是行政范围、不是编辑格 |
| **WorldSite Blueprint** | 任意整数 Surface Cell 尺寸 | 固定 Site 的精修布局与 bake 输入 | 不是 runtime scene / map piece |
| **Detail Patch** | 任意 Surface Cell 尺寸 | 局部覆盖与手工修饰 | 不是 chunk |
| **WorldComposer** | — | **whole-continent authoring**（大陆级 macro） | 不做逐格精修 |
| **FineEditor** | — | **1×1 Surface Cell local authoring**（Blueprint / DetailPatch） | 不绘制整片大陆 |
| **Final Continuous Surface** | 一大陆一张 | runtime 户外地理唯一真源 | 不保留 source authoring pieces |

当前主 Surface 实测：`base:surface_main_wilderness_v1`，cellSize `0.028`，origin `(-1.4, -1.4)`，**1900×850 Surface Cells**，**646 个 50×50 chunk**（38×17）。Composer 视角为 190×85 World Editor Cells。

## 4.2 Content 分层

**Base Terrain（基础地形，仅三种）：** `Plain`、`Mountain`、`Water`
**Overlay（覆盖层，仅一种）：** `Forest`
**Path（曲线 overlay）：** `River`、`Road`
**World Object（显式世界物件）：** 树、桥、岩石等（`WorldObjectPlacement`）

- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- Road、River 是带控制点、宽度/等级的 Surface 坐标曲线 overlay，**不是单独地图**。
- Authoring schema v3：基础地形只有 `Plain / Mountain / Water`，覆盖层只有 `None / Forest`；旧 v1/v2 制作源加载时迁移；未设置的 Fine Cell = `Inherit`。

**MAP-01 V1 固定合成顺序（不得随意重排）：**
`Macro Base Terrain → Forest Coverage → River（最终基础地形为 Water）→ Road → Detail Patch terrain override → WorldSite Blueprint terrain override → Composition World Object / Blueprint object overlay → Preview / Final Continuous Surface Bake`

Preview 与 Bake 必须消费**同一个** `CompositionEngine`；确定性只依赖 world seed、全局 Surface 坐标与 source intent，**不依赖随机调用顺序**。

## 4.3 Authoring Source 与 Runtime Publish 分离（硬规则）

| 类别 | 谁编辑 | 谁消费 | 内容 |
|---|---|---|---|
| **Authoring Source** | WorldComposer / FineEditor | 只有 baker | `WorldCompositionDocument`、`WorldSiteBlueprintDocument`、`DetailPatchDocument` |
| **Runtime Generated Content** | Bake 产生 | runtime loader（只读） | Final Continuous Surface、Runtime Chunk、final terrain/geography、object placements、WorldSite 位置与 content、navigation input、WorldMap LOD/cache input |

- Authoring Source **不得**继续作为正常 runtime Data 直接塞进 `Content/BaseGame/Data` 被 runtime loader 当正式 gameplay definition 加载。
- 运行时**不需要知道**某个位置来自哪个 Blueprint / Detail Patch / 旧 LocalMap。
- 当前 `base:surface_main_wilderness_v1` 仍是混合容器（chunk source + old Site bake + LocalMap bridge + fallback）；其未来等价物是**纯 bake runtime output**。

## 4.4 编辑器 UI 语言

**WorldComposer / FineEditor 的 user-facing UI 目前要求中文。**

---

# 5. WorldComposer UX 决策（**以后不得重新争论**）

- WorldComposer coarse authoring 使用 **Area Brush**（1/2/4/8/16 编辑格、连续 stroke、Fill、Eraser、框选、pan/zoom、grid）。
- **River / Road 使用 path / spline**（Catmull-Rom 控制点；河流保存宽度；道路保存 `Trail / SmallRoad / Road / MajorRoad` 等级；交叉口可 `Unresolved` 或标记 `Bridge / Ford`）。
- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- **Road / Bridge / tree 可作为 authored world assets**（Blueprint / Composition 显式对象 placement）。
- **Runtime Chunk 只作为 Debug overlay** 显示（Runtime Chunk ≠ authoring unit，不进任何 gameplay authority）。
- **Terrain Expansion deterministic**（边缘展开由 world seed + 全局 Surface 坐标 + 周边 source intent 决定）。
- **Authoring Source 与 Runtime Publish 分离**（见 §4.3）。
- Blueprint 支持 quarter-turn rotation 与通用对象 placement（稳定 id、kind、Content/Asset 引用、局部 Surface 坐标、footprint、rotation、metadata）；缺失/损坏链接、越界、重复 id、重叠、未解决道路河流交叉口进入 Problems。
- 地图缩放：**Editor 用 `Alt + mouse wheel` zoom**。
- **WorldMap strategic marker（Council Hall / FactionFlag / Site label）属于 world-space presentation：**
  - 地图放大 → 图标和文字**一起变大**；
  - 地图缩小 → 图标和文字**一起变小**；
  - **不是** fixed-pixel HUD marker。
- **Header / Flyout 等 UI chrome 才是 fixed screen-space。**

---

# 6. Current Continuous World Runtime 架构

## 6.1 Normal Outdoor authority（MAP-03 之后）

- **SurfaceId** + **exact WorldPosition**（Canonical WorldPosition）
- **SurfaceGroundNavigation**（换路、寻路、walkability、river 不可通行 / bridge 可通行）
- **WorldSite CoreWorldPosition**（fixed Council Hall 精确世界坐标）
- **Actual Administrative Control**（与实际领土范围分离）
- **Continuous placements**（农田、储藏室、恢复处、工作区等 Continuous asset）
- **Runtime Chunk 3×3 streaming 仍保留**（只影响表现、缓存与近场 runtime，不是行政/建筑身份/编辑边界）

## 6.2 Hex 字段的当前地位（**已降级**）

`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`HexPath`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`StrategicAnchor`、`BattleAnchorHex`、旧 Outdoor `LocalMapId`、旧 `mapLayout`

→ MAP-03 之后全部为 **Legacy / Derived Compatibility**。它们仍存在于代码、Save 与 Content 中，但**不再是 Main Continuous Surface 正常 Gameplay 的 authority**。

> “代码里还能 grep 到 Hex”**不是**判定未完成的依据；判定依据是它是否仍决定正常 gameplay 的位置/路线/合法性。MAP-04 的剩余项见 §15。

## 6.3 独立空间（Separate Space）架构（SPACE-01）

| | Continuous Outdoor Surface | Separate Space |
|---|---|---|
| 例 | `base:surface_main_wilderness_v1` | `base:map_ch01_cave` |
| 位置权威 | Continuous `WorldPosition` + SurfaceId | `ActiveMapLayoutId` + Interior local position |
| Streaming | Runtime Chunk | 独立 MapLayout / WalkGrid / LocalPlaceSet |
| 进入后 | — | 暂时离开 Outdoor playable presentation（Domain 保留） |
| 离开后 | — | 回到进入前 **exact Surface return** |

- Domain 真源：`SeparateSpaceTransitionService`（`ExplorationService.EnterLocalMap / LeaveLocalMap` 是薄封装）。
- Session 真源：`LocalMapSession` **已收窄为 Separate Space Session**（类型名保留兼容，语义不再是“所有局部世界”）。
- 正式可见性入口：`LocalMapVisibility.IsEntityVisibleInCurrentPlayableSpace`（SeparateSpace-first）。
- `SpaceKind`：`Cave / Interior / Dungeon / SeparateMap`，由 `MapLayoutDefinition.spaceKind` + `SeparateSpaceResolver` 解析（**业务代码禁止写死 map id**）。

---

# 7. WorldSite / Territory 当前最终规则

## 7.1 Site Core

| | Permanent Site | Runtime FactionFlag Site |
|---|---|---|
| Core | fixed Council Hall（议政厅） | 可拆卸 FactionFlag |
| `CoreIsRemovable` | `false` | `true` |
| WorldMap marker | house / council-hall symbol | flag symbol |
| anchor | exact `CoreWorldPosition` | exact flag world position |

## 7.2 控制与范围

- **Actual Control 与 theoretical range 分离**：实际行政资产 / 建造 / 公库存取消费 **Actual Administrative Control**。
- **同势力范围：WorldMap union presentation**（`WorldSiteActualControlOverlayBuilder.BuildFactionUnion`）。
- **FactionFlag 被敌人打爆 → 直接摧毁**；**议政厅不可直接永久删除**，只能走既有 Site warfare / occupation 规则。
- 150×150 Surface Cells 只是**一级 SiteCore 的理论行政控制范围**，不是 Chunk / Blueprint / World Editor Cell / authoring 最小尺寸。

## 7.3 当前永久 Site（Permanent Sites）实测

来自 `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` → `siteRegions`（**6 个**）：

| SiteId | DisplayName | siteType | arrival (WorldX, WorldY) | ownerFactionId |
|---|---|---|---|---|
| `base:site_chengzhen` | 青石镇 | Town | (18.61955, 11.25) | — |
| `base:site_guanai` | 青石关 | Pass | (21.65064, 16.5) | `base:faction_xijin` |
| `base:site_huangcun` | 青石荒村 | Village | (6.49519, 11.25) | `base:sect_huangcun_labor` |
| `base:site_lingdi` | 灵地 | SpiritLand | (38.10512, 9.0) | `base:faction_shuofeng` |
| `base:site_linjian` | 林间 | Forest | (12.99038, 7.5) | `base:sect_huangcun_labor` |
| `base:site_zhuangyuan` | 庄园 | Village | (29.01185, 6.75) | `base:faction_nan_yan` |

- 六处均已拥有 **fixed Continuous `controlCore`**（`sitePlacements` 中 `kind=controlCore` 共 6 个）；青石镇、青石关、灵地、林间、庄院的五棵树原有连续世界中心被用作固定议政厅 Core 中心（见 [244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）。
- 另有 `Content/BaseGame/Data/Sites/sites.json` 中的 `base:site_abandoned_cave`（废弃洞府）——它是 **Separate Space 入口 Site（洞穴）**，不是永久 controlCore Site。
- `factionFlags` 实测 **11 面**（`base:flag_player_origin`（主角据点）＋ 荒村劳工 4 ＋ 渔村 3 ＋ 西晋 3），全部 `createsWorldSite: true`、`coreLevel: 1`。
- `openingEntityAnchors` 实测 **24 条**（New Game 断言 24 anchors / 24 spawned / 24 presence）。

---

# 8. MAP-02 WorldMap 正式产品规则（已封板）

- **Continuous Surface only** 是正常产品路径；**不显示 gameplay Hex grid**。
- Surface bounds、**exact WorldPosition click**、Surface travel 目标、Actual Control、Site Core marker、FactionFlag marker、NPC Squad、角色/势力面板、坐标网格。
- **UI 结构：** `Fixed Header` + `Full Map Viewport` + `Optional Inspect Flyout`（底部支援控制条仍固定）。
- **地图 content 不允许覆盖 Header**；地图渲染统一在 `BeginGroup(mapRect)` 内使用局部 `Rect(0,0,w,h)` 投影，组结束后一次性把 marker 命中区域转屏幕坐标（不再有永久右栏与 `GUI.matrix` 平移抵消）。
- **坐标网格：** 稀疏正交线，**无 X/Y 数字标签**（按缩放取 1/2/5×10^N Surface Cell 间隔，不进存档/寻路/拾取）。
- **Site marker：** Permanent Site → house / council-hall symbol；Runtime Flag Site → flag symbol；anchor = exact `CoreWorldPosition`。
- **图标/名字：** world-space scaling with map zoom；名称从图标右侧 8 Surface Cells 起排，字号按投影后标签高度缩放。
- **WorldMap 打开时是独立 planning overlay：** 打开时关闭背包/建造/任务 UI、冻结 LocalVisible 旅行、顶层输入阻断；**WorldMap open 不移动 Player**；**closing WorldMap 才继续 AutoTravel**。
- 无 Surface 时显示「当前世界没有可用的连续世界地图。」并走 Content error，不伪造 Hex 地图。

---

# 9. MAP-03 已验收的 Normal Gameplay Authority

制作人于 2026-09-17 人工验收通过（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）：

- **PlayerParty Surface travel**：以精确当前位置与目的地查询 `SurfaceGroundNavigation`，不以 HexPath 作为启动条件；最终按物理距离结算；成员 Presence = `SurfaceId + WorldPosition`。
- **exact destination**：WorldMap 点选与路线目标共用 exact WorldPosition。
- **travel Save/Load**：保存精确目的地、到达半径与 Site 意图；Content shell 注册导航后按保存起点重新求路。
- **New Game opening exact anchor**：`openingSurfaceId` + 已发布 Surface Site/FactionFlag + `openingEntityAnchors`；不调用 Hex strategic bootstrap，不加载 HexWorld JSON，不激活 Outdoor WorldRegion。
- **WorldSite continuous membership**：正常 WorldSite 上下文由 continuous 行政/物理查询判定，不回落 Hex footprint。
- **FactionFlag world-space authority**：先以 `SurfaceId + WorldPosition` 验证地表与 Actual Control；`StrategicAnchor` 只做派生兼容；读档不要求保存的 AnchorQ/R 落在 Hex grid 内。
- **NPC Squad / Background Character continuous movement**：后台 NPC 到连续 Site 走地表寻路 + authored Site arrival；`SquadWorldMotion` 与个人后台旅行以精确起点、Surface route 和 authored arrival 求路。
- **Battle world-space anchor**：`BattleParticipantSnapshot` 增加 `HasBattleAnchorWorldPosition` / `BattleAnchorWorldX/Y` / `BattleAnchorSurfaceId`，offer 冻结时从实际接触点记录。
- **Residual world-space authority**：弥留/尸体的个人 `SurfaceId + WorldPosition` 可独立成为稳定空间 authority。
- **Outdoor Site 不切旧 Outdoor LocalMap**：正常 WorldSite 上下文不再装载 Outdoor LocalMap。
- **Interior / Cave 仍允许 separate space**（SPACE-01 承接）。
- **CharacterEncounter 正式战斗规则：**
  `Continuous Outdoor → BattleOffer → independent battle field`（这是当前 Outdoor 正式战斗规则）。

---

# 10. Battle 当前封板规则（**不要重新设计**）

**Outdoor CharacterEncounter（ADR-0023 / ADR-0033 / Phase 4 已封板）：**

- unified `BattleOffer`
- participant roster（参战名单）
- manual / auto battle
- **independent battlefield**（同源独立遭遇空间）
- **main world freeze**（具名 CharacterEncounter pause owner；见 ADR-0023／0033）
- battle participant only
- **post-battle exact return**（返回战前精确世界位置）
- report（战报）
- active-character takeover（战斗中控制权接管）
- lingering / downed / residual persistence（弥留、倒地、残留）

**Squad / PlayerParty spatial authority 已支持组内归属与每名真实角色位置**；战后分别按各自战前 world anchor 回写，不建立共同 Army 坐标。

---

# 11. SPACE-01 完整状态

## 11.1 目的

SPACE-01 建立 **Continuous Outdoor ↔ Separate Space** 的统一切换机制，而不是继续给 Cave 打零散补丁。

**Separate Space 将来用于：** Cave、Interior（室内）、Dungeon、Secret Realm（独立秘境）、其他 isolated playable spaces。
**当前 Cave 是第一份样板**（`base:map_ch01_cave`，`spaceKind: cave`）。

## 11.2 已实现的架构（`c05a3d2`）

- **Domain 真源收口：** `SeparateSpaceTransitionService.Enter / Leave`；Host 只负责 presentation rebuild / input / registry place activation。
- **Enter 流程：** resolve entrance/map/LocalPlaceSet/spawn → validate → **capture Outdoor exact return（SurfaceId + WorldX/Y）→ 建立 SeparateSpaceSession → 移动 PlayerParty membership + deterministic formation → Host 切换 presentation**。任意中途失败：Host 回滚 PlaceSet，**不得半进洞**。
- **Leave 流程：** validate session → clear Interior places/session → **restore exact Surface return** → Host 重新激活 Surface streaming/neighborhood/camera。**禁止**返回 Hex / Site center / fake entrance center。
- **Return authority：** 只有 `ReturnSurfaceId` + `ReturnWorldX/Y` + `ReturnLocationId`（不依赖 Hex / AnchorHex / PresenceHex / Overworld MapLayout）。
- **Occupant / visibility：** Separate Space 内只认 PlayerParty occupants + Active MapLayout/LocalPlaceSet 居民；Outdoor WorldSite / Surface chunk / Background NPC 不参与当前 playable presentation（Domain 保留）。
- **Combat policy：** `SeparateSpaceCombatPolicy`。Separate Space 内为 **direct in-place combat**（当前 MapLayout）；`CharacterEncounterService.RequiresEntry` 在双方均属 active Separate Space 时返回 `false`；`HostNpcMeleeAssault` / `HostNpcContextMenu` / `HostCharacterEncounter.Request` guard 均走 in-place；`HostPlayerPartyController.TickCombatFollow` 对 occupants 自动协战。
- **Entry Party Rule：** 进入 Separate Space = 当前真正随队成员**全部自动**transition（authority：`PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty`）；**已删除 `HostLocalMapEnterPrompt` 队员选择弹窗**；Host API = `IssueEnterSeparateSpace(leader, entranceId)`，旧 `IssueEnterLocalMapWithParty` 仅 legacy wrapper（忽略 party 数组）。
- **Physical Exit Trigger：** `HostSeparateSpaceExitTrigger` + Core `SeparateSpaceExitEdgeTrigger`。只有 **Active Controlled Character** 的 View 进入 authored Exit geometry 才触发；edge-trigger 防 spawn/Load 立刻离开；无确认窗；战斗中/失能/modal 不触发。**已删除**：右键「离开」、`TryPickInteriorExitAtMouse` pick 路径、Action Menu「离开洞窟」。
- **Snapshot（additive）：** `StrategicSnapshotDto.SeparateSpace`（`SeparateSpaceSessionSnapshotDto`：IsInSeparateSpace / SpaceKind / ActiveMapLayoutId / ActiveLocalPlaceSetId / Entry / Return / ReturnSurface + WorldX/Y / OccupantIds / ActiveCharacterId / EntryReason）。
- **Presentation restore 优先级（严格）：** 1) **Active Separate Space** → `RebuildSeparateSpacePresentationAfterLoad`（禁止 Outdoor Surface 抢先） 2) Independent CharacterEncounter → Encounter restore 3) Continuous Outdoor → `ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore`。Separate Space restore **不重新调用 `Enter()`**。
- **旧档迁移：** 无新 DTO 但 placements/推断 mapId 指向 Cave/Dungeon/Interior/Arena 时，用 PlayerPartyTravel exact position 重建 return authority；信息不足 → 明确 `SnapshotInvalid`，**不默默送回荒村**。
- **Content 边界：** `base:loc_ref_cave`（Outdoor entrance only）／`base:loc_cave_chamber`（Interior only）／`base:map_ch01_cave`（`spaceKind: cave`）。

## 11.3 Cave 当前已经基本工作的流程（依据代码 + 制作人自测记录）

| 阶段 | 当前真实行为 |
|---|---|
| **Discovery** | Hidden Cave entrance → 附近 strange-presence hint → Survey（神识）→ reveal entrance。Reveal authority = **PlayerParty knowledge**（`KnownSites`），**不是**全球 NPC knowledge。Survey 只做 `KnownSites.Discover` + reveal visual + 轻量 toast；**不**激活 Interior places、不改 ActiveMapLayoutId、不建 Session、不 deactivate Surface、不移动 Party。 |
| **Approach** | Revealed + Far → 右键 = 只走向 entrance；Revealed + Near → 出现 Enter action。 |
| **Entry** | 当前 PlayerParty traveling members **自动一起进入**（不再有 participant checkbox 选择）。 |
| **Cave Combat** | Separate Space 内 **direct in-place combat**：不进 BattleOffer、不建 CharacterEncounter、不开第二个独立战场；current Party followers 自动 assist。 |
| **Save/Load** | Cave 内 Save/Load 已基本验证可用（Load 后留在洞内、不默默回 Outdoor）；`[SeparateSpaceRestore]` / `[SeparateSpaceRestoreInvariantFailure]` 诊断日志已就位。 |
| **Exit** | **Physical Exit Trigger**：Active Controlled Character 实际进入 Exit geometry 才触发；不是右键菜单；返回 exact Outdoor position。 |

---

# 12. SPACE-01 Final Hardening（本轮已实现 · 待人工验收）

> 主体实现 checkpoint：`c05a3d2`。Final Hardening 代码在工作树**未提交**。
> 状态仍为 **Implementation Complete / Producer Acceptance Pending**。详见 [246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)。

| 项 | 状态 |
|---|---|
| A. Enter membership fallback 删除 | **Done** — 空成员明确失败 |
| B. Leave 严格 `ShouldMemberTransitionWithParty` | **Done** |
| C. stranded／downed／corpse 不错误 teleport | **Done**（代码）；待 Unity 人工验 |
| D. `PartyWorldPresenceMode.InSeparateSpace` | **Done**（additive=5；0–4 不变） |
| E. active-map 全部 Character local placement | **Done**；restore 不污染 membership |
| F. Producer Accepted／Sealed | **Pending** |

MAP-04 继续 **Paused / Producer Acceptance Pending**。

---

# 13. MAP-04 当前真实做到哪里

> **MAP-04 仍然 In Progress（Paused）。不要写完成，不要写 Accepted。**

## 13.1 Already migrated / removed（已提交，证据为 `596d9c9` / `54141d1` / `c05a3d2` 与当前仓库实测）

**A. Runtime / Bootstrap**

- `openingSurfaceId` 成为 Scenario 唯一 opening authority；`WorldRegionBootstrap` **已删除**（独立地图改用 `InteriorLocalPlaceBootstrap`）。
- `HexStrategicSessionBootstrap`、`HexStrategicMapBootstrap`、`HexTestWorldBootstrap`、`HexWorldStressMapBuilder`、`Ch01HexPrototypeMapBuilder`、`Ch01ScenarioStrategicSetup`、`StrategicBootstrap`、`HexWorldContentLoader`（+legacy adapter）**已移出 Runtime**，只在 `Assets/Tests/EditMode/LegacyHexFixtures/` 保留（8 个 fixture 类）。
- `HostFormalArmyContinuousPresenter`、`WorldSitePresentationLayer`、`FactionFlagWorldMapPresentation`、`BattleEngagementWorldMapDebug`、`HexMapMousePick`、`HexMapViewportProjection`、`HostHexGridDrawing`、`HostHexWorldRenderer`、`HexMapEditorService`、`WorldRegionBoard`（→ `LocalPlaceBoard`）**已删除**。
- `WorldVec2` 从 `Core.World.Hex` 移入中性 `Core.World`。
- `PlayerPartyContinuousFormationResolver`、`FormalArmyContinuousFormationResolver`、`CharacterEncounterSpatialAuthorityResolver`、`ContinuousCharacterSpatialAuthorityResolver`、`ContinuousSurfaceSessionBootstrap`、`ContinuousOpeningSpawnPresenceResolver` 等 Surface 侧 authority 已就位。

**B. Content（`Content/BaseGame/Data`）**

- **已删除**：`Worlds/ch01_hex_world.json`、`Worlds/travel_mvp_hex_world_30x15.json`、`Worlds/w1c_wilderness_acceptance_surface.json`、`Regions/world_regions.json`、**35 个 Outdoor MapLayout JSON**（含 `huangcun_01`、`player_camp_map`、各 `ch01_site_*_map`、wilderness fallback maps）、**34 个 Outdoor LocalPlaceSet JSON**、`world_node_stub_*`。
- **现存 `Maps/` 只有 2 个**：`ch01_cave_map.json`（`spaceKind: cave`）、`strategic_encounter_arena.json`。
- **现存 `LocalPlaces/` 只有 2 个**：`ch01_cave_places.json`、`strategic_encounter_arena_places.json`。
- `Regions/` 目录**已不存在**。
- 遭遇战术图由 `world_node_stub` 更名为独立 arena。
- 旧内容作为 EditMode 历史 fixture 保留在 `Assets/Tests/Fixtures/LegacyWorld/`（`ch01_hex_world.json`、`ch01_reference_map.json`），**不被当前 ContentPackage 加载**。

**C. 编辑器 / 工具链（ExternalTools）**

- `WorldGraphEditor`、`RegionEditor` **已删除**（项目、solution 项、manifest 项、启动器 `.cmd`）；`Shared/HexWorld/*`（14 个文件）与 `Shared.Tests` 的 8 个 Hex/Territory 测试**已删除**。
- `MapEditor` / `LocalPlaceEditor` 收窄为 Interior / Cave / 独立地图；`WorldComposer` 当前发布 UI 去掉“兼容模式”。
- `EditorManifest.json` 成为唯一 metadata 真源；`publish.ps1` 为 staging all-or-nothing。

**D. Host / WorldMap**

- `HostWorldMapPanel` 收敛为 Surface 地图（大幅删除 Hex renderer / grid drawing / picker / projection）；Surface-only WorldMap product 已建立。
- Outdoor opening authority、Site/FactionFlag 连续 anchor、Actual Control overlay 均已迁到 Surface。
- **部分** Hex / WorldRegion / Outdoor LocalMap fallback 已移除（Surface route 失败不再回退 Hex）。

**E. SPACE-01 附带的 legacy 删除**

- 删除 `HostLocalMapEnterPrompt`（478 行）；右键离开、Action Menu「离开洞窟」、`TryPickInteriorExitAtMouse` pick 路径均删除。

## 13.2 Still remaining（**2026-09-19 重新 grep 当前仓库的结果，不是旧聊天假设**）

| 类别 | 实测残留 | 数量 | 说明 |
|---|---|---|---|
| Hex 拓扑/表现源码 | `Assets/Scripts/Core/World/Hex/*`（HexWorld、HexCoord、HexMath、HexMetrics、HexPathfinder、HexTerrainCatalog/Presentation/Type/VisualInset、HexTravelMode、HexWorldLayout/MapRenderBounds/Scale、HexCell） | 14 文件 | 大量仍被 derived compat 消费者引用；不能一次性删 |
| Hex strategic 服务 | `ArmyHexBattleAnchorService`、`ArmyHexCommandService`、`ArmyHexLingeringArrivalService`、`ArmyHexMigrationHelper`、`ArmyHexPosition`、`ArmyHexPursuitService`、`ArmyHexTravelService`、`BattleEngagementHexDistance`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery`、`HexFootprintSpatialGeometry`、`HexFootprintSpatialMapping`、`HexResidualContextQuery`、`HexRightClickResolver`、`HexStrategicLegacyGuard`、`HexStrategicRuntime`、`PlayerPartyHexPursuitService`、`PlayerPartyHexTravelService` | 19 文件（Hex 命名文件共 36 个） | `PlayerPartyHexTravelService` 的 HexPath/Footprint 旅行、Site `PresenceHex`、Flag strategic anchor 仍在 |
| Hex 字段消费者 | 命中 `CurrentHex/DestinationHex/FinalDestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/StrategicAnchor` 的文件 | **113 文件** | 多数为 derived/compat + Snapshot DTO；需逐 consumer 判定 |
| WorldRegion | 含 `WorldRegion` 的文件（`WorldRegionDefinition`、`DefinitionSchema`、`ContentPackageLoader`、`ContentReferenceValidator`、`MapLayoutDefinition/JsonLoader`、`OpeningScenarioDefinition`、`SpawnZoneApplier`、`LocalPlaceBoard`、`ContinuousMaterializePlacementSync`、`ContinuousOutdoorSurfaceRuntime`、`HostSnapshotLocalPlacementCaptureSync`、`PlayableHostBootstrap` 等） | 15 文件 | definition parser 只用于旧包输入；Content 侧 `world_regions.json` 已删 |
| Outdoor LocalMap compat | `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelongingQuery/Explain`、`LocalMapHexDirectionProjection`、`BackgroundLoadedLocalMapArrivalDebug`、`PlayerPartyWildernessTransitionService`、`LoadedLocalMapPlacementSnapshotRestore` | 35 文件命中 compat token | 旧 Outdoor LocalMap snapshot ID 在 Host restore 时转 Surface；旧遭遇图 ID 映射到 arena |
| 旧 Content exporter | `Assets/Scripts/Data/Content/HexWorldContentExporter.cs`、`HexWorldContentDefinition.cs` | 2 文件 | 仍编入 Runtime |
| 历史测试 fixture | `Assets/Tests/EditMode/LegacyHexFixtures/`（8 类）、`Assets/Tests/Fixtures/LegacyWorld/`（2 JSON） | — | 按“不跑 Unity Test”约束未执行 |
| 旧档兼容 | `WorldSnapshot`、`StrategicSnapshotHelper`、`FormalArmySnapshotRestore`、`HostSnapshotSessionRehydration` 的 Hex 坐标/Presence/旧 LocalMapId 读取 | — | Intentional Legacy Compatibility，需先审计再删 |

**MAP-04 未通过的 Completion Gate（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)）：**

1. **Build All Apps 切换失败**：`publish.ps1` 候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚成功 → 现有 `Apps/` 仍为旧 exe（见 §17 问题 6）。
2. **Legacy Hex gameplay 源码面仍广**：当前产品**不能**声称“完整 Hex Gameplay stack 已物理删除”。
3. **WorldMap 功能回归风险**：Surface-only 组件只经离线编译，未做 Unity 视觉/交互验收。
4. **旧战略可视化编辑入口**：`WorldGraphEditor` 的 `FactionManagerWindow` / `OpeningStrategicEditorWindow` 已删；当前 WorldComposer 可发布 Surface faction flag，但**尚无等价的独立可视化外交/开局战略编辑窗口**。
5. **历史 EditMode 测试**：旧 fixture 已搬迁，但按约束未运行；其语义覆盖旧 Hex，不是当前产品验收。

---

# 14. MAP-04 过程中已经发生的主要 Regression 与修复

> 记录这些是为了让未来开发者理解“为什么代码里存在某些 guard”。简短根因，不贴长代码。真源见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md) 与 [42-devlog](42-devlog.md)。

| # | 现象 | 根因 | 修复方向 |
|---|---|---|---|
| 1 | **New Game NPC 全消失** | 删除 `WorldRegionBootstrap.ApplyOpening` 后，15 名既无 `worldSiteId` 又无 `localLocationId` 的 Ch01 NPC 失去旧 `EntityLocation`；旧 place→Site 推断失效 | 改为 **anchor-first presence**：Normal Surface 开局直接以 `openingEntityAnchors`（稳定 SpawnKey/DefinitionId/SiteId/WorldX/Y）建立 presence + 独立 anchor census 断言 24/24/24 |
| 2 | **Snapshot load 后远处 NPC 泄漏/抽搐** | presentation 与 Domain spatial authority 冲突：先有 View 再回写 Domain，且 SiteArrival fallback 覆盖精确位置 | **snapshot restore 必须优先于 opening anchor**；首帧只允许 Domain→View；loaded chunk 是硬门；不一致 → `SnapshotInvalid`；加 `[SnapshotMaterializationLeak]` 诊断 |
| 3 | **FormalArmy authored deployment 三支部队堆在一起** | `assemblySite` personal presence 与 authored deployment 竞争 authority；idle `FormalArmyMemberPresenceSync` 保留旧个人位置；`SyncLegacyFromWorldMotion()` 每次把 `UsesHexStrategicPosition` 写回 `HasPosition` 覆盖 Surface 语义 | 新增 `initialSurfaceDeployment = {surfaceId, anchorSiteId, offsetCellsX/Y}`；authored Surface 部署直接建立首次 Army anchor；普通 idle tick 只保留锚点附近 near-field 个人位置；队形不写回 canonical position；间距 3 Surface Cells |
| 4 | **CharacterEncounter `LegacyUnqualifiedPosition`** | Personal space 被当成通用要求；但 FormalArmy / PlayerParty 是 **group authority** | 新增 `CharacterEncounterSpatialAuthorityResolver` / `ContinuousCharacterSpatialAuthorityResolver`；个人空间不再是通用前置 |
| 5 | **Runtime FactionFlag Site 启动 invariant 失败** | 动态 runtime Site ≠ authored Opening SiteRegion（authored 列表不含运行时新建 Site） | 启动 invariant 区分 authored 与 runtime Site，不用 authored 列表校验动态 Site |
| 6 | **SiteCore warfare 枚举异常** | `TickOccupation` 遍历 `PlayerPartyRuntime.Members` 时，位置解析再次读取 `Members`，getter 清空并重填同一投影列表 → `Collection was modified` | getter 每次返回独立快照（**PlayerParty group spatial authority** 相关） |
| 7 | **WorldMap UI 回归** | 曾出现 Continuous Surface 透出、被规划 overlay 覆盖、Header 被地图内容覆盖、flyout 改变视口、点击优先级错乱 | 恢复不透明全屏底色与视口底色；恢复顶层输入阻断；打开时关闭背包/建造/任务 UI 并冻结 LocalVisible 旅行；固定 Header + Full viewport + 按需 flyout；战略面板点击优先级；真实 checkbox UI |
| 8 | **Cave transition 回归** | Survey／reveal 生命周期混乱（成功后 `RefreshMapStampsOnly` 触发旧 MapLayout 全图刷新导致伪切场景）；presentation 未分离；Save/Load 后回 Outdoor；右键离开语义不明 | Survey 只 reveal + 轻量 toast；`LocalMapVisibility` SeparateSpace-first；presentation restore 优先级；`SeparateSpaceSessionSnapshotRestore`；**physical exit trigger** 取代右键/菜单 |

---

# 15. Current Known Issues / Deferred Work

> 只列有证据的问题。每项含 Severity / Current behavior / Desired behavior / Likely files-services / Recommended fix direction。

## Issue 1 — SPACE-01 Final Hardening deferred

- **Severity:** High（SPACE-01 封板前置）
- **Current:** §12 的 A～F 全部仍存在（membership fallback、leave 过宽、失能/尸体未验证、`AtHex` 语义复用、洞内 NPC 落点不持久化）。
- **Desired:** transition membership 严格化；逃离只迁移 `ShouldMemberTransitionWithParty`；失能/尸体/落单不被 teleport；`InSeparateSpace` 语义；洞内 resident/enemy 落点持久化。
- **Files:** `Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`LoadedLocalMapPlacementSnapshotRestore.cs`、`Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`
- **Direction:** 先写清楚四态目标行为（对照 ADR-0019 / CW-02），再改 Core，不改 Host 行为契约。

## Issue 2 — MAP-04 final physical cleanup pending

- **Severity:** Medium-High（技术债，非当前玩法阻断）
- **Current:** §13.2 的残留仍全部存在（Hex 113 文件命中、19 个 Hex strategic 服务、15 文件 `WorldRegion`、35 文件 LocalMap compat、2 个 HexWorld 导出器）。
- **Desired:** 逐 consumer 审计后删除，或明确标注为 intentional legacy。
- **Files:** 见 §13.2 表格。
- **Direction:** 先做 consumer audit 清单（每个文件 → 谁在 normal gameplay 调用 → 是否可删/需迁移），再分批删除；**不要**在 SPACE-01 未封板时激进删除地图 runtime。

## Issue 3 — Old-save migration coverage 尚需最终验证

- **Severity:** Medium
- **Current:** 旧档迁移路径已实现（旧 Outdoor LocalMap ID → Surface；旧 Hex 人物/军队位置 → Surface 世界坐标；旧遭遇图 ID → arena；SPACE-01 legacy → Separate Space），但**没有 Unity 人工验收记录**。
- **Desired:** 至少覆盖：旧 Outdoor LocalMap、纯 Hex 人物位置、军队旅行、遭遇战术图 ID、Cave 内旧档。
- **Files:** `HostSnapshotSessionRehydration.cs`、`StrategicSnapshotHelper.cs`、`FormalArmySnapshotRestore.cs`、`SeparateSpaceSessionSnapshotRestore.TryMigrateLegacySeparateSpace`
- **Direction:** 制作人用真实旧档人工验收；信息不足时继续保持 `SnapshotInvalid` 明确失败，不做 silent teleport。

## Issue 4 — Remaining Legacy Hex consumer

- **Severity:** Medium
- **Current:** 113 个文件仍命中 Hex 字段名；其中 `PlayerPartyHexTravelService`、`PlayerPartyHexPursuitService`、`ArmyHex*`（7 个）、`HexStrategicRuntime`、`BattleEngagementHexDistance`、`HexRightClickResolver`、`HexResidualContextQuery`、`HexStrategicLegacyGuard`、`HexFootprintSpatial*`、`LocalMapHexDirectionProjection`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery` 仍在 Runtime。
- **Desired:** 正常 gameplay 不依赖任何 Hex authority；Hex 只留在明确的 legacy/derived 边界。
- **Files:** 见 §13.2。
- **Direction:** 以 `HexStrategicLegacyGuard` / `HexWorld.HasGrid` 为入口做 consumer 分类，不做全局正则删除。

## Issue 5 — Remaining Outdoor LocalMap consumer

- **Severity:** Medium
- **Current:** `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelonging*`、`PlayerPartyWildernessTransitionService` 等仍在（35 文件命中 compat token）。
- **Desired:** Outdoor 不再有 LocalMap authority；LocalMap 只剩 Separate Space（Cave/Interior/Dungeon/Arena）语义。
- **Files:** 同上 + `Assets/Scripts/Unity/Host/LocalMapVisibility.cs`
- **Direction:** 区分 “Outdoor LocalMap legacy” 与 “Separate Space（正当使用者）”；后者保留，前者清。

## Issue 6 — `Apps/` 生成产物陈旧（Build All 未在删除 Editor 后成功重跑）

- **Severity:** Medium（工具链/交付，非 gameplay）
- **Current（实测）:** `ExternalTools/ContentAuthoring/Apps/` 仍含 **12 个 exe**，包括已从仓库删除的 `RegionEditor.exe`（2026-09-16 02:13:04）与 `WorldGraphEditor.exe`（2026-09-16 02:13:10）；`build-all.log` 末次成功记录为 **2026-09-16 02:13:10 exit 0**。245 记录了 09-17 的一次 `publish.ps1` 尝试：10 项 publish 成功，但把候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚，最终 exit 1。
- **Desired:** 关闭占用 `Apps`/staging 的外部进程后重跑 Build All，得到与当前 manifest（10 项，无 RegionEditor/WorldGraphEditor）一致的扁平产物。
- **Files:** `ExternalTools/ContentAuthoring/publish.ps1`、`EditorManifest.json`、`Apps/`（gitignored 生成产物）
- **Direction:** 制作人允许后重跑一次 Build All；`Apps/` 不是版本真源，不因陈旧而 commit。

## Issue 7 — Cave 内非 occupant NPC 落点不持久

- **Severity:** Medium（见 §12 E）
- **Current:** `LoadedLocalMapPlacementSnapshotRestore.Capture()` 只保存 active map 的 **occupants**；洞内居民/敌人移动后不落点。
- **Desired:** active Separate Space 内全部真实 entity 的 local position 随 Save/Load 保留。
- **Files:** `Assets/Scripts/Core/Persistence/LoadedLocalMapPlacementSnapshotRestore.cs`
- **Direction:** 扩展 capture 范围为 active map 的全部可见 entity；注意与 `[SeparateSpaceRestoreInvariantFailure]` 的 saved placement 校验语义保持一致。

## Issue 8 — `PartyWorldPresenceMode.AtHex` 语义复用

- **Severity:** Low-Medium
- **Current:** Separate Space 激活时把 `PartyWorld.Mode` 写成 `AtHex`（5 处），使独立空间与 Hex 兼容语义混淆。
- **Desired:** 新增 `InSeparateSpace`（或等价中性值）。
- **Files:** `Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`、`SnapshotActiveControlledLocalMapResolver.cs`
- **Direction:** 加枚举值 + 迁移所有写入点 + 确认 Snapshot 序列化兼容（`Mode` 是整型）。

## Issue 9 — 两处恒等/死分支（代码卫生，不改行为）

- **Severity:** Low
- **Current（实测）:** ① `SeparateSpaceTransitionService.Leave()` 末行 `return continuousReturn ? Result.Success() : Result.Success();`；② `HostNpcContextMenu` 的 LocalCombat 分支内 `if (IsActiveStrategicCombatTarget(...)) return false; return false;`。
- **Desired:** 语义清晰（若真需区分则返回不同结果；否则删除冗余分支）。
- **Files:** 上述两文件。
- **Direction:** 纯清理；**本轮不做**。

## Issue 10 — 未验证项（不得当作已验证）

- **Severity:** Informational
- **Current:** 本轮**没有**打开 Unity、没有跑 PlayMode / Unity Test Runner / batchmode。只有离线编译（Core/Data/Unity `ALL_OK`）。SPACE-01 的 Unity 端表现（视觉、输入遮挡、真实 Save/Load 落点、physical exit 手感、失能边界）**没有**本轮证据。
- **Desired:** 由制作人人工验收。
- **Direction:** 按 §16 的 checklist 执行。

---

# 16. Do Not Regress

> 未来任何重构**必须保持**下列行为。违反即视为回归。

1. **WorldMap 不显示 gameplay Hex grid。**
2. **WorldMap 打开时是独立 planning overlay**（地图内容不覆盖 Header）。
3. **WorldMap open 不移动 Player。**
4. **closing WorldMap 才继续 AutoTravel。**
5. **River unwalkable / Bridge walkable。**
6. **Outdoor Site remains same Continuous Surface**（不切旧 Outdoor LocalMap）。
7. **Cave / Interior alone can be separate space。**
8. **Outdoor CharacterEncounter uses independent battle**（BattleOffer → 独立战场 → 战后精确回归）。
9. **Separate Space combat is in-place**（不建 BattleOffer / 第二战场）。
10. **PlayerParty current members auto-enter Separate Space**（无队员选择弹窗）。
11. **Site Core exact world position。**
12. **FactionFlag exact world position。**
13. **Battle returns exact world position。**
14. **runtime chunk != authoring unit**（50×50 chunk 只是 streaming 分区；authoring 是 World Editor Cell / Surface Cell）。
15. **no Hex authority resurrection**（不得新增任何 Hex gameplay authority）。
16. **no Outdoor LocalMap authority resurrection。**
17. **WorldMap marker 是 world-space scaling**（图标与文字随 zoom 变大小）；Header/Flyout 才是 fixed screen-space。
18. **Forest 是 overlay、可通行；River/Road 是曲线 overlay，不是独立地图。**
19. **Authoring Source ≠ Runtime Publish**（authoring 不得直接当 runtime gameplay definition 加载）。
20. **Separate Space Load 不重新 Enter**（沿用 snapshot session/occupants/saved placements）。
21. **Separate Space presentation restore 优先于 Outdoor Surface restore。**
22. **Leave Separate Space 只能通过 physical exit trigger**（不是右键菜单/Action Menu）。
23. **Actual Control 与 theoretical range 分离**；行政资产/建造/公库消费 Actual Control。
24. **Permanent Site 的 fixed Council Hall 不可直接永久删除**；FactionFlag 被敌人打爆直接摧毁。
25. **FactionFlag 世界坐标 authority 与 Actual Control 判定不得回退 Hex anchor。**

---

# 17. Recommended Resume Order（已归档）

本节原顺序已全部失效：SPACE-01、MAP-04、LEGACY-FINAL-A／B／C 与 Final Seal 后续均已验收并提交。不得复制旧 hardening／consumer audit／seal 指令重新开工；当前入口只有页首摘要与系统现状总表。

---

# 18. Historical Producer / Codex Workflow Rules（已被现行规范替代）

本节旧规则不再单独生效。现行执行与验收规则只看 `AGENTS.md`、[52 协作规范](52-ai-collaboration-protocol.md) 与页首“执行与验收规则”；特别是不编写／运行自动测试、不启动 Unity、普通实施不自动提交、“封板”才授权选择性 commit。

---

# 19. Git / Working Tree 历史快照（2026-09-19；非当前状态）

> 生成时间：2026-09-19 00:33～00:45（本地）

- **current branch:** `dev_openworld`
- **HEAD commit hash:** `c05a3d26651693d0346db09e1b86220c3965d757`（`先传一版洞府也好了`，author `Patoooo`，2026-09-19 00:32:36 +0800）
- **remote:** `origin` = `https://github.com/Siyuan-Yu/ProjectCultiva.git`
- **last pushed checkpoint:** 本会话**未执行 `git fetch`**；按本地 remote-tracking ref，`origin/dev_openworld` 与 HEAD **一致**（`git status -sb` 无 ahead/behind）→ 视为 `c05a3d2` 已推送。**下次会话建议先 `git fetch` 确认。**
- **git status summary（生成本文件前）：** **clean**（无 staged / unstaged / untracked）。
- **生成本文件后：** 预期仅新增一个未跟踪文件 `docs/40-process/247-project-handoff-current-state-2026-09-18.md`（+ 本轮可能的文档同步改动），**未 `git add` / `commit` / `push`**。
- **MAP-04 是否已 commit：** **部分**。第一批大清理 = `596d9c9`（`清理hex相关先传一个`，329 files changed, +3591/-22554），第二批 + FormalArmy / Snapshot 回归修复 = `54141d1`（`势力范围这一块基本好了……`）。**仍有 §13.2 的剩余项未做，且无 seal 提交。**
- **SPACE-01 是否已 commit：** **是**，= `c05a3d2`（46 files changed, +2093/-830），包含全部 SPACE-01 代码、新增 `docs/40-process/246-*.md` 与 `tools/space-01-sanity.ps1`，以及 `docs/00-project/03-glossary.md`／`245`／`42-devlog.md` 的更新。**由制作人在本会话审计期间提交，非本会话所为。**

## 19.1 审计开始时的工作树快照（已解析进 `c05a3d2`）

按 subsystem 分组：

- **SPACE-01 Core（新增）:** `Core/Exploration/SeparateSpaceExitEdgeTrigger.cs`、`SeparateSpaceKind.cs`、`SeparateSpaceResolver.cs`、`SeparateSpaceTransitionService.cs`、`Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs`、`Core/World/Strategic/SeparateSpaceCombatPolicy.cs`
- **SPACE-01 Host（新增）:** `Unity/Host/HostSeparateSpaceExitTrigger.cs`
- **SPACE-01 Core（修改）:** `ExplorationService.cs`、`LocalMapSession.cs`、`SnapshotActiveControlledLocalMapResolver.cs`、`StrategicSnapshotHelper.cs`、`WorldSnapshot.cs`、`CharacterEncounter.cs`
- **SPACE-01 Host（修改）:** `ContinuousOutdoorSurfaceRuntime.cs`、`HostActionMenu.cs`、`HostCaveEntranceQuery.cs`、`HostCaveSurveyPresenter.cs`、`HostCharacterEncounter.cs`、`HostCommandBridge.cs`、`HostLevelTesterCheatPanel.cs`、`HostNpcContextMenu.cs`、`HostNpcMeleeAssault.cs`、`HostPlayerPartyController.cs`、`HostSnapshotSessionRehydration.cs`、`HostWorldMapPanel.cs`、`LocalMapVisibility.cs`、`PlayableHostBootstrap.cs`
- **SPACE-01 Host（删除）:** `HostLocalMapEnterPrompt.cs`（+ meta）
- **Content:** `Content/BaseGame/Data/Maps/ch01_cave_map.json`（+`spaceKind: cave`）
- **Docs:** `docs/00-project/03-glossary.md`、`docs/40-process/245-*.md`、`docs/40-process/246-*.md`（新增）、`docs/40-process/42-devlog.md`
- **Tools:** `tools/space-01-sanity.ps1`（新增）
- **其它:** `Assets/Scripts.zip`（M）、`Assets/Scripts.zip.meta`（D）

**审计开始时的完整 `git status --short`（原样保留）：**

```text
 M Assets/Scripts.zip
 D Assets/Scripts.zip.meta
 M Assets/Scripts/Core/Exploration/ExplorationService.cs
 M Assets/Scripts/Core/Exploration/LocalMapSession.cs
 M Assets/Scripts/Core/Persistence/SnapshotActiveControlledLocalMapResolver.cs
 M Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs
 M Assets/Scripts/Core/Persistence/WorldSnapshot.cs
 M Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs
 M Assets/Scripts/Data/Content/ContentPackageLoader.cs
 M Assets/Scripts/Data/Content/DefinitionSchema.cs
 M Assets/Scripts/Data/Content/MapLayoutDefinition.cs
 M Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs
 M Assets/Scripts/Unity/Host/HostActionMenu.cs
 M Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs
 M Assets/Scripts/Unity/Host/HostCaveSurveyPresenter.cs
 M Assets/Scripts/Unity/Host/HostCharacterEncounter.cs
 M Assets/Scripts/Unity/Host/HostCommandBridge.cs
 M Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs.meta
 M Assets/Scripts/Unity/Host/HostNpcContextMenu.cs
 M Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs
 M Assets/Scripts/Unity/Host/HostPlayerPartyController.cs
 M Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs
 M Assets/Scripts/Unity/Host/HostWorldMapPanel.cs
 M Assets/Scripts/Unity/Host/LocalMapVisibility.cs
 M Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs
 M Content/BaseGame/Data/Maps/ch01_cave_map.json
 M docs/00-project/03-glossary.md
 M docs/40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md
 M docs/40-process/42-devlog.md
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs.meta
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs.meta
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs.meta
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs.meta
?? docs/40-process/246-space-01-separate-space-interior-transition-v1-2026-09-18.md
?? tools/space-01-sanity.ps1
```

**审计开始时的 `git diff --stat`：**

```text
 Assets/Scripts.zip                                 | Bin 1862803 -> 1874506 bytes
 Assets/Scripts.zip.meta                            |   7 -
 .../Scripts/Core/Exploration/ExplorationService.cs | 172 +-------
 Assets/Scripts/Core/Exploration/LocalMapSession.cs | 101 ++++-
 .../SnapshotActiveControlledLocalMapResolver.cs    |  30 ++
 .../Core/Persistence/StrategicSnapshotHelper.cs    |   4 +
 Assets/Scripts/Core/Persistence/WorldSnapshot.cs   |  23 +
 Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs |   4 +
 .../Scripts/Data/Content/ContentPackageLoader.cs    |   3 +-
 Assets/Scripts/Data/Content/DefinitionSchema.cs    |   2 +-
 Assets/Scripts/Data/Content/MapLayoutDefinition.cs |   4 +
 .../Unity/Host/ContinuousOutdoorSurfaceRuntime.cs  |   3 +
 Assets/Scripts/Unity/Host/HostActionMenu.cs        |   6 -
 Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs |  30 +-
 .../Scripts/Unity/Host/HostCaveSurveyPresenter.cs  |  13 +-
 Assets/Scripts/Unity/Host/HostCharacterEncounter.cs |  21 +-
 Assets/Scripts/Unity/Host/HostCommandBridge.cs     | 109 ++--
 Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs |  17 +
 Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs | 478 ---------------------
 Assets/Scripts/Unity/Host/HostNpcContextMenu.cs    | 114 ++---
 Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs   |  20 +-
 .../Scripts/Unity/Host/HostPlayerPartyController.cs |   6 +
 Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs |  40 +-
 Assets/Scripts/Unity/Host/HostWorldMapPanel.cs     |   8 +
 Assets/Scripts/Unity/Host/LocalMapVisibility.cs    |  61 +++
 Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs | 215 ++++++++-
 Content/BaseGame/Data/Maps/ch01_cave_map.json      |   1 +
 docs/00-project/03-glossary.md                     |   6 +-
 ...45-map-04-physical-legacy-cleanup-2026-09-17.md |   5 +-
 docs/40-process/42-devlog.md                       |  22 +
 31 files changed, 696 insertions(+), 840 deletions(-)
```

## 19.2 最近提交历史（`git log -10 --oneline`）

```text
c05a3d2 先传一版洞府也好了                       ← SPACE-01（2026-09-19 00:32:36）
54141d1 势力范围这一块基本好了，现在有洞府还没有做，只做到一半没额度了，之后再来。  ← MAP-04 第二批 + 回归修复
596d9c9 清理hex相关先传一个                       ← MAP-04 第一批大清理（pull --ff）
b04920b docs: seal MAP-03 continuous surface cutover
37de675 删压缩包
e01af91 很好
e133d57 mp-03 暂时版
5bf6734 feat: 完成连续世界制作与世界地图收口
7c29454 重置方向基本正常
9a4a428 编辑器 中途先传一版
```

**提交信息风格说明：** 本项目提交信息常为制作人中文口语短句，不代表 milestone 状态。**milestone 状态以 process docs 的 Producer Acceptance 记录为准。**

---

# 20. 同步的 Current Docs

本轮（外加 SPACE-01 已提交的那一轮）对 current docs 的最小同步：

| 文档 | 同步内容 |
|---|---|
| **本文件 247**（新增） | Project Handoff / Resume Snapshot —— 新会话入口 |
| **[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)** | 顶部状态改为 `Paused / Producer Acceptance Pending` + 暂停原因（SPACE-01 dependency）+ 「勿写成 MAP-04 Accepted」；本轮追加真实 checkpoint 与剩余项 |
| **[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)** | 追加当前基本通过的功能 + **Deferred Final Hardening A～F** + 提交 checkpoint（`c05a3d2`）/ 未验收状态 |
| **[41-roadmap](41-roadmap.md)** | 顶部状态改为 `SPACE-01 final hardening pending` / `MAP-04 paused / resume after SPACE-01` |
| **[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)** | 只做简短 **Implementation Status** 更新（不大改系统正文），指向本文件 |
| **[42-devlog](42-devlog.md)** | 追加本轮 Documentation / Handoff 条目 |

**未修改历史 acceptance 文档：** [240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) —— 已 Accepted / Sealed，**不得重写**，只被本文件引用。

---

# 21. Archived Copy-Paste Context（禁止继续使用）

> 以下整段仅保留 2026-09-18～19 的历史恢复证据，包含已完成的 WIP 与过期指令。**不得复制到新会话。** 新会话只读页首 Current handoff、ADR-0038 与最新 devlog。

```text
ARCHIVED / DO NOT EXECUTE — SPACE-01、MAP-04、LEGACY-FINAL-A/B/C 已全部封板。
项目：Unity 2D top-down 仙侠 RPG（修仙世界模拟 + RPG + 战略层）。仓库 D:\UnityProjects\XianXia，分支 dev_openworld，最新提交 c05a3d2。请把它当成“已实现大量内容、正在收口”的项目，不要重新设计架构。

【架构（已冻结）】
- Continuous Surface 已是正常 Outdoor 世界：一大陆一张 Final Continuous Surface；正常户外以 SurfaceId + exact WorldPosition +
  SurfaceGroundNavigation + Continuous SiteCore + Actual Administrative Control 为 authority；普通户外没有 LocalMap 切换。
- Hex 与 Outdoor LocalMap 正在退役（CurrentHex/DestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/
  StrategicAnchor/旧 mapLayout 均为 Legacy or Derived Compatibility）。还能 grep 到 Hex 不等于没做完。
- 比例术语固定：Surface Cell = 1×1 gameplay 地形最小单位；World Editor Cell = 10×10 Surface Cells（Composer 大陆级 macro
  authoring）；Runtime Chunk = 50×50，只是 streaming 分区，不是 authoring 单位；Blueprint / Detail Patch 尺寸任意。
- 地形只有 Plain/Mountain/Water；Forest 是 overlay 且可通行；River/Road 是曲线 overlay，不是独立地图。
- Authoring Source（WorldComposer/FineEditor，UI 要求中文）与 Runtime Bake 产物必须分离。
- WorldMap 是同一 Surface 的战略 LOD：exact WorldPosition 投影与点选、不显示 gameplay Hex grid、固定 Header + 全屏视口 +
  按需 Flyout、地图不得盖住 Header、稀疏坐标网格无 X/Y 数字。Site marker：永久 Site 画房屋、FactionFlag 画旗，anchor 是
  exact CoreWorldPosition；图标与文字随 zoom 缩放（world-space），只有 Header/Flyout 是屏幕空间。打开 WorldMap 是独立
  planning overlay：不移动玩家，关闭后才继续 AutoTravel。
- 独立空间：Outdoor ↔ Separate Space（Cave/Interior/Dungeon/秘境）。Domain 真源 SeparateSpaceTransitionService；authority
  是 ActiveMapLayoutId + Interior local position；离开必须回到进入前 exact Surface return（禁止回 Hex 或 Site 中心）。独立空间内
  是原地直接战斗（不进 BattleOffer、不开第二战场）；Outdoor 的 CharacterEncounter 才是 BattleOffer → 独立战场 → 战后精确位置回归。
  进洞时当前随队成员全部自动进入（已无队员选择窗）；离开只能靠物理出口触发（主控走进出口几何），不是右键菜单。

【已验收封板】Editor Toolchain Cleanup；MAP-01 WorldComposer/FineEditor（09-16）；MAP-02 Surface WorldMap（09-16）；
MAP-03 正常玩法 Surface authority cutover（09-17）。

【当前 WIP，未验收】
- SPACE-01 Separate Space V1：已实现并提交（c05a3d2），Cave 是样板。已基本可用：隐蔽洞口 + 近处气息提示 → 神识 Survey 只揭示
  （不激活内景）→ 远处右键只走近、近处才出进入 → 随队成员自动进入 → 洞内原地战斗（随从自动协战）→ 洞内存读档留在洞内 →
  物理出口回到 exact 户外位置。状态：Implementation Complete / Producer Acceptance Pending，**不是 Accepted**。
- MAP-04 物理删除旧地图栈：In Progress 且 Paused（等 SPACE-01）。已删旧 WorldGraphEditor/RegionEditor、Shared/HexWorld、
  35 个 Outdoor MapLayout、34 个 Outdoor LocalPlaceSet、world_regions.json、HexWorld JSON、旧 Hex WorldMap 渲染/拾取/投影、
  WorldRegionBootstrap 等；仍剩约 36 个 Hex 命名文件、113 个文件仍引用 Hex 字段、15 个文件仍含 WorldRegion、
  35 个文件仍含 Outdoor LocalMap 兼容代码、2 个 HexWorld 导出器、历史 EditMode Hex fixtures。

【SPACE-01 尚未完成（封板前必须做；动手前先向制作人确认）】
A) CollectTransitionMembers 在收集为空时会 fallback 抓所有 Player characters，应删除该 fallback；
B) 撤离应严格只迁移 PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty 为真的成员；
C) 主控出洞时失能/尸体/脱队/落单队员行为未验证，必须确保不被错误传送；
D) Separate Space 仍复用 PartyWorldPresenceMode.AtHex，未来加 InSeparateSpace；
E) 洞内非 occupant 的 NPC（居民/敌人）移动后的 Local position 读档后不保留，需补；
F) 以上完成前不得封板。

【工作流纪律】Unity 由制作人自己打开并人工验收；开发侧不打开 Unity、不跑 Unity Test Runner/batchmode、不跑大量自动化测试，
只做离线编译（tools/offline-compile.ps1）、静态检查、Content 校验、git diff --check。每轮给制作人一份可复制的唯一指令 +
简短验收 checklist；制作人确认验收前不要 commit/push，除非明确授权 checkpoint。不要把 commit 里的口语短句当 milestone 状态，
状态以 docs/40-process 的 Producer Acceptance 为准。实质改动更新 docs/40-process/42-devlog.md，重大决定写 ADR，术语走
docs/00-project/03-glossary.md；不要修改已封板的历史文档（240/242/243/244）。Core/Data 禁止 UnityEngine；随机用 IRandomSource；
WorldTick 是唯一世界时间轴；RelationshipLedger 是唯一关系真源；Dead ≠ Removed。

【历史建议，已完成，禁止执行】SPACE-01 hardening、MAP-04 consumer audit 与物理清理、LEGACY-FINAL-A/B/C 均已验收封板。

【入口文档】交接 docs/40-process/247-project-handoff-current-state-2026-09-18.md；SPACE-01 = 246；MAP-04 = 245；
系统真源 = docs/20-systems/2N-continuous-surface-world-authoring-and-composition.md；决策 ADR-0036 / ADR-0037；
路线图 docs/40-process/41-roadmap.md；日志 docs/40-process/42-devlog.md
```

---

# 22. 本轮未做实现修改的确认

- 本轮**没有**修改任何 Gameplay 逻辑、Content 逻辑、ExternalTools 功能。
- 本轮**没有**打开 Unity、没有运行 PlayMode / Unity Test Runner / batchmode。
- 本轮**没有**继续实现任何 TODO，没有修 SPACE-01 deferred hardening、没有做 MAP-04 Hex cleanup、没有删除 Editor。
- 本轮**没有**执行 `git add` / `git commit` / `git push`。
- 本轮实际执行的验证仅为：只读 git 状态查询、文档阅读、Content JSON 解析核对、`findstr` 静态检索、`tools/offline-compile.ps1`（Core/Data/Unity 离线编译 → `ALL_OK`）。
- 工作树中出现的 `c05a3d2` 提交由**制作人从外部提交**，不是本会话所为（见 §2 并发提交说明）。

---

# 23. FINAL-SEAL Consolidated Acceptance Closure（2026-09-21）

状态：上一轮 **Producer Accepted / Sealed**；后续 Legacy-only 最小静态尾项已完成，保持未提交。

熟练系统现在从正式学习、自然增长、修为灌注、突破、效果重挂到 Snapshot 恢复使用同一份已加载 profile。当前全部功法／斗技均显式配置 entry→minor→major→perfect→transcendent，四段门槛为 20/30/40/50，灵草与粗木各需 1/2/3/4。Host 学习仪式在开始和完成时都调用 Core 正式资格；关闭详情页后，选中该角色的常驻面板仍绘制仪式读条。重复学习已知技能幂等且不消耗 RNG。

Snapshot 在 Content rehydrate 后统一规范化 mastery 门槛并幂等恢复 modifier／修炼速度；同时修复 NPC-only Snapshot 被错误要求 ControlledSquadId 的集成回归。任务 `learnmanual` 保持立即直授例外，满熟练不影响正常 CultivateAction 或斗技施放。

关联复核另发现并关闭失能 NPC 在 Action 启动前仍可预占工位、Host 农作／拆毁会话只拦 dead 而未拦 incapacitated 的可达序列；现统一使用自主行动生命资格，且只释放本人的预约／表现移动。

验证：Core／Data／Unity／Tests 离线编译通过；稳定 headless 集中矩阵 40/40，覆盖熟练链、行动取消与预约、奖励事务、普通户外授权、singleton／Surface／Return／Interior 关键回归和 Snapshot。未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。

未扩协议：剧情 Boards 的完整永久存档、特殊空间公库继承仍未定义。对应轮次的运行行为人工验收已由制作人确认通过；这些未定义政策不是本专项授权的新实施任务。

# 24. Legacy-only concentrated closeout（2026-09-21）

状态：**Producer Accepted / Sealed（2026-09-22）**。本节原 Pending／smoke 说明仅保留为当轮历史记录，不再是当前任务。

本轮删除匿名 modal pause 兼容计数、无调用 StrategicClockFreeze host-presentation 闭包、Host saved-speed／旧原地战包装、退役住房 UI 字段与未使用 snapshot 局部变量；保留具名 pause owner、现代 CharacterEncounter freeze、ManualPaused 和输入限制。该测试契约当前现名为 `LegacyPartyFocusCompatibility.SyncPartyFocus`，因 EditMode 回归仍有四个直接调用而保留。

代码注释与局部命名已从 Army／StrategicEncounter／WORLD_COMBAT 旧语境改为 Squad／CharacterEncounter／owner；`HexMetrics` 明确只服务 legacy Hex、工具与测试，不是 Continuous Surface authority。项目入口、systems／tech 正文、路线图、ADR 索引与本交接统一指向 ADR-0038；§1～22 的旧 WIP、Resume Order 与复制指令已归档且禁止继续执行。

验证：Core／Data／Unity／EditMode Tests assembly 离线编译 `ALL_OK`；现代 CharacterEncounter 定向纯 C# 回归 8/8；目标符号、已知链接与 `git diff --check` 通过。旧 Phase2C Hex／Outdoor LocalMap 测试套件仍在共同 fixture 前置失败，本轮未删除或改写这些历史测试。

上一轮窄范围 smoke 覆盖具名 modal 暂停恢复、CharacterEncounter 全流程、Continuous travel／WorldMap 与合法旧档兼容读取；不要求重跑 A／B／C、MAP、SPACE 的完整历史验收。

制作人已确认上述上一轮人工 smoke 全部通过。其后静态尾项只删除空 `StrategicDayHandler`／bootstrap 注册、恒空 `LegacyArmyId` 诊断，并修正 `HostDemoTileMap.EndInstanceBuild` 注释；不要求重跑玩法验收。`SyncPartyFocus` 因 `PlayerPartyContinuousWorldPhase2CTests.ARRIVAL_02／07／11` 共 4 次调用而继续保留，仅服务旧 travel fixture 的防回写契约，不是现代 Gameplay 入口。静态尾项离线编译 Core／Data／Unity／Tests `ALL_OK`，引用复查与 `git diff --check` 通过。

# 25. Hex／Army naming compatibility documentation closure（2026-09-22，历史实现）

状态：**Producer Accepted / Sealed（2026-09-22）。** 本节保留物理退役前的当时事实；当前实现以页首和 ADR-0038 §6.1 为准。

- **A — 当时的 Army Content 输入：** 外部协议曾由 runtime adapter 接受；当前 Loader 已拒绝，三类 identity 规则移至离线转换器。
- **B — 稳定 Snapshot／Encounter 协议：** `SquadCommandKind.LegacyFormalArmyWorldMotion = 2`、Encounter spatial `LegacyFormalArmy = 2` 保持数值；`EncounterCharacter.LegacySourceFormalArmyId` 对应 wire `sourceFormalArmyId`。PlayerParty Snapshot `CurrentHexQ/R` 与 JSON `currentHexQ/R` 不改。
- **C — 当时的 Hex 边界：** runtime 容器与 Core Hex 几何随后已物理删除；当前只有离线转换器实现必要的历史 Odd-R 换算。
- **D — PlayerParty compatibility：** `PlayerPartyWorldMotion` 的旧专用 API 已逐成员显式 Legacy：`LegacyCurrentHex`、Legacy path／destination、`LegacyHexSegmentIndex/Progress`、Legacy departure／travel presentation 状态，以及无 SurfaceId 的 WorldPosition 写入、Hex-center snap 与旧 route 方法均不再以普通名称暴露。正常 Surface 上 `LegacyCurrentHex` 是兼容摘要；旧 executor 中可为已提交路线格／旧缓存，不保证即时投影。`LegacyPartyFocusCompatibility.SyncPartyFocus` 只服务旧 EditMode fixture。
- **E — 当时的尺度边界：** 旧 Hex 尺度适配随后已由 `outdoorSurface.movementScale` 唯一注入取代；正常 authority 始终为 `SurfaceId + exact WorldPosition`、`SquadWorldMotion`、`CharacterEncounter`。

本轮复验：Core／Data／Unity／Tests offline compile `ALL_OK`，阈值定向纯 C# 检查 10/10，`git diff --check` 通过。本轮未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。

# 26. WorldSite／Hex footprint naming documentation closure（2026-09-22，历史实现）

状态：**Producer Accepted / Sealed（2026-09-22）。** 本节登记物理退役前的当时磁盘实现名；当前实现以页首和 ADR-0038 §6.1 为准。

- `WorldSite` 当前兼容成员为 `LegacyAnchorHex`、`LegacyPresenceHex`、`LegacyOccupiedHexes`、`SetLegacyHexFootprint`、`EnsureLegacyPresenceHexValid`、`HasLegacyPresenceAnchorMismatch`、`EnumerateLegacyFootprintHexes`、`OccupiesLegacyHex`；旧 `WorldSite.HexCoord` alias 已删除。
- `WorldSiteBoard` 当前查询为 `TryGetAtLegacyHex`、`GetSiteIdsAtLegacyHex`、`TryResolveLegacySitePresenceHex`。相关类为 `LegacyWorldSiteHexLocationCompatibility`、`WorldSiteHexFootprintValidator`、`WorldSiteHexFootprintSpatialMapping`、`WorldSiteHexFootprintBakeTransform`；`WorldSitePhysicalRegionQuery` 与 `WorldSiteOutdoorMigrationPolicy` 保持现名。
- 当前 compatibility invariant 强制 `LegacyPresenceHex == LegacyAnchorHex`。`LegacyPresenceHex` 是旧兼容代表格，不随 Surface 位置即时派生；`DerivedPresenceHex` 才是 `WorldToHex(CanonicalWorldSurfacePosition)` 的独立只读结果。
- Hex footprint 的非空、六邻接连通、star-shaped non-empty kernel 三项规则只约束旧 Hex footprint 输入，不是 Continuous Surface、SiteCore 理论范围或 Actual Administrative Control 的通用几何规则。`WorldSiteHexFootprintBakeTransform` 仍有 `sitePlacements`／`sitePlaces`／`OpeningEntityAnchors` 与旧 LocalPosition migration 消费者，不是 dead legacy runtime。
- 历史页 ADR-0027、212、213、245 仅增加旧名到现名索引；正文历史、旧 devlog 与已 superseded 的 ADR-0025 等保持原样。外部 `anchorQ/R`、`presenceQ/R`、`footprint[]` key 不变。
- `WorldVec2`、`HostStrategicRosterPanelLayout`、`PlayableHostSession` 的代码改动仅为注释纠正，无行为变化。Core／Data／Unity／Editor／Tests／PlayModeTests／Assembly-CSharp 离线编译 `ALL_OK`；旧精确符号、外部 key、脚本 GUID 唯一性与 `git diff --check` 均已复核。未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode；工作区保持未暂存、未提交。

# 27. EVENT-EDITOR-V2.5 可读人物选择（2026-09-23）

EventEditor 的人物入口统一使用 Shared `CharacterPicker`，目录 authority 是当前已加载 ContentPackage 的 `character` definitions，而非 Authoring CSV。制作界面显示中文名、稳定 ID 与定义来源，支持搜索和来源筛选；Event Inspector、新建事件与指定 Speaker 共用实现。左侧 Browser 保持中文名主标题与灰色 ID，来源仅放 Tooltip。磁盘 Event 仍只保存 DefinitionId，未修改 Runtime、Content schema/组织或 Graph 行为。

# 28. EVENT-EDITOR-V2.6 Repeat 语义与全局人物来源（2026-09-23）

Resolver 现状已满足 Repeatable／Once／Priority／fallback 契约：`once=false` 不读写 fired gate；Once 只在完成时按 scope 写 fired；候选只返回最高 Priority 同层。当前“验收·问点私事”为 P50 repeatable、0 Conditions，P100 完成后在没有更高层时每次交谈都应重新进入 P50 candidate layer。Runtime 未修改。

EventEditor 顶部统一持有人物来源 filter，四个人物入口与按对象 Browser 共用；Picker 内不再有独立来源下拉。被筛掉的当前 binding 保留并提示，不写 Content、不 dirty。重复与保底文案已拆清，未改 Graph、Opportunity、Snapshot、Quest、Character schema 或 Content layout。

# 29. EVENT-EDITOR-V2 Final Patch 与 Producer Acceptance Seal（2026-09-23）

顶部人物来源现按规范化绝对 Package Root 保存在 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json`；重开同一 Package 时只恢复仍存在的实际 source，否则回退“全部来源”。设置读写失败仅在状态栏提示，不阻止编辑；恢复和切换均不进入 Content、layout、Undo 或 dirty。

制作人确认 EventEditor V2 全范围实际验收通过。当前正式状态为 **Producer Accepted / Sealed**；EVENT-02 Opportunity Director、随机 NPC/Object、tag/archetype binding、ContentIntent、NPC 找玩家／跟随、概率、剧情持久化、新 Graph node 或新 Runtime framework 均未实施，属于后续里程碑。
