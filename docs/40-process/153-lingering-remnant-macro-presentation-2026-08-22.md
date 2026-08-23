# 153 · 弥留残留战场收束 + 自动战宏观头像 + 接战名单与追击修复（2026-08-22）

> 状态：**已落地（EditMode 已补；手操待验）**｜日期：2026-08-22  
> **Presentation 更新（2026-08-23）：** Hex WorldMap 正式表现改为 PURE DERIVED 聚合 Marker（`Hex × Relation × DEAD/DOWNED`）；残留位置 = Encounter Hex（`AtHex`），不再用 Battle Node／散落 portrait／匿名 remnant 作为主路径。BattlefieldLingering 再入保留。  
> 上级：[150 残留 Offer](150-lingering-battlefield-batch3-offer-2026-08-21.md)／[152 RTS 点击纪律](152-worldmap-rts-click-discipline-2026-08-22.md)／[147 接战点弥留](147-battlefield-linger-no-teleport-2026-08-21.md)  
> 关联代码：`BattleOfferService`／`StrategicEncounterSpawner`／`StrategicEncounterResolveService`／`LingeringBattlefieldPartyService`／`HostWorldMapPanel`／`HostStrategicInterruptPresenter`／`StrategicResidualPresentationQuery`  
> 游玩入口：`Assets/Scenes/LevelTester.unity` → Ch01 大地图

---

## 1. 一句话

**弥留／尸体**与**自动战结算**在大地图上表现与「进 LocalMap 再出来」一致；**接战强制名单**只含**行动决定人 + 半径内弥留／尸体**；**第一人撤退**不再清掉**后续追击者**的接战弹窗。

---

## 2. 问题与目标

| 问题 | 目标 |
|------|------|
| 自动战全灭敌军后大地图只显示**一个军团栈**，无个体「尸」头像 | 结算后立即刷 tracked 实体 + `WorldPresence`，逐人画头像 |
| 进战／残留再进时**附近活人被强制参战** | 仅**当前下令者**（选中活人）+ **弥留／尸体**强制；其余为 Optional |
| 派 A、B 依次攻击；A 撤退后 B 到站弹「抵达节点」 | B 仍保留追击标记，到站弹**接战 Offer** |
| 自动战胜率与战力观感不符 | 调整 `EstimateAutoWinPercent` 公式 |
| 处决／弥留与 `Armies.Remove` 语义混乱 | 栈上 `IncapacitatedMemberCount`／`CorpseMemberCount`；处决留尸体不删栈 |
| 再进 LocalMap 把弥留倒计时刷满 | `HasReusableTrackedPresence` 含尸体；禁止误 `ClearSpawned` |

---

## 3. 代码改动摘要

### 3.1 自动战宏观残留（个体头像）

| 项 | 说明 |
|----|------|
| `StrategicEncounterSpawner.EnsureMacroRemnantSpawns` | 自动战胜后**未进 LocalMap**时，按人数立刻刷弥留／尸体 NPC + 接战点 `WorldPresence` |
| `SpawnRemnantNpcEntities` | 与 `ApplyPending` 共用刷怪逻辑 |
| `BattleOfferService.BindEncounterAfterAutoResolve` | 调用 `EnsureMacroRemnantSpawns`；不再仅设 `SpawnOnNextMapLoad` |
| `HostWorldMapPanel.DrawArmyStacks` | `HasDownedRemnant` 栈**隐藏聚合标记**，只显示个体头像 |
| `RefreshEnemyDownedWorldPresence` | 公开包装，宏观刷怪后钉接战点 |

### 3.2 战损／尸体／弥留语义

| 项 | 说明 |
|----|------|
| `ArmyStack` | `IncapacitatedMemberCount`／`CorpseMemberCount`／`HasDownedRemnant` |
| 处决 | `CorpseMemberCount = members`；**不** `Armies.Remove` |
| 未处决 | `IncapacitatedMemberCount = members` |
| `ApplyCorpseToLivingTrackedSpawns`／`ApplyIncapacitatedToLivingTrackedSpawns` | 已有 spawn 时同步生命周期 |
| `ReconcileAfterLifeDecay` | 尸体腐烂后清抽象栈 |

### 3.3 接战强制名单（Batch 4）

| 项 | 说明 |
|----|------|
| `LingeringBattlefieldPartyService.CollectViewParty` | 新增 `mandatoryLiving`：只强制**行动决定人**，不再把半径内所有活人塞进 Mandatory |
| `TryBuildOfferForLingeringBattlefield` | 透传 `mandatoryLiving`；Host 传**大地图选中活人** |
| `StrategicBoard.SetPendingLingeringVisit` | 探望弥留记录派出名单；到站 Offer 只强制**实际派出者** |
| `BattleParticipantSnapshotBuilder.CollectOptionalFriendly` | 跳过 `IsLingeringDowned`（弥留 + 可见尸体） |

### 3.4 多人追击 + 撤退

| 项 | 说明 |
|----|------|
| `HostStrategicInterruptPresenter` 撤退 | `ClearPursuitForAgents(retreatParty)` 替代 `ClearPursuit(world)` |
| `BattleOfferService.ResolveAuto` 胜 | `ClearPursuitForEngagedKeepEnRoute` 替代全清 |
| 行为 | 第一人撤退后，**仍在路上的增援者**保留 `CombatPursuitStackId`，到站弹 Offer |

### 3.5 其它

| 项 | 说明 |
|----|------|
| `CombatPowerCalculator.EstimateAutoWinPercent` | Logistic + 向 50% 收束，8%～92% |
| `LingeringBattlefieldPartyService.IsLingeringDowned` | 弥留 OR 可见尸体统一交互 |
| EditMode | `AutoBattle_*`／`LingeringReenter_*`／`Pursuit_FirstRetreatFromOffer_*`／`LingeringOffer_OnlyDecisionMakerLivingIsMandatory` 等 |

---

## 4. 流程（验收用）

```text
自动战胜利 + 处决/弥留
  → EnsureMacroRemnantSpawns（宏观个体头像）
  → 结算弹窗确认 → Park 残留战场

残留再进
  → 选中活人 + 右键弥留/尸体 → Offer
  → Mandatory = 选中活人 + 半径内弥留/尸体
  → Optional = 其它附近活人（默认不勾）

多人追击
  → A、B 分别/同时 BeginPursuit
  → A 到站弹 Offer → 点撤退 → A 停原地
  → B 继续 → 到站仍弹 Offer（非到站查看）
```

---

## 5. EditMode 测例（重点）

- `AutoBattle_ExecuteOnWin_LeavesCorpseRemnant` — 处决留尸体 + 宏观 spawn 数量
- `AutoBattle_SpareOnWin_AllIncapacitatedRemnant_NoKills` — 弥留 + 立刻 spawn
- `AutoBattle_SpareOnWin_SpawnsMacroRemnantsImmediately` — 不再 `SpawnOnNextMapLoad`
- `LingeringReenter_PreservesEnemyIncapAndCorpseTimers` — 再进不刷满倒计时
- `Pursuit_FirstRetreatFromOffer_SecondStillGetsBattleOffer` — 撤退不清后继追击
- `LingeringOffer_OnlyDecisionMakerLivingIsMandatory` — 强制名单边界

---

## 6. 与目标模型（2A）的关系

本轮仍属 **ADR-0023 接战 Prototype** 收束：`ArmyStack`／`PartyWorldPresence`／整数 `MemberCount` **未迁移**至 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 的真实 Character Army。  
残留战场、宏观头像、接战 Offer 行为在 Prototype 层已对齐产品预期；正式 Faction／Diplomacy／Capture 实现见 [ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)。

---

## 7. 手操待验

1. 自动战处决 → 大地图多个「尸」头像（非单栈）  
2. 自动战不处决 → 多个「弥」头像 + 可再进  
3. 残留再进：只选 1 人，旁边其它活人不应默认强制参战  
4. A 撤退、B 追击同一敌军 → B 到站弹接战  
5. 再进 LocalMap → 弥留／尸体倒计时未被刷满  

---

## 8. Hex WorldMap 正式入口（2026-08-23）

| 交互 | 规则 |
|------|------|
| 左键 Residual Marker | 仅 Residual Detail 面板 |
| 右键 Hex | `HexResidualContextQuery` → Context Menu |
| 我方残留 | **进入残留战场**（不要求先选军团）→ `BattleOfferService.TryEnterFriendlyLingeringAtHex` |
| 敌方残留 | 先选我方军团 → **攻击残留战场** → `TryAttackEnemyLingeringAtHex` / `ArmyHexLingeringArrivalService` |
| 远端攻击 | Hex 路径移动，抵达后再进；禁止 Pursuit／瞬移 |
| Runtime 查询键 | `BattleAnchorHex` + `BattlefieldLingering`（`LingeringBattlefieldQueryService`） |

**EditMode：** `LingeringBattlefieldHexEntryTests`（LINGER-01..08）

**Save/Load 再进：** `StrategicEncounterRuntime` 仍标注「不入 Snapshot」→ **UNSUPPORTED**

---

## 9. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-23 | Hex Context 正式入口；Query/Arrival 服务；退役 Hex 下 avatar/stack 残留菜单 |
| 2026-08-22 | 初版：宏观残留 + 强制名单 + 追击撤退 + 测例 |
