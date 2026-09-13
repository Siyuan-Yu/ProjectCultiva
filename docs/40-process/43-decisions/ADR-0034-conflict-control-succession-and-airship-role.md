# ADR-0034：人物／建筑冲突、控制继承与飞舟运输职责

> **2026-09-13 部分替代：** [ADR-0035](ADR-0035-unified-squads-and-encounter-scope.md) §6 明确替代旧组织分层、自由战场裁切、第三队初始和范围外援军建议；本文保留历史决定及未冲突的单 Active、停表、政治／控制、真实战果与原锚点回归契约。

> 状态：已采纳（设计已确认；实现待迁移／核查；制作人验收待完成）
> 日期：2026-09-12
> 关联：[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[27](../../20-systems/27-characters-and-population.md)、[28](../../20-systems/28-jianghu-relations.md)、[ADR-0020](ADR-0020-focus-vs-control-authority.md)、[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)、[ADR-0030](ADR-0030-social-bond-attitude-and-snapshot-boundary.md)

## 决策

1. 人物私人关系、私人敌对、本场交战、势力态度和正式战争分别表达。攻击人物或其实际同行小队不自动替双方势力宣战；散修也不是共享政治势力。
2. 攻击某势力有效拥有的任意建筑都要处理对该有效 Owner 的战争后果。主世界首次攻击时，一个遭遇窗口合并战斗与宣战确认；确认后才允许攻击／伤害。
3. 已在人物战中转攻未交战势力建筑时，暂停当前战场并说明新增后果；确认后在同一战场扩大冲突、提交战争和守备响应，不重置伤势／消耗。取消只取消扩大战争行为。未经授权的 V1 范围技能默认不损伤非交战势力建筑。
4. 普通 PlayerParty 与 NPC 同行小队最多六人（含队长），不是整场参战人数上限。真实同行者按现场纳入，远方非主控战斗仍自动处理，不开放隔空逐人手操。
5. 任意时刻只有一个 Active。当前主控失能时，按 Party 固定顺序依次自动切换到下一名可控成员，不按战力排序，不弹继承选择窗口。
6. 只有当前 Party 全员真正死亡后才触发势力继承；全员暂时不可战或弥留但仍有生者不等于全灭。
7. 继承者使用现有战力口径，从玩家势力中选择存活、可担任主控且战力最高者；并列稳定处理。旧“必须身处己方 Site／必须未出征”等限制不再排除实际最强合格者。先结算旧队真实结果，再在继承者自己的实际位置取得控制，不传送、不复活、不自动补队。
8. 玩家势力无人时的终局明确延期；本轮不定义 GameOver、强制重开、免费复活或凭空创建继承者。
9. 飞舟只运输真实人物，不是拥有宣战、占领或独占移动资格的 FormalArmy 战斗实体。主控可登船或不乘船；登船后位置随舟，下船后从合法部署点恢复地面移动，人物不得在船地两处同时存在。
10. 移除“只有军队能在大地图打开时移动”的类型特权。WorldMap 是观察与下令视图，开关地图不更换位置／移动权威；所有合法移动按自身执行方式推进，统一服从世界暂停。运抵后由真实修士战斗和接管，飞舟不占地。

## 保留与替代

- 部分替代 [ADR-0020](ADR-0020-focus-vs-control-authority.md) 和 Freeze v0.2 的“Focus 不可用后早期 GameOver／后期继承待定”：队内顺序接替及全队死亡后的最强合格继承已经确定，只有空势力终局延期。
- 部分替代 [ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) Decision #7 的继承者“己方 Site／未出征”限制，以及 Party／FormalArmy 独占 Attack／Capture 与旧地图移动职责。单 Active、Party 最多六人、真实 Character、远方自动战等保留。
- [ADR-0024](ADR-0024-real-cultivators-and-army-strategic-model.md) 的真实修士和战果回写保留；FormalArmy／飞舟不再拥有政治或地图移动类型特权。
- [ADR-0028](ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md) 仍适用于 FormalArmy 编组地点，但不能成为继承资格或大地图移动特权。
- [ADR-0030](ADR-0030-social-bond-attitude-and-snapshot-boundary.md) 的 Bond／Attitude／RelationshipLedger 分离全部保留；本 ADR 只补足私人冲突与 War 的边界。

## 状态边界

全员弥留的既有安全出口、控制顺序持久性、最强角色稳定平局、舟中主控与旧 Army 任务兼容、无政治势力剧情的有效 Owner／外交主体，都须在实现前定向核查。设计已经确认，运行时迁移和制作人验收尚未完成。

