# ADR-0038：Continuous World Legacy Migration Final Seal

> 日期：2026-09-21  
> 状态：**Decision Sealed；Hex／Army 正式运行依赖退役与 021915 统一收尾 Producer Accepted / Sealed（2026-09-22）**
> 前置：LEGACY-FINAL-A、LEGACY-FINAL-B、LEGACY-FINAL-C 均已 Producer Accepted / Sealed  
> 关联：[247 Project Handoff](../247-project-handoff-current-state-2026-09-18.md)、[250 LEGACY-FINAL-C](../250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)、[ADR-0035](ADR-0035-unified-squads-and-encounter-scope.md)、[ADR-0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)

## 1. 决策

### 2026-09-21 Skill Mastery acceptance fix

熟练档位效果与升级路径继续分离：`mastery.tiers` 只描述各档效果，只有从当前 tier 出发且 `progressRequired > 0` 的 `mastery.breakthroughs` 行才授权继续积累与突破。显式 profile 没有当前路径时不得回落到默认路径；只有未声明 mastery 的旧合法定义生成既有默认 entry→minor profile。

功法与斗技灌注在 Core 共用资格／计划计算：先解析真实定义和有效门槛，再计算 requested/actual gain；只有实际增量为正且修为足够才同时扣修为、写熟练。失败不得推进随机或改变背包、公库、modifier、tier/progress。Host 查询同一规则，不以 `!IsAtBottleneck` 推断仍可灌注。最初洞府秘诀小成无后续路径时仍扣修为且零增长是实现漏洞；后续制作人已为当前全套功法／斗技明确补齐至化境的 Content 路径。

### 2026-09-21 Non-World follow-through

FINAL-SEAL 同批收口非世界一致性边界：功法／斗技学习与两类熟练突破只使用已进入 Snapshot 的 `world.Random`，预览、取消和非法尝试不推进随机流；自主行动由 `AutonomousActionContinuationService` 在生命状态改变与每次 `Advance` 前统一取消，释放本人的移动／工作位预约，但不取消 `RecoveryAction` 或持续 modifier。

`PartyInventory.TryAdd` 继续允许部分添加，`TryAddAll` 改为无副作用容量预检后的全有或全无。`ContentOutcomeApplier.ApplyAll` 对 Inventory、Flags/history、Counters、Daily、Quest runtime、ContentEvent active/fired、Relationship ledger/cache、Cultivation/manual modifiers、KnownSites 与 DomainEvent queue 建立窄事务；领奖的 `Completed`、事件的 once-fired/active clear 在同一提交边界完成，Host 小游戏只在 Core 提交后打开。

Normal Continuous Outdoor 的战略公库资格只由 `PlayerPartyTravel.SurfaceId + exact WorldPosition` 经 `WorldSiteAdministrativeControlResolver` 得出，`CurrentOutdoorWorldSiteId` 不再是权限证据。Separate Space／Independent Encounter 的公库继承没有正式规则，本轮保持既有非普通户外兼容行为，政策继续待定义。

清理已被仪式替代的即时背包学习 wrapper、空 `HostSelectedUnitChrome`、只转发的人际面板、无生产入口的关系种子器与重复派生标签 helper；启动请求不再携带无 consumer 的 `SpiritRootPlaceholder`，也不再构造假的 LocalMaps／Settlements。内容 parser 的旧 `spiritRootPlaceholder` 输入字段仍作为 schema 兼容边界，实际 `SpiritRoots` 字典和有真实 consumer 的 `InitialRealmPlaceholder` 保留。

`Assets/Scripts/Runtime/**` 整体保留：`Demo_v0_1.unity` 仍在 Build Settings 且其 Prefab／MonoBehaviour GUID 有真实序列化引用，符合 Prototype bridge 的“停止扩展、保留行为参考／回归场景”契约；现代 `PlayableHost` 不以该岛作为第二套正式 session authority。本轮未发现可无资产风险单删的 Runtime 原型脚本。

明确未纳入：`WorldFlagBoard`、`QuestBoard`、`ChapterBoard`、`ContentEventBoard`、`ContentCounterBoard`、`ContentDailyBoard` 的通用 Save/Load 协议。当前 Snapshot 已覆盖 PartyInventory、关系 ledger、随机状态及窄范围洞府 taken-loot；上述剧情 Boards 仍是 session-only，影响旗标、任务领奖／进度、章节、一次性事件、计数与每日标记的跨存档恢复。后续必须把这些 Boards 与 outcome/claim/choice 的事务提交边界一起定义，不能通过背包物品或重跑 New Game 猜测恢复。

正常 New Game、现代存档与正常 Current BaseGame runtime 只能使用下列 authority：

| 领域 | 唯一现代 authority |
|---|---|
| Character | `SurfaceId + exact WorldPosition`，或 Separate Space / Independent Battle 的 Interior `EntityLocation` |
| PlayerParty | `PlayerPartyTravel` 的 `SurfaceVisible + SurfaceId + exact WorldPosition` |
| NPC group | `Squad + SquadWorldMotion` |
| Territory | `WorldSite + TerritoryClaim + Actual Administrative Control` |
| Combat | `CharacterEncounter + real Character participants + exact Surface anchor` |
| Separate Space | `LocalMapSession + Interior EntityLocation` |

CharacterEncounter 内部位置进一步冻结为：`Origin`＝source canonical authority，`Return`＝开战前实际主世界 EntityView 位置，`Tactical`＝独立战场当前位置。`Return` 在 Active 前冻结后不可变，Tactical 永不成为战后 world position；Encounter 在 `CommitAndReturn` 前独占 participant spatial state，弥留／死亡只在现有 View 原地更新。普通 residual handoff 从返回主世界后才开始。

## 2. Legacy quarantine

`FormalArmy`、`ArmyStack`、`TerritoryRegion`、`AtHex`、Outdoor LocalMap 与 Hex travel
只能作为旧 Content／Snapshot wire、边界检测、离线 migration 或历史／测试记录。任何新玩法禁止依赖这些
API、字段或命名作为 runtime authority。旧数据必须先在 runtime 外迁移为上表中的现代状态，之后才能进入正常 runtime。

合法保留输入边界包括 `FormalArmySnapshotDto`、`ArmyMembershipSnapshotDto` 等历史 Snapshot wire DTO、
BattleAnchorHex／AtHex 历史数值与旧 Content key。它们只可用于拒绝诊断或交给离线转换器，不授予现代写入权。

> **历史实现注记（已由 §6.1 物理退役取代）：** 早先曾以 `Legacy*` runtime API 隔离旧输入；这些名称不再是当前入口。外部 wire key 继续保持可识别，以支持明确拒绝与离线转换。

## 3. Source boundary

- **A — Modern runtime：** Continuous Surface、WorldPresence exact position、PlayerParty Surface travel、
  Squad/SquadWorldMotion、WorldSite/Claim/Actual Control、CharacterEncounter、Separate Space。
- **B — Compatibility runtime adapter（历史实施层，现已退役）：** 曾用于旧 PlayerParty Hex／Outdoor LocalMap
  执行；当前正常产品不编译或调用该层。
- **C — Legacy serialization/content input：** 上述 DTO／wire key、旧 numeric values 与旧 Content schema；
  Runtime 只识别并拒绝，离线转换器负责单向转换；现代 Save 不重新输出 retired authority。
- **D — Removed dead code：** zero-caller WorldTravel order wrappers、旧 Hex FactionFlag construction chain、
  dead WorldSite access wrappers，以及此前 C 已删除的 TerritoryRegion／StrategicEncounter／
  RetreatingArmy／LingeringBattlefield runtime。

## 4. Development guards

以下 guard 是 Final Seal 当时的过渡实现，当前已由 Loader 拒绝、无 Hex runtime 类型与离线转换边界取代：

- normal Continuous WorldPresence 不含 AtHex；
- PlayerParty 不使用 legacy execution；
- Continuous Site 不使用 AtWorldSite；
- Current opening scenario 不声明 `InitialLegacyFormalArmyIds`；
- normal Outdoor 不激活 Outdoor LocalMap；
- modern snapshot 不输出 TerritoryRegion、legacy residual、RetreatingArmy、old StrategicEncounter、
  Ch01 formation compatibility 或 AtHex Character authority。

该 guard 不做每帧全世界扫描。

## 5. Freeze rule

后续 Gameplay／Content 开发若需要读取旧数据，必须在 Data／Serialization／LegacyMigration 边界先转成现代
authority。禁止在 normal Gameplay 新增 FormalArmy、ArmyStack、TerritoryRegion、AtHex、Outdoor LocalMap 或
Hex travel 依赖；禁止恢复 compatibility adapter 作为新功能捷径。

## 6. Seal state

废弃入口、误导性命名与兼容身份整理已经 **Producer Accepted / Sealed**，构成本轮物理退役的已验收基线。§6.1 的真实运行依赖退役与 021915 统一收尾现已 **Implementation Complete**，并由制作人人工验收后 **Producer Accepted / Sealed**。不得按 Hex／Army／Legacy 关键词再开一轮扫描删除。

制作人已确认本版运行行为人工验收通过。静态审查基线为 `Scripts(20260922-040154).zip`。封板前提交号（含 `cf77d80`）**不包含也不代表**本轮 seal commit。

正常玩法继续使用 Continuous Surface／exact WorldPosition、PlayerParty／Squad、CharacterEncounter、WorldSite／Actual Administrative Control。真实 Hex 几何、Hex footprint 工具及受支持工具／原型契约保留，但不是连续世界通用地形或位置 authority。`LegacyHexWorld`、`LegacyCurrentHex`、WorldSite `Legacy*` 成员和旧位置／焦点入口只服务已限定兼容用途，不得绕过现代 authority 回写正式状态。

在此前 Seal 阶段，runtime 曾保留旧输入单向迁移类；当前这些类已由 §6.1 的离线转换器取代。外部协议、稳定数值空洞、JSON key 与稳定 ID 边界保持；旧 Content identity 使用 `squad:migrated:`／`squad:legacy:`，旧 Snapshot identity 使用 `squad:army:`，不得混写。

此前 Seal 阶段的 movement budget 曾只读适配旧 Hex 尺度；当前由 §6.1 的 `outdoorSurface.movementScale` 唯一注入取代。原公式和 ticks 行为不变。内容目录名、历史记录与 wire 字符串不等于当前 runtime 身份，例如 `Armies/` 中的现行 `npcSquad` 数据不得被描述为 `formalArmy` runtime。

本封板只关闭上述专项，不表示整个游戏已完成或不存在潜在缺陷，也不表示仓库中的 `Hex`／`Army` 字符串已清零。后续只有具体错误调用、authority 越界、失真说明或回归证据才可提出明确问题；不得仅凭关键词命中重开循环扫描。下一步等待制作人讨论后确定，尚未授权新功能实施。

### 6.1 正式运行依赖退役落实（2026-09-22）

本节只登记 §1～§5 已冻结规则的最终实现，不修改稳定 Snapshot 数值／ID 或既有决策：

- `SimulationWorld` 已无 HexWorld；`Assets/Scripts/Core/World/Hex/` 已物理删除。PlayerParty、WorldPresence、Site／Flag／CharacterEncounter／SeparateSpace current 链无 Hex 参数、缓存或运行时几何依赖，正常产品不编译旧 Hex 几何。
- Runtime Content 只接受当前 `outdoorSurface`／`npcSquad`。`ContentPackageLoader` 对 `hexWorld`、`formalArmy` 与 `openingHexWorldId`／`initialFormalArmyIds` 等历史入口明确失败，不执行自动 migration。
- `ExternalTools/ContentAuthoring/LegacyRuntimeConverter` 只无损转换旧 FormalArmy Content，以及全部 current authority 完整、仅 FormalArmy 待转的 transitional Snapshot。`type=hexWorld`／`openingHexWorldId` 只检测并在写文件前拒绝；地图必须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径，无迁移样例时不得猜测或声称已生成 `outdoorSurface`。输入只读，输出必须是不同且尚不存在的独立文件；任何 authority 缺失或无法保真均明确失败，不生成部分输出。
- 旧 Content ID 规则仍区分 `squad:migrated:<normalizedRuntimeArmyId>` 与 `squad:legacy:<normalizedDefinitionId>`；旧 Snapshot 使用 `squad:army:<armyId>`。三者不可混用。已移除的旧 Army／AtHex 枚举值 `2` 不得复用；其数值空洞与旧 wire key 仅供边界检测和离线转换。
- `ContinuousWorldMovementScale.Resolve` 现在只读 `SimulationWorld.ContinuousWorldMovementScale`；唯一运行时注入点来自当前 opening `outdoorSurface.movementScale`。BaseGame 显式值为 `1.0`。`movementScale` 不是 `cellSize`，既有移动公式与 tick 行为保持。
- §2～§4 中列出的 `Legacy*` runtime adapter／guard 名称是此前实施历史，已由本节物理退役结果取代；它们不得被解释为当前可恢复 API。Freeze 的 authority、wire 稳定性与离线转换边界不变。

此前实施阶段实际执行记录（不是封板重跑）：正式 Core／Data／Unity／Tests 离线编译通过；关键纯 C# 回归 24/24，随后空间／存档／据点定向复核 28 项；021915 当前行为矩阵 82/82；代表性 current Gameplay 存档完成 serializer + Core restore 及 content-dependent 二阶段恢复。额外运行的两项既有 SiteCore defender/content 断言曾失败，后经当时专项修复纳入既有矩阵。当时未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。制作人其后确认人工验收通过，本节现为 Sealed。

> 下方 §7～§12 记录各实施轮次当时的验证与 Pending 状态；这些历史状态已由本节正式 Seal 结论取代，不再构成待执行 smoke、清理或恢复指令。

## 7. Final dead-runtime residue purge

全仓 caller audit 后删除以下零调用 residue：

- `BattleEngagementKinds`（旧 Battle initiator／decision option 类型）；
- `FormalArmyLocationKinds`（旧 FormalArmy location／movement／order／route enum）；
- `HexStrategicRuntime`（旧 Hex runtime mode switch）；
- `PlayerPartyWorldMotion` 中退役的 FormalArmy pursuit metadata；
- `PartyWorldPresence` 中从未产生非空值的 FormalArmy focus metadata；
- 零 caller 的 `HexTerrainPresentation` 与 `HexTerrainVisualInset` 表现辅助类型。

`HexMetrics` 与 `HexWorldMapRenderBounds` 仍有测试 caller，继续保留。旧 FormalArmy／ArmyMembership
snapshot DTO、AtHex 与 owner／command enum numeric value、TerritoryRegion input、Ch01 compatibility、
`LegacyPlayerParty*Compatibility` 等读取／迁移边界也继续保留。本次只收口死代码与注释，现代 runtime
authority、CharacterEncounter、PlayerParty travel、NPC Squad 与 Separate Space 行为均未改变。

## 8. Consolidated correctness repair（2026-09-21）

最终静态审计发现，少数 consumer 仍把“角色属于某个 Squad”误当成“该角色当前由 Squad 物理移动”。本轮统一冻结如下：

- `SquadMembership` 只表示组织归属；只有 `SquadWorldMotionService.OwnsCharacter` 或独立 command schedule 才表示真实移动／命令 ownership。自动生成的单人 Squad 不得阻止个人户外旅行、个人精确位置、到达物化或旧档个人位置恢复。
- incapacitated/dead handoff 只允许在非 Encounter、非 Separate Space 且角色当时仍被 Squad 物理拥有时捕获一次。角色在 A 点失能后，Squad 移到 B 点再死亡，不得用 B 覆盖已冻结的 A。
- 精确世界位置是第一等查询结果。`CharacterWorldPresenceQuery.AtWorldPosition` 可从有限 `WorldPosition` 直接派生 Hex；派生 cache 缺失不代表没有位置，真实 `(0,0)` 也不得被当成失败默认值。
- PlayerParty、Squad 与后台个人旅行均以明确 `SurfaceId` 选择导航图。只有旧数据缺少 SurfaceId 时才允许唯一 Surface 推断；重叠 Surface 不得静默改写显式 ID，跨 Surface 目的地必须失败。
- 后台个人旅行恢复拆为 raw restore 与 post-content finalize：raw 阶段保留 DTO 的显式 SurfaceId 与精确位置；注册 Surface/Site 后再校验、补 legacy 唯一推断、重建 moving route。idle 不得自动重启，现代冲突状态直接 `SnapshotInvalid`。

同时完成 dead-dependency closure：移除零 caller 的 battle hex/range policy、arrival notice、旧 battle runtime snapshot metadata、runtime follow/pursuit stack 字段、`IsAutoSettlement` Host 分支、未绑定 aggression prompt 与残留 resolver/helper。旧 Snapshot DTO、JSON reader、枚举 numeric compatibility 与真实工具／测试 caller 保留，不把 serialization compatibility 误删为 runtime residue。

本轮纯 C# 回归覆盖 singleton membership、真实 group ownership、A→B casualty、精确位置／`(0,0)`、null party context、重叠 Surface 与后台 snapshot finalize。状态继续为 **Implementation Complete / Producer Acceptance Pending**；必须完成人工 smoke 后才可宣称 Accepted / Sealed。

## 9. Consolidated acceptance closure（2026-09-21）

本轮用有限矩阵关闭熟练系统全链及与既有 FINAL-SEAL 修复的集成边界，不重做已经通过人工验收的设计：

| 类别 | 结论 |
|---|---|
| A — 保留并验证 | 四类学习／突破判定继续使用 `world.Random`；任务 `learnmanual` 保持立即直授；满熟练不禁止正常修炼或斗技使用；既有行动取消、奖励事务、精确空间 authority 保持。 |
| B — 本轮修复 | 研读资格在 RNG 前统一校验且重复学习幂等；灌注按当前 profile 计算真实收益；突破查询与执行聚合重复材料；parser 报告重复／倒退路径及非法 cost；Snapshot 在 Content 恢复后规范化门槛并幂等重挂效果；NPC-only Snapshot 不再错误要求 ControlledSquadId；失能 NPC 不再预占工位，Host 农作／拆毁会话也在失能时停止。 |
| C — Content 边界 | 2026-09-21 制作人要求当前全部功法／斗技显式配置至化境：entry→minor 为 20／材料各 1，minor→major 为 30／各 2，major→perfect 为 40／各 3，perfect→transcendent 为 50／各 4。未来定义仍可合法缺少后续路径，Core 不自动生成。 |
| D — 兼容／工具契约 | 缺少整个 mastery 的旧定义继续使用既有默认 entry→minor；旧 DTO／枚举数值、`InitialRealmPlaceholder`、任务直授、统一人物档案、现代 CharacterEncounter UI、具真实 Scene／Prefab／测试引用的 Prototype 保留。 |
| E — 未定义政策 | 通用剧情 Boards 的完整永久存档，以及 Separate Space／Independent Encounter 是否继承公库，仍待正式产品规则；本轮不以推断补协议。 |

执行规则冻结为：资格查询只读；所有无实际尝试的失败都不抽签、不扣费、不改熟练／modifier；合法概率失败仍按既有契约消费材料和一次随机；成功只结算一次。有效门槛始终来自已加载 profile，而非盲信 Snapshot 缓存。Restore 在 Content 注册完成后调用 `NormalizeLoadedMasteryState`，保留 tier/progress，只修门槛缓存并按绝对值／先移除后重挂契约恢复效果。

当前清理结论也在本轮封口：即时学习 wrappers、空 SelectedUnitChrome、旧 RelationPanel forwarding、OpeningRelationsSeeder、重复派生关系 helper 与 runtime `SpiritRootPlaceholder` 携带已删除；假的 WorldInitData LocalMap／Settlement 布局已移除。输入 parser 的 `spiritRootPlaceholder` 字段、真实初始化用 `InitialRealmPlaceholder`、任务 `learnmanual` 及有 Unity 序列化或工具／测试契约的 Prototype 均保留。

稳定 headless matrix 为 40/40；受影响 Core／Data／Unity／Tests 离线编译通过。未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。状态保持 **Implementation Complete / Producer Acceptance Pending**。

## 10. Scripts-only legacy closeout（2026-09-21）

最终 C# caller audit 删除不可达的私有旧实现及其专属 helper／字段：Exploration 旧 LocalMap party 搬运链、HostFormalHud 已退役页签、Cave Survey 无入口 modal、旧 focus placement no-op，以及零调用的 mastery、content parser／validator、movement、tile、exit-zone helpers。`WorldTravelTarget` 没有任何 C# consumer，连同脚本 meta 删除；`TryHealSiteDrift` 同样无 caller，删除兼容别名。`TryResolveRouteStartHex` 仍有 EditMode 测试 caller，保留并明确标记为旧 Outdoor LocalMap compatibility。

`ReconcileLoadedStrategicPopulation` 的 reconcile 行为与零调用空壳已永久退役；显式请求 refresh 时仍执行真实的 viewable ids refresh、缺失视图生成与隐藏视图裁剪。`LegacyFormalArmyDefinition` 只作为 Content 转换输入保留，注释明确其转换到 `NpcSquad / SquadWorldMotion`，不会产生 FormalArmy runtime 或 ArmyStack UI；opening census／normalize 诊断同步采用 SquadWorldMotion 命名。此次仅移除不可达代码、简化恒定 no-op、校正注释与诊断名称，不改变现代 Continuous Surface、PlayerParty、Squad、CharacterEncounter、Separate Space 或 Gameplay 行为。状态继续为 **Implementation Complete / Producer Acceptance Pending**。

## 11. Final Scripts-only closeout（2026-09-21）

Continuous World / Legacy Runtime Migration 的实现清理已完成。现代 authority 冻结为：

- PlayerParty：`SurfaceId + WorldPosition + SurfaceVisible / ContinuousSurfaceRoute`；
- NPC group：`Squad + SquadWorldMotion`；
- Character spatial：`AtWorldPosition`、background/resident `AtSite`、`InSeparateSpace`、`InEncounter`；
- Territory：`WorldSite + TerritoryClaim + Actual Administrative Control`；
- Combat：`CharacterEncounter`；
- Separate Space：Interior LocalMap / `EntityLocation`。

最终 closeout 删除无 caller 的 Phase-2B step state、Residual 查询残片、旧 aggression alias、Hex runtime aliases 与无消费者常量；敌对上下文命名改为现代 Encounter combat context。仍被 EditMode 回归直接调用的 Hex travel、LocalVisible approach、route-start、presence query 与测试 fixture API 明确保留，它们只构成 compatibility/test contract，不是现代 runtime authority。

`Legacy`、`Hex`、`FormalArmy`、`TerritoryRegion`、旧 Outdoor LocalMap 名字今后若属于 DTO、parser、migration、derived metadata、显式 compatibility 或 Demo/test contract，不构成 migration 未完成，也不得仅凭搜索命中启动新的 cleanup milestone。只有这些边界重新成为 modern runtime authority，或产品明确终止旧存档／旧 Content 兼容时，才重新处理。

状态：**Implementation Complete / Producer Acceptance Pending**。本轮不提前 git seal。

## 11.1 Hex／Army implementation naming closure（2026-09-22，历史实现）

本节登记 §6.1 物理退役前的当时实现，不改变 §1～§5 的决策；下列符号不再是当前 API：

- 旧 Army Content 单向链：外部 `initialFormalArmyIds` → `InitialLegacyFormalArmyIds` → `LegacyArmyContentToSquadMigration` → `NpcSquadContentBootstrap`；输入定义为 `LegacyFormalArmyDefinition`，不创建 FormalArmy／ArmyStack。
- snapshot identity 继续由 `LegacySquadMigrationIdentity.SquadIdFromLegacyArmyId` 产生，稳定 `squad:army:` 不变；`SquadCommandKind.LegacyFormalArmyWorldMotion = 2` 与 Encounter spatial `LegacyFormalArmy = 2` 的数值不变。
- `EncounterCharacter.LegacySourceFormalArmyId` 继续读写 JSON `sourceFormalArmyId`。这是兼容 source metadata，不是现代 owner。
- PlayerParty 内部现名为 `LegacyCurrentHex`、`LegacyHexPath`／`LegacyHexPathCount`、`LegacyDestinationHex`、`LegacyFinalDestinationHex` 及对应 `BeginLegacyHexAutoTravel`／`SetIdleAtLegacyHexCenter`／`AlignLegacyCurrentHex`。Snapshot `CurrentHexQ/R` 与 JSON `currentHexQ/R` 保持协议稳定。`LegacyCurrentHex` 在正常 Surface 上只是兼容摘要；旧 executor 中可为已提交路线格／缓存，不保证即时投影。
- 当时仍编译旧 Hex 几何、runtime 容器、测试投影与 Hex 尺度适配；这些实现随后由 §6.1 物理删除或改由 `outdoorSurface.movementScale` 取代。
- `ModuleId.Army`／`armyOpen` 已删除；旧 focus fixture 入口为 `LegacyPartyFocusCompatibility.SyncPartyFocus`，现代 Gameplay 无 caller。

## 12. Legacy-only concentrated closeout（2026-09-21）

最终集中扫描确认并删除四条无合法消费者的 residue：`PlayableHostSession` 的匿名 modal pause depth／Push／Pop 链；`StrategicClockFreeze` 未被现代 CharacterEncounter 使用的 Begin／End／Host presentation capture 与 saved pause／speed 字段；Host 的 `ApplySavedSpeedMultiplier`；以及旧“当前 surface 原地 WORLD_COMBAT”包装入口。同步删除 Formal HUD 两个退役住房 UI 字段和 Snapshot rehydrate 的未使用 armyCount 局部变量。

现代语义保持不变：Modal pause 只由具名 owner 获取／释放；CharacterEncounter 继续直接拥有 freeze `Reason`、`Clear`、`IsWorldTickFrozen` 与 `IsModalEncounter`；ManualPaused、输入限制和战后恢复不改。该测试契约当前已归入 `LegacyPartyFocusCompatibility.SyncPartyFocus`，因 EditMode 回归测试仍有四处直接调用而保留，不伪装成 zero-caller。`HexMetrics`、旧 DTO／枚举／parser、明确 Legacy compatibility adapter 与 Scene／Prefab／GUID 约束同样继续保留。

局部命名和注释已对齐当前模型：CharacterEncounter spatial resolver 的局部 `armyId` 改为 `ownerId`；hostile participant 查询不再使用 Strategic/WORLD_COMBAT 名称；Continuous materialization、LocalMap visibility、`ContinuousManualCombatPresentationState` 与 `HexMetrics` 不再暗示 Army／Hex 是正常 Gameplay authority。项目入口、系统页、技术页、路线图、ADR 索引与 247 handoff 统一指向 ADR-0038；旧恢复指令已明确归档，191／192／158 三处相对链接已修正。

本节是 **Implementation Complete** 记录，不是 Producer Acceptance。Core／Data／Unity／EditMode Tests assembly 离线编译 `ALL_OK`，现代 CharacterEncounter 暂停／返回纯 C# 回归 8/8；`git diff --check` 与目标符号／已知链接复查通过。额外尝试旧 `PlayerPartyContinuousWorldPhase2CTests` 时，其大量已退役 Hex／Outdoor LocalMap case 在共同 `BuildParty` 前置处失败；本轮未删除或改写这些测试，也不把该旧套件声称为通过。未打开 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode，等待制作人窄范围 smoke。
