# ADR-0049 NPC 个体生活与冷热战斗权威

> 日期：2026-10-21｜状态：Design Confirmed / Partial Implementation / Producer Acceptance Pending｜优先级：P0
> 规则真源：[317 NPC 自主行为](../317-npc-autonomy-design-confirmation-2026-10-21.md)、[27 人物](../../20-systems/27-characters-and-population.md)、[23 战斗](../../20-systems/23-combat.md)。不修订 Freeze v0.2。

## 决定

1. 现有 `MortalCivilianBoard` 继续独占受管凡人的需求、日程、职业和捕获；非受管人物的个人饱食、精力、当前心境及三档倾向由同一真实 Entity 上的 `PersonalLifeComponent` 持有。`AttributeId.MindState` 仍是基础属性，不用 Modifier 扣减冒充当前状态。只有玩家势力永久 Character 可编辑倾向。旧档缺少可选个人字段时以默认 75／平衡补齐，不改现行 Snapshot v17 数值与既有实体 ID。
2. `WorldTick` 是生活唯一时间轴。当前有效玩家指令、Party/Squad、战斗和凡人日程不由自由生活强行接管；修炼沿用 `CultivateAction` 的进度／瓶颈，娱乐以真实 `WaitAction` 时长推进。食物只从有权使用的 SitePublicStock、或队伍背包的现有 food 扣除，不生成虚构资源。热区取食必须由 Host 消费真实 MovementIntent，到达后扣粮；冷区仅在人物真实所在 Site 允许抽象取食。
   - 2026-10-21 现场返修：自主修炼的地点由 Surface 内容中的 `cushion` 蒲团经静态目录提供；选择同 Site 可达蒲团、真实到位后才能启动既有 `CultivateAction`，离屏逐段推进同一人物的持久世界位置。无可达蒲团不得原地获得修为。疲劳中断自主修炼并优先返已分配住宅床；床边到达半径使用 Surface CellSize，同 Core 床边判定一致；路径或到位失败时保底就地休息，后续有界重试。
3. 自动先手资格集中于 `NpcHostilityService`：正式 War、RelationshipLedger 中显式私人敌对事实、内容 `hostile` tag。态度数值不构成交战资格；袭击事件可新增显式被袭者对袭击者敌对事实。Ledger 事件流是唯一关系 authority，缓存仅为其派生索引。
4. 加载现场由 Host 的人物 View／移动／现有伤害服务结算，洞府直接原地战斗，不生成第二层 CharacterEncounter；普通玩家户外接战仍走 CharacterEncounter。双冷 NPC 由 `BackgroundNpcConflictService` 按 WorldTick 对真实 Character 造成伤害，同一名现场人物不可再参加冷区本 Tick 伤害。冷热切换不克隆生命、关系、位置或战利品。
5. 失败撤退、投降和死亡必须区分。独立战场 NPC 劣势退出只标记本场 `Withdrawn` 并在原有返场和接触抑制权威下离场；“投降”不自动变成俘虏，也不授予战利品／占点。正式捕获仍走既有俘虏权限。玩家撤退保留独立战报与失能成员留场边界。

## 兼容与未闭环

- 这是 v17 的可选人物扩展，不重写旧档；旧客户端不保证保留新字段，跨版本回退须备份存档。未来若要把缺失个人字段视为错误，须另立迁移 ADR，不得暗改稳定 v17。
- 当前离屏生活逐 WorldTick 更新个体需求、按行动真实计时；离屏冲突每 8 Tick 评估。更大规模的分桶性能及冷热边界人工验收仍未完成。
- 洞府玩家逃跑仍明确不做；特定 NPC 投降后的关押／谈判属于单独既有捕获流程，不在本轮凭空接管。
