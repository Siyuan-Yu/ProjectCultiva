# Phase 4：Battle Authority — Manual / Auto / Retreat + Participant Gathering（2026-08-28）

> **状态：Accepted / Sealed（正式封板）**｜封板日期：2026-08-28  
> 实现完成 · EditMode 用例已入仓（以仓库测试为准）· **`Assets/Scenes/LevelTester.unity` 人工验收通过**  
> **本文件 §1 = Phase 4 Battle Authority 当前正式真源**  
> 产品契约对齐：[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)／[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)  
> **未开始 Phase 5**

---

## 0. 目标与边界

| 项 | 说明 |
|----|------|
| **Goal** | FormalArmy / PlayerParty 战斗权限与接战流程；`BattleInitiator` vs `PlayerDecisionSubject`；Participant Gathering；Manual / Auto / Retreat；拒绝远程 FormalArmy Manual |
| **Must Not Break** | ADR-0023 WorldTick 冻结、战损回写、Army vs Army / War 链、Snapshot 向后兼容 |
| **Explicitly Out of Scope** | 盟友援军、中立第三方参战、动态增援、多方大战、Legacy 入口删除、**Phase 5+**、战略 AI 主动交战 |
| **封板结论** | Phase 4 = **Accepted / Sealed**；Deferred 见 §8（不阻塞封板） |

---

## 1. 最终 Battle Authority（正式真源 · Sealed）

以下为封板后唯一有效规则。历史 Initiator-centered／中心距离 ≤1 等口径一律废弃（见 §1A）。

### 1.0 Battle Trigger

普通 Army 主动攻击 Defender：

- **仅当** Initiator **当前 Hex** 与 Defender **当前 Hex** 真正 **共边相邻** 时，才允许成立接战／创建 Pending Battle。
- **禁止**使用 `WorldPosition` 浮点距离提前触发。
- 位置以 **已提交 Hex**（Hex Authority）为准，不以 ContinuousWorldPosition 中途派生格替代。

### 1.1 BattleArea

普通 Army vs Army：

```
BattleAreaHexes = { Defender 当前所在 Hex }
```

即：被攻击方所在 Hex 就是本次战场实际位置。

多 Hex WorldSite：若战斗明确属于整个 Site，则

```
BattleAreaHexes = Site 实际占据的全部 Hex
```

**禁止**仅用 AnchorHex 代替整站 Footprint。

### 1.2 SupportArea

```
SupportAreaHexes =
  BattleAreaHexes
  ∪ { 与 BattleAreaHexes 中任意 Hex 直接共边相邻的全部 Hex }
```

参战空间资格：

```
UnitHex ∈ SupportAreaHexes
```

**明确禁止：**

- Initiator 周围范围
- `InitiatorEngagementLocation` 作为资格中心
- WorldPosition radius／浮点距离
- 链式援军扫描（已纳入单位再向外扩圈）

邻接判定必须走统一 **Hex topology Authority**（Odd-R → axial → Neighbor／Distance → Odd-R），见 §7。

### 1.3 Participants

| 角色 | 规则 |
|------|------|
| **Initiator** | 强制加入 |
| **Defender** | 强制加入 |
| **交战双方势力合法单位** | 位于 SupportArea → **强制加入** |
| **Player / Active Character** | 属于交战一方 **且** 位于 SupportArea → **强制加入**；否则不加入 |
| **第三方／中立** | **不**自动加入（即使站在 SupportArea） |
| **已被其他 Battle Lock 的单位** | **不能**重复加入 |
| **援军连锁** | **不**进行 |

创建时**单次**扫描并冻结名单；Save／Load 恢复 Locked Participants + 冻结 BattleArea／SupportArea，**禁止** Load 后重新扫描。

### 1.4 Manual

- **只有** Player／Active Character **实际进入本次战场**（被 Gathering 纳入／位于 SupportArea）时，才具备 Manual Battle 基础资格。
- **不能**远程接管远方 FormalArmy。
- Manual 资格读 `PlayerPartyIncluded`（或等价字段）；**禁止**因 Manual 按钮反向把 Player 写入 Participants。

### 1.5 Auto / Retreat（封板范围内）

| 选项 | 规则 |
|------|------|
| **Auto** | 涉及玩家侧时可用；远方交战默认 Auto |
| **Retreat** | 仅 PlayerDecisionSubject；撤至接战前合法位置 |

敌军主动攻击我方时的 Retreat、AI vs AI 主动接战完整人工验收 → **Deferred**（§8），不阻塞封板。

---

## 1A. Superseded 规则（勿再实现）

<details>
<summary>2026-08-28 日间 Initiator-centered（已废弃）</summary>

```
Distance(Candidate, BattleInitiator engagement-start location) <= 1
```

</details>

<details>
<summary>2026-08-28 晚「BattleLocationHex 中心距离 ≤1」（已废弃）</summary>

```
HexDistance(UnitHex, BattleLocationHex) <= 1
```

已由 **SupportAreaHexes 集合模型**（§1.1–1.2）取代。
</details>

---

## 2. 封板验收记录

| 项 | 结果 |
|----|------|
| 实现 | ✅ 完成并入仓 |
| EditMode 自动测试 | ✅ 用例已入仓（`BattleAuthorityTests` 等）；以仓库为准记录，本轮不另报 Editor 全绿数字 |
| 人工验收 Scene | ✅ `Assets/Scenes/LevelTester.unity` |
| 人工验收结论 | ✅ **通过** |
| Phase 状态 | **Accepted / Sealed** |
| Phase 5 | **Not Started**（本轮不启动） |

---

## 3. 实现落点（索引）

| 文件 | 职责 |
|------|------|
| `BattleEngagementSupportArea.cs` | 冻结 BattleArea + 共边邻接 → SupportArea；`Contains` |
| `BattleEngagementTriggerService.cs` | Battle Trigger：已提交 Hex 共边／SupportArea 门控；禁派生格 |
| `BattleEngagementSpatialQuery.cs` | 已提交 Hex Authority；Player 与 WorldMap Marker 对齐 |
| `BattleParticipantGatheringService.cs` | Gathering：`SupportAreaHexes.Contains`；Initiator/Defender 强制；无 seed 旁路 |
| `BattleParticipantInclusionReason.cs` | IncludedReason + Player 判定链 Trace |
| `BattleParticipantSpatialGuard.cs` | Gathering／Snapshot 后硬断言 |
| `BattleEngagementAuthorityService.cs` | Trigger → 冻结 → Gather → Lock → Snapshot |
| `PendingEngagementSnapshotRestore.cs` | Restore Locked + SupportArea，不重新 Gather |
| `Assets/Scripts/Core/World/Hex/HexMath.cs` | Odd-R Hex topology Authority（Neighbor／Distance／CollectHexLine） |

过程根因（PlayerHex Authority 分裂、Snapshot `seedMandatoryAttackers` 旁路等）已在封板前修复；细节保留在 Git 历史与既有测试名中，不再作为开放问题。

---

## 4. EditMode 测试索引（入仓）

覆盖 Trigger 共边、SupportArea 集合、Player 两格外不加入、第三方不加入、无连锁、Save／Load 不重扫、多 Hex Site Footprint、派生格不得提前 Offer 等。详见 `Assets/Tests/EditMode` 下 `BattleAuthority*`／相关回归。

---

## 5. LevelTester 人工验收要点（封板已通过）

1. A 攻 B：仅共边相邻可触发；BattleArea = Defender Hex。
2. SupportArea = BattleArea + 共边邻格；邻格友军加入；两格外 Player 不加入、无 Manual。
3. 第三方邻格不纳入。
4. WorldMap 高亮 BattleArea／SupportArea 与规则一致。
5. 弹窗态 Save→Load：Locked 名单与 SupportArea 不变。

---

## 6. 本轮附带验收通过的体验调整（非独立 Phase）

| 调整 | 说明 |
|------|------|
| WorldMap Army／Character 管理列表 | 尺寸收紧并支持滚动 |
| WorldMap Zoom | 最大 Zoom In 范围扩大（Zoom Out 保持封板前口径） |
| Cheat Tools | 与 F10 HUD Hide 解耦；外层 UI 可直接进入 |

不扩写成新 Phase。

---

## 7. Hex topology Authority 修复（非 Phase 4 特补丁）

### 正式 Layout

- **Odd-R offset** + **pointy-top**
- 存储：`HexCoord` = Odd-R（Q=列，R=行）

### 旧错误

`HexCoord` 实际存 Odd-R，但 Neighbor／Distance 曾**直接按 axial** 计算 → 奇数行固定错误邻格 → SupportArea 向某一斜向多出一格，并连带造成 Player 误拉入、非共边位置可能提前触发 Battle。

### 正式修复

```
Odd-R → axial → Neighbor / Distance / CollectHexLine → Odd-R
```

Authority：`Assets/Scripts/Core/World/Hex/HexMath.cs`  
回归：`HexAdjacencyAuthorityTests`、`HexCollectHexLineAuthorityTests`

### 归类

属 **Hex topology Authority** 修复，**不是** Phase 4 业务特补丁。该修复同时校正：

- Battle SupportArea 邻接错误
- Player 被错误拉入战场
- 非真正共边位置可能提前触发 Battle

### CollectHexLine

**已在代码中按同一 Odd-R→axial 路径修复**（`HexMath.CollectHexLine` + `HexCollectHexLineAuthorityTests`）。**不**记为未收掉技术债。

---

## 8. Deferred / Future Regression（不阻塞 Phase 4）

因当前**缺少敌方主动攻击／AI 主动交战能力**，下列项无法完整人工制造场景：

| 项 | 标记 | 原因 |
|----|------|------|
| 敌军主动攻击我方时的 Retreat 人工验收 | **Deferred / Future Regression** | 无敌方主动攻击能力 |
| AI vs AI 主动发起接战／自动战人工验收 | **Deferred / Future Regression** | 无 AI 主动交战能力 |

- **不是** Phase 4 验收失败  
- **不要**为补验现在实现战略 AI  
- 未来实现相关 AI 后 **必须回归**这两项  

---

## 9. 变更日志

| 日期 | 内容 |
|------|------|
| 2026-08-28 | Phase 4 初版入仓 |
| 2026-08-28 | 多轮规则迭代（Initiator-centered／中心距离 ≤1 → SupportArea 集合；均已 superseded） |
| 2026-08-28 夜 | Trigger Committed Hex；PlayerHex Authority；删除 Snapshot seed 旁路；IncludedReason／SpatialGuard |
| 2026-08-28 | Hex topology Authority：Odd-R↔axial（Neighbor／Distance／CollectHexLine） |
| 2026-08-28 | **Phase 4 Accepted / Sealed**；Deferred Retreat／AI vs AI；附带 UX 调整记入 §6 |
