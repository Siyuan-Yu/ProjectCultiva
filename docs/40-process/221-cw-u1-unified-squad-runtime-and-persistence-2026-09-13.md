# CW-U1 统一小队运行时、共同移动与存读档交接

> 日期：2026-09-13  
> **CW-U1 Implementation Completed / Producer Acceptance Pending**。CW-U0 已在制作人确认范围内 Accepted / Sealed；未开始 CW-U2A。

## 已完成的正常玩法

所有持久真实人物在开局完成或动态人物注册刷新时获得唯一行动小队。无同行者使用 `squad:character:<EntityId>` 单人小队；玩家使用 `squad:player`；旧军队使用 `squad:army:<ArmyId>`，并保留稳定 `LegacyArmyId`。成员顺序、队长和共享命令由 Squad 唯一持有，Character 反向索引由同一服务同步维护。

PlayerParty 现只持有 `ControlledSquadId`、Active 与失能控制状态，成员读取统一 Squad。FormalArmy 保留 ArmyId、政治／任务和 WorldMotion 兼容职责，成员读取同一 Squad。正常“跟随主控／停止跟随”和既有军队编组入口都调用统一转移服务；离队人在当前真实位置成为稳定单人小队，不改 Faction、HomeSite、关系、库存或人物实体。

玩家 Follow 与 FormalArmy WorldMotion 继续使用已验收执行器；Squad 记录 `FollowLeader` 或 `FormalArmyWorldMotion` 命令来源。多人共享命令期间，NPC Schedule mover 让出人物位置写入权。Chunk 卸载只销毁 View，Squad 与 Character 保持。一个 EntityId 仍只由既有 EntityView registry 创建一个 View。

Strategic JSON 以 `hasSquadSnapshotAuthority`、`squads[]`、`controlledSquadId` 保存 SquadId、顺序、队长、命令、LegacyArmyId 与玩家控制关系；PlayerParty／FormalArmy 旧字段继续输出派生兼容数据。旧档没有 Squad 时，先从 FormalArmy 建对应 Squad，再从旧 PlayerParty 建玩家 Squad，最后为其余人物建立 singleton；重复读取带新字段的档不会重新分配 ID。

## 权威表

| 事实 | 唯一写入者 | 主要读取者 | 切换点 |
|---|---|---|---|
| Squad 成员顺序／队长 | `SquadMembershipService` | PlayerParty、FormalArmy、UI、战斗名单收集 | 创建、转队、离队、旧档迁移 |
| Character → Squad | `SquadBoard`，仅由同一成员服务维护 | 诊断、UI、执行器选择 | 与正向成员变更同一提交 |
| 玩家 Active | `PlayerPartyRuntime` | 输入、相机、战斗控制 | 主动切换、顺序失能接替、恢复重绑 |
| 共同命令来源 | `SquadState.CommandKind/Revision` | Follow、Schedule 排他、Army 适配 | 玩家队／Army 创建及组织迁移 |
| 人物真实位置 | 现有 WorldPresence／PlayerPartyTravel／FormalArmyWorldMotion 与近场精确位置链 | materialization、移动和存档 | 原执行器生命周期；Squad 不复制坐标 |
| 旧 Army 适配 | ArmyId ↔ SquadId／LegacyArmyId | Army marker、任务、旅行、既有战斗 | Content 创建、正式 Restore／Finalize |

## 保留的兼容边界

本轮没有迁移独立遭遇、战前精确锚点、旧 Hex 支援、大地图攻击入口、关系介入、完整势力继承或飞舟。现有战斗仍消费 PlayerParty／FormalArmy 的统一成员投影；冻结 Participant 记录不因失能或组织表现变化重建。非活跃成员保留 Squad 关系，只让出移动／攻击资格并按既有 residual 位置表现。

共享命令没有扩展成新的 NPC 战略 AI 或全队工作／修炼系统。玩家地面 Follow 与旧 FormalArmy 旅行仍是两个明确适配器，但它们不再分别拥有成员事实。旧存档只有 Hex 的人物仍没有凭空生成的逐人精确历史，这项留给后续遭遇锚点工作。

## 制作人人工验收路线

1. **组织观察：** 在 `Assets/Scenes/LevelTester.unity` 正常 New Game。点选荒村普通单人 NPC，人物面板应显示 `小队1/1 · 队长`；点选主角、同伴甲、同伴乙，按现有跟随入口组成三人队后，面板应分别显示稳定的 1/3、2/3、3/3 和唯一队长／Active。走近大地图原位置的 `荒村山匪`（`army:formal_bandit_patrol_1`），四名 BanditLeader／A／B／C 应显示同一四人队且没有复制人物。
2. **加入、离开、重入：** 使用当前可管理的同伴甲或同伴乙，点“跟随主控”加入；点该 follower 的“停止跟随”，确认其停在当下地面位置并显示 1/1 单人小队；再次点“跟随主控”。顺序恢复到本次加入的队尾，关系、势力、物品和生命状态不变化。战斗 Offer 已冻结时同一入口应明确拒绝改队。
3. **共同移动与停止：** 三人队在荒村 Continuous Outdoor 用 WASD／点击移动并停止，两个 follower 各自寻路跟随，不堆成同一点、不穿墙过河、不被日程拉回工作点。随后观察现有 `army:formal_bandit_patrol_1` 沿其已有调度／任务移动：四个成员保持稳定编队，同一人物没有 Schedule 与 Army presenter 同时拉扯。
4. **卸载与 A/B/C 存档：** A 档在同伴入队前保存，B 档在三人队成立后保存；走远使山匪 Chunk 真正卸载再返回，人物数量、队伍与 Army marker 不变。读取 A 应恢复入队前组织，读取 B 应恢复三人顺序／Active；从 B 离队后保存 C，再读 C 应恢复原地 singleton。读档不应新增 Character、Squad 或 View。
5. **战斗与失能回归：** 用三人队地面攻击 `army:formal_bandit_patrol_1`，完成现有多人入场→结束→战报→继续，名单含倒下者且只结算一次。让当前 Active 进入弥留，确认按队伍固定顺序切到下一可控成员；若按已有恢复规则恢复，仍在同一 Squad 且只重新绑定一个 Active，不复建人物或改变顺序。

第 1～4 项须由制作人运行 Unity 验收；本轮没有新增测试按钮。若当前内容中的山匪任务没有自然移动窗口，只记录该 NPC 共同移动项未测，不修改 Content 迁就验收。

## 实际检查

使用 `& ./tools/offline-compile.ps1 -Only @('XianXia.Core','XianXia.Data','XianXia.Unity')`，实际编译 Core 456、Data 77、Host 143 源文件通过；仅有既有 warning。已定向检查成员写入点、JSON 字段读写、恢复顺序、Schedule 排他与 UI 入口。交付前执行链接存在性和 `git diff --check`。未启动 Unity，未运行 EditMode、PlayMode、Test Runner、batchmode、自动测试或 Bake。
