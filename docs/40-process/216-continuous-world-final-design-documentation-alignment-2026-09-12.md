# Continuous World 最终设计文档对齐（2026-09-12）

> **CW-U0 局部替代：** 本文旧自由战场裁切、范围外关系远援和 CW-06 排序由 [ADR-0035](43-decisions/ADR-0035-unified-squads-and-encounter-scope.md) 替代。CW-01／02 原验收范围保留，CW-03 主体制作人反馈基本完成；统一小队与最终独立遭遇仍待实现和验收。

> 性质：Documentation only
> 状态：**Design: Confirmed｜Documentation: Updated｜Implementation: Not migrated / Partially present / Needs verification｜Producer Acceptance: Pending**
> 决策：[ADR-0032](43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)、[ADR-0033](43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md)、[ADR-0034](43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md)

本记录只说明本轮跨文档对齐、迁移依赖与验收债务。玩法权威正文分别位于 [24](../20-systems/24-world-and-settlements.md)、[26](../20-systems/26-territory-management.md)、[23](../20-systems/23-combat.md)、[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) 等系统页，不能以本记录替代正文。

## 1. 规则块到正式归属

| 最终规则块 | 权威文件／章节 |
|---|---|
| 4.1 世界与地理 | [24 §2](../20-systems/24-world-and-settlements.md#continuous-outdoor)、[ADR-0031](43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md) |
| 4.2 WorldSite 与 SiteCore | [24 §2.1](../20-systems/24-world-and-settlements.md#worldsite-sitecore)、[ADR-0032](43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md) |
| 4.3 重叠、升级与行政管理 | [26 §2](../20-systems/26-territory-management.md#sitecore-administration)、[2J 当前规则](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md) |
| 4.4 小队、关系、势力战争 | [2K §3、§9](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2A §19](../20-systems/2A-factions-armies-diplomacy-and-capture.md) |
| 4.5 私人仇敌预警与附近敌情 | [28 §3.1](../20-systems/28-jianghu-relations.md#private-hostility-warning)、[2M §8](../20-systems/2M-character-social-relations-v1.md) |
| 4.6 统一遭遇窗口 | [23 §2.1](../20-systems/23-combat.md#encounter-confirmation) |
| 4.7 建筑攻击与战内升级 | [2A §19.4](../20-systems/2A-factions-armies-diplomacy-and-capture.md#conflict-and-building-war) |
| 4.8 同源独立战场与时间 | [23 §2.2](../20-systems/23-combat.md)、[21 §10](../20-systems/21-core-loop-and-time.md) |
| 4.9 参战、援军与收尾续战 | [23 §3.1](../20-systems/23-combat.md#encounter-participation)、[23 §3.2 生命周期](../20-systems/23-combat.md#encounter-lifecycle) |
| 4.10 战场接管、胜利与结算 | [23 §12.2](../20-systems/23-combat.md#encounter-capture-victory)、[2A §37](../20-systems/2A-factions-armies-diplomacy-and-capture.md) |
| 4.11 各回战前位置并保留战果 | [23 §12.1](../20-systems/23-combat.md#encounter-return-anchors)、[ADR-0033](43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md) |
| 4.12 控制接替、继承与撤退 | [2K §4](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md#control-succession)、[27 §3.2](../20-systems/27-characters-and-population.md) |
| 4.13 飞舟与大地图 | [2K §8.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md#airship-worldmap-movement)、[2K §5.8.2](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md#worldmap-target-resolution)、[ADR-0034](43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) |

## 2. 旧规则替代矩阵

| 旧描述（不再作为目标） | 当前正式目标 | 正式位置 | 实现状态／迁移影响 |
|---|---|---|---|
| 普通户外 Site／Hex 是一张探索 LocalMap | 连续 Surface；Site 是核心行政范围；战斗才进入临时独立场景 | 24 §2–3；ADR-0031/0032/0033 | Continuous 部分存在；完整目标待迁移／核查 |
| 原地交战，全部旁观者局部逃难 | 同源独立战场；非参战居民不搬入 | 23 §2.2 | 待迁移 |
| 战时无窗口就开始新战斗 | 敌对右键提交意图；实际新接战前暂停确认一次 | 23 §2.1 | 待迁移／核查 |
| 打人或攻击军队成员自动替势力宣战 | 人物冲突与势力战争分离 | 2A §19.4；28 §3.1 | 待迁移 |
| 只有战略核心攻击才触发战争，普通建筑可直接拆 | 攻击任何势力有效拥有的建筑都先处理战争后果 | 2A §19.4 | 待迁移 |
| 已在战斗中即可攻击第三方建筑 | 同战场暂停确认后果并升级；取消不影响原战斗 | 2A §19.4 | 待迁移 |
| 同行成员随机缺席／只拉被点角色 | 初始真实同行者按现场参战；关系第三方才有限判定 | 23 §3.1 | 待迁移 |
| 援军每帧抽签或递归无限增加 | 有限真实候选、有限节点、最多一次收尾续战 | 23 §3.1 | 待迁移 |
| 必须返世界再接管／船才有占领权 | 真人可在战场实际接管；飞舟只运输 | 23 §12.2；2K §8.1 | 待迁移 |
| 必须全歼且占领才胜利／杀人直接得地 | 正式接管 OR 打倒本次有效对手可结束；未接管不送土地 | 23 §12.2 | 待迁移 |
| 战后保留战术最终坐标／统一 BattleAnchor | 各回自己的战前世界锚点，真实战果不回滚 | 23 §12.1；ADR-0033 | 待迁移；锚点与唯一结算须核查 |
| 拆旗删除建筑／范围一律无主 | 资产与库存保留；按有效覆盖接续管理，否则暂停行政功能 | 26 §2；ADR-0032 | 旧旗能力部分存在；待迁移 |
| 管辖范围裁掉河流／范围禁止重叠 | 建设与管辖同一区域，建筑个体判断；理论覆盖允许重叠 | 26 §2；2L §3 | 待迁移 |
| 老核心升级按出生早覆盖全部新范围 | 已取得控制保留；扩张不得追溯夺地 | 26 §2；ADR-0032 | 数据表达待核查 |
| 只有军队开图可走，玩家关图才出发，开关换 Executor；WorldMap 只能点 Hex／Site | 同一位置／移动权威和统一暂停；关图不取消旅行；有效地面点击可解析连续目标，同 Hex 点击仍可不同 | 2K §5.8.2、§5.8.8、§8.1 | 旧阶段行为曾验收；新目标待迁移 |
| 船型未定／船沿地面过桥 | 飞舟纯运输，主控可乘，不受地面过桥约束 | 2K §8.1 | 延后实现 |
| 失能等于全队死亡；随机／手选继承；需己方 Site／非出征 | 队内固定顺序接替；全队真正死亡后自动选势力最强合格者 | 2K §4；27 §7 | 待迁移／安全出口待核查 |
| 无继承者立即开发 GameOver／重开／复活 | 空势力终局明确延期 | 2K §4；ADR-0034 | 明确延期 |

## 3. 状态与迁移依赖

- **Design: Confirmed**：本轮规则已经确认，不因参数未调重新投票。
- **Documentation: Updated**：系统正文、主契约补丁、术语、索引、阅读指南、ADR、路线与 Devlog 已接线。
- **Implementation**：保留 Continuous Surface、真实 Character、WorldTick 冻结、Control Asset、Construction、Social Relations 等已有能力；新遭遇生命周期、回位、建筑战争升级、SiteCore 范围、继承与飞舟仍为未迁移、部分存在或待核查。
- **Producer Acceptance: Pending**：历史验收只适用于当时版本，不证明上述新行为已经实现或验收。

建议后续依赖顺序：先核查稳定身份、动态资产存档、战前锚点、暂停和控制生命周期；再做 SiteCore 范围与建筑归属；然后做同源遭遇和人物战闭环；再接战内建筑战争、接管、胜利与收尾；其后处理有限介入、接替／继承和预警；最后接入飞舟运输与统一 WorldMap 观察／下令。每阶段需要正常游戏人工验收，本文不授权一次性实现。

## 4. 明确延期与实现前核对

明确延期：空势力最终结局；途中拦截、舰战、甲板战和船内自由行走；完整俘虏／赎金、全城治安、传闻、无限远援和无限扩边；旗升级成永久议政厅、同城多核心；大型大陆编辑器及本轮以外玩法。

后续调参：Site 等级范围与成本；建筑成本／时长；飞舟数量、载员、速度、设施和上下客流程；仇恨阈值、天数、距离与缓和；援军候选范围、时点、概率和撤离距离。

实现前核对：全员弥留的既有安全出口；实际控制历史与稳定平局；胜利／接管／一次收尾／最终关闭边界；Encounter 候选未决／已决、在途／到达、一次收尾已用和唯一结算的持久化；战前锚点、尸体与战利品唯一性、非法原点最小修正；同次攻击抑制；舟中主控／继承者与旧 Army 任务兼容；Ch01 的人物成员身份、玩家管理势力、建筑有效 Owner 与联盟／附庸链外交主体如何安全解析。Outdoor 动态破坏物状态的存档失败属于未通过技术债，是战场往返的必要依赖。

## 5. 后续文档验收场景（本轮不运行）

1. Site：越界不切图；升级不夺既有控制；水面仍属辖区但建筑各自判放置。无 Site 地点立旗可建立新 Site，不要求先处于既有 Site；拆旗后建筑保留并接续／暂停管理；接管不收编守卫。
2. 遭遇确认：无论我方或敌方主动、已有战争或私人仇敌，新战斗都在第一击／第一发弹道前暂停一次；我方取消尚未实施的攻击不产生攻击结果，被袭方不能靠取消免战。
3. 战内扩展：先打人物，再点第三方势力建筑；确认后同战场宣战并扩大冲突，取消则建筑不受伤且原人物战继续。未经授权的 AOE 不得静默损伤第三方建筑。
4. 同源与胜利：当前已建／已拆的墙、门、桥、议政厅对应真实状态；接管完成即使守军仍活也可结束，或先打倒本次对手后留场接管；未接管退出不自动得地。
5. 回归与介入：双方真实同行者和有限关系／守备援军按规则入场；援军、主控、敌人、弥留者及尸体各回自己的战前锚点，伤亡、消耗、关系、建筑损伤与接管保留且无重复实例。
6. 生命周期存读档：在候选已决定、援军在途／已到场、`OneTail` 已使用或 `VictoryAvailable` 等节点保存再读取，不重抽候选、不重复加人、不重发奖励、不重复结算。
7. 控制继承：Active 倒下按第二／第三位固定顺序切换；全队弥留但仍有生者不触发势力继承；只有全队真正死亡才切到势力最强合格者的实际位置；空势力终局保持延期。
8. 飞舟与地图：飞舟载人位置唯一；开关 WorldMap 不切换移动权威、不取消既有旅行或改变暂停状态；同一 Hex 内两个有效地面点击可得到不同连续目标，Site 标记到达真实合法位置。
9. 上述能力完成后必须覆盖存读档；本次文档任务不运行 Unity、编译、测试或 Bake。

## 6. 2026-09-13 实施状态更新

- **CW-02 Producer Accepted — 当前交付范围**：依据制作人反馈，Continuous 过渡版原地手动战斗、实际冻结参战名单隔离、暂停 ownership、队内失能接替、结束结算与手动战报闭环通过当前范围验收。
- **仍延期**：最终临时独立同源战场及真实裁切范围的初始资格、有限远援在途／到场、完整势力继承、大地图攻击入口退役。初始资格的最新正文见 [23 §3.1](../20-systems/23-combat.md#encounter-participation)；WorldMap 攻击入口退役见 [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。
- **CW-03**：新 Site／立旗建站进入实现阶段；实现交接见 [219](219-cw-03-new-worldsite-and-flag-core-closure-2026-09-13.md)。完成代码与静态检查不等于制作人验收。
