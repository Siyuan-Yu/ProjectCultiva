# 152 · 战略 Faction / Formal Army / Capture 实现分期计划（2026-08-22）

> 状态：**Phase A–K 已实现 + 静态复核通过；Unity 运行时验证制作人暂缓（延期）**｜日期：2026-08-22（rev.3 + Phase E–K 实现）  
> 性质：**实现分期计划（非代码）** — 未来实施时 **一次只批准一个 Phase**；无制作人明确批准 **不得** 自动进入下一 Phase。  
> 产品真源：[2A 势力、军队、外交与战略占领](../20-systems/2A-factions-armies-diplomacy-and-capture.md)  
> 架构决策：[ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)  
> 代码审计：2026-08-22 只读审计（Character→WorldMap／PartyWorldPresence／ArmyStack／Faction／Travel／Capture／Battle Return；Current→Target Migration Map）  
> 关联 Prototype 过程：[138](138-world-strategic-battle-offer-plan-2026-08-17.md)～[153](153-lingering-remnant-macro-presentation-2026-08-22.md)  
> **编号说明：** 同目录另有 [152-worldmap-rts-click-discipline](152-worldmap-rts-click-discipline-2026-08-22.md)（RTS 点击纪律，已落地）；**本文件**为 2A Formal Army **实现计划**专用 slug。

---

## 0. 实施纪律（必读）

| 规则 | 说明 |
|------|------|
| **一次一刀** | 每次只实现 **一个 Phase** → EditMode／集成测 → Host 手操 → 更新 devlog／roadmap → **STOP** |
| **禁止 scope creep** | 各 Phase「禁止顺手」节所列内容，该 Phase 内 **零实现** |
| **禁止大爆炸** | **禁止** Phase 1 直接把 `WorldPresenceBoard` 键从 `EntityId` 全面替换为 `ArmyId`（审计方案 B） |
| **Formal Army 为领域真源** | `WorldAgentPresence` 在过渡期仅为 **兼容投影**；禁止 Army 位置与成员 Presence 三套并行互写 |
| **ArmyStack 不得永久双系统** | 允许 Adapter 过渡；最终 `MemberCount`／`CombatPower` 匿名模型 **必须退出** |
| **敌军真实 Character** | Formal Army 战斗闭环阶段起，测试敌军也使用 `MemberCharacterIds[]`，不再长期依赖匿名整数 |
| **Snapshot 延后** | Session 闭环先跑通；Schema 字段级设计 **DEFER** 到 Phase K |
| **内容／AI DEFER** | 不编 Ch01 具体势力；不实现 AI 组军／宣战／Retreat 路径 AI |

---

## 1. 已拍板迁移策略（不再讨论 A/B/C 候选）

### 1.1 总体路线

**Formal Army Domain + Compatibility Adapter**（审计方案 C 为主，吸收方案 A 的局部兼容）。

```text
Formal Army（领域真源）
  ArmyId / FactionId / LeaderCharacterId / MemberCharacterIds[] / StrategicState / StrategicPosition
        │
        ▼
ArmyMembership（Character 侧：最多一支 Army）
        │
        ▼
Compatibility Adapter（单向：Army → 投影）
        │
        ├── WorldAgentPresence（逐成员 bridge；非第二战略真源）
        ├── ArmyStackBoard（过渡：收敛为 Formal Army 的战略视图／兼容 ID）
        └── WorldTravelPathService / BattleOfferService 等（扩参／Adapter，非重写）
        │
        ▼
KEEP：WorldGraph / WorldTravel 算法 / StrategicTravelDriver / BattleOffer 链 / Encounter Return
```

### 1.2 核心约束：避免双位置真源

| 层级 | 身份 | 规则 |
|------|------|------|
| **真源** | `FormalArmy` + `StrategicPosition` | 谁在移动、在哪、何状态（AtNode／OnRoute／Garrisoned） |
| **投影** | `WorldAgentPresence` | 由 Adapter **从 Army 派生**；成员 Mode／Node／Route **不得** 独立下令改写 Army 真源 |
| **焦点** | `PartyWorldPresence` | 会话／镜头摘要；**Phase A–J 不重命名** |
| **过渡** | `ArmyStack` | 逐步降级为 Adapter 内部视图或 alias；禁止与 FormalArmy 长期双写 `MemberCount` |

**Adapter 防漂移机制（计划层，各 Phase 落地）：**

1. 所有战略移动 **唯一入口**：`ArmyTravelCommandService`（或同等 Formal Domain API）  
2. `WorldTravelService.StartTravel*` 对玩家单位 **禁止** 再接受裸 `EntityId` 列表（Phase D 起）  
3. 每次 Army 状态变更后 **同步投影**（Push）或 **惰性 Pull** 二选一，全工程统一；禁止混用  
4. EditMode 断言：`Army.StrategicPosition` 与 Leader 投影 Presence 一致；成员 Presence 仅反映成员是否在 Army 内且跟随 Army  

### 1.3 Capture：Owner 与 SettlementAuthority 并存

| 概念 | 职责 |
|------|------|
| `WorldNode.OwnerFactionId` | 战略政治归属（2A） |
| `SettlementAuthority` | LocalMap 管理特权（现有主管府 grant） |

Capture 完成链（目标）：

```text
全部 CaptureObjective HP=0 + Capture Zone 完成
  → Node.OwnerFactionId = 攻击方 FactionId
  → 若存在 SettlementAuthority → 同步／授予新 Owner 本地权限
```

`ControlCore*` 作为 **第一个 CaptureObjective 兼容实例**（**Phase H** 泛化，第一刀不 rename 全工程）。**Phase H 必须建立在 Phase G War Gate 之上**（见 §1.6、Phase G/H）。

### 1.4 FactionId 统一

全系统仅 **`FactionId`**（`base:faction_*`）：Character Membership、Army、Node Owner、Alliance Member、Vassal Overlord／Vassal、War Participant。  
**禁止** CharacterFactionId／StrategicFactionId／DiplomaticFactionId 三套映射。

### 1.5 敌我：War + Faction；Hostile 保留

| 主体 | 敌我来源 |
|------|----------|
| 正规 Faction 角色／Army | `FactionId` + `War` 状态 |
| 妖兽／野兽／无 Faction 剧情敌 | 保留 `PersonalityProfile` Hostile／Scripted |

**禁止** Phase 1 删除全部 Hostile 标签。

### 1.6 Legacy Character Travel（Phase B–D 过渡，非正式系统）

正式规则（2A／ADR-0024）：Character **不能** 单独跨 Node；必须 Character → Army → Army Travel。

在 **Phase B～C**（Army 表现已切换、Army Travel 尚未落地）期间，允许 **短期** 保留现有 Prototype 路径以回归保护 Ch01：

```text
Character → WorldAgentPresence → WorldTravel   （Legacy Path，非 Formal Army）
```

**Legacy Path 约束（写死）：**

| 约束 | 说明 |
|------|------|
| 非 Formal Army Domain | 不属于 Army 领域真源；不改变 ADR-0024 正式规则 |
| 非 Army 位置真源 | Formal Army **不得** 根据 Character Presence 反推自身位置 |
| 仅回归用途 | 暂时保证 Ch01／LevelTester 已验收 WorldMap 闭环可继续手操 |
| **有死亡日期** | **Phase D 验收通过后**，玩家正式入口 **必须** 关闭 Character 单独 Travel |

**Phase D — Legacy Exit 验收（必做）：**

```text
A = 未加入 Army 的 Character
Army1 = [B, C]

→ 对 A 下 WorldMap MoveOrder → Blocked
→ 对 Army1 下 WorldMap MoveOrder → Success
→ 正式玩家交互中不存在「点散装 Character 直接上路」
```

若底层仍保留 `StartTravel(EntityId)` 等旧方法供 Encounter／EditMode 回归：**仅 Internal Compatibility API**，**不得** 再作为玩家战略入口。

**禁止永久 Legacy Feature Flag：** 不得设计 `UseLegacyCharacterTravel = true/false` 作为长期产品配置。实现期间若需 **临时** 开发 Flag，必须在 **Phase D 结束前删除或强制关闭**，不进入 Release 配置。

### 1.7 Legacy Debt — Presence-Based Friendly Node（Phase B 引入，Phase H 退出）

`ArmyFormationNodePolicy` 在 **Ch01 Prototype 节点无 OwnerId** 时，允许「该 Faction 角色 AtNode 在场」作为 **临时** 组军合法节点判断。

| 属性 | 说明 |
|------|------|
| 性质 | **Legacy Scenario Compatibility only** — 不是正式领土规则 |
| 正式规则 | `Node.OwnerFactionId == Army.FactionId` |
| 禁止升级 | Presence ≠ Ownership；不得泛化为 Domain 规则 |
| **退出点** | **Phase H** Capture/Owner 正式化后，通用 Domain 层 **必须移除** presence-based friendly node |
| Ch01 若仍需要 | 仅允许保留在 **Ch01 / LevelTester Scenario Adapter**，不得留在 `ArmyFormationNodePolicy` 通用路径 |

---

## 2. 总迁移图（CURRENT → TARGET）

```text
CURRENT (Prototype)                          TARGET (2A + ADR-0024)
─────────────────────                        ─────────────────────

Character ──► WorldAgentPresence             Character ──► ArmyMembership
     │              │                               │
     │              ▼                               ▼
     │         WorldTravel (EntityId)          Formal Army ◄── FactionId
     │              │                               │
Enemy ArmyStack ────┼──► BattleOffer            StrategicPosition
 (MemberCount)     │                               │
     │              ▼                               ▼
     └──────► Encounter / Return              Travel Adapter ──► WorldTravel (KEEP)
                    │                               │
ControlCore ──► SettlementAuthority            Army vs Army ──► BattleOffer (MODIFY)
 (no Node Owner)         │                               │
                         ▼                               ▼
                  Residual / Linger              Battle Return (KEEP+ADAPTER)
                                                       │
                                                       ▼
                                               Minimal War + Attack Gate (Phase G)
                                                       │
                                                       ▼
                                               CaptureObjective (Phase H)
                                                       │
                                                       ▼
                                               Node.OwnerFactionId
                                                       │
                                                       ▼
                                               SettlementAuthority sync
                                                       │
                                                       ▼
                                               Alliance / Vassalage (Phase I)
                                                       │
                                                       ▼
                                               Captured / RetreatingArmy (Phase J)
                                                       │
                                                       ▼
                                               Strategic Snapshot (Phase K)
```

| 组件 | 判定 |
|------|------|
| WorldGraphBoard / Bootstrap | **KEEP** |
| WorldTravelService / PathService / StrategicTravelDriver | **KEEP + ADAPTER** |
| StrategicPursuitService / FollowService | **KEEP + ADAPTER** |
| BattleOfferService / InterruptQueue / ClockFreeze | **MODIFY**（参战方解析） |
| HostStrategicInterruptPresenter | **KEEP**（接 Offer／PostBattle） |
| StrategicEncounterResolveService / Spawner / Lingering* | **KEEP + ADAPTER** |
| CombatLifeStateService | **KEEP** |
| ArmyStack | **MODIFY → 收敛**（非永久双系统） |
| WorldAgentPresence | **MODIFY**（投影语义） |
| FactionDiplomacyBoard | **ADD 接线** |
| FormalArmyBoard / ArmyMembership | **ADD** |
| War / Alliance / Vassalage | **ADD** |
| CaptureObjective | **ADD**（自 ControlCore 泛化） |
| Snapshot Strategic 字段 | **DEFER → Phase K** |
| AI Decision | **DEFER** |

---

## 3. Phase 列表（依赖顺序）

| Phase | 名称 | 一句话 |
|-------|------|--------|
| **A** | Formal Faction Identity + Formal Army Domain + ArmyMembership | 领域真源与不变量；**无战略移动、无战斗改动** |
| **B** | 最小组军 UI + Garrison/Disband + WorldMap Army 头像投影 | AtNode 组军；地图只显示 Army 级头像 |
| **C** | ArmyStack 收敛 Adapter + 敌军真实 Character 测试 Army | 4 人 bandit 真实成员；消灭匿名整数双真源（敌军侧） |
| **D** | Formal Army Travel Adapter + **Legacy Character Travel 正式退出** | Army 移动；Phase D 后禁止玩家 Character 单独 Travel |
| **E** | Army vs Army BattleOffer + Chase Adapter | 接战双方解析为 Formal Army（**Phase G 前**仍可为 Prototype 攻击入口） |
| **F** | Auto / Manual / Battle Return 真实成员闭环 | 真实 Character 伤亡／弥留／Return |
| **G** | **Minimal Formal War + Attack Gate** | `DeclareWar`／`IsAtWar`／`CanAttack`；正规军事攻击门槛 |
| **H** | **CaptureObjective + Node Owner + SettlementAuthority** | 占点易主；**必须** 在 Active War 下才可军事攻点 |
| **I** | Alliance / Vassalage / Tribute | 外交条约层（**晚于** War／Capture 基础） |
| **J** | Captured / Escaped / RetreatingArmy / Landless 衔接 | 领域状态；概率／Retreat AI **DEFER** |
| **K** | Strategic Snapshot Upgrade | Schema 另文档；本轮不字段级设计 |

---

## Phase A — Formal Faction Identity + Formal Army Domain + ArmyMembership

> **状态：已实现 · STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

### 1. 目标

- 建立 **Formal Army 领域真源** 与 **ArmyMembership** 不变式  
- 建立测试用 **FactionId** 与 **Node OwnerFactionId** 种子（修复 `StrategicBootstrap` 清空 Owner 的 Prototype 行为——至少对测试图生效）  
- **尚不能** 在大地图移动 Army；**尚不能** 改变 BattleOffer 行为  

### 2. 数据真源变化

| 对象 | 身份 |
|------|------|
| `FormalArmy` / `ArmyBoard` | **新增真源**：ArmyId、FactionId、LeaderCharacterId、MemberCharacterIds[]、StrategicState、StrategicPosition |
| `ArmyMembershipComponent`（或等价） | **新增真源**：Character → ArmyId（0..1） |
| `FactionMembershipComponent.FactionId` | **KEEP 真源**（Character 势力） |
| `WorldNodeState.OwnerId` | **MODIFY 语义** → 文档与代码注释统一为 OwnerFactionId；测试节点恢复 Owner |
| `WorldAgentPresence` | **不变**；本 Phase 不写入 Army 投影 |
| `ArmyStack` | **不变**；演示栈暂留 |

**Adapter：** 无（本 Phase 只建 Domain API，不接 Travel／Battle）

**防双真源：** Army 创建／解散／改成员 **只** 通过 `ArmyService`；禁止 UI 直接改 Presence

### 3. 预计修改范围

| 族 | 文件／模块（代表） |
|----|-------------------|
| **ADD** | `Core/World/Strategic/FormalArmy*.cs`、`ArmyBoard.cs`、`ArmyMembershipComponent.cs`、`ArmyService.cs`、`ArmyInvariants.cs` |
| **MODIFY** | `SimulationWorld.cs`（挂 ArmyBoard）、`Entity.cs`／`EntityStore.cs`（Membership 组件）、`StrategicBootstrap.cs`（测试 Owner 种子）、`OpeningScenarioApplier`（可选测试 Faction） |
| **MODIFY** | `StrategicPhaseTests.cs` 或新 `ArmyDomainTests.cs` |
| **KEEP** | `WorldPresenceBoard`、`WorldTravelService*`、`BattleOfferService*`、`ArmyStack.cs`（不动语义） |

### 4. KEEP / MODIFY / ADD

| | |
|---|---|
| **KEEP** | WorldGraph、Travel、Battle、Encounter 全链 |
| **MODIFY** | StrategicBootstrap（Owner 种子）、Node Owner 字段注释 |
| **ADD** | Formal Army Domain、ArmyMembership、ArmyService、不变式校验 |

### 5. 禁止本 Phase 顺手做

- Army 移动、WorldMap 下令、BattleOffer、ArmyStack 收敛、Capture、War、Alliance、**任何 Army UI／Debug UI**、Snapshot

### 6. 验收标准（可操作）

**主要验收 = EditMode 自动化。** 下列 Domain 行为 **仅** 通过自动化测试验证，**不要求** Host 上玩家可操作 Army。

```text
测试夹具：TestFactionA、TestNodeA（Owner=TestFactionA）、Character A/B/C @ TestNodeA

1. A/B/C 均未组军 → ArmyService 查询无 Army
2. 选 A+B → CreateArmy(FactionA) → Army-1：Leader=A，Members=[A,B]
3. C 仍在 Node 但未入 Army
4. 再让 C 加入另一 Army → 失败（一 Character 一 Army）
5. 尝试混入 TestFactionB 角色 → 失败（禁止跨 Faction 混编）
6. 在 Owner≠TestFactionA 的 TestNodeB 尝试 CreateArmy → 失败
7. DisbandArmy → Membership 清空；Army 实体移除
8. GarrisonArmy（语义：AtNode 驻扎，不解散成员关系）→ 状态=Garrisoned；Members 仍在
9. Leader A 标记 Removed/Dead → PromoteLeader 递补 B
```

### 7. 自动化测试（Phase A 核心验收，必须全绿）

- `Army_Create_TwoMembers_SameFaction_Success`  
- `Army_Create_CrossFaction_Fails`  
- `Army_Membership_OneArmyPerCharacter`  
- `Army_Form_OnlyOnFriendlyNode`  
- `Army_Disband_ClearsMembership`  
- `Army_Garrisoned_DoesNotDisband`  
- `Army_LeaderFallback_OnLeaderInvalid`  
- `NodeOwner_TestFixture_NotClearedByBootstrap`  

### 8. 手工验收（Host — **仅回归，不验 Army 操作**）

**Phase A 不包含正式 Army UI。** 禁止新增 Army Debug Window／Panel／HUD／WorldMap Army 按钮／Console 菜单等仅为「手工验 Army Domain」的 UI。

Host 手工验收 **只** 证明新 Domain **没有把旧游戏搞坏**。启动 Ch01／LevelTester，确认：

- LocalMap 正常进入  
- Character 正常显示  
- 当前 Prototype WorldMap 仍正常  
- 当前 Travel 路径仍正常（Legacy Path 尚未改动）  
- 当前 BattleOffer 仍正常  
- Manual Encounter 仍正常  
- 弥留／Residual 链不因新增 Army Domain 而报错  

**不在 Phase A 验证：** 玩家正式组军 UI、Army 战略移动、WorldMap Army 头像。

### 9. 回归保护

- 现有 WorldMap 移动、BattleOffer、Manual Encounter、弥留／Residual、LocalMap Visibility **行为不变**  
- EditMode 现有 StrategicPhaseTests **全部仍绿**

### 10. 完成后文档更新

- `42-devlog.md`：Phase A 完成条目  
- `41-roadmap.md`：2A 实现进度勾选  
- 本文件 Phase A 节首状态改为「已落地／待验收」  

### 11. Phase A 合法停点状态

验收通过时 **允许** 且 **预期** 出现：

- Formal Army Domain 已存在；EditMode 可 CreateArmy（Leader=A，Members=[A,B] 等）  
- Host 上 **无** 正式 Army 组军 UI  
- Formal Army **尚不能** 战略移动  
- WorldMap 仍运行旧 Prototype Character Travel（Legacy Path，尚未切断）  

**禁止** 为「看起来已经能玩」而扩大 Phase A 范围。

### 可停点

**是。** 完成后 STOP，等待制作人批准 Phase B。

---

## Phase B — 最小组军 UI + Garrison/Disband + WorldMap Army 头像投影

> **状态：已实现 · STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

### 1. 目标

- 玩家可在 **己方 Node** 通过 **最小 UI** 创建／解散／驻扎 Army  
- 大地图 **只显示 Army 级头像**（Leader 代表）；未组军 Character **不可** 作为独立战略移动单位显示  
- 启动 **Army → WorldAgentPresence 投影 Adapter**（AtNode only；仍不移动）

### 2. 数据真源变化

| 对象 | 身份 |
|------|------|
| `FormalArmy.StrategicPosition` | **真源**（NodeId、AtNode/Garrisoned） |
| `WorldAgentPresence` | **投影**：Army 成员同步到 Leader Node；**隐藏**未组军玩家的独立战略头像 |
| `PartyWorldPresence` | **KEEP** 焦点摘要；Sync 优先 Army Leader |

**防漂移：** `ArmyPresenceAdapter.SyncFromArmy(armyId)` 为唯一写 Presence 入口；禁止 Host 直接 `SetAtNode` 玩家 Character

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `ArmyPresenceAdapter.cs`、`HostArmyFormPanel.cs`（最小 GUI） |
| **MODIFY** | `HostWorldMapPanel.cs`（DrawAvatars：Army 头像）、`HostFormalHud.cs`（入口）、`LocalMapVisibility.cs`（未组军不战略显示）、`WorldTravelService.SyncPartyFocus` |
| **KEEP** | Travel 算法、BattleOffer |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| WorldGraph、Battle 链 | HostWorldMapPanel、LocalMapVisibility、SyncPartyFocus | 组军 UI、Presence Adapter |

### 5. 禁止顺手做

- Army 跨 Node 移动、BattleOffer 改动、ArmyStack 删除、War、Capture

### 6. 验收标准

```text
TestNodeA 有 A/B/C
→ 未组军：大地图无 A/B/C 独立头像（或灰显不可选）
→ 组 Army1(A+B)：地图 1 个 Army 头像（Leader A）
→ C 未组军仍不可单独下令（Formal 路径；见 Legacy 例外 below）
→ Disband：头像消失；A/B 回 Node Resident（LocalMap 可进）
→ Garrison：Army 头像仍在；语义≠Disband
```

**Legacy Path（Phase B～D，§1.6）：** 本 Phase **尚未** 切断 Character 单独 Travel。若 Ch01 回归需要，**已组军 Character** 仍可通过 Legacy Path 移动时，须在 Phase B 文档／测试中明确为 **Legacy 行为**，且 **不得** 与 Formal Army 位置真源混写。Phase D 验收后 Legacy 玩家入口关闭。

### 7. 自动化测试

- `ArmyPresenceAdapter_SyncsMembersAtLeaderNode`  
- `WorldMap_UngroupedCharacter_NoIndependentPresence`  
- `ArmyPresenceAdapter_OnlyAdapterWritesPresence`  

### 8. 手工验收

- LevelTester → 打开大地图 → 组军 UI → 确认单头像  
- LocalMap 进入／离开不受影响  

### 9. 回归保护

- 敌军 `ArmyStack` 演示栈仍显示  
- BattleOffer／Encounter **未改**，仍可按旧路径触发  
- **Legacy Character Travel** 仍可支撑 Ch01 WorldMap 手操回归（§1.6；**非** 永久 Feature Flag）

### 10. 文档更新

- devlog、roadmap；补充 `PartyWorldPresence` vs `WorldAgentPresence` 注释（`WorldPresenceBoard.cs` 文件头 **仅注释**，非 rename）

### 可停点：**是**

---

## Phase C — ArmyStack 收敛 Adapter + 敌军真实 Character 测试 Army

### 1. 目标

- 引入 `ArmyStackAdapter`：**FormalArmy 为真源**，ArmyStack 为 **兼容视图**（BattleOffer 仍用 stackId 时映射到 FormalArmyId）  
- 替换 `StrategicBootstrap.SeedDemoArmies` 匿名栈为 **4 真实 Character**（BanditLeader/A/B/C）组成的 TestFactionB Army  
- `MemberCount` → **derived** from `MemberCharacterIds.Count`（敌军侧先落地）

### 2. 数据真源变化

| 旧 | 新 |
|----|-----|
| `ArmyStack.MemberCount` 手写 | **Derived**（只读或 Adapter 填充，禁止手改） |
| `ArmyStack.CombatPower` 整数 | **Derived** from members（Phase C 可先 `CombatPowerCalculator.ForEntity` 求和） |
| `army:bandit_patrol_1` | FormalArmy + 4 Character entities |

**双真源退出（本 Phase）：** 敌军 `MemberCount`／`CombatPower` 手写 **禁止** 在新测试 Army 上使用

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `ArmyStackAdapter.cs`、`TestStrategicBootstrap.cs`（夹具） |
| **MODIFY** | `StrategicBootstrap.cs`、`ArmyStack.cs`（标记 derived 字段）、`CombatPowerCalculator.cs` |
| **MODIFY** | Content 或 Opening 夹具 spawn 4 bandit characters |
| **KEEP** | BattleOffer 签名暂留 stackId |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| BattleOffer UI 流程 | StrategicBootstrap、ArmyStack 语义 | Adapter、测试敌军 Character |

### 5. 禁止顺手做

- 玩家 Army 移动、War 门槛、AutoResolve 改伤亡算法（Phase F）

### 6. 验收标准

```text
大地图山匪 = 1 Army 头像
→ Inspect：Members=4 真实 EntityId
→ ArmyStack.MemberCount == 4（derived）
→ CombatPower == sum(成员 ForEntity)
→ 修改 MemberCount 直接赋值路径不存在或 assert 失败
```

### 7. 自动化测试

- `ArmyStackAdapter_MemberCountDerived`  
- `ArmyStackAdapter_CombatPowerFromMembers`  
- `TestBanditArmy_FourRealCharacters`  

### 8. 手工验收

- 大地图仍可见山匪；点击信息展示 4 成员（调试 UI 即可）

### 9. 回归保护

- BattleOffer 仍能对该 stack／Army 触发  
- Residual／弥留逻辑不因 MemberCount derived 崩溃  

### 10. 文档更新

- devlog；2A 实现进度「敌军真实 Character 夹具」  

### 可停点：**是**

---

## Phase D — Formal Army Travel Adapter + Legacy Character Travel 正式退出

### 1. 目标

- **Formal Army** 为移动命令主体；复用 `WorldTravelService`／`WorldTravelPathService`／`StrategicTravelDriver`  
- 玩家 **正式入口** 不可再对未组军 Character 下宏观移动令（**Legacy Exit**）  
- Adapter 将 Army Route 状态 **投影** 到成员 `WorldAgentPresence`  
- **本 Phase 验收通过 = Legacy Character Travel 从玩家入口正式退出**（§1.6）

### 2. 数据真源变化

| 真源 | 投影 |
|------|------|
| `FormalArmy.StrategicPosition`（RouteId、Dest、Progress、RemainingTicks） | 成员 `WorldAgentPresence` Traveling/RouteAnchored |
| `ArmyTravelCommandService.MoveArmyTo(target)` | 内部调用现有 `StartTravelRouteSegment` 等 |

**防漂移：** Travel tick 后 **先** 更新 Army 真源，**再** `ArmyPresenceAdapter.SyncFromArmy`

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `ArmyTravelCommandService.cs`、`ArmyTravelAdapter.cs` |
| **MODIFY** | `HostWorldTravelDeparture.cs`、`HostWorldTravelConfirmPrompt.cs`、`WorldTravelPathService.cs`（Army 入口）、`HostWorldMapPanel.cs`（选中 Army） |
| **MODIFY** | `StrategicTravelDriver.cs`（可选 Army tick hook） |
| **KEEP** | `WorldTravelService` 核心算法、`AdvanceTravel` |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| WorldGraph、Route 算法、StrategicTravelDriver 编排 | Departure、PathService 入口、Map 选中 | ArmyTravelCommand、Adapter |

### 5. 禁止顺手做

- BattleOffer 参战方改造、War 门槛、Capture、Alliance

### 6. 验收标准

```text
【Army Travel】
Army1(A+B) @ TestNodeA
→ 下令至 TestNodeB
→ Army 头像沿 Route 移动；A/B 无独立移动头像
→ 到达 TestNodeB：Army AtNode；成员投影同步
→ 改目的地（打断）仍有效

【Legacy Exit — 必验】
A = 未加入 Army 的 Character
Army1 = [B, C]
→ 对 A 下 WorldMap MoveOrder → Blocked
→ 对 Army1 下 WorldMap MoveOrder → Success
→ 正式玩家交互中不存在「点散装 Character 直接上路」
→ 若底层保留 StartTravel(EntityId)：仅 Internal Compatibility API，非玩家入口
→ 不存在永久 UseLegacyCharacterTravel 产品配置；临时开发 Flag 已关闭/删除
```

### 7. 自动化测试

- `ArmyTravel_MovesViaWorldTravelAdapter`  
- `ArmyTravel_SyncsMemberPresence`  
- `ArmyTravel_UngroupedCharacter_MoveBlocked`  
- `ArmyTravel_Arrival_SyncPartyFocus`  
- `LegacyExit_UngroupedCharacter_PlayerMoveOrderBlocked`  
- `LegacyExit_ArmyMoveOrder_Succeeds`  

### 8. 手工验收

- RTS 点 Node／Route → 确认 → Army 移动；LocalMap 成员 Despawn 行为与现有一致  

### 9. 回归保护

- `StrategicFollowService`／`StrategicPursuitService` **尚未改**但不可崩溃  
- 敌军 ArmyStack Route 演示仍 Advance（`ArmyStackService`）  

### 10. 文档更新

- devlog；[139](139-world-map-rts-orders-2026-08-17.md) 增「Army 为下令主体」注记  

### 可停点：**是**

---

## Phase E — Army vs Army BattleOffer + Chase Adapter

### 1. 目标

- BattleOffer 参战方：**AttackerArmyId** vs **DefenderArmyId**（UI 仍可显示 Leader 名）  
- Chase／Follow：从「Entity 列表追 stack」改为「Army 追 Army」；内部仍用 `CombatPursuitStackId` **或** 映射到 DefenderArmyId  
- `BattleParticipantSnapshot` 记录 **MemberCharacterIds** 而非隐式 party 扫描

### 2. 数据真源变化

| 模块 | 变化 |
|------|------|
| `BattleOfferPending` | 增 `AttackerArmyId`；`ArmyStackId` → Defender 兼容映射 |
| `StrategicPursuitService` | 输入改为 ArmyId；Adapter 写 Entity CombatPursuit |
| `ArmyStackAdapter` | Offer 解析 stackId → FormalArmy |

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **MODIFY** | `BattleOfferService.cs`、`StrategicPursuitService.cs`、`StrategicFollowService.cs`、`HostWorldMapPanel.cs`（ExecuteAttackStack）、`BattleParticipantSnapshot.cs` |
| **KEEP** | `BattleInterruptQueue`、`StrategicClockFreezeService`、`HostStrategicInterruptPresenter` Offer UI 壳 |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| Offer UI、Queue、Freeze | BattleOfferService、Pursuit、Follow、Snapshot | 无（Adapter 已有） |

### 5. 禁止顺手做

- AutoResolve 伤亡、Manual Spawner 全改、War 宣战 UI、Capture

### 6. 验收标准

```text
Player Army1 @ Node 追击 TestBanditArmy
→ 到站弹 BattleOffer
→ Offer 显示双方 Army；Participant 名单 = 各 Army MemberCharacterIds
→ 可选 Auto/Manual 入口仍在（战斗结果可仍 stub）
```

### 7. 自动化测试

- `BattleOffer_BuildsFromArmyVsArmy`  
- `Pursuit_ArmyChase_ArrivesOffer`  
- `BattleParticipantSnapshot_RecordsMemberIds`  

### 8. 手工验收

- 组军 → 攻击山匪 → Offer 弹出；撤退／Queue 仍正常  

### 9. 回归保护

- 残留战场 Offer（153）仍可用  
- Lingering visit 逻辑需 **Adapter 回归测**  

### 10. 文档更新

- devlog；[138](138-world-strategic-battle-offer-plan-2026-08-17.md) Prototype→Army 注记  

### 可停点：**是**

---

## Phase F — Auto / Manual / Battle Return 真实成员闭环

### 1. 目标

- **双方**伤亡作用于 **真实 Character**（Lifecycle／WorldPresence）  
- AutoResolve：`CombatPower` 来自成员；敌我 deaths/incap **写 Character**  
- Manual：`StrategicEncounterSpawner` spawn **MemberCharacterIds** 内实体  
- Battle Return：`StrategicEncounterResolveService` 按真实成员恢复／Park 残留  
- **验收 slice：** Army A（真实）vs Army B（4 bandits）Auto **或** Manual 至少一条完整链

### 2. 数据真源变化

| 退出双真源 | 方式 |
|------------|------|
| `ArmyStack.IncapacitatedMemberCount` | → 从成员 Lifecycle **聚合展示**（derived）；不再独立递增 |
| `AutoBattleCasualtyService` 写 stack 整数 | → 写 Character + Adapter 刷新 derived |
| `StrategicEncounterSpawner` 抽象敌军 | → 按 DefenderArmy MemberIds spawn |

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **MODIFY** | `AutoBattleCasualtyService.cs`、`CombatPowerCalculator.cs`、`BattleOfferService.ResolveAuto`、`StrategicEncounterSpawner.cs`、`StrategicEncounterResolveService.cs`、`LingeringBattlefieldPartyService.cs` |
| **KEEP** | Freeze、PostBattle UI 壳、BattleAnchor 语义 |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| Resolve 链、Linger 链 | Auto/Manual/Spawner/Casualty | 无 |

### 5. 禁止顺手做

- Capture、War、Captured 战俘、Alliance、Snapshot

### 6. 验收标准

```text
Player Army vs Bandit Army
【Auto】胜 + 处决 → 4 bandit Character Dead/Corpse；宏观头像；栈 derived 一致
【Auto】败 → Player 成员 Incapacitated；Return Anchor 正确
【Manual】进场 → 4 敌实体可战；击杀/弥留 → End Battle → Park/Destroy 与 153 行为一致
```

### 7. 自动化测试

- `AutoBattle_RealMembers_CasualtiesOnEntities`  
- `ManualEncounter_SpawnsDefenderMemberIds`  
- `BattleReturn_ParksLingering_FromRealIncap`  
- `ArmyStackAdapter_DownedCountsDerivedFromMembers`  

### 8. 手工验收

- 完整打一场手动战 + 一场自动战；确认 Return／Residual／153 回归  

### 9. 回归保护

- 153 测例 **全部仍绿**  
- Reinforcement Optional／Mandatory 规则  
- Multi-pursuit retreat（153）  

### 10. 文档更新

- devlog；新建 **Phase F 验收记录**（可附 152 子节或独立 155-acceptance）  

### 可停点：**是**

---

## Phase G — Minimal Formal War + Attack Gate

### 1. 目标

- 建立正规 Faction **军事敌对行为** 的最小合法性基础（**范围极小**）  
- **取代** Prototype「点攻击即接战」— 在 **Phase G 落地后**，对正规 Faction 的军事攻击 Army **必须** 处于 Active War  
- **Phase F 已跑通的 Army vs Army 攻击链** 在 `DeclareWar` 后 **允许正常继续**  
- **保留** LocalMap `Hostile` 标签（非正规主体）

### 2. 数据真源变化

| 概念 | 说明 |
|------|------|
| `War` / `WarId` | **ADD** 运行时实体 |
| `Attackers` / `Defenders` | 参战 Faction 集合 |
| `Active` | War 进行中状态 |
| `IsAtWar(FactionA, FactionB)` | 最小查询 |
| `CanAttack(attackerFaction, defenderFaction)` | 军事攻击合法性（Army 接战入口） |
| `DeclareWar(A, B)` | 最小测试／玩家入口 |
| `FactionDiplomacyBoard` | **MODIFY**：从死代码变为 War 查询（**不含** Alliance 逻辑—留 Phase I） |

**依赖：** Phase F 战斗闭环已存在；本 Phase **只加 Gate**，不重写 BattleOffer／Encounter。

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `War*.cs`、`WarBoard.cs`、`WarGateService.cs` |
| **MODIFY** | `HostWorldMapPanel.cs`（ExecuteAttackStack 前校验）、`BattleOfferService.cs`（正规 Faction 攻击前校验） |
| **KEEP** | Phase F Auto/Manual/Return 链；Hostile personality |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| Battle 链、Hostile | Map/BattleOffer 攻击入口、Diplomacy 接线 | War 实体、Gate |

### 5. 禁止本 Phase 顺手做

- Alliance、Vassalage、Tribute  
- War Score、War Goal、Casus Belli、Peace negotiation、战争赔款  
- 强制 Truce、战后保护期、宣战冷却  
- Occupation／Controller、割地谈判  
- CaptureObjective、Node Owner 易主（**Phase H**）  

正式规则：**系统不自动生成战后保护期**（2A 已冻结）。

### 6. 验收标准

```text
【和平 — War = none】
FactionA、FactionB 未宣战
→ FactionA 无法军事攻击 FactionB 的 Formal Army（Blocked + 提示需宣战）
→ （Phase H 落地后）FactionA 也无法对 FactionB Node 发起军事攻点

【宣战 — Active War】
DeclareWar(A, B)
→ A 与 B 处于 Active War
→ Phase F 已验收的 Army vs Army 攻击链允许正常继续（接战／Auto／Manual）

【非正规】
无 Faction Hostile NPC：LocalMap 战斗不受影响
```

### 7. 自动化测试

- `WarGate_BlocksAttackWithoutWar`  
- `WarGate_AllowsAttackWhenAtWar`  
- `DeclareWar_SetsActiveWarBetweenFactions`  
- `IsAtWar_ReturnsCorrectState`  

### 8. 手工验收

- 最小 `DeclareWar` 测试入口（按钮或调试命令即可，**非** 正式外交 UI）  
- 未宣战攻击被挡；宣战后攻击山匪／测试 Army 可接战  

### 9. 回归保护

- Phase F 战斗闭环 **仍绿**  
- 153 残留战场再攻（在 Active War 或测试夹具下）  
- LocalMap Hostile 战斗  

### 10. 文档更新

- devlog；2A War 章节实现状态  

### 可停点：**是**

---

## Phase H — CaptureObjective + Node OwnerFactionId + SettlementAuthority 同步

### 1. 目标

- 泛化 `ControlCore*` → `CaptureObjective` 接口（第一实例仍主管府 workArea）  
- 多 Objective Node：**全部**完成 → `OwnerFactionId` 易主  
- 同步 `SettlementAuthority` 给新 Owner  
- Node Defense：**Resident Characters + Garrisoned Armies**（Formation **DEFER**）  
- **必须建立在 Phase G War Gate 之上**：军事占点 **仅** 在 Active War 下允许

### 2. 数据真源变化

| 真源 | 说明 |
|------|------|
| `CaptureObjectiveBoard` | **ADD**；ControlCore 注册为 Objective 实例 |
| `WorldNode.OwnerFactionId` | **真源**（战略政治归属） |
| `SettlementAuthority` | **MODIFY**：Capture 后 grant 给新 Owner（与 Owner **并存**，职责不同） |
| `WarGateService` | **MODIFY**：军事攻点入口校验 Active War |

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `CaptureObjective*.cs`、`NodeDefenseService.cs` |
| **MODIFY** | `ControlCoreService.cs`、`ControlCoreBoard.cs`、`HostControlCoreAssault.cs`、`WorldNodeState`、`WarGateService`（攻点门槛） |
| **MODIFY** | `StrategicNodeAccessService`（敌对 Node + War 联动） |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| 破门+站立节奏、Phase G War | ControlCore*、SettlementAuthority、WarGate 攻点 | CaptureObjective、NodeDefense |

### 5. 禁止顺手做

- Alliance、Formation 玩法、rename 全部 workArea、War 扩展（已在 G）

### 6. 验收标准

```text
【无 War — 禁止军事攻点】
FactionA Army 攻击 FactionB 拥有的 Node（含 CaptureObjective）
→ War = none
→ 不允许正式发起军事攻点链（Blocked）

【Active War — 允许攻点】
DeclareWar(A, B) 后
→ 允许进入 Capture 链（破门 + Capture Zone + 全部 Objective 完成）
→ Node.OwnerFactionId = FactionA
→ SettlementAuthority 同步／授予新 Owner 本地权限
→ 原 ControlCore PlayerControlled 语义迁移或映射正确
```

### 7. 自动化测试

- `Capture_BlockedWithoutWar`  
- `Capture_AllowedWhenAtWar`  
- `Capture_AllObjectives_TransfersNodeOwner`  
- `Capture_GrantsSettlementAuthorityToNewOwner`  
- `NodeDefense_CountsGarrisonedArmiesAndResidents`  

### 8. 手工验收

- 未宣战：无法对敌方 Node 主管府／测试 objective 军事占点  
- 宣战后：LocalMap 破门 + 站立 → 战略 Node 归属变化  

### 9. 回归保护

- 现有主管府玩法在 **未占点／非军事攻点** 场景仍可用  
- Phase G War Gate、Phase F Battle 链不受影响  

### 10. 文档更新

- devlog；`26` 占点相关注记  

### 可停点：**是**

---

## Phase I — Alliance / Vassalage / Tribute

### 1. 目标

- Alliance（独立 Faction 最多 1 个；成员战争绑定）  
- Vassalage（Overlord/Vassal；禁止套娃；Vassal 不独立入 Alliance）  
- Tribute 数据结构与最小结算 hook（**数值公式 DEFER**）

### 2. 数据真源变化

| ADD | 说明 |
|-----|------|
| `AllianceBoard` / `VassalageBoard` | 2A §16–28 简化第一版 |
| Opinion/Trust/Threat | 可先 **stub 常量**（UI 不展示曲线） |

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `Alliance*.cs`、`Vassalage*.cs`、`TributeService.cs`（stub） |
| **MODIFY** | `WarGateService`（Alliance 扩展）、`FactionDiplomacyBoard` |

### 4. KEEP / MODIFY / ADD

| KEEP | MODIFY | ADD |
|------|--------|-----|
| War、Army、Capture | DiplomacyBoard | Alliance/Vassalage/Tribute |

### 5. 禁止顺手做

- AI 外交决策、Opinion 公式、Trade、Landless 完整玩法

### 6. 验收标准

```text
FormAlliance(A,C) → 成功；A 再 FormAlliance(B) → 失败
Vassal(VassalB, OverlordA) → B 不可独立 Alliance
War(A, X) → C 与 X 敌对（绑定）
Tribute hook 可被调用（数值可 placeholder）
```

### 7–10. 测试／回归／文档

- EditMode：Alliance 唯一性、Vassal 约束、War 绑定（扩展 Phase G War）  
- 回归：**Phase G** War、**Phase H** Capture、Phase F 战斗  
- devlog、2A 外交节进度  

### 可停点：**是**

---

## Phase J — Captured / Escaped / RetreatingArmy / Landless 衔接

### 1. 目标

- 战后结算：**Surviving Defender** → `Captured` / `Escaped` 领域状态（`LifecycleState.Captured` **接线**）  
- `RetreatingArmy` 实体（**路径 AI DEFER**）  
- `LandlessFaction` 最小 hook（失土仍存活 Faction）  
- **概率公式 DEFER**

### 2. 数据真源变化

| ADD | 说明 |
|-----|------|
| `BattleAftermathService` | Capture/Escape 分配（规则占位） |
| `RetreatingArmy` | ArmyId + 成员 subset + 状态 Retreating |
| Landless | Faction 无 Node 仍可有 Character/Army |

### 3. 预计修改范围

| 族 | 模块 |
|----|------|
| **ADD** | `BattleAftermath*.cs`、`RetreatingArmy*.cs` |
| **MODIFY** | `StrategicEncounterResolveService.cs`、`CombatLifeStateService.cs` |

### 4. 禁止顺手做

- Retreat 路径 AI、Capture 概率 tuning、战俘 UI 大制作

### 6. 验收标准（最小）

```text
Manual 战结束 → 指定测试角色 state=Captured（脚本/调试触发）
→ Membership/Army 规则符合 2A（Captured 不可战斗等—按 2A 最小 subset）
→ Escaped 成员可形成 RetreatingArmy 记录（无需 AI 移动）
```

### 可停点：**是**

---

## Phase K — Strategic Snapshot Upgrade

### 1. 目标

- 战略层 **可存档／读档**（Session 闭环稳定后）  
- **本轮不做** 字段级 Schema；仅定义覆盖范围与迁移原则

### 2. 必须覆盖（范围级）

- FactionState、FormalArmy、ArmyMembership、Army StrategicPosition  
- Node OwnerFactionId、War、Alliance、Vassalage  
- 必要战后状态（Linger/Retreat 引用）  

### 3. 预计修改范围

`SnapshotService`、`WorldSnapshot`、`JsonSnapshotSerializer`、版本号升级 ADR（**另开 ADR／154-schema 文档**）

### 4. 禁止

- 在本 Phase 同时改 Army 领域语义  

### 可停点：**是**（且需 Schema ADR 先行）

---

## 4. 双真源退出计划

| 双真源 | 降级策略 | 退出 Phase | 最终形态 |
|--------|----------|------------|----------|
| Army 位置 vs `WorldAgentPresence` | Adapter 单向投影 | D 建立；D–F 强化 | 仅 FormalArmy.StrategicPosition 可写 |
| Army 位置 vs `ArmyStack` Route | Stack 由 Army Adapter 填充 | C 起 | ArmyStack 移除或纯 DTO |
| `MemberCount` vs `MemberCharacterIds.Length` | Derived | C（敌）F（全） | 删除 MemberCount 写入口 |
| `CombatPower` vs 成员战力 | Derived | C–F | 删除 CombatPower 写入口 |
| Character/Army/Node Faction | 统一 FactionId | A（域）**G**（War） | 无 StrategicOwner 别名 |
| Node Owner vs SettlementAuthority | 并存；Capture 同步 | **H** | Owner=政治；Authority=Local 权限 |
| Legacy Character Travel | Phase B–C 临时回归；D 玩家入口关闭 | **D** | 仅 Army Travel |
| `PartyWorldPresence` vs `WorldAgentPresence` | 文档澄清；不重命名 | B 注释 | 可选未来 rename Phase |
| Snapshot 无战略 | DEFER | K | StrategicSnapshot v2 |

**长期禁止：** FormalArmy + ArmyStack + StrategicArmy + ArmyGroup 四套并存（允许 **Adapter 类名**，不允许 **四套独立真源**）。

---

## 5. 第一阶段保护区（优先 KEEP）

以下 **只允许 Adapter／扩参**；无制作人批准 **不得** 重写核心：

- `WorldGraphBoard` / `WorldGraphBootstrap`  
- `WorldTravelService` / `WorldTravelPathService` / `StrategicTravelDriver`  
- Route 投影、`MoveOrder`、Chase/Interception **算法**  
- `BattleOfferService` / `BattleInterruptQueue` / `StrategicClockFreezeService`  
- `HostStrategicInterruptPresenter`  
- `StrategicEncounterResolveService` / `StrategicEncounterSpawner` / `LingeringBattlefieldPartyService`  
- `CombatLifeStateService`  
- BattleAnchor / Residual Battlefield / 153 宏观头像行为  

**若必须较大修改：** 在 Phase 计划内写理由 + 回归清单 + 制作人签字。

---

## 6. Recommended First Implementation Slice

**推荐：Phase A（且仅 Phase A）**

| 项 | 内容 |
|----|------|
| **为什么** | 零依赖 Travel/Battle/UI 大改；可独立 EditMode 验收；建立 **Formal Army 真源** 后所有 Adapter 才有锚点；风险最低 |
| **涉及文件族** | 新增 `Army*` Domain、`ArmyMembershipComponent`、`ArmyService`、`ArmyBoard`；轻改 `SimulationWorld`、`StrategicBootstrap`（测试 Owner）、测试项目 |
| **不涉及** | WorldTravel*、BattleOffer*、HostWorldMapPanel 移动、ArmyStack 删除、Capture、War、Snapshot |
| **自动测试** | §Phase A 所列 8 用例（**核心验收**） |
| **手工验收** | **仅 Host 回归**（LocalMap／WorldMap／Travel／BattleOffer／Encounter／弥留）；**不验 Army 操作** |
| **风险** | 与现有 `CharacterIds` party 列表概念重叠—**B 才 UI，D 才切 Travel** |
| **完成后可见** | EditMode 可验 CreateArmy/Disband/Garrison/Leader 递补；**Host 无 Army UI**；**Legacy Travel 仍可跑 Ch01** |

---

## 7. Scope 总表

| 系统 | 状态 | 预计 Phase |
|------|------|------------|
| Faction Identity / 统一 FactionId | ADD/MODIFY | A |
| Node OwnerFactionId | MODIFY | A（夹具）**H**（Capture 易主） |
| Formal Army Domain | ADD | A |
| ArmyMembership | ADD | A |
| Army 组军最小 UI | ADD | B |
| Legacy Character Travel（临时） | KEEP（B–C）→ EXIT（D） | B–D |
| WorldAgentPresence 投影 Adapter | ADD/MODIFY | B–D |
| ArmyStack 收敛 Adapter | MODIFY | C–F |
| 敌军真实 Character | MODIFY | C |
| Army Travel | MODIFY/ADAPTER | D |
| Chase / Follow | ADAPTER | E |
| Army vs Army BattleOffer | MODIFY | E |
| Auto / Manual / Return 真实成员 | MODIFY | F |
| **War / Attack Gate** | **ADD** | **G** |
| **CaptureObjective** | ADD/MODIFY | **H** |
| Alliance | ADD | I |
| Vassalage | ADD | I |
| Tribute | ADD（stub） | I |
| Captured / Escaped | ADD | J |
| RetreatingArmy | ADD（无 AI） | J |
| Landless Faction | ADD（hook） | J |
| Strategic Snapshot | MODIFY | K |
| Formation 阵法 | DEFER | Future |
| Node Defense 完整公式 | 部分实现 | H（接口）Future（数值） |
| AI 组军／宣战／Retreat 路径 | DEFER | Future |
| PartyWorldPresence rename | DEFER | Future |
| War Score / Casus Belli / Truce | DEFER | 禁止擅自加 |

---

## 8. 明确 DEFER 项

- Ch01 具体势力／角色／境界／守军配置  
- AI：组军、选人、宣战、求和、支援、Retreat 路由、Independence Desire  
- 数值：Tribute、Capture 概率、ArmyCapacity 上限 tuning、Opinion/Trust/Threat 公式  
- Formation 完整玩法  
- War Score、Casus Belli、强制停战、割地谈判、Controller、法理领土  
- Snapshot 字段级 Schema（Phase K 只定范围）  
- 飞书全量同步（若权限失败则跳过）  
- `PartyWorldPresence` / `WorldAgentPresence` **rename**  

---

## 9. 仍存在但不阻塞第一刀的问题

1. **Ch01 演示** 与 **TestFaction 夹具** 并存策略—Phase A 建议 Feature flag 或独立 test scenario  
2. **Legacy Character Travel** 在 Phase B–C 保留、**Phase D 必须退出玩家入口**（§1.6；禁止永久 Feature Flag）  
3. **ArmyStack ID** 与 **FormalArmyId** 映射命名—Phase C 统一，不阻塞 A  
4. **Reinforcement Optional** 与 Army 编制关系—Phase E/F 再定  
5. **153 手操待签** 与 2A 并行—不阻塞 Phase A  
6. **Phase E/F 在 Phase G 前** 攻击链可无 War Gate—**G 落地后** 正规攻击必须宣战

---

## 10. 审计引用（Implementation Gaps 摘要）

| Gap | 计划 Phase |
|-----|------------|
| Character 单独跨 Node | D 起禁止玩家入口；B–C Legacy 临时 |
| ArmyStack 匿名成员 | C–F 退出 |
| Diplomacy 零调用 | **G**（War）–I（Alliance）接线 |
| StrategicBootstrap 清空 Owner | A 修复夹具；**H** 全面 Capture 易主 |
| Capture 不改 Owner | **H** |
| 军事攻击无 War | **G** |
| LifecycleState.Captured 无赋值 | J |
| Snapshot 无战略 | K |

---

## 11. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-22 | 初版：制作人拍板 Migration=C+Adapter；A–K 分期；第一刀=Phase A |
| 2026-08-22 rev.2 | 审核后小修：War(G) 先于 Capture(H)；Legacy Travel B–D 与 D 退出验收；Phase A 收紧（自动化为主、Host 仅回归、禁 Debug UI） |
| 2026-08-22 Phase A | Formal Army Domain 实现；静态复核通过；Unity 验证 延期 |
| 2026-08-22 Phase B | 组军 UI + WorldMap Army 投影；静态复核通过；Unity 验证 延期 |
| 2026-08-22 Phase C–K | ArmyStack/Travel/War/Capture/Diplomacy/Snapshot v2；静态复核通过；Unity 验证 延期 |
| 2026-08-22 Final Closure | Legacy anonymous ArmyStack 退出正式路径；玩家 Character 战略入口关闭；Ch01 Scenario 外交隔离；Snapshot v1 明确拒绝；FINAL STATIC CLOSURE PASSED |
| 2026-08-22 Manual Acceptance UI | Host `StrategicAcceptancePanel`（F8／大地图入口）；War/Alliance/Vassalage/Army/Aftermath/Node Owner/Snapshot 最小验收 UI；**非 final UX** |

---

## 13. Manual Acceptance UI（2026-08-22）

> **状态：MANUAL ACCEPTANCE UI 已实现 · STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

| 验收入口 | 说明 |
|----------|------|
| `HostStrategicAcceptancePanel` | F8 或大地图「战略验收」；标注 DEVELOPMENT / ACCEPTANCE UI |
| `StrategicAcceptanceCommands` | DeclareWar / FormAlliance / BindVassalage / Army member / Tribute hook — 只调 Domain |
| Node Inspect | `OwnerFactionId` + CaptureObjective 状态 |
| Aftermath | Auto/Manual 战后 Captured / Escaped / RetreatingArmy |
| Snapshot | F5/F9 + Last Save/Load；v1 explicit reject 提示 |

**明确不做：** Ch01 附庸谈判剧情、正式外交 UX、Tribute 数值平衡 UI。

---

## 12. Final Closure（2026-08-22）

> **状态：A–K 已实现 · FINAL STATIC CLOSURE PASSED · MANUAL ACCEPTANCE UI 已实现 · UNITY VERIFICATION 延期**

| 收口项 | 结论 |
|--------|------|
| Legacy anonymous cultivator ArmyStack | `StrategicDayHandler` → `EnsureBanditScoutArmy`（FormalArmy + 真实 Character）；`MemberCount`/`CombatPower` Formal 链接不可写 |
| Legacy Character Travel 玩家入口 | `CanReceivePlayerMacroTravelOrder` + `WorldTravelPathService` 拦截；军团 UI 为唯一宏观移动主体 |
| Legacy Party Pursuit 玩家入口 | `StrategicPursuitService` / Host 仅 `BeginPursuitArmy` |
| Ch01 Generic Bootstrap 污染 | 迁出至 `Ch01ScenarioStrategicSetup` + `Ch01ScenarioProgressionHooks` |
| Snapshot | **v1 = UNSUPPORTED（explicit reject）**；**v2 = SUPPORTED** |
| Presence-based friendly node | 通用 Domain 已移除；仅 `Ch01ScenarioArmyFormationPolicy` |

---

**【代码修改】 Phase E–K 已实现**  
**【运行时数据修改】 NONE（Session 外）**  
**【是否已经开始实现】 YES — Phase A–K + Manual Acceptance UI 已落地，Unity 验证 延期**
