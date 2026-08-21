# ADR-0023：Manual Encounter 冻结战略 WorldTick（全战式 Modal 遭遇）

- 状态：**已采纳**
- 日期：2026-08-21
- 决策者：项目负责人
- 相关：ADR-0018（WorldTick 唯一轴）｜[21](../../20-systems/21-core-loop-and-time.md)｜[23](../../20-systems/23-combat.md)｜[138](../138-world-strategic-battle-offer-plan-2026-08-17.md)｜实施 [144](../144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)｜验收 [145](../145-adr0023-phases-af-acceptance-2026-08-21.md)｜打磨 [146](../146-adr0023-host-ux-polish-2026-08-21.md)  
- 飞书：https://my.feishu.cn/docx/Ykt4dH45zorlHqxY4ANc7vn9n6e

## 背景

旧模型允许「手动战斗期间战略世界继续推进」：`BattleOffer` 结束后恢复暂停，LocalMap 交战与 Travel／Schedule／ArmyStack 同吃 `WorldTick`。多人分遣时出现「一人进村、一人挂 InEncounter、以后回战场」等复杂交互（见已 superseded 的 [143](../143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md) 1A）。

产品正式改为 **Total War 式**：战略层与手动遭遇互斥；遭遇是 Modal；现实交战时长不映射为战略时间消耗。

## 选项

**A. 战斗期间世界继续跑（旧）**  
- 优点：同时性叙事（甲打架乙采药）  
- 缺点：进出场景／增援／清场挂起极度复杂；与全战心智模型冲突  

**B. BattleOffer→冻结 WorldTick→Auto／Manual→PostBattle→Resolve→恢复（采纳）**  
- 优点：规则清晰、可测、对齐全战；增援在开战前勾选  
- 缺点：放弃「同 Tick 异地同时劳作」叙事；需区分「战略冻结」与「战术暂停」  

**C. 独立战斗时钟**  
- 缺点：违反 ADR-0018「禁止两套世界时间」  

## 决策

采纳 **B**。补充 ADR-0018，**不**另开世界时间轴。

### 冻结规则（正式）

1. 全世界仍只有一个 `WorldTick`。  
2. 战略正常运行时：LocalMap／WorldGraph／Travel／Schedule／ArmyStack 均消费同一 `WorldTick`。  
3. 任一 `BattleOffer` 产生 → **立即冻结** `WorldTick` 推进。  
4. 选 Manual → 整场手动战期间 `WorldTick` 不推进；现实时长不映射战略 Tick。  
5. PostBattle 期间继续冻结。  
6. 仅当 Encounter **完全 Resolve** 后，恢复开战前的 pause／time scale。  
7. AutoResolve＝战略层瞬时结算，**不**额外推进 `WorldTick`。  

### Modal Encounter

- ActiveMap 锁定遭遇 LocalMap；禁止切其他 LocalMap。  
- 禁止给世界其他位置下战略命令；大地图可只读查看。  
- 禁止经大地图把参战者中途派往他处；逃跑必须走 Battle Retreat 结果。  

### 接战点与弥留残留（2026-08-21 补丁）

- **不**在 WorldGraph 插入临时 Node；接战点＝路锚／节点 `BattleAnchor`。  
- 「结束战斗」＝退出 Modal、解冻 WorldTick；**不等于**销毁战场。  
- 参战者（含勾选支援）离开 LocalMap／确认自动结算后，宏观位置**必须**留在 BattleAnchor，**禁止**瞬移回出发村／PreBattle。  
- 场上仍有任一弥留（敌或我）→ 战场残留：大地图可见头像；可再「攻击」敌残留／「查看」我方弥留进入原 Encounter 图。  
- 场上**无**弥留 → 销毁遭遇实例。  
- 丹药复活／背回／搜刮另开，不在本 ADR 必做。  

### 同 Tick 多接战

- `BattleInterruptQueue`：确定性顺序逐个 Resolve；禁止并行 Manual；禁止丢弃；处理中不得推进战略世界。  

### 废弃默认行为

- `FieldCleared` 后玩家宏观离开、世界恢复跑、他人仍 `InEncounter`、以后「回战场」——**不再作为默认**。  
- **修订：** 普通道路遭遇在「结束战斗」后若**仍有弥留**，保留 Encounter＋接战点残留（见上节）；仅无弥留时销毁。  
- 持久占点战场另走 Persistent LocalMap（未来）。  

## 影响

- **文档：** 修订 `21`／`23`／`33` §3／§10／138～140／143／glossary／roadmap；本 ADR 为时间纪律真源。  
- **Core：** `StrategicClockFreeze`；日后 ParticipantSnapshot／ReinforcementRange／Queue／PostBattle。  
- **Host：** 战略冻结 ≠ 战术 `IsPaused`；Modal 锁图／锁令；结束战斗才恢复。  
- **Snapshot：** 冻结态与遭遇会话需可存（分阶段）。  
- **测试：** 开战不推进 Tick；Manual 中 Tick 不变；Resolve 后恢复。  

实施分期见 [144](../144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)。

## 不在本 ADR

攻城、战场动态援军、飞行支援、ArmyGroup 实体化、复杂 NPC 支援 AI、伏击、阵型、战后追击。
