# Phase 5S-B2-3.4/3.5：PlayerParty Battle Initiator V1 + Remote Attack / Pursuit Parity（2026-09-02）

> 状态：**已实现、已验收、已提交并推送 origin/dev（commit `3824178`）**｜优先级：P0｜最后更新：2026-09-02
> 上级：`docs/20-systems/README.md`（2A / 2K）、`docs/40-process/186-phase-5s-final-architecture-closure-2026-09-01.md`（Phase 5S 权威收口）
> 关联：`docs/40-process/185-phase-5s-b2-3-...`（WORLD_COMBAT 原地结束与人口迁移）、`docs/20-systems/2K-...`（RPG-First 真源）
> 覆盖范围：PlayerParty 作为独立战略军事主体主动攻击 Enemy FormalArmy 的两轮产品补全 —— ① Battle Initiator V1（WorldMap 主动攻击 + DirectInitiator）；② Remote Attack / Pursuit Parity（远距离右键 Attack → 自动追击 → 进入 SupportArea 接战）。

---

## 0. 一句话总结

PlayerParty 不需要组成 FormalArmy，即可在 WorldMap 上主动攻击一支 living Enemy FormalArmy：进入 Defender SupportArea 内右键 → 立即 PendingEngagement；距离远时右键 → 发出 Attack 命令 → `PlayerPartyHexPursuitService` 自动追击（每 tick 以 target 当前战略位置为真源 retarget）→ 一旦 committed Hex 进入 Defender SupportArea → 由**同一** `TryBuildOfferForPlayerPartyAttack` 建立 BattleOffer，完整复用已验收的 WORLD_COMBAT 主链（Manual / Auto / Retreat / Residual）。

核心不变式：**SupportArea = Engagement trigger，不是 Attack menu visibility gate**；PlayerParty 与 FormalArmy 都是战略 Attack Actor，远距离右键都出现「攻击军队」，行为差分只存在于接战时机（立即 vs 先追）。

---

## 1. 背景与问题链

| # | 现象 | 根因（确认） |
|---|---|---|
| 1 | PlayerParty 选中后右键 Enemy Army 无「攻击军队」菜单 | 上一轮 Initiator V1 的 `CanAttackArmyNow` 把 SupportArea 距离当作菜单 gate；FormalArmy 可在任意距离右键 Attack（自动追击）→ 行为差分错误 |
| 2 | 远距离想攻击必须先手动走到 SupportArea | 无 PlayerParty pursuit；旧 `StrategicPursuitService.SyncSoloPursuerToStack` 在 Pure Hex 下已删除 solo agent macro pursuit，不能复活 |
| 3 | 若简单放开菜单而不做追击 | 点击 Attack 在远距离时无路可走 → 命令失效，体验断裂 |

---

## 2. 已确认的代码事实（本轮依据）

- **V1 基础已存在**（上一轮，commit `3824178` 前半）：`BattleInitiatorKind.PlayerParty` / `BattleDecisionSubjectKind.PlayerParty`、`BattleRetreatService` 的 PlayerParty case、`PreEngagementLegalLocation.CapturePlayerParty`、`BattleEngagementSpatialQuery.TryGetCommittedPartyHex`（PlayerPartyWorldMotion 权威；WorldSite 内 footprint Hex 从 WorldPosition 即时派生）、`BattleParticipantGatheringService` 的 Support Player 收集。
- **FormalArmy pursuit 模型**（不可改动）：`ArmyHexPursuitService.BeginAttackArmy/CancelPursuitForAttacker/AfterTravelTick` + `ArmyPursuitTargetService`；`FormalArmyWorldMotion.OrderTargetArmyId` 为 target 真源；`Encounter.PursueAttackerArmyId/PursueStackId` 承载 Army pursuit 状态。
- **PlayerParty 战略移动权威** = `PlayerPartyWorldMotion`（不是每个 Character WorldPresence 独立移动）→ pursuit 必须是「PlayerPartyWorldMotion ↔ target FormalArmy」的 movement adapter，不能复活 Character-per-agent 旧 pursuit。
- **Strategy Travel 顺序**（`StrategicTravelDriver.AfterTravelTick`）：`ArmyHexTravelService.AdvanceAll` → `PlayerPartyHexTravelService.AdvanceAll` → … → `ArmyHexPursuitService.AfterTravelTick` → `ArmyHexLingeringArrivalService.AfterTravelTick`。pursuit tick 应放在 Army/PlayerParty travel 均已 Advance 之后。
- **Core 无 PlayerPartyRuntime 引用**：`PlayerPartyRuntime` 由 Host `PlayableHostSession` 持有 → PlayerParty pursuit tick 由 Host `StepTick` 驱动（与 `LoadedStrategicPopulationMaterializer` reconcile 同一模式），`StrategicTravelDriver` 只加注释说明。
- **PlayerParty travel Save→Load 语义**：`StrategicSnapshotHelper.RestorePlayerPartyTravel` 恢复 world position 后 Movement 恢复 Idle（DTO 注释明确）→ pursuit target 不需跨 Save 恢复，Load 后清空（与普通 travel 同契约，不单独引入更强 persistence）。

---

## 3. 架构决策（不可违反）

1. **SupportArea = Engagement trigger**，不是菜单 gate。PlayerParty 选中 + 任意距离右键合法 living Enemy FormalArmy → 必须出现「攻击军队·XXX」（与 FormalArmy 一致）；「请先走到支援范围」不作为菜单 gate 文案。
2. **命令资格拆两概念**：`CanIssueAttackOrder`（目标/派系/战争/阻塞 gate，**不检查距离**）+ `CanEngageArmyNow`（= CanIssueAttackOrder + `CanTriggerPlayerPartyEngagement`）。Host 只发「Attack Enemy Army」，立即接战 or 先追击由 Core 决定。
3. **pursuit target 真源 = targetArmyId（`PlayerPartyWorldMotion.AttackOrderTargetArmyId`）**：target 当前 committed Hex 每 tick 从 `FormalArmy.WorldMotion` 解析，绝不记忆点击时的旧 Hex；该字段只是 strategic order metadata，**不是第二份 position authority**。禁止把 `PlayerPartyWorldMotion` 重构成 `FormalArmyWorldMotion`。
4. **pursuit 是薄 movement adapter**（`PlayerPartyHexPursuitService`）：只做 `PlayerPartyWorldMotion ↔ target FormalArmy` 的移动；Battle trigger / Offer / participant gathering / Manual / Auto / Retreat / Residual 全部继续共享既有 WORLD_COMBAT 主链。禁止新增第二套 Battle 系统（PlayerPursuitBattleOffer 等）。禁止复活旧 `StrategicPursuitService` solo pursuit。
5. **进入 SupportArea 即接战，不要求走到 target exact Hex**：contact 判断始终 `CanTriggerPlayerPartyEngagement`（PlayerParty committed Hex ∈ Defender SupportArea），保持既有 BattleArea / reinforcement radius 模型。
6. **普通 Move 命令覆盖 Attack pursuit**：Host 普通旅行 dispatch（`TryExecutePlayerPartyTravel` / `ExecuteGatewayConfirmTravel`）接受新命令前先 `CancelPursuit`；pursuit 内部 retarget 用专用 `BeginPursuitTravelLeg`（不清自身 intent）。「先 validate 后覆盖」：非法 target / 友军 / WarGate 不允许 → 不 CancelTravel、不中断当前旅行。
7. **Initial no-route / 追击中途无路**：保留 canonical position，清除 pursuit order，不留半个 TargetArmyId、不 teleport / snap。
8. **已验收的 PlayerParty Initiator / Battle 逻辑不动**：`TryBeginPlayerPartyEngagement`、DirectInitiator gathering、`AttackerArmyId=""`、`DecisionSubjectKind.PlayerParty`、Manual/Auto WORLD_COMBAT、BattleHex commit、Retreat、Residual 全部原样；pursuit 只是在正确时机调用同一个 `TryBuildOfferForPlayerPartyAttack`。
9. **FormalArmy pursuit 现状不改坏**：`ArmyHexPursuitService` / `ArmyPursuitTargetService` / `FormalArmyWorldMotion.OrderTargetArmyId` 保持。两 Domain Authority 不强行合成一个大 generic motion。
10. **Save→Load 不强于普通 travel**：Load 后 Movement 恢复 Idle、pursuit target 清空；pursuit 不引入单独 persistence。

---

## 4. 实现清单

### 4.1 PlayerParty Battle Initiator V1（上一轮，3824178 前半）

- **Trigger**（`BattleEngagementTriggerService`）：抽共享 `CanTriggerFromCommittedHex(world, initiatorHex, defenderArmyId, out reason)`（= `BattleEngagementSupportArea.ResolveAndFreeze` 冻结集合 `Contains`）；FormalArmy 旧入口 `CanTriggerEngagement` 与新增 `CanTriggerPlayerPartyEngagement`（用 `TryGetCommittedPartyHex`）都走它。零复制。
- **Engagement Authority**（`BattleEngagementAuthorityService`）：新增 `TryBeginPlayerPartyEngagement(world, party, defenderArmyId, primaryEnemyStack, offerId, out resolvedWithoutPlayerPrompt)`；与 FormalArmy 共用抽出的 `CommitEngagement` 核心（SupportArea freeze / BattleLocation / EngagementId / faction / GatherAndLock / snapshot lifecycle）。字段：`InitiatorKind=PlayerParty`、`InitiatorFormalArmyId=""`、`InitiatorIsPlayerSide=true`、`AttackerFormalArmyId=""`、`DefenderFormalArmyId=enemy FormalArmyId`、`DecisionSubjectKind=PlayerParty`、`DecisionSubjectFormalArmyId=""`、`DecisionSubjectRetreatLocation=CapturePlayerParty`、`PrimaryPlayerFactionId=player faction`、`PrimaryEnemyFactionId=defender faction`、`PlayerPartyIncluded=true`、`PlayerInclusionReason=DirectInitiator`。
- **InitiatorEngagementLocation**（`BattleEngagementHexDistance`）：新增 `ResolvePlayerPartyInitiatorEngagementLocation(world, party)`（Hex = `TryGetCommittedPartyHex`；`PlayerPartyTravel.LocationKind==AtWorldSite` → SiteId 填 travel.SiteId，否则空）。只作 frozen/debug/persistence consistency，不成为 eligibility authority。
- **DirectInitiator gathering**（`BattleParticipantGatheringService`）：`GatherAndLock` 检测 `InitiatorKind==PlayerParty` → `TryGatherDirectPlayerPartyInitiator`（Entity 存在、非 FormalArmy、`IsLivingForMacroOrder`、committed Hex ∈ frozen SupportArea；Followers 同为 living party member 一并 DirectInitiator，不 Optional）；**Active Character 必须成功加入，否则整个 engagement 拒绝**（`ContainsLockedPartyMember(Active)` 校验失败即 Clear，不生成无 initiator 的 PendingEngagement）。Snapshot 走既有 `AddPlayerPartyMandatory` → `BattleParticipantKind.MandatoryFriendly`；`IncludedReason=DirectInitiator`、`FormalArmyId=""`。
- **`AttackerArmyId=""` 下游审计**：`CollectParticipantFormalArmyIds` 的 `AddUnique` 跳过空；`ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle` 的 `TryResolveAttackerArmy` 遇到含 PlayerParty 成员的 mandatory party → false → **no-op（绝不把附近 friendly FormalArmy 写成 AttackerArmyId）**；`ClearAttackOrdersAfterBattle` 空值跳过；`CommitArmyAtExactBattleHex` 只按 snapshot records 收集（PlayerParty 记录 FormalArmyId="" 被跳过）。Friendly support FormalArmy 仍由 `SyncParticipantFormalArmiesAfterBattle` 处理；Enemy primary 正常 `SyncEnemyArmyAfterBattle`。
- **BattleOffer API**（`BattleOfferService`）：新增 `TryBuildOfferForPlayerPartyAttack(world, party, stack, title=null)`（gate → `TryBeginPlayerPartyEngagement` → 复用现有 Offer 创建：LocalMapResolver / BuildSnapshot / power labels / clock freeze / presentation）；抽出 `CompleteOfferAfterEngagement` 共用尾巴，不复制 PlayerBattleOfferService。
- **WorldMap UI**（`HexRightClickResolver` + `HostWorldMapPanel`）：AttackArmy 菜单条件改为「FormalArmy selection OR eligible PlayerParty selection」；PlayerParty 仅 `CanAttackArmyNow==true` 时产生 AttackArmy（远距离不阻断右键旅行）。执行 `ExecuteAttackEnemyArmyFromHex` 按 selection authority 分流：FormalArmy → 既有 `ExecuteAttackStack`；PlayerParty → `PlayerPartyStrategicCombatCommandService.AttackArmyNow`。Host 不自己创建 PendingEngagement / gather / freeze / 写 snapshot。
- **Retreat**：`DecisionSubjectKind=PlayerParty` → `BattleRetreatService` 既有 PlayerParty branch → `ApplyRetreatToPlayerParty` 回 `PreEngagementLegalLocation`；不创建 Army、不动 nearby friendly support Army；`CancelEngagementOrders` 对空 AttackerArmyId 安全跳过。
- **Save/Load**：DTO 已有 `InitiatorKind/InitiatorFormalArmyId/DecisionSubjectKind/DecisionSubjectFormalArmyId/PlayerPartyMemberIds/RetreatIsPlayerParty/InitiatorEngagementLocation/SupportArea/BattleLocation/DefenderFormalArmyId` → **不加新 DTO 字段**；`PendingEngagementSnapshotRestore.Restore` 按 `InitiatorKind==PlayerParty` 恢复 `PlayerInclusionReason=DirectInitiator`（不重新 Gather participants）。

### 4.2 PlayerParty Remote Attack / Pursuit Parity（本轮，3824178 后半）

- **命令 gate 拆分**（`PlayerPartyStrategicCombatCommandService`）：
  - `CanIssueAttackOrder`：Hex 战略激活 + party HasActive + Active 非 FormalArmy + 无 blocking/modal battle + target FormalArmy 存在 + `HasMacroOrderLivingMember` + linked ArmyStack 存在 + faction 不同 + `WarGateService.CanAttack`。**绝对不检查 SupportArea distance**。
  - `CanEngageArmyNow` = CanIssueAttackOrder + `CanTriggerPlayerPartyEngagement`。
  - `CanAttackArmyNow` / `AttackArmyNow` 保留为兼容别名（= CanEngageArmyNow / AttackArmy）。
  - `AttackArmy(world, party, targetArmyId)`：`CanIssueAttackOrder`（先 validate 后覆盖）→ `CanEngageArmyNow` ? `EngageNow`（CancelTravel 保留位置 → `TryBuildOfferForPlayerPartyAttack`）: `PlayerPartyHexPursuitService.BeginAttackArmy`。`TryResolveLinkedStack` 公开供 pursuit 复用。
- **pursuit order metadata**（`PlayerPartyWorldMotion`）：新增 `AttackOrderTargetArmyId` + `SetAttackOrder` / `ClearAttackOrder`；`Clear()` 全局重置时清。`CompleteMove` / `ClearMovementKeepMembers` **不清**（contact 流程 `CancelTravel` 不误清 pursuit intent）。
- **`PlayerPartyHexPursuitService`（新 Core 薄 adapter）**：
  - `BeginAttackArmy`：CanIssueAttackOrder（validate 后才覆盖）→ 防御性检查非 SupportArea → `SetAttackOrder` → `BeginPursuitTravelLeg`（= `PlayerPartyHexTravelService.BeginTravel(world, party, target.CurrentHex)`）；失败（无路径）→ `ClearAttackOrder` 不留半状态。
  - `CancelPursuit`：`ClearAttackOrder` + 若在移动 `CancelTravel`（保留位置）。
  - `AfterTravelTick(world, party)`（Host StepTick 驱动）：LocalVisible 模式跳过（Local 层不推进 World pursuit，路线保留，关图回 World 继续）→ ① 条件校验（CanIssueAttackOrder 失效 / target 消失 → CancelPursuit）→ ② **先检查 contact**（`CanTriggerPlayerPartyEngagement` true → CancelTravel → 建 Offer → 成功清 target / 失败 CancelPursuit）→ ③ 未接触且（target 当前 committed Hex != DestinationHex || Player 已停）→ `BeginPursuitTravelLeg` retarget；失败 → CancelPursuit。
  - `BeginPursuitTravelLeg`：pursuit 内部专用；`BeginTravel` 不触碰 `AttackOrderTargetArmyId` → retarget 不会清掉自己的 pursuit intent。
- **StrategicTravelDriver**：加注释说明 PlayerParty pursuit tick 由 Host `StepTick` 驱动（Core 无 party runtime；Army/PlayerParty travel 均已 Advance 后调用），不另加 Core 循环。
- **Host StepTick**（`PlayableHostBootstrap`）：`TickOnce` 成功后、`PruneHiddenViews` 前调 `PlayerPartyHexPursuitService.AfterTravelTick(_session.World, _session.PlayerParty)`（与 reconcile 同模式）。
- **WorldMap UI 菜单与执行**（`HostWorldMapPanel`）：`TryResolvePlayerPartyAttackEligibility` 改调 `CanIssueAttackOrder`（与距离解耦）→ 右键任意距离合法 target 出现「攻击军队」；`ExecuteAttackEnemyArmyFromHex` PlayerParty 分支调 `AttackArmy`，状态提示按结果（HasBattleOffer / HasPursuit）。`TryExecutePlayerPartyTravel` 与 `ExecuteGatewayConfirmTravel`（两处普通旅行接受新命令处）在 `BeginTravel` 前 `CancelPursuit`。
- **Save/Load**（`StrategicSnapshotHelper.RestorePlayerPartyTravel`）：Load 恢复位置后 `ClearAttackOrder`（与 Movement 恢复 Idle 同契约）。

---

## 5. 修改文件清单

**新增（Core）**
- `Assets/Scripts/Core/World/Strategic/PlayerPartyHexPursuitService.cs`（+190，含 `.meta`）
- `Assets/Scripts/Core/World/Strategic/PlayerPartyStrategicCombatCommandService.cs`（+233，含 `.meta`；上一轮 Initiator V1 所建，本轮重构）

**修改（本轮 Pursuit Parity）**
- `Assets/Scripts/Core/World/Strategic/PlayerPartyWorldMotion.cs`（+19：AttackOrderTargetArmyId）
- `Assets/Scripts/Core/World/Strategic/StrategicTravelDriver.cs`（+5：注释）
- `Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`（+6：StepTick pursuit tick）
- `Assets/Scripts/Unity/Host/HostWorldMapPanel.cs`（+81：菜单资格/执行分流/普通 Move 取消）
- `Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs`（+3：Load 清 target）
- `Assets/Scripts/Core/World/Strategic/HexRightClickResolver.cs`（+9：攻击资格参数）

**修改（上一轮 Initiator V1，同一 commit 3824178）**
- `BattleEngagementTriggerService.cs`（+46）、`BattleEngagementAuthorityService.cs`（+172）、`BattleEngagementHexDistance.cs`（+24）、`BattleParticipantGatheringService.cs`（+50）、`BattleOfferService.cs`（+82）、`HexRightClickResolver.cs`、`HostWorldMapPanel.cs`、`PendingEngagementSnapshotRestore.cs`（+7）、`StrategicSnapshotHelper.cs`

**未改**：FormalArmy travel / ArmyHexPursuitService / ArmyPursuitTargetService / StrategicEncounterSpawner / StrategicEncounterResolveService / LoadedStrategicPopulation* / StrategicResidualPresence* / PlayerPartyHexTravelService 路径算法 / Content JSON。

---

## 6. 验证状态

- Host 全链编译（真实 Unity 2022.3.6f1 dll + Core + Data + 全部 Unity 脚本，强制全量）：**0 错误**（2 个既有无关 warning：HostWorldMapPanel:725 CS0162、HostFormalHud:123 CS0169）。
- `git diff --check`：通过（exit 0）。
- 人工验收（LevelTester / Unity）：**Case A–I 全部通过**（commit message「玩家主控大地图发起战斗没问题」）——远距离出现攻击菜单；点击后自动追击；Enemy 移动自动改道；追击中右键普通格转普通 Move 且不被抢回；攻击覆盖普通 Move；友军/不可攻击不出现菜单不中断旅行；Initiator 字段保持（InitiatorKind=PlayerParty / AttackerArmyId="" / DirectInitiator）；Manual / Auto 无回归；FormalArmy 远程攻击无 regression。
- 未跑 Unity Test Runner / PlayMode / EditMode。

---

## 7. 人工验收清单（Case A–I）

1. **Case A**：PlayerParty 距荒村山匪很多 Hex，选中 PlayerParty 右键山匪所在 Hex → 立即出现「攻击军队·荒村山匪」，不需先进入 SupportArea。
2. **Case B**：点击 Attack → PlayerParty 自动前往敌军；Enemy 不动 → 一进入 SupportArea 立即弹 BattleOffer，不要求走到 Enemy exact Hex。
3. **Case C**：追击过程中 Enemy Army 移动 → PlayerParty 自动改道追新位置，无需再次右键。
4. **Case D**：追击中右键普通 Hex → 立即取消 pursuit 转普通 Move，之后不被 pursuit tick 抢回敌人路线。
5. **Case E**：普通 Move 中右键 Enemy → Attack → 旧 Move 被新 Attack order 正常覆盖。
6. **Case F**：右键友军 / 不可攻击军团 → 不出现 Attack，不中断当前 Travel。
7. **Case G**：追到 SupportArea 后 PendingEngagement 保持 InitiatorKind=PlayerParty / InitiatorFormalArmyId="" / AttackerArmyId="" / DecisionSubjectKind=PlayerParty / IncludedReason=DirectInitiator。
8. **Case H**：随后分别选 Manual / Auto → 保持上一轮已验收行为。
9. **Case I**：选 FormalArmy 远程攻击 → 现有 Army pursuit 完全不 regression。

---

## 8. 未决事项 / 后续（非本轮）

- **WorldSite Attack / Capture**（AttackWorldSite / CaptureObjective / Owner transfer / Siege / Garrison battle）—— 下一刀。
- PlayerParty 对移动敌军的持续追击在 LocalVisible 层：当前 Local 层不推进 World pursuit（路线保留、关图回 World 继续）—— 若需要 Local 层实时追击再独立评估。
- 上一轮 Auto WORLD_COMBAT 修复（BattleOffer 冻结 LocalMapResolutionKind / Auto 前置 commit / bind 分叉 / Confirm 后 LocalMap 准备）已随 186 closure 文档归档（commit `1886c02`「自动战斗也没问题」）。
- Prototype Bandit → Content JSON 迁移（`FormalArmyDefinition` / `OpeningScenarioDefinition.InitialFormalArmyIds` / `FormalArmyContentBootstrap` / `ArmyService.CreateAuthoredArmy`）已随 commit `f94d287` 归档（devlog 2026-08-31）。

---

## 9. 提交记录

- `3824178 玩家主控大地图发起战斗没问题`：PlayerParty Battle Initiator V1 + Remote Attack / Pursuit Parity（16 files, +930/−19），已推送 origin/dev。
- 前置：`1886c02 自动战斗也没问题`（Auto WORLD_COMBAT 修复 + 文档 186）、`f94d287`（残留战场入口退役 + Content 迁移 + devlog）。
- 本页随 devlog 追加一起以中文 commit 提交到 `origin/dev`。
