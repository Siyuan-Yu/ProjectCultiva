# CW-U1 连接补齐至 CW-U4 连续实施记录

日期：2026-09-13。基线：`4277654e85f9854ad03800d1d36e88e8750e733f`，开始时工作区与暂存区均干净。

## 当前连续实施（制作人补充范围输入之后，2026-09-14 收口）

**Implementation Completed / Combined Producer Acceptance Pending**。这里的完成指默认调用链实现与非 Unity 编译/静态核查完成，不代表 Unity 运行或制作人人工验收通过。

### U2–U4 联合验收修复（2026-09-14）

本轮重接（起始HEAD e6fbdc2）：应用既有修复后继续收口共享绘制、报告输入owner、恢复重试/取消、未激活staging、准备期间位置/小队修订校验及按受影响chunk更新破坏导航。后续增补覆盖下方早期“动态刷新重新准备全场”的描述：现在破墙仅定位并修补相关chunk，完整独立导航仅入场/恢复各构建一次；返回普通邻域另有一次小范围合成，不会第三次合成全场。

最短人工路径（未运行）：普通人物右键攻击→统一接战预览→手动战斗→读取/合成/构建计数→窗口关闭→按正常继续按钮解除原ManualPaused后移动/攻击→破墙通行→结束条→完整战报→继续→普通世界第二场。补查构建取消后无迟到入场、战中读档恢复及恢复失败重试、报告期间其他面板不能解除输入锁。500×500保持世界单位；当前Main Surface仍为646个1.4×1.4 chunks，source50×50、cellSize=.028，输出矩形1900×850=1615000格，每chunk一个草地背景renderer（646个，不含有身份物件）。运行时日志记录实际内容来源，不把这些静态量级当作帧率或耗时验证。

后续制作人截图反馈仍停在构建界面。对照 Scripts.zip 确认普通草地仍每 chunk 创建625个prefab，此前的合并说明只适用于 geography；本次改为Continuous每chunk一个背景renderer，并增加真实构建计数、约4ms预算、嵌套协程取消/异常收口及Active隐藏确认窗口。具体证据与检查见42-devlog最新“人物遭遇构建界面停留修复”；运行验收仍未通过声明。

- 展示入口统一为 `HostStrategicInterruptPresenter`：人物遭遇复用羊皮纸接战、结束条和正式战报；`HostCharacterEncounter.OnGUI` 的重复确认／结束／简表报告已移除。请求预览只读，不在 Layout/Repaint 分配 EncounterId 或写 Participants。
- 单次入场只产生一个 `PreparedIndependentField`。它保存绑定 World、来源 Surface、冻结状态、相交 chunks、输入数、合成 grid 与拓扑修订。`Prepare` 与 grid/边界裁剪均以协程预算推进；表现也先在 staging owner 下逐 chunk 构建。所有 staging 成功后，才调用 `CharacterEncounterService.Begin` 并短暂原子接管；失败会清 staging，接管期异常还会 AbortEntry 和恢复普通 surface。
- `WalkGridComposer.Job` 保留全部输入、格点对齐和保守 overlap 语义，复杂度从 output×inputs 改为 output + sum(inputs)。独立场动态破坏不再因 `_independentFieldId` 直接跳过：会合并为一次同样的分帧纯数据刷新，旧 grid 在结果有效前继续使用。
- 返回不重复 Compose；Restore 走同一分帧 Prepare/Commit 机制。入场确认／准备／报告由 `EncounterModalLock` 合成输入禁用，避免 `HostQuestJournal` 的普通 bool 写入解除；成功入场仅释放本请求 modal，保留领域 WorldTick 冻结，并不强制修改 `ManualPaused`。
- 实际 Content 复核：`base:surface_main_wilderness_v1` 有 646 chunks，chunk 1.4×1.4 world units，cellSize 0.028，source layout 50×50，mapper 为 35.714 presentation units/world unit，完整内容边界约 53.2×23.8 world units。故 500×500 逻辑范围覆盖当前完整 Main Surface；无正式 Surface 的大陆外仍为不可走空域。地理背景构建沿用每 chunk 同色 run 合并，未改写为逐 cell 独立对象。
- 静态核查：现成 `tools/offline-compile.ps1` 成功（Core459/Data78/Unity145，保留既有 warning），`git diff --check` 成功。未运行 Unity、EditMode/PlayMode/Test Runner、Bake 或 batchmode。

此前范围 ACR 已解除：一级议政厅/势力旗统一 500×500 世界单位，中心为核心真实位置；野外独立配置同为 500×500。旧 4.2×2.8 不再作为控制范围来源。下方早期未完成矩阵保留为历史检查点，不代表本段之后的新实现状态。

| 阶段 | 当前实施证据 | 验证 | 提交 |
|---|---|---|---|
| C1/U2A | 普通人物 HostNpcContextMenu→HostCharacterEncounter.Request/BeginConfirmed→CharacterEncounterService；独立场地使用场次 owner 实例、完整正式 source chunks 和独立导航；实际 Character/个人原锚点、战术坐标、时间、HP基线、名单版本和状态进入 characterEncounter JSON；稳定 Active/ReadyToEnd 恢复重建场地；CommitAndReturn 唯一提交并逐人回位、报告继续释放 | Core459/Data78/Host145 离线编译通过；静态核对，不代表人工验收 | `ccdadeb` |
| C2/U2B | 人物分类/到达/近战/最终伤害门禁→HostCharacterEncounter；WorldMap 菜单与执行及 PlayerParty 命令退役，玩家 Army 旧档攻击回调取消；战中名单沿用 C1 JSON，报告后释放 | Core459/Data78/Host145 离线编译通过 | `a8b322a` |
| C3/U3 | Prepare 固定候选→Advance/DecideCandidate→资格与场地预检→名单/报告事务→PresentJoinedParticipants；JSON 恢复固定池/roll/期限/版本；最终关闭余下候选 | Core459/Data78/Host145 离线编译与静态检查 | `2f48380` |
| C4/U4 | 普通首战→唯一报告→逐人返回→继续→分流保存→正式恢复/场地重建→新场次入口已接线；旧回调隔离、重复接触抑制、核心元数据、技能冷却和普通跟随 ownership 收口 | Core459/Data78/Host145 离线编译及少量静态检查通过；Unity 运行待人工验收 | `5afa318` |

C1 Content：worldSpatialRules→loader校验→registry→SpatialRules→CoreLevelControlRange→预设核心/ConstructionCatalog/WorldSiteCoreCoverageResolver/遭遇冻结范围；同等级共用规则。新 runtime Site 存 CoreLevelFormat=1、等级、身份与位置，范围由 Content 派生；旧缺等级只在 format=0 迁一级。500 世界单位保持逻辑边界，定义大陆之外是无 Surface 的不可行走空间，不生成随机地形补齐。建筑占地/碰撞继续原数据。

C1 生命周期：旧 Continuous marker 仅作为现有显示/伤害权限 API 的派生投影，新场地身份、导航和保存由 CharacterEncounter 权威持有；不调用旧 Army 场地预检/共同 BattleHex 提交。WorldTick 冻结期间仅参战者的弥留/尸体期限消费本场秒数，普通世界不推进。所有真实伤害继续经过现有 MeleeCombatService。

## C4 交付核对与制作人人工验收路线

- 核心配置：`Content/BaseGame/Data/Worlds/world_spatial_rules.json` 唯一 Level 1 500×500；野外另设500×500。loader/validation/registry→RuntimeContentShell→ConstructionCatalog/WorldSiteCoreCoverageResolver/管理查询/CharacterEncounter。预设核心元数据随 WorldSiteOwners 保存，玩家旗随 RuntimeWorldSites 保存；二者从等级配置派生尺寸。
- 新入口：HostNpcContextMenu、HostCombatSkillBar、到达攻击、HostNpcMeleeAssault 和最终 MeleeCombatService gate；真实两 Squad 初始参战。旧 AI Hex 触发若涉及实际玩家，只能经个人接触请求新入口，不创建旧远程 Offer；远处非玩家 AI 继续原服务。
- 实际运行权威：CharacterEncounterState + 独立 owner 场地/独立合成导航，HostCharacterEncounter 驱动各人物目标、普通攻击和本场时间；普通跟随、旅行、日程不能改写本场位置。技能冷却同属本场存档。
- 保存与恢复：HostSnapshotLocalPlacementCaptureSync 分流；SnapshotService/CharacterEncounterJson 校验边界、身份、原锚点、战术位置、目标/冷却、HP 基线、候选池/roll/时点/状态及 rosterVersion。HostSnapshotSessionRehydration 校验来源，RebuildAfterWorldRestore 重建同源场地；不重扫候选或重抽。准备/报告提交阶段明确拒绝保存，稳定 Active/ReadyToEnd 可存。
- 清理：CommitAndReturn 校验 settlement identity，唯一报告提交，保留损耗并逐人返回；优先原点，阻挡时只尝试相邻最近合法格，找不到则明确失败，不改为出生点。报告继续释放自己暂停，抑制同次接触但允许新明确攻击。旧正常地面重建后下一次攻击生成新场次。

制作人统一验收（本轮未执行）：

1. 普通连续世界攻击巡逻卫甲/乙：初始仅双方当前小队；驻镇卫甲/乙只能进入固定候选，未加入时不显示/不受伤/不计战报。观察实体 ID 与真实原锚点不被共用 Army 锚点覆盖。
2. 一级议政厅与新一级势力旗分别核对中心 ±250，建筑本体碰撞仍原尺寸；Site 失效后野外范围以实际接战点为中心。既有破墙、桥门与旗状态随正式来源呈现。
3. 普攻换目标、主动技能、同伴自动行动、单 Active 接替、全队失能后的结束按钮；改变 ManualPaused 后确认报告开关不覆盖该值。
4. 战中保存/恢复一次；候选预告期再保存/恢复，确认 roll/期限不重置、加入不重行、原参战 HP 基线不重采。可结束状态保存/恢复后唯一结束/战报。
5. 结束→继续→普通保存/读档→第二次明确攻击：确认来源、范围、名单正确，无旧场地/View/攻击回调残留，不恢复旧远程攻击订单。候选开发观察/受限决定入口位于现有开发工具“战斗”页。

检查记录：保存槽始终只读，SHA256 `6D60DA9B5851740D29CF7C47162D5B0889E65AE4D5D73875988797A85B835122` 未变。Content 相对 2883cd8 仅改 buildings 两个旧控制尺寸字段并新增 world_spatial_rules；驻镇/巡逻 roster 与部署原样保留。未改 Freeze、ProjectSettings、Packages，未 push，未运行 Unity 或任何自动测试。

边界：逻辑范围不因已定义大陆/Chunk不足而缩小；无正式 Surface 的位置保持不可走的空域，不生成随机补齐地形。旧档没有可信个人来源时仅沿已有明确旧格式迁移，不将 View 缺失或 Army 组织身份作为新存档空间权威。以上需人工检查的视觉/运行行为尚未宣称通过。

## 以下为先前检查点原始记录（历史，不代表当前状态）

制作人确认 CW-U1 当前正常玩法人工验收通过。本轮明确授权连续执行 C0→C4，中间不等待逐轮人工验收；覆盖 ADR-0035 的旧逐阶段等待要求。新增行为仍待合并人工验收，不扩大 U1 已验收范围。

| 检查点 | 当前状态 | 改动／检查 | 本地提交 | 剩余边界 |
|---|---|---|---|---|
| C0／U1 连接补齐 | 已形成检查点，继续复核接线 | 成员事务、创建边界、派生位置、存档严格校验、共享命令接线；Core 456／Data 77／Host 143 编译通过 | `9db54f1ade253424ed98a34d847c0586f44fac7e` | 旧档无 Squad 字段仅做迁移；独立遭遇尚未实现 |
| C1／U2A | 未完成 | 同源独立战场、个人锚点、战术时钟与稳定态存读档 | 无 | 待实现 |
| C2／U2B | 未完成 | 统一两队入口、多目标战斗与地图攻击退役 | 无 | 待实现 |
| C3／U3 | 未完成 | 有限关系介入及同场名单追加事务 | 无 | 待实现 |
| C4／U4 | 未完成 | 整合与旧档兼容、合并验收路线 | 无 | 待实现 |

## 检查纪律

仅使用现成 `tools/offline-compile.ps1`，分别确认 Core／Data／Unity Host 实际源文件数量与结果。未启动 Unity，未运行自动测试或 Bake。
首轮 C0 编译发现 `WorldAgentPresence.UsesHex` 名称错误；已按实际 `UsesHexPresence` 修正，必须重编成功后才形成阶段检查点。后续程序集当次因旧 Core 产物缺少新增符号失败，不计为通过。

## 下一动作

C1 定向核查已开始，尚未修改独立遭遇运行流程。当前 `ContinuousOutdoorSurfaceRuntime.TryPrepareManualCombatEntry/CommitPreparedManualCombat` 仍消费 loaded neighborhood；`ContinuousManualCombatPresentationState` 仍仅运行时 marker；`PendingEngagementSnapshotRestore.Capture` 在 offer resolved 后退出；`StrategicEncounterResolveService.ResolveAndEnd` 的真实世界战仍按旧共享 BattleHex 收口。下一步必须一起替换场地、战前精确锚点、战术时钟、恢复与结算接线，不能只加 DTO 或把现有 marker 改名宣称完成。

## C0 接线

- `SquadBoard.Register` 完整预验证；离队复用 Transfer 的队长、反向成员、空 Army／stack 清理。Army 解散先验证所有成员及战斗锁；拒绝不先清 ArmyId 或提交位置。正常 Create 最多六人，旧超限仅显式 snapshot import。
- 真实 `EntityStore.CreateCharacter/CreateNpc` 完成时建立 singleton；可见列表刷新不再写入成员权威。正式恢复在候选 World 校验 Squad 全覆盖、唯一身份、队长、命令目标及 Army 映射，控制绑定预检成功才切换 Session.World；新版不回退旧 DTO 推断。
- 共同命令持有 executor kind、目标人物、revision；玩家跟随消费目标，Army 消费既有 WorldMotion 路线并在启动／停止／替换时更新命令。Core／Host 的个人日程使用同一 ownership predicate，取消仅限 Schedule source，释放占位；普通旅行只在编组成功后取消。
- 弥留／尸体保持成员关系；SyncMember、近场 Army presenter、residual predicate 不再让组织身份拖走残留者。全部失能时停止 Army 行程，不解散队伍。已有 precise residual 不重复锚定。
- 开发诊断原 FormalArmyNearField 输出补充 command、revision、共同目标；未新增面板、测试或 Unity 调用。
- 后续静态复核修正：闲置 field Army 的日程 ownership 不能因路线停止而退给 Core；已有精确 residual 的战略 Hex 从角色自身坐标推导；LegacyHex 命令目标从既有 DestinationHex 推导，不能读取仅 Surface route 设置的 PhysicalDestination。修改后 Core 456／Data 77／Host 143 再次编译通过。

## 尚未完成的交付边界

当前只形成 C0 检查点。C1～C4 没有实现完成，不能标整项 `Implementation Completed`，也不能把旧 Continuous 手动战斗视为新的独立遭遇。没有运行制作人人工验收，没有新的 Producer Accepted 声明。

## 本次续接核查与个人位置检查点（2026-09-13）

入口附件要求继续完成 C1→C4，禁止 Unity、自动测试、Bake、push；阶段授权仍有效，不再次等待阶段验收。本次开始分支 `dev_openworld`，HEAD `5e4e446`，包含 `9db54f1`。本地其他分支和 worktree 未发现后续独立遭遇实现；未切换分支。未跟踪 `Assets/Scripts.zip`、`Content.zip` 保留，未加入提交。

### 已核对的实际存档

只读读取 `C:/Users/Pato/AppData/LocalLow/DefaultCompany/XianXia/vs04_slot0.json`，91633 字节，读取前后 SHA-256 均为 `6D60DA9B5851740D29CF7C47162D5B0889E65AE4D5D73875988797A85B835122`。未保存覆盖、删除或转换该文件。

- Character 32 是朔风驻镇卫甲，33 是同队成员；两人的 `characterWorldPresences` 均为 `(18.1865329742432,10.5)`，与驻镇 Army 锚点相同。32 的 `hasEntityLocation=false`、`hasPresentationOverride=false`。
- 巡逻 34/35 均为 `(16.4544830322266,10.5)`，与另一 Army 锚点相同。两队身份未合并，部署未修改。
- 以上证明保存文件含共用派生位置，不证明历史个人落点。没有运行 Unity，不能据此伪称复现或确定截图当时全部运行时根因。

### 本检查点实现及限制

代码及本次核查本地提交：`e4c59ba12c9dda22aa0e847679f8999ce0bd9556`（个人位置修复检查点，非 C1 完成提交）。

- 复用 WorldPresence 的 WorldPosX/Y，增加 `PersonalSurfaceId` 来源元数据；正式 JSON 的 `characterWorldPresences.personalSurfaceId` 读写、坐标显式性校验、Domain 恢复和 Content Surface 引用校验均接线。缺标记仍为旧未限定来源，不能据此宣称历史精确位置；显式但无坐标、无效坐标或不存在 Surface 拒绝恢复。
- `CharacterPersonalSpaceQuery` 以当前 World 实体和同 Surface 的正式个人位置查询，不依赖 View 或 Army 身份。当前过渡入口优先消费它；不同/损坏的显式来源不能用残留 View 回退。**过渡入口仍有旧名单和 loaded 范围限制，不是 U2A 独立空间。**
- 正常 Continuous 保存不再因 ActiveMapLayoutId 为空退出；materialization 收尾、Party 移动同步、NPC 日程及 Army 近场 presenter 同步当前个人位置，全部通过 mapper。Interior 仍走原 occupant 采集；旧 Continuous 战斗不会被作为普通户外覆盖采集。
- FormalArmy Snapshot motion / Finalize 保留已恢复的个人坐标；AtWorldSite motion 使用已保存 WorldX/Y，不重新计算 Site.AnchorHex。闲置及已 materialize 的个人明确位置不再由旧群体 SyncMember 覆盖。**离屏移动仍属既有 Army 执行适配，派生写入会清除个人来源标记；没有宣称完成离屏成员独立导航。**
- 重建时个人 Site / Party / 闲置 Army 优先消费明确来源位置；Surface 退出清理使用原绑定 World，而非 Snapshot 替换后的 Session.World，避免旧人口集合清除新档 override。新增来源不是另一份 Character 或另一套坐标。
- 现有一次操作诊断汇总增加 CharacterId、SquadId、LegacyArmy、Source、Surface、WorldPosition、materialized/override、IncludedReason。保存前和表现重建后接入，Domain / Finalize 沿用既有调用。不新增面板或逐帧诊断。
- 最终现成离线编译实际处理 Core **457** / Data **77** / Host **143** 源文件，三个程序集均成功，保留既有 warning。`git diff --check` 通过。未启动 Unity，未运行测试或 Bake。

### ACR：预设 Site 缺失可消费的正式核心控制范围

这是尚未确认的必要规则输入，不是缺少阶段开工授权。

1. `ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata` 为预设议政厅绑定 CoreAssetId、SurfaceId、真实中心、Level=1，但**没有**给 CoreRangeWidth / CoreRangeHeight 赋值。
2. `WorldSiteCoreCoverageResolver.Contains` 必须通过 `HasContinuousCore`，而后者要求两个尺寸均大于 0。因此当前预设 Site 没有这条正式物理控制范围；新旗站才有完整尺寸。
3. Content 的 `Buildings/buildings.json` 对新旗站配置 `siteRangeWidth=4.2`、`siteRangeHeight=2.8`。仓库没有确认这些值也适用于所有预设城镇；擅自套用将改变预设 Site 控制定义，另按旧 Hex 推导则违反本次范围要求。
4. 已向制作人询问：预设 Site 是否统一消费该一级核心配置，或提供逐 Site 范围。等待此必要输入时，先完成上述独立位置检查点。没有把预设城镇当野外绕过，也没有提前开展 CW-04 控制算法。

### C1—C4 实际进度矩阵

| 阶段 | 实际默认入口 | 核心状态/服务 | Content | 保存恢复 | 结束清理 | 编译结果 | 提交状态 |
|---|---|---|---|---|---|---|---|
| C1/U2A 未完成 | HostNpcContextMenu 普通人物仍 LocalAttackConfirm | 当前仅个人位置查询/同步修复，独立 Encounter 未实现 | 预设 Site 核心范围缺输入；野外遭遇配置未接 | 个人来源闭环部分补齐；正式战中会话未实现 | 修复旧 Surface 清理新 World；个人战后返回未实现 | 457/77/143 成功，仅证明检查点 | 本节对应位置修复检查点，非 C1 完成提交 |
| C2/U2B 未完成 | Army 专属路由、GatherAndLock Hex 支援仍存在 | 多目标与统一入口未替换 | 两支原有二人队保留 | 地图攻击追击兼容未退役 | 未实现新多对多清理 | 无阶段编译声明 | 无 |
| C3/U3 未完成 | 无正常候选介入入口 | 候选冻结/加入事务/名单版本未实现 | 介入配置与消费者未实现 | 候选与随机判定未实现 | 最多一次续战/关闭候选未实现 | 无 | 无 |
| C4/U4 未完成 | 未切换新默认流程 | 尚不能宣称统一 Encounter 权威 | 未完成整合 | 连续两场+战中恢复待实现并待人工验收 | 旧路径未退役 | 无 | 无 |

本节是可恢复的未完成检查点，**不是 C1/C2/C3/C4 Implementation Completed，也不是 Combined Producer Acceptance Pending 交付**。下一动作是取得预设核心范围输入后继续 C1 独立场地、正常入口、战术时间和会话持久化，再依次 C2→C4；既有连续授权不变。
