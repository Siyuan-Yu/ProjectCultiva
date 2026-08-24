# ADR-0024：修士真实角色与 Army 战略载体模型

- 状态：**已采纳**
- 日期：2026-08-22（第二轮补充：编组／FactionId／联盟／占点收尾）
- 决策者：项目负责人（战略势力层设计拍板）
- 关联：[2A 势力、军队、外交与战略占领](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[2J Hex Territory / WorldSite / Bandit](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)

## 背景

Architecture Freeze v0.2 与 `27`／`34` 曾将「第三层普通修士」表述为 `CultivatorPopulation` 聚合模拟，并将 `ArmyGroup` 作为军队群体数据对象。战略层 Prototype（`ArmyStack`、`MemberCount`、`CombatPower` 整数）已落地接战 MVP，但与制作人 2026-08-22 拍板的方向冲突：

- 所有修士必须是持久真实 `Character`
- 修士组成的战略 Army 必须是真实 `MemberCharacterIDs[]`，不是匿名人数
- 跨 WorldNode 战略移动必须经 Army 载体
- 势力面板统计从 Roster 计算，不维护漂移的 `QiRefiningCount` 类字段

同时，「全员真实」不能与「全员实时 Actor／AI」等同；须与 ADR-0007 分级模拟一致。

## 选项

**A. 维持 CultivatorPopulation + ArmyGroup 聚合修士战争** — 与「每个修士有故事、关系、生死」冲突；势力统计不可审计。  
**B. 全员真实 Character + LOD 模拟 + Army 作为战略载体** — 数据真实、表现分级；配置与模拟成本更高，但符合产品方向。  
**C. 混合：玩家侧真实、AI 侧匿名** — 双轨语义，Ledger／关系／复仇无法统一。

## 决策

选 **B**。

### 核心条文

1. **所有进入修仙体系的修士都是持久 `Character`**，各自拥有 CharacterID、FactionMembership、Realm、Lifecycle、关系与历史等。  
2. **禁止**用 `QiRefiningCount`／`FoundationCount` 等匿名计数代表不存在的修士（势力展示数字 = Roster 统计）。  
3. **真实存在 ≠ 始终实例化。** 离屏修士按 LOD 模拟：  
   - **Cold / Data：** 低频／事件驱动 CharacterState  
   - **Strategic：** 在 Army 中，记录 MemberCharacterIDs，无 LocalMap Actor  
   - **Hot：** 进入 LocalMap／手动 Encounter 等才实例化 Actor；离开后写回 CharacterState  
4. **Army 是 WorldGraph 跨 Node 移动的唯合法载体。** 即使 1 人出征，也必须是 1 人 Army。  
5. **Army 保存 `MemberCharacterIDs[]` 与 `LeaderCharacterID`**，CombatPower 从成员计算，不是独立匿名池。  
6. **Node Defense** 来自 Resident Characters + Garrison Armies + Formation 等真实状态；禁止按 Node 等级临时刷匿名守军。  
7. **战略战斗结果**（死亡、伤势、Army 损失、Owner 变更、资源）必须回写真实世界状态。

### 2026-08-22 第二轮补充（见 [2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)）

8. **统一 FactionId** — Character／Army／Node Owner／Alliance／Vassalage／War 共用同一套 ID；`FactionMembership` 是成员关系，不是另一套 Faction 实体。  
9. **Army 编组** — 增减成员／换 Leader／解散仅能在己方 Node；禁止跨 Faction 混编；同势力成员；驻扎不自动解散。  
10. **外交／战争** — 无系统强制战后保护期；独立 Faction 最多一个 Alliance；第一版 Alliance 成员战争绑定。  
11. **手动占点收尾** — 全部 CaptureObjective 完成后 Owner 易主，可「结束战斗」进结算；残余守军 Captured／Escaped，可成 RetreatingArmy；Landless Faction 仍保留真实 Character。

### 与 ADR-0008 的关系

[ADR-0008](ADR-0008-army-group-aggregate.md) **不被删除**，但对「修士 Army」部分 **被本 ADR supersede／收窄**：

| 场景 | 模型 |
|------|------|
| **修士战略 Army** | 真实 Character + Army 载体（本 ADR） |
| **凡人大军／大规模非修士军队** | 仍可保留 `ArmyGroup` 聚合思想（ADR-0008） |
| **CultivatorPopulation** | **不再**作为正式修士数量或战争真源；见 `34` 修订 |

### 与 ADR-0007 的关系

[ADR-0007](ADR-0007-multi-party-lod-simulation.md) **仍然有效**。本 ADR **补充明确**：修士虽全员真实 Character，仍按 Cold / Strategic / Hot 分级模拟；与 ADR-0007「持续模拟 ≠ 始终完整加载渲染」一致，**不是冲突关系**。

### 与 Prototype 的关系

当前 Host 仍使用 `PartyWorldPresence`（选中 Character 直接上路）与 `ArmyStack`（`MemberCount`／`CombatPower` 整数）。这些是 **historical Prototype**，验收记录（`139`～`150`）继续有效。正式产品目标以本 ADR + [2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md) 为准；**实现迁移路径本轮不决定**。

## 影响

- `27`：第三层改为「修士真实个体 + LOD」；凡人仍 Population 聚合  
- `34`：`CultivatorPopulation` 不再代表修士战争真源；`ArmyGroup` 收窄至凡人／群体军事  
- `2A`：战略势力层产品设计真源  
- `26`：`ControlCore` generalize 为 `CaptureObjective`；War 为军事占点前提  
- `28`：个人 RelationshipLedger 与 Faction Diplomacy 分层  
- `33` v0.2：§4／§10 增加 2026-08-22 后续决策注记，指向本 ADR（**不升级 Freeze v0.3**）  
- `138`～`140`：增加 target-model 注记，保留 historical Prototype 描述  

## 未决

- `PartyWorldPresence` → `Army` 的代码迁移策略  
- Snapshot schema 是否纳入 FactionState / Army / War  
- 单支 Army 人数上限、ArmyCapacity 公式  
