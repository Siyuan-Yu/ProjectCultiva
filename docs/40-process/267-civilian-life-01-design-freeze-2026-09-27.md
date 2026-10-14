# CIVILIAN-LIFE-01 — Mortal Needs, Faction Work, Occupation, Captivity & Recruitment V1

**Implementation Complete / Producer Acceptance Pending（2026-09-27）。** Rescue / Haul / Construction 缺口及 P1 通用 Character 势力忠诚 authority 已接线，等待制作人人工验收。

## Product Goal

让据点里的凡人由真实、可持续追踪的人物承担生活、劳动与战后命运。玩家能回答“谁在劳动、谁逃走、谁被俘、谁后来加入”，同时以需求优先级、职业单选和离屏 LOD 控制操作量与运行成本。

Core/Data/Host/Content 已接通本轮 V1；§89 的可操作闭环等待制作人人工验收。未修改既有 AUCTION-01 规则。

### P1 食宿动作闭环、失败处理与地图可达性（2026-09-27）

- 本轮以仓库当前 `Assets/Scripts`、`Content/BaseGame`、制作人提供的问题代码事实和 `Content.zip` 为证据源。代码复核确认旧实现会在无粮时停留于 `FetchFood`、在抵达住所前写入睡眠开始时间，且 `HostInputGate.BlockWorldInteraction` 会中止全部移动更新；当前 Content 与参考包均确认荒村初始 grain 为 0、目标门墙原为 4 cells。修复针对这些已证明的根因，不改初始库存来掩盖行为缺口。
- `FetchFood` 使用显式阶段：来源/设施/权限失败、等待 Host 寻路、行走、不可达、到达、进食。只有合法来源、权限、StorageRoom 和现存 food 同时成立才创建移动 intent；抵达后重新校验并原子扣 1 份 food，随后只结算一次 Satiety。无粮、无设施、无权限或不可达保留可见原因，并按条件签名变化或最多 8 WorldTick 后有界重试，不在同一失败状态逐 tick 重建路线。
- `Energy < 10` 的紧急睡眠高于失败后的取食重试；无粮且极低精力会在同一决策 tick 转入就地睡眠，不在 FetchFood 与 Sleep 之间振荡。正常睡眠分为 `TravelingToResidence`、`SleepingAtResidence`、`SleepingOnGround`：返家路上不恢复 Energy、不写 `SleptToday`，只有同一住所 intent 的真实 `HostArrived` 才开始床上睡眠。
- `MovementIntent.Revision` 与 Host request/accept/failure feedback 形成一次 intent 生命周期。Core 清除或替换已接受的 civilian intent 时，`HostNpcScheduleMover` 只取消该 civilian 的旧 Host path；PlayerParty、Encounter、SeparateSpace 与 NPC squad 运动边界保持。暂停仍冻结移动；人物菜单等 `BlockWorldInteraction` 只屏蔽玩家输入，不再冻结自治 NPC。
- managed mortal 的 Continuous Surface 个人位置由 Host 移动逐步提交回 `WorldPresence`；普通 `CivilianLocal` 不再被静态 resident squad anchor 覆盖。CapturedEscorted 保持 carrier 同步；真实战略 squad 路线、多成员 follower、PlayerParty、Encounter 与 SeparateSpace 继续优先。
- 荒村 guard housing 门洞的阻挡墙 `base:site_huangcun:place_wall_040130496` 在 source blueprint 与 baked surface 同步由 4 cells 缩至 3 cells，稳定 ID 和起始位置不变。离线复算中关键格 `(246,443)` 从 blocked 变为 walkable；3 名 guard 到 StorageRoom 的路径由不可达变为 39/42/43 格，外部起点到 guard housing 由不可达变为 21 格，原 farmer 与普通住房路径保持可达。
- Snapshot schema 仍为 v14。睡眠阶段随现有民生状态持久化，避免读档把返家误当已睡；食物搬运仍为瞬时计划，读档从 Satiety、位置、Site、权限、StorageRoom 与库存重新求值。
- LevelTester Content 页提供四个互相独立的准备入口：`准备食宿 A：有粮取食`、`准备食宿 B：正常回床`、`准备食宿 C：紧急睡眠`、`准备食宿 D：无粮且极低精力`。入口只准备当前选中的正常 managed mortal、当前实际 Site、需要的库存和 Needs，不替代正式行为完成；诊断同时显示阶段、实际位置、intent revision、Host path 反馈、源/目标 grid、首个 gate、WorldTick 和 pause 状态。

### P1 Physical Food Fetch / Formal Work UI（2026-09-27）

- 普通自由凡人饥饿时进入 `FetchFood`，只前往当前合法 Site 的启用 `StorageRoom`；到达后才从该 Site 的 `SitePublicStock` 原子移除一份带 `food` tag 的资源，再进入短暂 `Eat` 并恢复 Satiety。出发前不扣粮，多人争抢由到达时事务决定；无储藏室、无权限、不可达或无粮时保持饥饿并留下可见诊断。
- Detained 继续使用既有抽象 Site 公粮照料；PlayerParty 继续使用既有队伍供给逻辑。`FoodFetchStorageRoomId`、待取资源和进食阶段均为瞬时任务状态，不进入 Snapshot；读档后从正式 Satiety、位置、Site、StorageRoom 与库存重新求值。
- 正式 HUD 常驻“工作”入口。面板按当前查看／当前玩家人物所在 Site 列出当地真实凡人，读取统一 Activity、Schedule Phase、Satiety、Energy、Faction Loyalty 与管理资格；玩家只能为玩家势力、永久可管理、`Normal` 的凡人通过 `MortalCivilianService.SetProfession` 改职业。
- 专业工区必须与人物当前 Site、该 Site 的实际所属势力和所需 tag 同时匹配；不再从全世界取第一个同 tag 工区。紧凑人物卡与头顶字优先读取 `MortalCivilianState.Activity`，明确显示“取食／进食”等真实活动，课表阶段单独呈现。
- 荒村初始职业：阿石／阿青 `Unassigned`，阿土／阿禾 `Farmer`，阿兰 `HerbFarmer`，阿杏 `Medic`，阿木／阿柴／阿枝 `Logger`。

### P1 Managed Mortal MovementIntent Host Bridge Fix（2026-09-27）

- managed mortal 从 FarmerWork／HerbFarmerWork 切换到取食、睡眠、伐木、救援、搬运、建设或逃亡时，`HostNpcScheduleMover` 在同一帧先释放旧 NPC-schedule farm worker，再消费 `MortalCivilianService` 当前 `MovementIntent`；不依赖 MonoBehaviour Update 顺序。
- `HostFarmFieldLabor.StopNpcScheduleFarmOwnershipOnly` 只释放田格 reservation、农田表现移动和 worker 记录，不清除 Core MovementIntent。managed mortal 的农田注册只认当前 `MortalCivilianState.Activity` 与对应 farm intent，不再回退残留 `WorkAction`／legacy Schedule。
- `MortalCivilianMovementAuthority` 是 Core evaluator、Host intent consumer 与 NPC squad presenter 的共同空间所有权规则。Battle、SeparateSpace、PlayerParty、真实活动路线中的 `SquadWorldMotion` 和多成员 `FollowLeader` follower 高于 civilian local movement；只有静态位置的 singleton resident squad 必须让位，且 presenter 不再把该人物写回 squad anchor。
- `WorldSiteStorageRoomState` 由 authored placement／constructed asset 重建中心与完整 footprint；FetchFood 根据人物起点选择最近 footprint 边缘外侧 access point，再交给 Host 小范围 walk-grid snap。Core 只以该 intent 的 `HostArrived` 完成取粮，不再把阻挡建筑中心作为目标或另设距离 gate；这些字段仍是 derived／session-only，不升级 Snapshot v14。
- Schedule Phase 只决定需求满足后是否开始工作，不拥有人物移动。Work 或 OffDuty 下 Satiety 触发 FetchFood 时，需求始终先于 Profession／Schedule。
- LevelTester 只读诊断增加统一 `CivilianMovementOwner`、Squad id／人数／命令／motion 状态、StorageRoom center／resolved access，以及既有 MovementIntent、Host path／arrived／farm／interaction 状态。

## Implemented V1

- `MortalCivilianBoard` 以真实 `EntityId` 保存 Satiety、Energy、Profession、Activity、Disposition、居所、逃亡目标、押送者、囚室与每日照料事实；`MortalActivityEvaluator` 是需求与工作的单一优先级入口。旧版将 Loyalty 存在该 Board 的描述已由 P1 取代。
- `CharacterFactionLoyaltyLedger` 以真实 Character EntityId 与当前 FactionId 保存唯一 Loyalty（0–100）；凡人、修士、NPC、永久玩家人物及有正式势力归属的 Temporary Quest Companion 均遵循同一规则。无势力人物不适用，人物概况固定显示“忠诚：—”。`initialLoyalty` 是人物对初始势力的配置种子，省略为 50；切换势力时旧值失效，明确招降写新势力 50，其它转移使用配置种子。
- WorldTick 驱动饥饿、睡眠、进食、专业劳动、真实伤员搬运、逃亡、押送与拘留。食物只识别 `food` tag；农夫、药农连接现有逐格农作，伐木工连接现有树木破坏结算，产物写入 Site 公共库存。通用 Haul/Construction 已改为 Site PublicStock → 工单 transport payload → 工地材料与正式 ConstructionService 完成，不再以 LocationLabor 数值冒充建设。
- `WorldSiteTerritoryTransferService` 在真实易主事务完成后触发旧势力凡人的投降或逃亡；不改 FactionMembership。阿青初始 Loyalty 10，阿土 60，阿土初始 Farmer。
- 右键交互提供投降招募/释放、逃亡捕获、拘留招募/释放/处决；CapturedEscorted 不开放普通 NPC 菜单，押送者靠近 PrisonerOnly 住房后从住房检视执行“安置押送俘虏”。捕获复用 CharacterEncounter，正式结算胜利且目标仍可捕获后才进入 CapturedEscorted。
- Residence 支持 Normal/PrisonerOnly，复用住房 Capacity；Faction/外交面板的玩家势力详情提供凡人人事列表与单选 Profession。
- Snapshot schema v14 保存全部民生/逃亡/押送/拘留/照料与 Residence usage authority，v1-v13 明确拒绝，恢复时校验数值、枚举和 Entity 引用。
- LevelTester Content 页提供黄村定位、公共粮食加入/清空、选中凡人需求设置、拘留忠诚设置、Reset 与诊断。

## Design Principles

- 每个凡人都是持久、独立、具有稳定 `EntityId` 的真实 Character；人口、劳动力和职业计数只能从人物状态派生，不能成为匿名权威。
- 凡人与修士共用 Character 基础。凡人不会因加入势力、接受工作或被玩家管理而自动成为修士或内门弟子；修士生活方式与日程属于 Future。
- 当前住房表现保持可见、真实，不引入房屋黑箱、private presence 或人物非物质化。未来性能优化通过 Character LOD 完成。
- 本设计取代旧“普通凡人以 `MortalPopulation`／岗位组作为真源、被关注后才实体化、可再归并”的方向；受保护的历史 Freeze 正文不在本轮改写，现行产品方向以 [ADR-0044](43-decisions/ADR-0044-individual-mortal-faction-work-needs-and-captivity-direction.md) 为准。

## Character 与 Faction 语义

- 有归属凡人拥有真实 `FactionId`／`FactionMembership`，可以接受该势力工作和人员管理。
- 无归属凡人使用 `FactionId=None`：仍是有需求与基本生活的真实 Character，不接受势力工作；Site 易主不会自动把他加入新势力。
- PlayerFaction 凡人由正式 `FactionMembership` 派生进入 Faction Personnel 管理，可分配 Profession；`FactionMembership != PlayerParty != ActiveCharacter`，招募不会调用 PlayerParty 加入路径。
- Site 所有权变化不会直接改 Character 的势力、忠诚、位置或身份。

## Mortal Needs V1

V1 只包含 `Satiety`（饱食）与 `Energy`（精力），建议均为 0–100，数值越高状态越好。Social Need 不在本里程碑；Mood、Comfort、Entertainment 只有在后续玩法证明需要时才设计。

正常决策优先级：

1. Combat／即时危险。
2. Fleeing／Capture／Escort／Detention 强制状态。
3. Hunger／Satiety。
4. Sleep／Energy。
5. 紧急 Rescue。
6. 当前 Faction Mortal Daily Schedule 阶段。
7. Work 时的专业职业工作、通用搬运／建设；OffDuty 时返家／休息／Idle。

饥饿通常先于睡眠与工作；逃亡者不会因轻微饥饿停下。`Energy < 10` 时可原地紧急睡眠，恢复慢于床位；正常返床阈值可调。

势力凡人没有个人物品栏。普通有势力居民从当前可访问、同势力 Site PublicStock 取食；永久 PlayerParty 凡人优先取当前己方 Site PublicStock，缺粮后从 PartyInventory 取食；无势力但拥有合法 Residence 的居民只有该住所 Site 的 food-only 取食权，不因此获得仓库管理、势力工作或势力身份；Detained 只从拘留他的玩家控制 Site PublicStock 取食。`food` tag 资源按稳定 ID 顺序选择，不凭空生成或跨币种折算。

Faction-affiliated Mortal 遵循统一 Faction Mortal Daily Schedule：Work / OffDuty。V1 起止时间集中在 `MortalCivilianTuning`（当前默认 08:00–18:00），属于可调默认值而非永久冻结规则；无势力者不接受此工作日程。即时战斗／危险、强制处置、紧急饥饿与睡眠优先于日程；紧急 Rescue 可打断 OffDuty。OffDuty 不主动开启 Farmer／HerbFarmer／Logger／Medic 工作，可返回 Residence。

## Work 与 Profession

每名凡人最多一个专业职业。V1 单选项为：

- `Unassigned`
- `Farmer`：食物／普通农业。
- `HerbFarmer`：药草农业。
- `Logger`：只选择当前同势力 Site 物理范围内的现有可破坏树木，复用 Host 砍伐结算并把粗木写入 Site PublicStock。
- `Medic`：真实治疗。

所有有行动能力的凡人都可执行通用 `Haul/Rescue` 与当前已提供正式事务的 `Construction` 工作；当前接通农田／恢复处／储藏室建设及势力旗拆除，正式 Repair 和普通建筑拆除 authority 尚不存在。搬运濒死者是紧急救援，不等于 Medic；所有人可救援，只有 Medic 提供专业治疗。通用工作在专业工作无可执行目标时领取；`Unassigned` 仍有需求并可搬运、救援、建设，但不执行专业生产。

V1 不包含 Miner、Generic Gatherer、Artisan、Weaponsmith、Cook、Cleaner、Hunter、Researcher 或商业 AI。现有农业／劳动应增量迁移为具名 Character worker，并能回答具体劳动者；本设计不授权重写农业系统。

## Personnel UI Direction

入口位于 Faction → 人员管理。紧凑人物行／卡片显示姓名、Mortal/Cultivator、势力职位、Profession、当前活动／状态、Satiety 与 Energy。Profession 使用单选列表 `Unassigned/Farmer/HerbFarmer/Logger/Medic`。不采用 RimWorld 式大型工作优先级矩阵。

## Site Occupation Aftermath 与 Loyalty

Site 完整易主后，旧势力有归属凡人不会自动换阵营、加入玩家、删除或传送。每个 Character 依据对**当前 Faction** 的 `Loyalty`（0–100）决定后续；Loyalty 不属于 Site、Relationship 或永久人格，加入新势力时建立新的 Loyalty。

- `Loyalty < 15`：留在原地但不自动加入。玩家右键可选择招募／劝降（立即成功）、攻击／杀死（正常 Encounter）或释放（进入 Fleeing）。
- `Loyalty >= 15`：进入 Fleeing，缓慢逃离。开始时间错开，选择不同合法可行方向，大体远离 SiteCore；不得整齐列队或随机撞墙。
- 逃离成功：人物越过正式 Site 物理／控制范围和一小段可调 buffer，成为 `EscapedFromSite/Displaced`。清除该 Site 的工作、居住、取食、睡眠移动、建设托运和救援绑定，不再计入居民／劳动力，但保留 EntityId、FactionId、Loyalty、Profession 身份、Needs、WorldPosition 和 displaced 事实。仍在 materialized neighborhood 内时保持真实 Character，就地 idle／基础 Needs，不发起长距离避难寻路；离屏后复用 BackgroundCharacterTravel，前往同 Surface 最近可达、仍由本 Faction 控制的 active Site。没有合法目标时保持当前位置与 Displaced；到达后仍不自动分配 Residence／workforce。
- 本规则只覆盖战败势力的有归属凡人；无归属居民的战后行为不在 V1 扩展。

## Player Civilian Interaction 与 Capture Encounter

对自由或逃亡凡人的攻击必须走既有 World interaction → `CharacterEncounter` → Separate battle → result → world，不能直接扣血或建立 `CivilianCombat`。玩家追上逃亡者时可攻击、捕获或释放；释放恢复 Fleeing。

捕获必须通过带 `Capture/Subdue` intent 的正常 Encounter。玩家获胜且目标仍存活、可捕获时才成功；真正死亡的 Character 不能被捕获。

## Escorted Captive、Prisoner Housing 与 Detention

被捕者仍是同一 Character。当前主动发起者成为 carrier／escort；被押送者不能自行移动、工作、被玩家控制或参与玩家劳动，Core 与 Host 将其 WorldPresence／EntityView 同步到真实 carrier。carrier 死亡、移除、进入不兼容 Encounter 或运输取消时，目标安全留在 carrier 最后合法位置并清除 carrier binding；不自动换人或瞬移到囚室。

没有合法监牢时，不能把人物抽象为“已拘留”；其状态保持 `Captured/Escorted`。普通 Residence 增加 `Normal/PrisonerOnly`（或与现有架构等价）的用途，复用真实住房容量，不创建第四套空间或匿名囚犯计数。只有实际带到有容量的合法 `PrisonerOnly` Residence 后才成为 `Detained`。

Detained Character 保留原 `FactionId` 与 Loyalty，有 Satiety/Energy，需要吃饭和睡觉，但不参加势力工作、耕作、伐木、建设或医疗岗位。

## Recruitment、Surrender、Release 与 Execution

- Detained 只有在当天获得食物和正常休息时，Loyalty 才下降；饥饿或休息不良不会下降。“虐待加速投降”不成立。V1 默认 `-5/World Day`。
- Detained `Loyalty < 15` 后允许招募／劝降。成功后 `FactionId → PlayerFaction`，建立新的 PlayerFaction Loyalty = 50；获得凡人／基础外部成员语义，仍不是修士。
- 招募者立即进入玩家 Faction Personnel，获得现有 PlayerFaction permanent manageable authority，可分配职业并参与需求与工作；不会自动加入当前 PlayerParty。人事面板提供“加入当前小队”，复用 PlayerParty 容量、共处、生命、战斗锁与 squad 校验；入队后可切换 Active，离队不删除势力、职业、忠诚与需求。Temporary Quest Companion 不获得该权限。
- Detained 可执行招募、释放、处决。释放后从当前玩家 Site 向外 Fleeing；处决是囚犯操作，不需要伪造势均力敌的 Encounter。自由／逃亡者的攻击仍必须走正常 Encounter。

## Death / Execution

自由或逃亡凡人的杀伤与死亡只通过正常 CharacterEncounter 结算，继续遵守 Dead ≠ Removed。已拘留囚犯的处决是明确 prisoner action，不再开启虚假的平等战斗；无论哪条路径，人物都不能被换算成匿名人口扣减。

## Persistence Requirements — Implemented in Snapshot v14

Snapshot v14 的 Entity DTO 持久保存个体需求、Profession、当前 Faction、`initialLoyalty` 种子、Fleeing／Displaced、`RefugeTargetSiteId`、离屏 Needs 结算 tick、Captured／Escort、Residence 用途、Detention 照料事实、睡眠阶段、逃亡目标/起始 tick、押送者与最后更新时间；Squad DTO 持久保存唯一的可选 `DefendedSiteId`，既有 BackgroundCharacterTravel DTO 保存避难旅行本身；顶层 `characterFactionLoyalties[]` 独立保存当前 CharacterId/FactionId/Loyalty。Restore 验证唯一性、范围及与人物当前 Membership 一致，不重新应用 CharacterDefinition 初值，也不从 Content 再覆盖已恢复的 Squad defense authority；v1-v13 与缺少新 authority 的未封板 interim v14 均拒绝。返家途中保存为 travel phase 且不产生睡眠恢复；Food Fetch 的 path/target/重试上下文仍为瞬时状态，读档重新求值。

## Performance Direction

V1 继续使用当前 WorldTick 与 Host streaming/presentation 行为，没有新增完整通用离屏 LOD。仅 Displaced 在离开 materialized neighborhood 后进入既有 BackgroundSimulationScheduler bucket：停止本地 MovementIntent、取食、睡眠寻路、职业、救援和建设，只按 elapsed WorldTick 批量降低 Satiety/Energy，并由 BackgroundCharacterTravel 持有空间移动 authority。身份始终是同一 Character，不折回匿名人口。

## Explicit Non-Goals

Social Need、Mood、Comfort、Entertainment、越狱、复杂囚犯待遇、酷刑、劝降小游戏、性格／概率劝降、难民自动安置／自动转籍、Miner、Generic Gatherer、Cook、Cleaning、Hunting、Crafting profession、武器生产、装备 AI、修士日常 AI、战略势力 AI、自动战争、巨型凡人军队、住房黑箱／private presence、完整 LOD 重写。

凡人主要是居民和劳动者；未来可讨论 guard／militia duty，但不把数百或数千凡人变成 RTS 大军。

## Implemented V1 Tunables

当前集中参数：Satiety/Energy 默认 100；饥饿阈值 40；正常睡眠阈值 35；紧急地面睡眠阈值 10；醒来阈值 85；进食恢复 55；床每 2 tick 恢复 1，地面每 4 tick 恢复 1；Satiety 每 4 tick 下降 1，清醒 Energy 每 5 tick 下降 1；逃离 buffer 为当前 Surface 的 3 个导航格、按 EntityId 错峰最多 12 tick；得到食物且在合法囚室睡眠的完整 World Day 使 Loyalty -5。食物按资源 `food` tag 从当前合法 Site PublicStock 确定性取用。招募写 PlayerFaction `Member`、Loyalty 50、Profession Unassigned，且不加入 PlayerParty。

## Producer Manual Acceptance

待制作人人工确认：每位居民有稳定身份与需求；饿了实际取食，困了找床或紧急就地睡眠；职业单选且通用救援／建设可插队；Site 易主后人物按 Loyalty 留下或真实逃跑；捕获必须经 Encounter，押送到真实 PrisonerOnly 容量后才拘留；良好供养逐日降低囚犯 Loyalty，招募后建立新的 PlayerFaction Loyalty 并成为可管理凡人。Save/Load 后所有身份、位置和状态保持。

## Rescue / Haul / Construction Realization

- Rescue 使用 `Rescuer EntityId + Target EntityId + RecoverySpotId` 搬运预订；Host `MovementIntent` 向真实伤员与具体恢复点导航，接触后伤员保持同一 Character 与生命／势力状态，WorldPresence 随搬运者移动，最后只在正式 recovery spot 物理位置放下。医疗 WorkArea 中心和 SiteCore 不再是 fallback 或完成判据。每个恢复点同时最多放置一名伤员；同一 Site／Surface 内优先医疗区绑定点，满位后继续选择区外恢复点，无空位或路径失败则不完成。搬运中目标不自主行动，也不进入 PlayerParty；送达不恢复 HP。搬运者死亡、战斗入场、需求打断或目的地失效时在搬运者当前位置安全放下并释放预订。
- 玩家仍由正式建筑 UI 选择农田／恢复处／储藏室落点。当当前己方 Site 有合法未入队 Unassigned 凡人和本地公共库存材料时，放置命令进入 `ConstructionService.TryQueueCivilianOutdoorConstruction`；否则沿用原即时建造。V1 工单只处理当前 Site 内短距离搬运，不创建 NPC 个人背包或跨 Site 物流。工人先到 SiteCore 公共库存取材，`WorldSitePublicStockService.TryRemove` 扣一次，材料进入工单的单人 transport payload；抵达工地后转入 delivered escrow。满足材料后工人贡献 12 tick 劳动，`ConstructionService.TryCompleteCivilianJob` 复用原有授权／重叠检查／正式建筑注册，并明确跳过第二次材料扣除。可见工地标记与 LevelTester 诊断显示进度和载荷。
- 正式势力旗 `TryDismantleFactionFlag` 已有拆除事务；有可用凡人时原确认按钮改发拆除工单，工人到场、贡献劳动后仍调用该事务；无可用工人时沿用原即时拆除。当前产品没有可复用的正式 Repair gameplay authority，V1 不虚构修理进度或第二套建筑系统；普通农田／恢复处／储藏室也没有正式拆除事务，仍属未来建筑扩展。
- Rescue 运输预订为可安全重建的瞬态状态；正式 `RecoverySpotId → InjuredCharacterId` 送达关系写入 Snapshot v14，Restore 同时校验恢复点、人物弥留状态与精确物理位置，已安置伤员不会读档后再次领取。建设工单、材料 delivered escrow、单人 transport payload／carrier 与 labor progress 同样存入 v14；Restore 不重扣材料、不重加进度。Site 易主撤销工单并退还可返库存；遇到战斗或工人失效时退还未交付载荷。

## Current Status

**Implementation Complete / Producer Acceptance Pending。**

### 当前功能与验收状态表（唯一维护点，2026-10-10）

> 本表是 CIVILIAN-LIFE-01 当前功能与人工验收状态的唯一清单。`114`、`247`、总览与 Devlog 只链接本节，不复制第二份状态矩阵。工具准备成功、离线编译通过均不等于制作人人工验收通过。

| 能力名称 | 当前代码入口 | 正式 UI / 玩家入口 | Content 前置 | 制作人已确认范围 | 仍待验收分支 | 已知阻塞 | 下一步动作 |
|---|---|---|---|---|---|---|---|
| 食宿与 Needs | `MortalCivilianService.Tick` / `ApplyActivity` | 自动生活；正式速度栏 | StorageRoom、Site PublicStock、Normal Residence | 规则与实现范围已确认 | 有粮取食、回床、紧急地睡、无粮低精力、Save/Load | Detained 受缺口 A 影响 | LevelTester A 卡按 1x/2x/5x/20x 人工验收 |
| 职业与工作 | `MortalActivityEvaluator`、`HostMortalWorkPanel` | HUD“工作”面板 | 同 Site/owner 的专业 WorkArea | 单选职业、通用救援/建设范围已确认 | Farmer/HerbFarmer/Logger、职业切换与存档 | 课表显示/执行受缺口 B 影响；Medic 治疗延期 | 用正式工作面板逐项人工验收 |
| 建设与搬运 | `CivilianConstructionJobService`、`ConstructionService` | 正式建设 UI | 己方 Site、公共木材、合法工人 | 工具只准备人物/材料，正式 UI 发起 | 取材、搬运、施工、读档续作 | Repair 与普通建筑拆除明确延期 | B 卡准备后从正式建设 UI 落点 |
| 占领、驻军与追击 | `WorldSiteTerritoryTransferService`、`SquadState.DefendedSiteId`、Flee MovementIntent | SiteCore 战争、人物右键交互 | 主管显式驻军；巡卫独立 patrol squad | 显式驻军、0.2 逃亡、真实追击、Displaced 范围已确认 | 全链与 Save/Load | 无工具阻塞；业务仍待人工确认 | C 卡观察实际名单、速度、位置与身份 |
| 医疗搬运 | `MortalCivilianService` Rescue、`RecoverySpotDeliveries` | 自动发现与真实搬运 | 同势力弥留者、空闲 recoverySpot | 只搬到具体恢复点并放下 | 自动发现、搬起、送达/占用、中断、Save/Load | 自动治疗、入院、出院、Medic 治疗均明确延期 | D 卡二次确认准备；不得用预填 RescueTarget 冒充发现 |
| 囚犯押送与交接 | `RequestCapture` / `CanDetain` / `Detain` | 正式 Capture Encounter、囚犯交接点 | CapturedEscorted、真实 Carrier、PrisonerOnly、显式 `prisonDropOff` | 真实押送、到点后安置范围已确认 | 运输中断、容量、Save/Load | 旧“缺少 PrisonDropOff”在最新 Content 中已消失 | E 卡强制准备只验押送/交接；俘虏战另走正式流程 |
| 囚犯照料与招降 | `MortalCivilianService.Tick` / `Recruit` | 人物交互、人员管理、加入小队 | Detained、食物、合法囚室、低 Loyalty | Proper Care 与招降权限规则已确认 | 食宿、照料日、忠诚下降、招降后权限/入队 | 缺口 A 当前阻断 Detained 食宿闭环 | 下一轮先修 A，再执行 F 卡人工验收 |
| CIVILIAN Snapshot v14 | `SnapshotService` / `HostLevelTesterSnapshotOps` | LevelTester“存档与会话” | 各分支已建立真实运行状态 | schema v14 与持久字段范围已确认 | 工作、送达、押送、关押、Fleeing/Displaced | 无工具自动验收；每条需人工对照 | G 卡逐状态保存/读取并核 EntityId/owner/target |
| 历史回归：SHOP/AUCTION/SOCIAL/DYNAMIC | 各已封板 service / Host panel | 各正式 UI | BaseGame 对应对象 | **历史已封板（Producer Accepted / Sealed）** | 本轮不重开 | 无 | 仅在“世界与内容”保留折叠/滚动入口 |
| LevelTester 独立小功能页重组 | `HostLevelTesterCheatPanel`、`LevelTesterToolCatalog`、各 Cheat section、`HostUiHitTest` | `` ` `` / 顶栏作弊工具 | LevelTester Scene | 五大聚合页签方案已 Superseded | 74 个具体 ToolId、九组二级折叠菜单、搜索临时展开、单功能页、双滚动区、关闭生命周期 | 等待制作人人工验收；完整迁移表见 [268](268-level-tester-direct-tool-pages-2026-10-10.md)，关闭核查见 [269](269-ui-foldout-and-close-audit-2026-10-11.md) | **Implementation Complete / Producer Acceptance Pending** |

状态词解释：历史已封板＝已有明确 Producer Acceptance；已实现待验收＝代码完成但没有人工结论；部分路径已人工通过必须有单独证据才可写；已确认缺陷见下表；明确延期不纳入当前通过条件；旧设想若被后续决定取代只保留 Historical / Superseded 标记。

### 已确认业务缺口（本轮只记录，不实施）

| 缺口 | 当前文件 / 函数 | 对玩家操作的影响 | 复核结论 | 建议下一轮 |
|---|---|---|---|---|
| A. 囚犯食宿早退 | `Assets/Scripts/Core/Npc/MortalCivilianSystem.cs`，`MortalCivilianService.Tick` 约 468–524 行；`life.State != Alive` 在 `ApplyActivity` 前 `continue` | Detained 的 Lifecycle 为 Captured 时虽然先衰减 Needs/结算旧日照料，但不会进入 Detained 取食/睡眠活动，因此无法自然形成新的 Proper Care 日 | **仍存在，已确认缺陷** | 单独业务轮决定 Captured+Detained 的合法 Tick 分支；不得把 Captured 改 Alive 伪装通过 |
| B. 两套日程 | `HostFormalHud.DrawEditableSchedulePanel` / `BuildScheduleText` 与 `HostCharacterPresentation` 读取 `ScheduleComponent`；`MortalActivityEvaluator` / `ApplyActivity` 读取固定 `FactionMortalSchedule.Current` | 右栏可查看/编辑的旧课表不会改变凡人实际 08:00–18:00 Work/OffDuty；UI 反馈与运行行为不一致 | **仍存在，已确认缺陷** | 单独设计/实施轮统一 authority 或明确拆分 UI；本轮不改需求优先级与日程 |
| C. 普通住房转囚室后缺交接点 | `MortalCivilianService.TryResolvePrisonDropOff` / `ValidateDetain`；`Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` 的 `base:site_huangcun:prison_dropoff` | 旧版本会因没有显式点而无法安置；当前荒村 PrisonerOnly 囚室已有绑定 `base:loc_ref_huangcun_prison` 的 `prisonDropOff` | **最新仓库已不存在**（保留显式交接要求） | 不改回住房中心 fallback；继续人工验收到点距离、容量和存读档 |

旧“架构冻结不编码”与“不切独立战斗”等提案均为 **Historical / Superseded**；现行实现与验收规则以本页、ADR-0044、当前 CharacterEncounter / Continuous Surface 代码和 [247 当前交接](247-project-handoff-current-state-2026-09-18.md) 为准，不删除历史记录，也不把代码改回旧方案。

## P1 Medical Rescue Area / Prisoner Delivery Area V1

- 荒村正式增加 `base:workarea_huangcun_medical`（`base:loc_ref_huangcun_medical`，tags `medical`／`recovery`）与 `base:workarea_huangcun_prison`（`base:loc_ref_huangcun_prison`，容量 4）。两者均为 source blueprint 与 baked surface 的非阻挡可达区域。医疗点不是住房；专用囚室以最小 `home`／`residentTags` 兼容复用 Residence，New Game 固定 `PrisonerOnly`，Restore 使用既有 Snapshot residence usage。
- Rescue 是所有正常可工作的 PlayerFaction 凡人的基础任务，不看 Profession。V1 只选择同 PlayerFaction、同 Site 的 `Incapacitated` Character；荒村三个 `recoverySpot` 均为正式目的地，其中 `recovery_spot_1/2` 绑定医疗区并优先，`recovery_spot_3` 位于区外且在前两点占用／预订后继续可用。救援者经 MovementIntent／Host／WalkGrid 实际接近、搬起、移动到点位中心并放下；每点一人，送达后只清除 carry，不恢复 HP／Lifecycle。
- 当前没有 Medic 自动治疗、远程回血、MedicalWard admission、出院或医疗 AI。医疗点正式 UI 由恢复点 authority 显示区内“恢复点：2”及已放置伤员；普通位于医疗区但未在正式恢复点的人物不算送达。
- CapturedEscorted 的第一版 carrier 是发起捕获的玩家主控 Character。`base:site_huangcun:prison_dropoff` 位于囚室南侧内部入口，且不与医疗区重叠；右键菜单专门分流为“安置俘虏／查看囚室”，绝不进入人物对话。选择安置后主控真实精确寻路，到达合法交互距离再重新校验并调用 `Detain`。Core `Detain/CanDetain` 共用 carrier、绑定、玩家控制 Site、PrisonerOnly 容量及 carrier／captive 对交接点的距离校验，成功后才转为 Detained，并在囚室合法位置解除押送。
- Rescue 的正式世界目标携带稳定 `RecoverySpotId` 与 Core delivery radius。Host 对这类目标不使用通用 2.5 presentation-unit 到达半径或目标 snap，而是把同一 world-space radius 换算到 presentation space，并要求 exact goal cell；若 A* 终点不可走或路径完成后仍在交付半径外，显式返回 path failure。恢复点身份变化会增加 MovementIntent revision、取消旧路径并重建 A*，不再被旧 `0.2` 世界单位坐标容差吞掉。
- carrier 死亡、移除、进入不兼容 Encounter 或运输取消时，Rescue／Prison transport 均在 carrier 当前合法位置安全放下目标并清除 carry binding；不会丢人物、保持隐形 carried 状态或远程完成送达。Snapshot 保持 v14；短期 Rescue transport 读档重新求值，正式恢复点送达关系、CapturedEscorted／Detained／Residence usage 持久保存。
- LevelTester 保留“定位荒村医疗点”“定位荒村囚室”及对应只读诊断。2026-10-13 已删除医疗搬运、囚犯押送、建设、四类食宿和主管生命恢复的一键准备入口及其专用逻辑；正式投送、治疗、拘留、建设与需求玩法均未修改。
- Core／Data／Unity／Editor 离线编译与 source/baked parity、地图连通、meta/GUID、`git diff --check` 进行静态复核；未启动 Unity、PlayMode 或 Runner，未运行自动测试。状态保持 **Implementation Complete / Producer Acceptance Pending**。

本轮两处设施均为 BaseGame 预置内容；未来玩家自行建造医院／监狱、治疗系统、狱卒 AI 与自动押运不在本 P1 范围。

## P1 Character Approach / Need Movement Realization

- Talk、Trade、Auction、Attack、Recruit、Capture、Release、Execute 共用人物接近意图。合作交互可让目标等待；Attack 与逃亡者 Capture 使用 pursuit，目标继续行动，主控按目标位移阈值和冷却重算路径，到达正式范围后才重新校验并触发命令。
- Recruit 到达后进行约 5 个非缩放现实秒的轻交互；Manual Pause 暂停计时，Modal、玩家移动、参战、目标状态或空间变化会取消。Capture 只在接近完成后设置 capture request，再创建 CharacterEncounter。
- Flee 保存来源 Site 和确定性出口序号，经 `MovementIntent → HostNpcScheduleMover → HostMoveController → CompositeWalkGrid` 实际移动。路径失败轮换八个出口；只有真实位置越过来源 Site 范围与 3 个导航格 buffer 才成为 Displaced。
- StorageRoom 从 footprint 派生八个访问槽。人物按 EntityId 分散起始槽，Host A* 失败后清除缓存并轮换，不再永久重试纯几何最近侧；返家路径明确失败时转为 Ground Sleep。
- 有效玩家世界命令可以解除 ManualPaused；ModalHardPaused 仍阻止命令。第一发致命伤只允许 Alive → Incapacitated；Dead 只能由 FinishingStrike、BleedOut、PrisonerExecution 或 Debug 等显式 death confirmation reason 产生。CharacterEncounter 的生命倒计时使用 `Time.unscaledDeltaTime`，不随 20x 战术速度缩短。
- 荒村巡卫住房南墙 `place_wall_040130496` 从 4 格缩短为 3 格，恢复真实门洞；`place_wall_040134006` 保持 4 格。按正式 Surface geography、site placements 和 blocker center-raster 规则的离线连通检查确认：9 名 acceptance mortal 起点均可到 8 个储藏室槽，普通/巡卫/主管住房可出入，住房可到粮田、药田、伐木区，Site interior 可到 8 个逃亡出口。

## P1 Explicit Site Garrison / Displaced Background Realization

- Site Garrison 是 `NpcSquadDefinition.defendedSiteId → SquadState.DefendedSiteId` 的显式单一绑定；`assemblySiteId` 只表示集结／出生位置。WorldSite assault 只收集 `DefendedSiteId == target.SiteId` 且继续满足阵营、生命、空间和去重条件的 Squad，不再把恰好站在 Site 内的同阵营巡逻队、商队或普通 NPC Squad 推断为守军。
- 荒村的 `base:npc_squad_huangcun_labor_garrison` 仅含杂役主管，并显式守卫 `base:site_huangcun`；巡卫甲／乙／丙属于独立 `base:npc_squad_huangcun_patrol`，没有 defense binding。普通攻击任一巡卫仍由 CharacterEncounter 收入目标当前 Squad 的三人 roster；攻击 Site Core 只自动收入主管驻军。
- Displaced 不再获得 Site PublicStock 特权，只有人物当前 FactionId 与当前 Site OwnerFactionId 相同才可用粮。近场 materialized 人物保持真实、可互动并只做 idle／基础 Needs；离屏后按 bucket 批量推进 Needs，并复用 BackgroundCharacterTravel 前往同 Surface 最近可达的己方 active Site。旅行过程中即使重新 materialize，background route 仍持有移动 authority直到到达或显式取消；没有己方 Site 时保留原位置 background idle。
