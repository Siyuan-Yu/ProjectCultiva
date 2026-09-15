# CW-08 / CW-09：SiteCore Warfare & WorldSite Takeover

> 状态：Implementation Completed / Producer Acceptance Pending｜优先级：P0｜最后更新：2026-09-15

制作人授权在当前工作树完成玩家发起的 SiteCore 战争；保持 CW-08 / CW-09 既有编号，不重排 CW-06 / CW-07。不创建提交。

## 正式规则

- CW-05A/B/Closing 已经正常玩法人工验收，正式 Producer Accepted / Sealed。Subsequently Producer Accepted after normal gameplay validation. 原记录的非 Unity 验证事实保持有效。
- `WorldSite.CoreIsRemovable` 是唯一类型判据。固定核心 HP=0 保留建筑、Site 与 Claim，攻破后可持续占领；可拆核心 HP=0 摧毁旗并令原 Site inactive，Owner 与 Claim 历史保留，绝不 Capture。攻方须正常新建自己的旗。
- 正常玩家攻城通过统一领域服务查询真实 Character/Squad，精确 Surface/WorldPosition 与战争侧确定守军，距离核心最近、EntityId 升序确定代表。复用 CharacterEncounter 的完整入场链，明确冻结目标 Site 范围；政治后果确认只出现一次，入场继续建筑攻击意图。
- 现有 Active / ReadyToEnd 遭遇可绑定范围内的一个战略目标；范围外或第二个未完成目标拒绝。同场新守军按当前精确位置加入，保留旧参战者 HP、冷却、时间与关系候选。
- 固定核心沿用原 HP、伤害与 OccupyHoldSeconds。活着的己方在占领圈内且无活着的敌方参战者争夺才推进；离开或争夺归零。占领完成由 `WorldSiteCoreWarfareService` 经 `WorldSiteTerritoryTransferService` 改 Owner，复原耐久，不重写 Claim、不改人物身份、不重建农田。
- Capture / destruction 完成让同场目标 resolved 并 ReadyToEnd，存活敌人仍保留。击倒守军仅赋予结束资格，不自动占地；ReadyToEnd 仍能处理目标。
- Encounter 持久化新增目标身份、种类、攻守势力与完成事实；HP/Owner/Claim 仍读各自权威。旧格式明确迁移，新格式缺字段拒绝。
- 仅删除失去消费者的两条旧玩家 Siege 编排服务；保留其它 BattleOffer、自动战斗、NPC 战略移动与旧档兼容。NPC 自动攻城、普通建筑战争、产权与人物政治后果延期。

## 实现与验证

`HostNpcContextMenu` 在既有一次政治确认前做范围/目标检查，确认提交后交给 `HostWorldSiteCoreWarfare` 适配统一领域入口。`RequestConfirmedWorldSiteAssault` 直接进入准备阶段，复用普通 Encounter 场地构建，避免再显示 NPC 攻击确认；onEntered 继续原核心目标。无守军直接靠近并攻击，不创建空场。

`PrepareForWorldSiteAssault` 复用普通 Prepare 内部实现，只明确指定目标 Site 范围，再附加 objective。初始参战者、个人锚点、候选冻结、关系、战术摆放与场地来源均保留。现有场内先绑定 objective，再事务加入真实 defender Squad，准备失败撤销新绑定；已在场者不重复入场。增援追加 EntryCondition/HP baseline 并更新 roster/report/presence 投影，旧成员状态保持；已因目标进入的关系候选保留原判定记录，但不再算待到场援军或重复介入。

`HostControlCoreAssault` 将现有建筑 footprint + MeleeMargin 换算后的空间判定交给领域占领服务；政治、存活、阵营和争夺均在 Core 决定。没有把旧 presentation 半径误当 Continuous 世界单位。Breached 目标即使玩家改打守军，也继续按真实站位判定占领。场内建筑攻击共用该 Character 已有 melee cooldown；ReadyToEnd 仍可执行当前目标。HUD 优先显示已攻破与占领进度，战内新增宣战 modal 持有独立暂停 owner。

固定占领调用 Warfare → Transfer → ResetAfterCapture；旗只调用原 ApplyStrike → TryDestroy。两者通过 NotifyStrategicObjectiveResolved 校验物理/政治完成事实后提供 ReadyToEnd。未对农田、人物身份、Claim 取得顺序增加写入。

### Persistence

WorldSnapshot 仍为 v6；CharacterEncounter 子格式升为 2，包含完整 objective 与 ObjectiveDefenderSquads。只有显式子格式 1 可迁移为空目标，格式 2 缺失/损坏不 fallback。RestoreJson 先验证结构和参战者；`RestoreHexPoliticalState` 在静态 Content shell 与历史政治数据恢复后验证目标 Site、核心资产、类型、范围和结果。这与现有 Host 两阶段恢复一致，不能在 Sites 尚未恢复时误判目标失效。Host 读档后可重绑建筑执行；保留手动暂停和原冷却。固定新 Owner/满耐久、旗消失/原 Site inactive、Claim 与 crop 均经完整 JSON + Content shell + political overlay 验证。

### 第一次 Unity 验收 blocker 修复：固定核心正式绑定

第一次制作人验收发现青石荒村议政厅能显示菜单但点击攻击没有后续。定位结果是正常入口仍通过 `ControlCore.LocationId → 当前 WorldRegion Location → LocalMapId → WorldSite.LocalMapId` 猜 Site；Continuous Outdoor 下当前 WorldRegion 并不承担固定核心身份权威，因此解析失败后 Host 静默关闭菜单。

固定核心现改为静态 Content 启动时建立正式链：outdoor `controlCore` placement 的 `SiteId` / `StableId` / `BoundLocationId` 分别对应同一 `WorldSite`、`WorldSite.CoreAssetId` 与 `ControlCoreBoard.TryGetByLocation` 得到的 `WorkAreaId`。`ControlCoreBoard.BindWorldSite` 同步维护 Core 正向字段和 Site 反向索引；Content runtime 在任何写入前校验缺字段、未知 Site、未知 Core WorkArea、一对多冲突、removable 类型冲突、既有 metadata/binding 冲突和 Level 1 空间配置。新游戏和读档壳均处理失败结果，损坏 Content 不再带半套 metadata 继续启动。

### Producer acceptance follow-up：CW-09.5

固定据点战斗、攻破、占领、Actual Control／农田管理、可拆旗摧毁及 CharacterEncounter 入场已通过本轮正常玩法检查。后续发现占领后右键菜单仍无条件展示攻击，以及 Fixed Core 仍借用旧 `CaptureObjective` runtime 保存绑定和物理状态；两项由 [236](236-world-object-interaction-fixed-core-capture-closure-2026-09-15.md) 收口。CW-08／CW-09 仍为 **Implementation Completed / Producer Acceptance Pending**，待 236 联合人工验收后再封板。

正常攻城、首击 Owner 检查、占领完成和 `WorldSiteCoreWarfareService.TryGetFixedCore` 只使用 canonical 双向查询，不枚举 Core，也不读取当前 WorldRegion、ActiveMapLayout、Hex、最近 Site 或 `LocalMapId`。旧 LocalMap 推断方法保留为明确命名的 legacy compatibility helper，但没有正常玩家攻城消费者。Host 对绑定缺失、目标验证、战争预览/提交、遭遇准备和建筑接近点失败均显示玩家可见反馈，不再只 `CloseAll()`。

### 第二次 Unity 验收 blocker 修复：独立战场返回主控表现

制作人结束 CharacterEncounter 时遇到 `ActiveCharacter EntityView missing`。代码追踪确认返回链在同一帧依次执行 `CommitAndReturn → LeaveIndependentField → ActivateSurface → startup invariant`；主控可用性此前只在 Encounter tick / 下一次 `HostPlayerPartyController.Update` 刷新。`CommitAndReturn` 清除 `ContinuousManualCombat` 后若主控刚进入不可用状态，Surface 启动会先读到过期的 Active/ControlState。现在清除 Encounter authority 后、重建连续世界前立即调用正式 `PlayerParty.RefreshActiveAfterLifeState` Host 入口，使可接任成员先成为 Active，再由同一次 normal materialization 建 View。

启动不变量同步区分正式控制状态：`ControlState=Active` 仍必须有可生成且位于 CompositeWalkGrid 的 Active View；`TemporarilyUnavailable` / `AllMembersDead` 的 `ActiveCharacterId=None` 是合法状态，不再伪报缺 View。若可用 Active 仍漏建，错误会包含 EntityId、Continuous visibility reason 与 materialized 状态，继续作为硬错误而不是被吞掉。`Removed` participant 已由 `CombatLifeStateService.FinalizeRemoval` 删除 WorldPresence / presentation，离场适配不再给它重新写 PresentationOverride；弥留、阵亡但尸体仍可见者继续按原规则返回。

### CharacterEncounter 统一入场 UX 修复

制作人后续 Unity 验收确认两个 seam：有守军的 Site assault 在政治确认后调用 `RequestConfirmedWorldSiteAssault → Request → BeginConfirmed`，使“人物遭遇／手动战斗”只闪一帧；独立场地 commit 后又立即设置人物目标、释放 `CharacterEncounterUI` ModalHardPause 并执行 `onEntered`，使场地刚显示就开始追击、伤害和攻城移动。

现统一为 Host presentation 状态：`Pending → Preparing → ReadyToCommit → ReadyToStart → Active → ReadyToEnd`。`ReadyToStart` 不进入 Core enum 或 Snapshot；此时 Core CharacterEncounter 已为 `Active`、独立场地和人物 View 已 materialize，但具名 pause 与 Encounter input lock 继续持有，Host Update 也显式禁止战术推进。顶部只显示“战场已就绪／已恢复 · 当前全场暂停”和“开始战斗”，不再遮住场地。

Site assault 有守军路径现只提交 `RequestWorldSiteAssault`，停在 Pending，并与普通主动人物攻击共用同一“取消主动攻击”入口。取消仅撤回本次战术攻击并执行既有 Pending 清理；政治确认已经提交的 War 不回滚，界面以“已撤回本次攻击；战争状态不变。”明确反馈。玩家点击手动战斗后完成场地构建，再点击开始战斗才原子消费 staged attacker/target 和一次性 callback：设置初始人物目标、记录人物攻击、切 Host Active、执行原 SiteCore assault intent、清 staging、明确 `ManualPaused=false`，最后释放 `CharacterEncounterUI` pause 与输入锁。普通人物攻击和自动接触选择手动战斗后共用同一 start gate。

Producer acceptance follow-up：此前 `RequestWorldSiteAssault` 显式传入 `allowPendingCancel=false`，导致同一个 `DrawCharacterEncounterOffer` 对 SiteCore assault 隐藏“取消主动攻击”。现统一为 player-initiated CharacterEncounter Pending 均可撤回当前主动攻击；SiteCore assault 在政治确认之后取消只终止 tactical assault，不撤销已经成立的 War。CW-08/CW-09 状态仍为 **Implementation Completed / Producer Acceptance Pending**。

恢复 Core `Active` 的 CharacterEncounter 也在 materialize 后停入 ReadyToStart；未解决 Site objective 的恢复攻击只保存为一次性 on-start action。恢复 Core `ReadyToEnd` 保持现有战后路径。已经处于同一 CharacterEncounter 时追加 Site objective／守军仍直接绑定当前场，不重新出现手动确认、加载或开始门；无守军 SiteCore 继续直接攻击。

### 正常 Content

复用 `base:scenario_ch01_reference` 的 **青石荒村**（`base:site_huangcun`）、**议政厅／主管府**、**荒村驻军**（代表人物杂役主管）及原有六片 grainField。现有 farm administrative anchors 共 260 格（包含药田），已有固定 Site 管理。守军开局采用既有 authored opening placement resolver 落点；攻城查询只消费真实 Character/Squad 的精确位置。

复用敌旗 `base:flag_q1_r10`，仅补正式中文名 **荒村西北前哨**，未新增重复据点、兵员或 Debug spawn。开局该旗范围无合格守军，可直接摧毁。现有旗是 removable，议政厅是 non-removable；保持现行 Content HP、Defense、占领秒数及等级范围不变。新名称在 reference NewGame 中生效；旧档保留已存 Site 名称。

### Legacy 范围

确认没有其它代码消费者后删除 `WorldSiteSiegeService.cs`、`FactionFlagSiegeService.cs` 及各自 meta。BattleOfferService、FormalArmy 数据/旧档/NPC 移动/自动战斗仍保留；既有驻军 Content 种子仍通过现行 Squad bootstrap 建立真实成员，不在攻城时扫描或克隆旧 Army。

### 实际验证

- ContentPackageLoader strict validation + reference bootstrap 通过；按现有 Data opening placement 解析真实守军，固定 Site 有农田，前哨无守军。
- offline compile：Core 466、Data 80、Unity Host 146、Unity Editor 6、Tests 197、PlayModeTests 3、Assembly-CSharp 51、Assembly-CSharp-Editor 1 sources，全部 0 error；仅既有 warnings。
- Mono headless 定向：SiteCoreWarfareTests 10/10；此前 ConstructibleFarmLifecycleTests 8/8 保持有效。新增验证覆盖 `BindSite` 双索引事务，以及真实青石荒村 placement 在清空整个 WorldRegion 后仍能 canonical 双向解析、无战争预检、宣战后首击和 Save/Load 重绑。其余测试继续覆盖 breach、contested/reset、原 Site capture/Claim/crop/NPC identity、旗摧毁及正常支付材料重建己旗、持久化、战争侧/确定性守军、同场加入不重置状态、击倒守军不占地及新旧格式严格校验。
- CharacterEncounterStartGateTests 定向覆盖 ReadyToStart 不设置初始目标、Host 战术推进硬门、world/domain/field/pause/input 前置条件、restore staging 清理、callback 单次消费和具名 pause 幂等释放。
- 另尝试运行通用 `ContentPackageTests`：5 项纯校验通过，4 项 BaseGame 路径用例在进入 Loader 前因现成测试直接调用 Unity `Application.dataPath`、本机 Mono/UnityEngine ECall 不兼容而失败；真实 BaseGame strict load 已由 SiteCore 定向类成功执行，不把该环境失败记作产品通过或回归。
- 正常 Host → Warfare/Assault 路径定向搜索无 BattleOffer 构建、旧军事攻击 offer、FormalArmies.Armies、ArmyStack、FormalArmyId 或两条旧 Siege 引用。
- `git diff --check` 通过；新源文件与 meta 保留未提交。未运行 Unity、Test Runner、batchmode 或 Bake。Host 点击/画面仍由制作人正常玩法验收。

## 制作人人工验收（两条）

1. **固定据点：** reference 开局前往青石荒村，右键议政厅攻击，完成一次政治后果确认；杂役主管所在真实驻军进入 CharacterEncounter，原攻击意图继续。打破核心后建筑保留；占领区有活敌时进度归零，清开后站满原约 10 秒接管。原农田立即变为己方管理并可右键农作；Save/Load 后据点与农田保留。
2. **前哨旗：** 前往青石荒村西北的荒村西北前哨，右键攻击势力旗；上一条已对同势力宣战时不重复确认。打到零血旗消失，不出现占领步骤，原 Site 不转给玩家。之后通过“建筑 → 势力控制建筑”正常支付材料建立自己的新旗，才获得己方控制。

No commit created.
Changes remain uncommitted for producer review.
