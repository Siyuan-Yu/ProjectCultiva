# SUCCESSION-01 — Player Faction Succession & Control Re-anchor V1

> 状态：**Implementation Complete / Producer Acceptance Pending**
> 日期：2026-09-24
> Snapshot：v8（未升版）
> 前置封板：QUEST-INSTANCE-01 **Producer Accepted / Sealed**

## 1. Authority 与触发边界

`PlayerFactionSuccessionService` 是势力继承唯一 Core authority。只有 `PlayerPartyRuntime.IsAwaitingSuccession == true`，即旧 Party 每名成员均为 Dead/Removed，才会扫描候选。`TemporarilyUnavailable`、弥留、倒地、战斗中暂时无可操作成员均不触发。普通 Party 内仍有合法成员时继续按既有成员顺序替换 Active，不使用战力排序。

CharacterEncounter 的 Preparing／Active／ReadyToEnd、Host pending/preparing presentation 或 continuous manual-combat authority 会延后继承。`HostCharacterEncounter.TryFinishBattle` 先执行 `CommitAndReturn`、return anchors、report 与 participant cleanup，离开 independent field 后才再次触发继承检查。

## 2. 候选、排序与位置捕获

候选必须是存在的 Character/NPC，具有 affiliated `FactionMembershipComponent` 且 FactionId 精确等于 `world.Strategic.PlayerFactionId`；不属于旧 Party；Lifecycle 为 Alive；`CanActAsActive` 通过；不受 encounter spatial authority 持有；`CharacterWorldPresenceQuery` 能给出 finite exact WorldPosition 与非空 SurfaceId，且点位在已注册 Surface authority 内。

排序只使用 `CombatPowerCalculator.ForEntity`：最高者胜出；同战力按较小 EntityId。遍历顺序、名称、View、随机和距旧战场距离均不参与。

在任何 Squad 修改之前捕获 successor 的 EntityId、CombatPower、SurfaceId、WorldPosition、Site context 与来源 SquadId。来源可以是 AtWorldPosition、带 exact anchor 的 AtWorldSite 或 SquadWorldMotion。

## 3. Squad 与精确世界接管

`SquadMembershipService.ReplacePlayerSquadForSuccession` 在完整预检后完成 membership transaction：

- 旧 `squad:player` 全员转为各自 singleton；不改 Entity、Lifecycle、corpse 或 world placement。
- successor 若属于 NPC squad，只移出本人；剩余成员顺序、command、world motion 保持，若 successor 是 leader 则按原成员顺序选新 leader。
- successor 若同时是来源 squad 的 command target，剩余 squad 会把 command target 稳定改为新 leader 并递增 revision，避免留下指向已拆出成员的悬挂命令。
- successor 的 background travel 被取消；空来源 squad 与其 motion 被回收。
- 重建只含 successor 一人的 `squad:player`，Leader/Active 均为 successor，Command 为 FollowLeader，并复用 `TryBindControlledSquad` 与 `ReconcilePlayerPartyAuthority`。

`PlayerPartyWorldMotion.SetAtSurfacePosition` 使用继承前捕获的 exact position，清除旧 Party route/destination/execution；Site context、TravelingMembers、successor WorldPresence 与 PartyWorld 随后同步。不会使用旧战场、旧 Party position、Site center/arrival 或 opening spawn。

## 4. Separate Space 与 Host re-anchor

Separate Space wipe 会先由 Host 冻结当前持久角色的 EntityLocation presentation placement，再由 `ReleasePlayerControlAfterPartyWipe` 只释放 active PlayerParty session/occupants。它不调用正常 Leave、不撤离尸体、不改尸体位置、不删除洞府持久状态。

Host 在 succession domain handoff 后检查 successor 是否处于当前 loaded neighborhood。Surface 改变或同 Surface 但超出 loaded 5×5 时，调用不捕获旧实体位置的 succession deactivate，再从新 PlayerParty motion 激活 Surface；若已经在 loaded neighborhood，只同步新 Party presentation。完成物化后才复用 `ApplyAutomaticActiveChange` 镜头与选中逻辑。

成功只发布一次轻量 `PlayerSuccessionResolved`，Host 以 transient toast 显示“人物名 接替了玩家势力主控”。无候选不发事件、不写死 GameOver、不复活人物，继续保持 AwaitingSuccession；HUD 明确显示暂无可接管人物，可存档或等待条件变化。

无候选重试在首次进入 AwaitingSuccession、Snapshot world shell 恢复和显式 Host 生命周期刷新时立即执行；普通 Update 无候选后最多每 0.75 秒扫描一次，不再逐帧遍历全世界，也不写 log/toast spam。

## 5. Snapshot v8

成功继承由既有 ControlledSquadId、Squads、PlayerParty runtime、ActiveCharacterId、PlayerPartyWorldMotion 与 WorldPresence 完整表达，无新增字段。成功态 Save/Load 不重新选择。无候选的 wiped Party 恢复后先重建 Content/Squad/Surface world shell，再重试继承，避免过早依赖尚未注册的 Surface。

## 6. LevelTester 验收工具

反引号打开 LevelTester →“战斗”：

1. 点击“SUCCESSION-01：准备 A/B 继承候选”。工具使用现有玩家势力非 Party 人物，A 设为 QiRefining、B 设为 Foundation；显示两者 EntityId、Power、Surface 与 exact position。B 被放到远端合法 chunk，并尽可能与一名非候选护卫组成 `squad:acceptance:succession-b`，用于验证只拆 successor、护卫不入新 Party。
2. 点击“SUCCESSION-01：使当前 Party 正式全灭”。工具逐人调用 `TryEnterIncapacitated` → `TryConfirmDeath`，不直写 Lifecycle private state，并立即触发正常继承边界。

该工具只改当前 LevelTester runtime 验收态，不修改正式 Content balance。

## 7. 制作人人工验收 CASE 1～6

### CASE 1 — 普通 Party 内接替不回归

让 Active A 进入弥留、Follower B 保持存活。确认 B 按 Party 顺序成为 Active；不扫描势力候选，不按战力改选。

### CASE 2 — 全灭后的最强势力继承

重置会话，依次点击“准备 A/B”与“正式全灭”。状态区确认 B Power 高于 A。预期 B 在自身 exact position 接管；旧 Party 仍死亡；新 Party 仅 B；若状态显示 B acceptance squad，其护卫继续留在原 squad；镜头到 B，WASD 可用。

### CASE 3 — Same Surface 远距离 re-anchor

使用 CASE 2 默认 fixture；B 位于同 Surface 的远端合法 chunk。确认继承后的 current/loaded neighborhood 以 B 所在 chunk 为中心，B EntityView 可见，无“Domain 有人但 View 不存在”。

### CASE 4 — Save / Load

CASE 2 成功后保存并读取。确认同一个 B 仍是唯一 Party member/Active，exact Surface position 不变，未再选择 A，移动与地图正常。

### CASE 5 — 无继承者

不要准备 A/B，或先使所有玩家势力非 Party 人物 Dead/Removed/失去 exact position，再正式全灭 Party。确认 Active=None、HUD 显示“暂无可接管人物”、可 Save、无 GameOver/复活/日志或 toast spam。

### CASE 6 — Separate Space wipe

在洞府内使当前 Party 正式全灭，同时外界保留合法 successor。确认 successor 在自己的 Surface 接管而非被拉进洞府；旧尸体仍保留洞内 EntityLocation/placement，未送回洞口；退出 active cave presentation 后 Continuous Surface 正常重建，重新进入时洞府持久状态仍在。

## 8. 验证与边界

- `tools/offline-compile.ps1`：全程序集 `ALL_OK`。
- `git diff --check`：通过。
- 仓库 `AGENTS.md` 禁止 agent 编写或运行任何自动测试，因此本轮没有新增/运行 Core、Snapshot、EditMode、PlayMode 或 headless tests；测试程序集仅参与离线编译。
- 未启动 Unity；未修改 Snapshot schema、正式 Content balance、普通 Party replacement、Quest/Event/Opportunity runtime 或 NPC AI。
- 未 `git add`、commit、push。

2026-09-24 Final Audit：候选门槛、CombatPower＋EntityId 排序、精确位置预捕获、玩家 Squad 单人重建、远 Surface／远 5×5 无捕获重锚、物化后 Camera/Selection、Separate Space 尸体保留与 Snapshot v8 恢复链均符合当前规则。仅做来源 Squad command target 悬挂修复、无候选 retry throttle，以及 LevelTester 护卫限定为玩家势力的小型 correctness hardening。状态仍为 **Implementation Complete / Producer Acceptance Pending**。

SUCCESSION-01 人工验收通过后的下一阶段计划为 **DYNAMIC-DISCOVERY-01 — Dynamic WorldObject + Discovery Foundation**；其后仍为 Knowledge + delayed event foundation → Full trading → Equipment/crafting → Production/logistics → NPC AI last。本轮未开始实现下一阶段。
