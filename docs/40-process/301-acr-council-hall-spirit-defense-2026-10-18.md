# ACR — 固定议政厅仙术自动防御

> 状态：Design Confirmed / Implemented / Producer Accepted / 封板授权（制作人 2026-10-19 明确确认攻击及弹道现场验收通过，并要求封板推送）｜正式规则见 [23 §12.2.1](../20-systems/23-combat.md)，权威边界见 [ADR-0045](43-decisions/ADR-0045-council-hall-encounter-defense-authority.md)

- 提出者角色：Development（转述制作人本轮需求）
- 日期：2026-10-18
- 问题：预设议政厅当前可被攻破、接管，却不会主动还击；新外观若只有屋顶，也看不出它是防御建筑。制作人希望它对近处敌对单位自动单体远程攻击，并暂以约自身尺寸 5 倍射程、20 伤害、3 秒间隔为基线。
- **制作人明确确认的触发范围：** 只有**已进入一场战斗，且议政厅正式加入了该战斗**时，议政厅才向该战斗里**对其所属势力敌对**的目标发射攻击；附近发生的普通作战也允许议政厅参战，不限于以该建筑为攻城 Objective 的战斗。平时的连续大地图、离屏模拟均不开火，议政厅也不主动挑起新战斗。不能因它恰好画在场景里就默认参战。
- **制作人明确确认的伤害及停火语义（2026-10-19）：** 20 是议政厅的**攻击力**，如普通人物攻击力为 20 一样参与正常攻防及承伤流程，不是固定扣除 20 HP。建筑防御耐久归零后立即停止攻击；同一场内即使完成接管、建筑耐久恢复，也不换边重新开火。下一场按当时合法归属重新判断。
- 已定案实现：为固定 `SiteCore/controlCore` 增加战斗内仙术防御能力。正式战斗状态记录议政厅参战、所属势力、当场停火状态，从**本场已加入且与建筑所属势力处于战争关系的存活 Character**中选择范围内目标。从建筑实际 footprint 边缘计算距离，射程为 footprint 较长边的 5 倍；目标按距离排序、稳定 ID 打破同距；每 3 秒至多一次。屋顶法阵发射单体灵力弹，仅表现正式领域出手，不由 Host 决定伤害。
- 影响面：`WorldSite` 有效 Owner、人物正式势力身份与本场阵营、独立 `CharacterEncounter` 的建筑参战身份与同源战场坐标、建筑防御状态、人物 HP／弥留／死亡、3 秒 tactical cooldown 与暂停／战斗存档恢复、非 Character 攻击者的伤害归责和事件、Host 投射物特效。**不涉及**平时主动侦敌、自动发起 Encounter、离屏炮击或地图范围守塔模拟。普通遭遇加入边界、战争关系目标筛选、无势力/弥留排除和停火存档已由 23 §12.2.1 与 ADR-0045 定案。
- 相关文档：[23 战斗 §12.1–12.2](../20-systems/23-combat.md)、[2A 军事占领 §19.2–19.4](../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[26 领土管理](../20-systems/26-territory-management.md)、[33 Freeze v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md)、[ADR-0033](43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md)。
- 已落实的实现边界：不能在 Unity Host 用 `Update` 直接扣人物血，也不能把建筑伪装成 Character 去绕过正式遭遇、攻击者、参战阵营和生命结算校验。战斗设计与 ADR-0045 已定案，建筑参战、战术计时、伤害及存档均在领域／数据层，Host 只播正式出手的弹道。

## 开发核查依据

`ControlCoreState` 目前只有耐久、防御、占领及 Site 绑定；`ControlCoreService` 处理人物攻击建筑，不处理建筑伤害人物。`MeleeCombatService.ApplyDamageStrike` 以人物攻击者为入口，检查独立遭遇和当前敌对参战；现有 Host `HostControlCoreAssault` 消费该流程攻击建筑。`CharacterEncounterState` 保存 Character 参战者及可选的 SiteCore Objective，但还没有建筑参战者或建筑攻击冷却；仅 Objective 存在也不能推断所有可见议政厅自动开火。现代 `CharacterEncounter` 期间 `WorldTick` 可冻结，故 3 秒节奏应属于场内 tactical time，并能在战斗存档恢复。

2026-10-19 追加核查：普通 `PrepareInternal` 会按接战位置的行政 Site 将战场固定在 Site 核心范围内，但 `SourceSiteId` 只是场地来源，不是建筑参战记录；非攻城战也没有 `Objective`。`EncounterCharacter.Enemy` 代表本场相对双方，不等于对建筑 Owner 的势力外交关系。人物普攻以 `Attack - Defense / 2`（至少 1）为基础，再走伤害承受/护盾与生命结算；建筑无人物属性与装备，不能伪装一个 Character 来复用 `MeleeCombatService.ApplyStrike`。这些是需在正式 System Design／必要时 ADR 中解决的接口边界，不是额外的制作人参数征询。

## 可独立先行的表现

本轮可将议政厅外观加一枚小型屋脊符阵，表达“有仙术防御设施”，仍不播放实际发射特效、弹伤或攻击 UI；图像接入记录见 [300](300-field-scale-and-council-hall-exterior-2026-10-18.md)。

2026-10-19：现有筑基纱衣攻击的统一可见弹道已按 [302](302-unified-visible-ranged-projectile-presentation-2026-10-19.md) 在 Host 接线；议政厅未来可复用同一视觉接口，但在建筑参战和正式伤害事件落地前不发射。

2026-10-19 实施：作者源配置的议政厅实际占地在运行内容绑定时交给 `ControlCore`；`CharacterEncounter` 独立记录本场议政厅、参战时 Owner、占地、冷却与永久停火位，JSON 可选字段保存。正式 Core 服务在 Active 中基于战争关系、存活参战者、占地边缘射程和三秒战术冷却择一出手，按 20 攻击力结算人物基础防御、护盾、HP 和弥留；破防和接管即时锁定本场停火。Host 消费成功出手播放现有弹道。基础离线编译通过；制作人随后现场验收议政厅攻击及弹道通过，开发侧未自行运行 Unity PlayMode。
