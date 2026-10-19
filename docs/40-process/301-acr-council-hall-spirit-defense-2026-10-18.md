# ACR — 固定议政厅仙术自动防御

> 状态：战斗内触发范围 Design Confirmed；建筑参战/伤害接口 Proposed / Gameplay Not Implemented / Producer Values Tentative｜2026-10-18

- 提出者角色：Development（转述制作人本轮需求）
- 日期：2026-10-18
- 问题：预设议政厅当前可被攻破、接管，却不会主动还击；新外观若只有屋顶，也看不出它是防御建筑。制作人希望它对近处敌对单位自动单体远程攻击，并暂以约自身尺寸 5 倍射程、20 伤害、3 秒间隔为基线。
- **制作人明确确认的触发范围：** 只有**已进入一场战斗，且议政厅正式加入了该战斗**时，议政厅才向该战斗里的敌方发射攻击；平时的连续大地图、离屏模拟均不开火，议政厅也不主动挑起新战斗。不能因它恰好画在场景里就默认参战。
- 建议改动：为固定 `SiteCore/controlCore` 增加战斗内仙术防御能力。先由正式战斗状态记录/解析该议政厅是否参战及所站阵营，再从**本场已加入的敌方 Character**中选择范围内一个合法、存活目标。建议从建筑实际 footprint 边缘计算最短距离，射程暂取 footprint 较长边的 5 倍；目标按距离排序、稳定 ID 打破同距；每 3 秒至多一次，命中伤害暂定 20。屋顶法阵发射单体灵力弹，仅表现消费正式命中结果，不由 Host 决定伤害。固定议政厅被攻破防御时停止发射；正式接管后归属随 `WorldSite.OwnerFactionId` 变更，并按本场战斗规则重新判定是否仍参战。除制作人已确认的战斗内触发范围外，其余细则仍待 Architecture 确认。
- 影响面：`WorldSite` 有效 Owner 与战斗阵营、独立 `CharacterEncounter` 的建筑参战身份与同源战场坐标、建筑防御状态、人物 HP／弥留／死亡、3 秒 tactical cooldown 与暂停／战斗存档恢复、伤害归责和事件、Host 投射物特效。**不涉及**平时主动侦敌、自动发起 Encounter、离屏炮击或地图范围守塔模拟。需要明确 20 是最终扣血还是参与防御公式的基础伤害；建筑在非攻城但战场包含它时如何加入、被攻破/接管后同一场内何时停火或改阵营、无战争但关系敌对、玩家直控、投降/囚禁/弥留等门槛。
- 相关文档：[23 战斗 §12.1–12.2](../20-systems/23-combat.md)、[2A 军事占领 §19.2–19.4](../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[26 领土管理](../20-systems/26-territory-management.md)、[33 Freeze v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md)、[ADR-0033](43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md)。
- 阻塞的实现：不能在 Unity Host 用 `Update` 直接扣人物血，也不能把建筑伪装成 Character 去绕过正式遭遇、攻击者、参战阵营和生命结算校验。须由 Architecture 在 System Design／必要时 ADR 定义建筑加入战斗、场内计时、伤害与存档边界后，Development 才能实现规则侧和 Host 投射物接线。

## 开发核查依据

`ControlCoreState` 目前只有耐久、防御、占领及 Site 绑定；`ControlCoreService` 处理人物攻击建筑，不处理建筑伤害人物。`MeleeCombatService.ApplyDamageStrike` 以人物攻击者为入口，检查独立遭遇和当前敌对参战；现有 Host `HostControlCoreAssault` 消费该流程攻击建筑。`CharacterEncounterState` 保存 Character 参战者及可选的 SiteCore Objective，但还没有建筑参战者或建筑攻击冷却；仅 Objective 存在也不能推断所有可见议政厅自动开火。现代 `CharacterEncounter` 期间 `WorldTick` 可冻结，故 3 秒节奏应属于场内 tactical time，并能在战斗存档恢复。

## 可独立先行的表现

本轮可将议政厅外观加一枚小型屋脊符阵，表达“有仙术防御设施”，仍不播放实际发射特效、弹伤或攻击 UI；图像接入记录见 [300](300-field-scale-and-council-hall-exterior-2026-10-18.md)。

2026-10-19：现有筑基纱衣攻击的统一可见弹道已按 [302](302-unified-visible-ranged-projectile-presentation-2026-10-19.md) 在 Host 接线；议政厅未来可复用同一视觉接口，但在建筑参战和正式伤害事件落地前不发射。
