# CIVILIAN-LIFE-01 — Mortal Needs, Faction Work, Occupation, Captivity & Recruitment V1 — Design Freeze

**Design Confirmed / Documentation Updated / Implementation Not Started（2026-09-27）。Producer Acceptance: Not Applicable Yet。**

## Product Goal

让据点里的凡人由真实、可持续追踪的人物承担生活、劳动与战后命运。玩家能回答“谁在劳动、谁逃走、谁被俘、谁后来加入”，同时以需求优先级、职业单选和离屏 LOD 控制操作量与运行成本。

本轮只冻结产品语义并对齐文档；不修改代码、Content、Snapshot schema、Runtime、UI 或既有 AUCTION-01 实现。

## Design Principles

- 每个凡人都是持久、独立、具有稳定 `EntityId` 的真实 Character；人口、劳动力和职业计数只能从人物状态派生，不能成为匿名权威。
- 凡人与修士共用 Character 基础。凡人不会因加入势力、接受工作或被玩家管理而自动成为修士或内门弟子；修士生活方式与日程属于 Future。
- 当前住房表现保持可见、真实，不引入房屋黑箱、private presence 或人物非物质化。未来性能优化通过 Character LOD 完成。
- 本设计取代旧“普通凡人以 `MortalPopulation`／岗位组作为真源、被关注后才实体化、可再归并”的方向；受保护的历史 Freeze 正文不在本轮改写，现行产品方向以 [ADR-0044](43-decisions/ADR-0044-individual-mortal-faction-work-needs-and-captivity-direction.md) 为准。

## Character 与 Faction 语义

- 有归属凡人拥有真实 `FactionId`／`FactionMembership`，可以接受该势力工作和人员管理。
- 无归属凡人使用 `FactionId=None`：仍是有需求与基本生活的真实 Character，不接受势力工作；Site 易主不会自动把他加入新势力。
- PlayerFaction 凡人属于玩家可管理的永久名册方向，但 `FactionMembership != PlayerParty != ActiveCharacter`。不能把全部居民自动塞入旅行 Party；具体永久管理 authority 与是否自动入队留待实现审计。
- Site 所有权变化不会直接改 Character 的势力、忠诚、位置或身份。

## Mortal Needs V1

V1 只包含 `Satiety`（饱食）与 `Energy`（精力），建议均为 0–100，数值越高状态越好。Social Need 不在本里程碑；Mood、Comfort、Entertainment 只有在后续玩法证明需要时才设计。

正常决策优先级：

1. Combat／即时危险。
2. Fleeing／Capture／Escort／Detention 强制状态。
3. Hunger／Satiety。
4. Sleep／Energy。
5. Rescue／紧急搬运。
6. 专业职业工作。
7. 通用搬运／建设。
8. Idle。

饥饿通常先于睡眠与工作；逃亡者不会因轻微饥饿停下。`Energy < 10` 时可原地紧急睡眠，恢复慢于床位；正常返床阈值可调。

势力凡人没有个人物品栏。进食必须消耗可访问的势力 Site 公共库存，不能凭空生成食物。玩家势力沿用 PartyInventory／可访问库存 authority；两者取用优先级留待实现决定。

## Work 与 Profession

每名凡人最多一个专业职业。V1 单选项为：

- `Unassigned`
- `Farmer`：食物／普通农业。
- `HerbFarmer`：药草农业。
- `Logger`：伐木并逐步接入现有树木／木材系统。
- `Medic`：真实治疗。

所有有行动能力的凡人都可执行通用 `Haul/Rescue` 与 `Construction`（建造／修理／拆除）。搬运濒死者是紧急救援，不等于 Medic；所有人可救援，只有 Medic 提供专业治疗。通用任务可以中断专业生产，完成后恢复原职业。`Unassigned` 仍有需求并可搬运、救援、建设，但不执行专业生产。

V1 不包含 Miner、Generic Gatherer、Artisan、Weaponsmith、Cook、Cleaner、Hunter、Researcher 或商业 AI。现有农业／劳动应增量迁移为具名 Character worker，并能回答具体劳动者；本设计不授权重写农业系统。

## Personnel UI Direction

入口位于 Faction → 人员管理。紧凑人物行／卡片显示姓名、Mortal/Cultivator、势力职位、Profession、当前活动／状态、Satiety 与 Energy。Profession 使用单选列表 `Unassigned/Farmer/HerbFarmer/Logger/Medic`。不采用 RimWorld 式大型工作优先级矩阵。

## Site Occupation Aftermath 与 Loyalty

Site 完整易主后，旧势力有归属凡人不会自动换阵营、加入玩家、删除或传送。每个 Character 依据对**当前 Faction** 的 `Loyalty`（0–100）决定后续；Loyalty 不属于 Site、Relationship 或永久人格，加入新势力时建立新的 Loyalty。

- `Loyalty < 15`：留在原地但不自动加入。玩家右键可选择招募／劝降（立即成功）、攻击／杀死（正常 Encounter）或释放（进入 Fleeing）。
- `Loyalty >= 15`：进入 Fleeing，缓慢逃离。开始时间错开，选择不同合法可行方向，大体远离 SiteCore；不得整齐列队或随机撞墙。
- 逃离成功：人物越过正式 Site 物理／控制范围和一小段可调 buffer，成为 `EscapedFromSite/Displaced`。清除该 Site 的工作与居住绑定，不再计入居民／劳动力，但保留 EntityId、FactionId、Loyalty、位置和 displaced 事实。跨 Site 难民 AI 属于 Future。
- 本规则只覆盖战败势力的有归属凡人；无归属居民的战后行为不在 V1 扩展。

## Player Civilian Interaction 与 Capture Encounter

对自由或逃亡凡人的攻击必须走既有 World interaction → `CharacterEncounter` → Separate battle → result → world，不能直接扣血或建立 `CivilianCombat`。玩家追上逃亡者时可攻击、捕获或释放；释放恢复 Fleeing。

捕获必须通过带 `Capture/Subdue` intent 的正常 Encounter。玩家获胜且目标仍存活、可捕获时才成功；真正死亡的 Character 不能被捕获。

## Escorted Captive、Prisoner Housing 与 Detention

被捕者仍是同一 Character。当前主动发起者成为 carrier／escort；被押送者不能自行移动、工作、被玩家控制或参与玩家劳动，其位置跟随押送者的移动结果。具体运输 authority 必须在实现前审计，不能先造第二套位置系统。

没有合法监牢时，不能把人物抽象为“已拘留”；其状态保持 `Captured/Escorted`。普通 Residence 增加 `Normal/PrisonerOnly`（或与现有架构等价）的用途，复用真实住房容量，不创建第四套空间或匿名囚犯计数。只有实际带到有容量的合法 `PrisonerOnly` Residence 后才成为 `Detained`。

Detained Character 保留原 `FactionId` 与 Loyalty，有 Satiety/Energy，需要吃饭和睡觉，但不参加势力工作、耕作、伐木、建设或医疗岗位。

## Recruitment、Surrender、Release 与 Execution

- Detained 只有在当天获得食物和正常休息时，Loyalty 才下降；饥饿或休息不良不会下降。“虐待加速投降”不成立。建议默认 `-5/day`，属于可调实现参数。
- Detained `Loyalty < 15` 后允许招募／劝降。成功后 `FactionId → PlayerFaction`，建立新的 PlayerFaction Loyalty = 50；获得凡人／基础外部成员语义，仍不是修士。
- 招募者进入玩家可管理名册，可分配职业并参与需求与工作。是否自动加入当前 PlayerParty 留待实现决定。
- Detained 可执行招募、释放、处决。释放后从当前玩家 Site 向外 Fleeing；处决是囚犯操作，不需要伪造势均力敌的 Encounter。自由／逃亡者的攻击仍必须走正常 Encounter。

## Death / Execution

自由或逃亡凡人的杀伤与死亡只通过正常 CharacterEncounter 结算，继续遵守 Dead ≠ Removed。已拘留囚犯的处决是明确 prisoner action，不再开启虚假的平等战斗；无论哪条路径，人物都不能被换算成匿名人口扣减。

## Persistence Requirements — Planned

未来实施必须持久保存：个体需求、Profession、当前 Faction 与 Loyalty、Fleeing／Displaced、Captured／Escort、Residence 用途与容量分配、Detention 照料进度，以及加入新势力后的 Loyalty。当前 Snapshot schema 不变；这些字段全部是 **Planned / Not Implemented**，Restore 规则在实现轮设计。

## Performance Direction

附近人物使用高细节模拟；离屏人物未来可降低更新频率或以可解释的活动进度结算，但身份始终是 Character，不可折回匿名人口。是否在 V1 实现 LOD，取决于对当前 streaming／schedule 的实现审计。

## Explicit Non-Goals

Social Need、Mood、Comfort、Entertainment、越狱、复杂囚犯待遇、酷刑、劝降小游戏、性格／概率劝降、跨 Site 难民 AI、自动投奔、Miner、Generic Gatherer、Cook、Cleaning、Hunting、Crafting profession、武器生产、装备 AI、修士日常 AI、战略势力 AI、自动战争、巨型凡人军队、住房黑箱／private presence、完整 LOD 重写。

凡人主要是居民和劳动者；未来可讨论 guard／militia duty，但不把数百或数千凡人变成 RTS 大军。

## Open Tunables

以下是实现参数，不是产品方向缺口：正常饥饿阈值、正常睡眠／返床阈值、床／地面恢复率、可食 Item tags、PartyInventory 与 PublicStock 优先级、逃离 buffer、逃跑速度与错峰、押送复用的移动 authority、囚犯每日 Loyalty 默认下降值（当前建议 5）、最终 PlayerFaction mortal role enum、招募名册是否自动加入 PlayerParty、人员 UI 视觉布局。

## Manual Acceptance Vision

未来实现验收应能观察：每位居民有稳定身份与需求；饿了实际取食，困了找床或紧急就地睡眠；职业单选且通用救援／建设可插队；Site 易主后人物按 Loyalty 留下或真实逃跑；捕获必须经 Encounter，押送到真实 PrisonerOnly 容量后才拘留；良好供养逐日降低囚犯 Loyalty，招募后建立新的 PlayerFaction Loyalty 并成为可管理凡人。Save/Load 后所有身份、位置和状态保持。

## Current Status

**Design Confirmed / Documentation Updated / Implementation Not Started。Producer Acceptance: Not Applicable Yet。**

下一实施轮开始前必须审计：现有 NPC work／labor、粮田／药田、树木／木材、injury／dying／rescue、Encounter intent/result、housing、FactionMembership、玩家 roster 与 PlayerParty 边界、Snapshot，以及 Site control transfer。AUCTION-01 已在后续封板轮更新为 **Producer Accepted / Sealed**；CIVILIAN-LIFE-01 本身仍未开始实现。
