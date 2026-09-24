# CONTROL-HANDOFF-01 — Emergency Faction Control Transfer

> 状态：**Producer Accepted / Sealed**
> 日期：2026-09-24
> Snapshot：v8（未升版）
> 关联：SUCCESSION-01／CONTROL-HANDOFF-01-P1 均于 2026-09-24 **Producer Accepted / Sealed**

## 1. 统一模型

`PlayerFactionControlHandoffService` 是 External Faction Control Handoff 的单一 Core authority。`PlayerPartyRuntime.NeedsExternalControlHandoff` 在 `TemporarilyUnavailable` 或 `AllMembersDead` 时为 true；`IsAwaitingSuccession` 仍只代表 `AllMembersDead`。

- Emergency Takeover：无任何 `CanActAsActive` 成员，但至少一个旧成员仍 Alive／Incapacitated。
- Succession：旧 Party 全员 Dead／Removed。

两者复用玩家势力、非旧 Party、Alive、可控制、无 Encounter ownership、可信 exact Surface position的候选过滤，以及 `CombatPowerCalculator` 最高优先、EntityId 稳定 tie-break。普通 Party 内仍有可控成员时只按原顺序切换，不进入外部候选扫描。

可信位置统一由 `CharacterWorldPresenceQuery` 解析，允许 AtWorldPosition、带 exact anchor 的 AtWorldSite 与当前 SquadWorldMotion；禁止 Site center、arrival point、旧 Hex、旧战场或旧 Party position 猜测。位置必须在 detach 前捕获。若接管者来自 NPC Squad，只移出本人，保留其余 roster／motion，并按稳定规则修复 Leader／command target；同时解除接管者 BackgroundTravel 和来源执行 authority。

## 2. 战斗与 Squad

Preparing／Active／ReadyToEnd CharacterEncounter、continuous manual combat 或残留 participant authority 会延迟两种 handoff。必须先 `CommitAndReturn`、落地伤亡和战报、清理 participant，再接管。

Emergency 成功时，旧 `squad:player` 的 Alive／Incapacitated 成员进入稳定 `squad:recovery:<sequence>`，保留玩家势力、Lifecycle、伤势和精确位置，`CommandKind=None`。Dead／Removed 成员继续进入既有 singleton/corpse authority。接管者从原 NPC squad 单独拆出，新 `squad:player` 只包含接管者。

Recovery Squad 不跟随、不传送、不治疗。旧成员恢复后也不会自动抢回 Active，只能通过正常 Party 管理重新加入。没有外部候选时完全不改旧 membership：弥留队伍保持 `TemporarilyUnavailable` 等待恢复；全灭队伍保持 `AllMembersDead / IsAwaitingSuccession`。

为保证同一战败队伍不会在先后弥留过程中提前拆散，`PlayerPartyLifeStateMembershipService` 现保留 Incapacitated follower；Dead／Removed 在仍有 Active 时继续按旧规则退休。Host follower movement 已有生命状态 gate，不会拖动弥留成员。

## 3. 空间、Separate Space 与表现

候选 exact position 在 membership 修改前捕获。成功后继续复用 PlayerPartyTravel、WorldPresence、Surface re-anchor、5×5 streaming、Camera 与 Selection 链；接管者在自己的原位置获得控制。

P1 收口战后即时落点：`Preparing / Active / ReadyToEnd` 才阻断普通 Continuous Surface；`Committed` 只保留战报，不再阻断普通 Surface、人口与 EntityView。独立战场释放时，若旧 Party 正等待 External Handoff，则不先重建旧队伍所在 Surface；下一次控制刷新直接完成 C 的 Surface 激活、当前邻域 Site／Squad／Population reconcile 与 EntityView 生成。`PrepareExternalHandoffPresentation` 是 Camera／Selection 前的同步门槛；Surface、目标位置、物化、reconcile 与接管者 View 任一未就绪时均不切镜头，并由 Host 后续刷新重试同一呈现入口。

跨 Surface 或同 Surface但落点在旧 loaded 5×5 外时执行 hard re-anchor，以 C 所在 chunk 为中心构建唯一 player-centered neighborhood；已在当前邻域内时允许复用。Battle Report lifetime 与 spatial authority lifetime 分离，关闭战报不负责恢复世界。

Separate Space 入口泛化为 `ReleasePlayerControlForExternalHandoff`：只释放当前 PlayerParty presentation authority，不执行正常 Leave，不改变旧成员 EntityLocation、不治疗或送回入口。真正死亡 Succession 同样复用该入口。

Emergency 发布 `PlayerEmergencyControlTransferred`，Toast 为“当前小队已失去行动能力，<C> 接管了主控。”；真正死亡继续发布 `PlayerSuccessionResolved`。

## 4. Snapshot v8

不新增 schema。Recovery Squad、新 `squad:player`、Active、WorldPresence 与 Squad authority 均由既有 v8 字段表达。Save／Load 后接管者保持 Active，旧成员留在 Recovery Squad，不重新触发 handoff。

无候选等待态在 Load 后先完整恢复 world shell，再重新检查当时合法候选；成功态不得重复 handoff。True-Death Succession 的 corpse／singleton membership 同样由 v8 既有字段保持。

## 5. LevelTester

反引号打开 LevelTester →“战斗”：

1. 点击“SUCCESSION-01：准备 A/B 继承候选”，建立远处合法候选。
2. 点击“CONTROL-HANDOFF-01：使当前 Party 全员弥留”。工具逐人调用正式 `CombatLifeStateService.TryEnterIncapacitated`，不写 private field。
3. 若当前 CharacterEncounter 尚未结束，工具只落地弥留状态并提示完成正式战斗；handoff 会在 `CommitAndReturn` 后执行。没有 active Encounter 时立即走已结算状态的正常 handoff 检查。

真正死亡验收按钮继续保留，用于 SUCCESSION-01。

## 6. 制作人人工验收

1. A 弥留、B 可行动、远处 C 更强：只在原 Party 切 B，不切 C。
2. A/B 全员弥留、战斗正式结束、存在 C：自动切 C；A/B 原地、未死亡、未治疗，进入同一 Recovery Squad；新 Party 只有 C；Surface/Camera 重锚到 C。
3. A/B 后来恢复：Active 仍是 C，可经正常 Party 管理重新加入。
4. 全员弥留但无 C：不拆 Party，保持 `TemporarilyUnavailable`；任一成员恢复后重新成为 Active。
5. 全员真正死亡：原 SUCCESSION-01 最强候选、精确位置接管与无候选等待规则不回归。
6. CASE 2 后 Save／Load：C、Recovery Squad、A/B 生命周期和位置保持，不重复 handoff。
7. Separate Space：旧 A/B 的持久 EntityLocation 留在洞府，C 在外部 Surface 接管。
8. 独立战斗中 A/B 全员弥留或死亡：正式结束战斗后，无需关闭战报或点击 HUD 头像，画面直接物化 C 并完成 Camera／Selection；战报可与 C 所在普通 Continuous Surface 共存。

## 7. 验证边界

定向 headless tests 覆盖两类 handoff、候选/无候选、Recovery/Dead 分流、Encounter defer、Committed report-only 门禁、独立战场退出分流、目的地呈现门槛、真正死亡双 singleton 唯一注册、恢复不抢控制、Separate Space、exact position 与 v8 JSON/Strategic restore。基础编译与 diff-check 结果见本轮最终报告。

未实现 NPC 自动救援、自动寻路、恢复后自动回队、GameOver、接管者选择 UI、医疗重构或新队伍管理 UI。

## 8. 制作人验收与封板

制作人于 2026-09-24 完成人工验收并反馈“这一轮很完美”。验收覆盖 Party 内 B 顺序接替、全员 Incapacitated 后远处 C 紧急接管、A/B 原地可营救、真正全员死亡后的 C Succession、exact position、远 5×5 re-anchor、C 周边人口即时物化、无需双击头像、Committed 战报不阻断 ordinary world，以及 Save／Load。SUCCESSION-01、CONTROL-HANDOFF-01 与 P1 同日进入 **Producer Accepted / Sealed**。
