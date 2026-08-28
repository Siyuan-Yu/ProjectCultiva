# Phase 4：Battle Authority — Manual / Auto / Retreat + Participant Gathering（2026-08-28）

> 状态：**实现入仓 · SupportArea 集合规则 · Trigger/Gathering 分离 · Participant 来源追踪 · 待 EditMode 跑通 · 未人工验收**｜最后更新：2026-08-28（夜）  
> 产品契约真源：[2K §8](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)／[163 Phase 4 规划](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`（`PlayableHostBootstrap`）  
> **未同步飞书**

---

## 0. 目标与边界

| 项 | 说明 |
|----|------|
| **Goal** | FormalArmy / PlayerParty 战斗权限与接战流程；`BattleInitiator` vs `PlayerDecisionSubject`；Participant Gathering；Manual / Auto / Retreat；拒绝远程 FormalArmy Manual |
| **Must Not Break** | ADR-0023 WorldTick 冻结、战损回写、Army vs Army / War 链、Snapshot v6 向后兼容 |
| **Explicitly Out of Scope** | 盟友援军、中立第三方参战、动态增援、多方大战、Legacy 入口删除、Phase 5+ |

---

## 1. 已确认产品语义（硬规则 · 当前有效）

### 1.0 Battle Trigger（A）与 Participant Gathering（B）分离

| 阶段 | 规则 |
|------|------|
| **A. Battle Trigger** | Initiator **已提交 Hex**（非 ContinuousWorldPosition 派生格）必须落入 Defender `SupportAreaHexes` 才允许创建 PendingBattle |
| **B. Participant Gathering** | 冻结 SupportArea 后，各单位 **已提交 Hex** 须满足 `SupportAreaHexes.Contains(UnitHex)`；Initiator/Defender 强制加入 |

禁止用 `WorldToHex` 中途派生格、WorldPosition 半径、或 Initiator 中心距离替代上述判定。

### 1.1 BattleAreaHexes

接战创建瞬间，先确定**实际战斗区域**：

| 场景 | BattleAreaHexes |
|------|-----------------|
| 普通单 Hex 野外战斗 | Defender **接战瞬间**所在 Hex |
| 多 Hex WorldSite 内战斗 | 该 WorldSite **Footprint 占据的全部 Hex**（禁止仅用 AnchorHex 代表整站） |

### 1.2 SupportAreaHexes（唯一空间 Authority）

```
SupportAreaHexes =
  BattleAreaHexes
  ∪ { 与 BattleAreaHexes 中任意 Hex 直接共边相邻的全部 Hex }
```

| 场景 | 支援范围 |
|------|----------|
| 单 Hex 战场 | 战场 Hex + 6 邻格 = 7 Hex |
| 多 Hex WorldSite | Site 全部 Footprint + 外围与任一 Footprint Hex 直接相邻的 Hex |

**空间资格判断：**

```
SupportAreaHexes.Contains(UnitHex)
```

**明确禁止：**

- Initiator 周围 1 Hex
- InitiatorEngagementLocation 周围 1 Hex
- Defender WorldPosition 圆形距离
- BattleLocation 的 Vector2 / Vector3 distance
- Site AnchorHex 周围 1 Hex 代替整站
- 已加入援军继续向外扫描下一圈
- `Distance(UnitPosition, SomePosition) <= radius`

接战创建时冻结 `BattleEngagementSupportArea`；`BattleLocationHex` 仅 **Presentation / BattleAnchor**（Defender PresenceHex）。

### 1.3 Initiator + Defender

**BattleInitiator** 与 **直接 Defender** **无条件**加入，不检查 `SupportAreaHexes`。

### 1.4 其他 FormalArmy

创建 Battle 时**单次**扫描。其他 Army 须同时满足：

- 属于当前交战双方中的某一方（同 Faction）
- `SupportAreaHexes.Contains(ArmyHex)`
- 未被其他 Pending / Active Battle Lock

**不包含：** 盟友、中立第三方（即使站在 SupportArea 内）。

### 1.5 PlayerParty / Active Character

- `SupportAreaHexes.Contains(PlayerHex)` → **强制加入**
- 否则 → **不加入**

Manual 空间资格：`PlayerPartyIncluded == true`（即 Player 实际被 Gathering 锁定）。

`ApplyLockedParticipantsToSnapshot` 中 `seedMandatoryAttackers` **不得**绕过 Gathering 空间规则。

### 1.6 禁止连锁

只依据冻结的 SupportAreaHexes 扫描一次；已纳入单位**不会**成为新扫描中心。

### 1.7 Save / Load

Pending Battle Restore **禁止**重新扫描 Participants；恢复 Locked 名单 + 冻结的 BattleArea / SupportArea Hex 列表。

### 1.8 InitiatorEngagementLocation（Debug-only）

不参与 Participant Gathering / Manual 空间资格。

---

## 1A. Superseded 规则（勿再实现）

<details>
<summary>2026-08-28 日间的 Initiator-centered 规则（已废弃）</summary>

```
Distance(Candidate, BattleInitiator engagement-start location) <= 1
```

</details>

<details>
<summary>2026-08-28 晚「BattleLocationHex 中心距离 ≤1」（已废弃）</summary>

```
HexDistance(UnitHex, BattleLocationHex) <= 1
```

该表述易被实现成错误的单点中心距离逻辑；已由 **SupportAreaHexes 集合模型**（§1.1–1.2）取代。
</details>

---

## 2. Manual / Auto / Retreat

| 选项 | 资格 |
|------|------|
| **Manual** | `PlayerPartyIncluded == true` |
| **Auto** | 涉及玩家侧时始终可用 |
| **Retreat** | 仅 `PlayerDecisionSubject`；撤至 `PreEngagementLegalLocation` |
| **远程 FormalArmy Manual** | 拒绝（`RemoteFormalArmyManualBlocked`） |

---

## 3. 实现进度

| 子项 | 状态 |
|------|------|
| Domain Runtime / Decision / Retreat | ✅ |
| Participant Gathering（SupportAreaHexes Authority） | ✅ |
| Battle Trigger（Committed Hex · 禁止派生格提前接战） | ✅ **2026-08-28 夜** |
| Player Hex Authority（与 WorldMap Marker 对齐） | ✅ **2026-08-28 夜** |
| Participant IncludedReason + SpatialGuard 断言 | ✅ **2026-08-28 夜** |
| 删除 seedMandatoryAttackers Snapshot 旁路 | ✅ **2026-08-28 夜** |
| Snapshot Restore（不重新扫描 + 冻结 SupportArea） | ✅ |
| WorldMap Debug 高亮 BattleArea / SupportArea | ✅ |
| EditMode Tests T1–T10 + 集成回归 | ✅ 入仓 · 待 Unity 跑通 |
| Debug UI（判定链 + IncludedReason） | ✅ |
| 人工验收 | ⏸ 未开始 |

---

## 4. 核心 Domain 落点

| 文件 | 职责 |
|------|------|
| `BattleEngagementSupportArea.cs` | `ResolveAndFreeze`；BattleArea + 六向邻接 → SupportArea；`Contains` |
| `BattleEngagementTriggerService.cs` | **A. Battle Trigger**：Initiator 已提交 Hex ∈ SupportArea(Defender) |
| `BattleEngagementSpatialQuery.cs` | 已提交 Hex Authority；PlayerParty 与 WorldMap Marker 对齐（`WorldToHex(WorldPosition)`） |
| `BattleEngagementHexDistance.cs` | Defender / Army PresenceHex；Presentation `BattleLocationHex`；Debug Initiator 快照 |
| `BattleParticipantGatheringService.cs` | **B. Gathering**：逐成员 `SupportAreaHexes.Contains`；Initiator/Defender 强制；**无 seed 旁路** |
| `BattleParticipantInclusionReason.cs` | IncludedReason 常量 + Player 判定链 Trace |
| `BattleParticipantSpatialGuard.cs` | Gathering / Snapshot 后硬断言；违规 `Debug.LogError` |
| `BattleEngagementAuthorityService.cs` | Trigger 门控 → 冻结 SupportArea → Gather → Lock → Snapshot |
| `PendingEngagementSnapshotRestore.cs` | Restore Locked Participants + SupportArea 列表，不调用 `GatherAndLock` |
| `BattleEngagementAuthorityDebug.cs` | 接战触发 + 参与者收集 + 判定链 + Snapshot IncludedReason |
| `BattleEngagementWorldMapDebug.cs` | LevelTester：橙框 BattleArea、蓝框 SupportArea |

---

## 4A. 根因分析（2026-08-28 夜 · LevelTester 实测）

### 结论摘要

| 问题 | 根因 | 是否共享 SupportArea 算错 |
|------|------|---------------------------|
| **提前触发 Battle** | Initiator 使用 ContinuousWorldPosition **派生 CurrentHex** 而非已提交 Step | 否（Trigger 阶段） |
| **Player 误加入** | **PlayerHex Authority 分裂** + **Snapshot 旁路** | **否** — BattleArea/SupportArea 在 Debug 中已正确 |

实测案例中 `BattleAreaHexes = (55,60)`（紫色 Defender）、SupportArea 7 格均**正确**；Player 被加入是因为 Domain 认为 `PlayerHex = (54,61)` ∈ SupportArea，而 WorldMap 黄色 Marker 实际在距 Defender **2 格**处。

### PlayerHex Authority 分裂

| 用途 | 旧数据源 | 问题 |
|------|----------|------|
| WorldMap Marker | `PlayerPartyWorldLocationQuery` → `PlayerPartyTravel.WorldPosition` | 视觉真源 |
| Participant Gathering（旧） | 非 `TravelingMembers` 时回退 **`WorldPresence.ResidualHex`** | 可与 Marker 不一致 |

修复：`BattleEngagementSpatialQuery.TryGetCommittedPartyHex` 优先 `PlayerPartyTravel`；idle 时用 `WorldToHex(WorldPosition)` 与 Marker 对齐。

### 第二条写入旁路（已删除）

`ApplyLockedParticipantsToSnapshot` → `AddFormalArmiesAsMandatory` 内 **`seedMandatoryAttackers` 循环**：

- 条件：`PlayerPartyIncluded && ContainsLockedPartyMember(id)`
- 可把追击 `ready` 列表中的 Party 成员写入 Snapshot，**不再校验 SupportArea**
- **2026-08-28 夜已整段删除**；Player 仅经 `AddPlayerPartyMandatory`（Gathering 锁定后 + 二次空间校验）

### ManualEligible 方向

```
Player ∈ SupportArea → PlayerPartyIncluded → ManualEligible
```

**不存在**反向：`ManualEligible` 不会把 Player 塞进 Participants（`BattleDecisionPolicy` 只读 `PlayerPartyIncluded`）。

### Faction 扩展

同 Faction **不**自动全员参战。Army / Player 各自满足 `SupportAreaHexes.Contains(OwnHex)`；仅 Initiator / Defender 无条件加入。

---

## 4B. Participant IncludedReason

每条 Snapshot 记录与 Engagement 均带 IncludedReason（Debug / 断言）：

| IncludedReason | 含义 |
|----------------|------|
| `DirectInitiator` | Initiator FormalArmy 强制 |
| `DirectDefender` | Defender FormalArmy / EnemyPrimary 强制 |
| `SupportAreaArmy` | 同交战方 Army，`ArmyHex ∈ SupportArea` |
| `SupportAreaPlayer` | PlayerParty 成员，`MemberHex ∈ SupportArea` |
| `PromoteInRangeIncapacitated` | 仅 downed 角色 · `PromoteInRangeIncapacitatedToMandatory` |
| `ExcludedNotInSupportArea` | Debug 候选未纳入 |

Player 判定链（Cheat Panel → 战斗）：

```
Player Included Before Gathering
Player Included After Gathering
Player Included After Snapshot
Player In Snapshot Records
Player IncludedReason(final)
Player Last Write Source
SupportArea.Contains(PlayerHex)
=== Snapshot Participants === (每条 IncludedReason)
```

违规时 `BattleParticipantSpatialGuard` 输出 `Debug.LogError`（Editor / Development Build）。

---

## 5. EditMode 测试（BattleAuthorityTests）

| 测试 | 验证点 |
|------|--------|
| **T1** | Initiator + Defender 永远加入 |
| **T2** | 同 Faction Army 在 SupportArea → 加入 |
| **T3** | Army 不在 SupportArea → 不加入 |
| **T4** | 邻接 Hex 资格不受 WorldPosition 偏移影响 |
| **T5** | PlayerParty 在 SupportArea → 强制加入 + ManualEligible |
| **T6** | PlayerParty 近 Initiator 但不在 SupportArea → **不加入** |
| **T7** | 第三方 Faction 在 SupportArea → 不加入 |
| **T8** | 无连锁：B 在 SupportArea，C 仅近 B → 只有 B |
| **T9** | Save/Load 恢复 Participants，Load 后不重新扫描 |
| **T10** | 多 Hex Site：BattleArea = 全 Footprint，非 Anchor 邻格援军加入 |
| **LevelTester 回归** | A 攻 B：DefenderHex=BattleArea；两邻格友军加入；Active 不在 SupportArea → 不 Manual |
| **SeedMandatoryAttackers** | 距 SupportArea 外的 roster 不得写入 Participant Snapshot |
| **Trigger_CommittedHexTwoAway** | 距 Defender 2 格不得 `TryBeginEngagement` |
| **Trigger_CommittedHexAdjacent** | 邻接格可触发 |
| **Trigger_DerivedHexAdjacentButCommittedTwoAway** | 派生格邻接、committed 2 格 away → 不弹 Offer |
| **Gathering_PlayerTwoHexFromDefenderNearReinforcement** | Player 近援军、距 Defender 2 格 → 不加入 |
| **Gathering_BelligerentReinforcementInSupportArea** | 援军在 SupportArea 加入；第三方不加入 |
| **Gathering_PlayerAdjacentToInitiatorButTwoFromDefender** | stale WorldPresence + PartyTravel 2 格 away → 不加入 |
| **OfferPath_PlayerTwoHexFromDefender** | 完整 `TryBuildOfferForArmyVsArmy` 路径 · 无 Player · ManualIneligible |
| 补充 | EnemyInitiated Retreat；Remote Manual 拒绝 |

---

## 6. LevelTester 人工验收清单（短）

1. Cheat Panel → **战斗**：判定链 + `IncludedReason` + `SupportArea.Contains(PlayerHex)`。
2. 勾选 **WorldMap 高亮 BattleArea(橙) / SupportArea(蓝)**：橙框必须在紫色 Defender Hex。
3. A 攻 B：`BattleAreaHexes = [(DefenderQ,DefenderR)]`；邻格友军 `inSupport=true` 并 Locked。
4. 黄色 Active 与 BattleArea **无直接共边相邻** → `PlayerInSupportArea=false`，`PlayerPartyIncluded=false`，无 Manual。
5. 第三方邻格 → 不纳入。
6. 若 Player 误加入 → Console 应出现 `[BattleParticipantSpatialGuard]` LogError。
7. 弹窗态 Save→Load → Locked 名单与 SupportArea 不变。

---

## 7. 变更日志

| 日期 | 内容 |
|------|------|
| 2026-08-28 | Phase 4 初版入仓 |
| 2026-08-28 日 | Initiator-centered 扫描（**已 superseded**） |
| 2026-08-28 晚 | BattleLocationHex 中心距离 ≤1（**已 superseded**） |
| 2026-08-28 晚 | `FormalArmyContinuousTravelService.AdvanceAll` 遍历时修改集合修复 |
| 2026-08-28 晚（二次） | Defender PresenceHex；LevelTester 回归测试 |
| 2026-08-28 晚（三次） | **SupportAreaHexes 集合 Authority**；修复 seedMandatoryAttackers 绕过 Gathering；T10 + Debug 扩展 |
| 2026-08-28 夜（四次） | **Battle Trigger**：`BattleEngagementTriggerService` + Committed Hex；禁止派生格提前接战 |
| 2026-08-28 夜（五次） | **PlayerHex Authority** 与 WorldMap Marker 对齐；删除 Snapshot `seedMandatoryAttackers` 旁路 |
| 2026-08-28 夜（六次） | **IncludedReason** + `BattleParticipantSpatialGuard` 硬断言 + 判定链 Debug + WorldMap 高亮 |
| 2026-08-28 夜（六次） | 逐成员 Gathering / Snapshot 二次空间校验；集成测试 `OfferPath_PlayerTwoHexFromDefender` |
