# 154 · Formal Army 大地图 RTS 收束 + 追击已知问题（暂缓）

> **日期：** 2026-08-23（v2 实现：2026-08-23 晚）  
> **Git：** `f6eb844` —「战略层 Formal Army 与大地图 RTS 追击/残留战场收束」  
> **状态：** 移动/攻击/预览/残留/名单 **部分已验收**；**追击 v2（移动目标）已实现 + PUR-01～11 测试已编码** — Unity EditMode / Host 手操 **待签收**  
> **相对：** [139 RTS 下令](139-world-map-rts-orders-2026-08-17.md)｜[141 追击贴敌](141-pursuit-stick-and-multi-melee-2026-08-18.md)｜[152 左右键纪律](152-worldmap-rts-click-discipline-2026-08-22.md)｜[153 弥留收束](153-lingering-remnant-macro-presentation-2026-08-22.md)｜[152 2A 实现计划](152-strategic-faction-army-capture-implementation-plan-2026-08-22.md)｜[ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)

---

## 1. 一句话

**Formal Army 已成为大地图战略移动/攻击真源**；右键攻击复用移动管线、下令后青色路径预览、残留战场不双倍刷怪、角色名单含弥留/尸体 — 均已编码。**追击对「会移动的敌军」仍不可靠**，制作人决定 **2026-08-23 起暂缓**，后续专章再改。

---

## 2. 本轮已交付（`f6eb844`）

### 2.1 Formal Army 战略层（Phase A–K 收束）

| 主题 | 做什么 | 真源 / 入口 |
|------|--------|-------------|
| **FormalArmy 真源** | ArmyId、成员、Route/Travel、Leader | `FormalArmy.cs` / `FormalArmyBoard` / `ArmyService` |
| **成员投影** | 禁止成员独立 WorldTravel；Presence 单向跟 Army | `ArmyPresenceAdapter` / `WorldTravelService.BlocksFormalArmy*` |
| **ArmyStack 兼容视图** | BattleOffer / 大地图仍用 stackId；链接 FormalArmyId | `ArmyStackAdapter` |
| **移动** | 节点跳、路中进度、多跳队列 | `ArmyTravelCommandService` / `ArmyTravelService` |
| **追击 Adapter** | FormalArmy 追击 tick 与单人追击分流 | `ArmyPursuitCommandService` / `StrategicPursuitService` |
| **战后同步** | 弥留/尸体不跟军团瞬移；伤亡脱离 | `ArmyPostBattleSyncService` |
| **Snapshot v2** | Strategic 状态 mandatory；v1 reject | `StrategicSnapshotHelper` / `WorldSnapshot` |
| **Ch01 边界** | 外交/战争与 Generic Bootstrap 隔离 | `Ch01ScenarioStrategicSetup` |

### 2.2 Host 大地图 RTS

| 主题 | 行为 | 文件 |
|------|------|------|
| **左选右令** | 左键选军团/人；右键下移动/攻击；右键不改选中 | `HostWorldMapPanel` + [152](152-worldmap-rts-click-discipline-2026-08-22.md) |
| **攻击 = 移动 + 追击** | 登记 `StrategicPursuitService` → `MoveArmyToStackAnchor` | `HostWorldTravelDeparture.BeginFormalArmyPursuit` |
| **路径预览** | **仅**下令后且仍选中该军团时画青色路径；**无**悬停预览 | `HostWorldMapPanel.SetArmyOrderPreview` |
| **Global Strategic UI** | 工具栏「角色」「军队」；Node 无组军入口 | `HostGlobalStrategicToolbar` / [153 清单](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md) |

### 2.3 残留战场与名单

| 主题 | 修复 | 文件 |
|------|------|------|
| **残留再进双倍敌人** | FormalArmy 链路复用真实成员；禁止再刷 generic grunt | `StrategicEncounterSpawner` |
| **角色名单** | 弥留/可见尸体进名单；**不可勾选**组军 | `HostStrategicRosterQueries` / `HostStrategicCharacterListPanel` |

### 2.4 已部分手操确认的行为

- ✅ 选军团 → 攻击 → 沿路移动（曾确认「移动攻击逻辑基本对了」）
- ✅ 不瞬移拽回青石荒村（同路 Clamp + 从当前进度续跑，见 `StartArmyTravelToRouteProgress` 注释）
- ✅ 下令后选中时青色路径预览；悬停不预览
- ✅ 手动进残留战场：4 真名 + 4 占位，不再 8 个重复「山贼1/2…」
- ✅ 弥留/尸体出现在角色列表且不可勾选

---

## 3. 追击 v2：移动目标 PursuitOrder（IMPLEMENTED · 待测）

> **2026-08-23 晚：** 按 Formal Army Moving-Target Pursuit v2 任务书实现；**普通 Army Move 保护区未改主链**。

### 3.1 根因（已修）

| # | 原问题 | v2 修正 |
|---|--------|---------|
| 1 | Traveling 时只 Clamp 到 stack 瞬时 progress | `ArmyPursuitTargetService.TryEnsurePursuitTravel`：同路追 chase endpoint，拓扑变才 `MoveArmyToTargetArmy` |
| 2 | 目标位置读 ArmyStack | **FormalArmy.StrategicPosition** 真源；`ArmyStackAdapter.SyncStackTravelFromFormalArmy` |
| 3 | `__route_progress__` 写死下令时 progress | 追击队列 leg 改为 `__route_pursuit__:` + 消费时解析目标当前位置 |
| 4 | ArmyStack / FormalArmy 双轨 Advance | `ArmyStackService` 跳过 FormalArmyId 链接栈；TravelDriver tick 后 Sync 全链接栈 |
| 5 | 相向交错无接战 | `ArmyPursuitTargetService.DetectSweptRouteContact`（仅 pursuit pair） |

### 3.2 新增 / 修改文件

| 文件 | 变更 |
|------|------|
| `ArmyPursuitTargetService.cs` | **新增** — MacroSignature、同路追击、 swept contact、动态 route leg |
| `ArmyPursuitCommandService.cs` | 改 — TargetArmy 真源同步 |
| `ArmyTravelCommandService.cs` | `MoveArmyToTargetArmy`、pursuit route leg |
| `StrategicPursuitService.cs` | AfterTravelTick army-to-army 接战 |
| `ArmyStackAdapter.cs` | `SyncStackTravelFromFormalArmy` / `SyncAllLinkedStacksFromFormalArmies` |
| `ArmyStackService.cs` | 链接栈不独立 Advance |
| `StrategicTravelDriver.cs` | FormalArmy tick 后 sync stacks |
| `HostWorldTravelDeparture.cs` | 攻击开拔 `MoveArmyToTargetArmy` |
| `ArmyPursuitMovingTargetTests.cs` | **新增** PUR-01～PUR-11 |

### 3.3 验收状态

| 项 | 状态 |
|----|------|
| PUR-01～PUR-11 EditMode | **STATIC TEST WRITTEN** — 待 `run-editmode-tests.ps1` |
| ATTACK-POS-01～07 EditMode | **STATIC TEST WRITTEN** — Attack 下令帧不瞬移 |
| ArmyPhaseD/E、StrategicPhase 回归 | **NOT RUN**（本机无 Unity batch） |
| Host 手操 CASE A～E | **NOT RUN** |
| RUNTIME ACCEPTED | **禁止** — 须制作人手操通过后 |

### 3.4 Future Strategic Vision Integration

> **制作人正式规则（2026-08-23）：** 本节锁定 Pursuit 与 **未来** Strategic Vision / WorldMap Fog of War 的关系。  
> **本轮仅文档；不预建 Vision API、不新增 Runtime 字段。**

#### CURRENT（开发阶段 · Strategic Vision 未实现）

Formal Army moving-target pursuit 语义：

```text
Attack Army → persistent PursuitOrder → TargetArmyId → 持续追踪 moving target
```

因 **Strategic Vision / WorldMap Fog of War 尚未实现**，当前允许 **开发阶段兼容行为**：

- Pursuit tick 可 **直接读取** Target FormalArmy 的实时 `StrategicPosition`（`FormalArmyBoard` / `ArmyPursuitTargetService`）。
- 这是 **临时全知追踪**，便于验收移动目标追击；**不是最终产品规则**。

**当前验收：** 不把「失去视野自动停止追击」列为 FAIL 项。

#### FUTURE（Strategic Vision / Fog of War 第一版）

当 WorldMap Strategic Vision / Fog of War 落地后，Pursuit **必须**受目标可见性约束：

| 条件 | 行为 |
|------|------|
| Target Army **当前可见**（在 Pursuer／Faction 有效战略视野内） | Pursuit **继续**；允许读取目标当前战略位置并改道 |
| Target Army **离开**有效战略视野 | **PursuitOrder 自动取消**；Pursuer 停止通过系统全知信息追踪 |

```text
Pursuit Tick
  → IsTargetVisibleToFaction(TargetArmyId, PursuerFactionId)?
       YES → continue tracking
       NO  → cancel pursuit
```

**禁止（Fog 下全知追踪）：** Target 已不可见，但 `PursuitController` 仍经 `TargetArmyId → FormalArmyBoard` 读取隐藏目标实时位置并自动改路 — 这会 **绕过 Fog of War**。

具体 Service / API 名称：**等 Strategic Vision 系统设计时再定**；本轮 **不预建接口**。

#### 明确不做（当前 & Vision 第一版）

| 项 | 状态 |
|----|------|
| Last Known Position 续追 | **不做** — Lost Vision → Pursuit Cancel（简单第一版） |
| `LastSeenTick` / `SearchOrder` / `InvestigateOrder` / Scout Pursuit | **不做** — 等 Fog / 侦察玩法专章再议 |
| WorldMap Fog rendering | **不做** |
| Scout / Detection runtime | **不做** |

是否未来增加「追到最后已知位置」→ **Fog of War / 侦察玩法讨论时再决定**。

#### Cross-reference TODO（Vision 系统设计时必回看）

当开始设计或实现 **Strategic Vision**、**WorldMap Fog of War**、**Army Detection** 时，**必须重新检查 FormalArmy Pursuit**，至少处理：

1. **Target visibility** — Pursuit tick 前合法性检查  
2. **Lost-vision pursuit cancellation** — 不可见即 `ClearPursuit`  
3. **Hidden Army position leakage** — 禁止 UI / Pursuit / BattleOffer 泄露不可见军位置  
4. **WorldMap Army presentation** — 仅可见军展示；与 Pursuit 状态一致  
5. **BattleOffer / pursuit legality after loss of contact** — 失去接触后不得凭空接战  

**相关文档：** [141 追击](141-pursuit-stick-and-multi-melee-2026-08-18.md) · [2A Army 规则](../20-systems/2A-factions-armies-diplomacy-and-capture.md) · [153 验收清单](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)

### 3.5 Attack-start teleport 根因与修复（2026-08-23）

**现象：** 路中 FormalArmy 点击 Attack 后，有时先瞬移到某 Node 再开始追击。

**根因（DOMAIN）：**

| # | 位置 | 问题 |
|---|------|------|
| 1 | `ReconcileArmyWithLivingMembers` | FormalArmy 已 `IsRouteAnchored`，但 leader Presence 仍为 `AtNode` 时，把军团 **降级成纯 AtNode**（清空 RouteId） |
| 2 | `BeginArmyRouteProgressTarget` 跨路分支 | 用 `ResolveArmyAnchorNodeId`（progress&lt;0.5 恒为 **FromNode**）当出口，路中 0.40 会朝错误端点开拔 |

**瞬移到的 Node 身份：** 多为 **Pursuer 当前 Route.FromNode**（或 path 规划首跳 Node），因 Reconcile 把路中位置抹成 AtNode(FromNode)。

**修复：**

- `ReconcileArmyWithLivingMembers`：`IsRouteAnchored` 时 **跳过**（FormalArmy 为真源）
- 跨路追击：新增 `ResolveRouteExitNodeToward`，按路径代价选 **ToNode/FromNode 出口**，再 `StartFromRouteAnchor` 从当前 progress 连续开拔

**测试：** `ArmyAttackPositionTests`（ATTACK-POS-01～07）

---

## 3-legacy. 暂缓项：追击仍有问题（BACKLOG · 已被 v2 取代）

> **决策（2026-08-23 午）：** 追击体验问题较多，**本轮不再继续改代码**；以下作为下一刀追击专项的输入。  
> **注：** 当晚 v2 已按本节方向实施，见 §3。

### 3-legacy.1 现象

- 对 **会沿路移动的敌军**（如山匪斥候 `army:bandit_patrol_auto`），我方 **不会持续跟着敌人路径改道**。
- 对 **驻路锚点** 的敌军，部分场景下曾可用（见 `Pursuit_RetargetsWhenStackMovesAlongRoute`），但与 **行军中敌军** 行为不一致。
- 整体追击：**跨路拦截、队列续跑、移动目标刷新** 仍不稳定；制作人主观感受「追击有很多问题」。

### 3.2 静态分析：高度可疑根因

| # | 位置 | 问题 |
|---|------|------|
| 1 | `ArmyPursuitCommandService.SyncFormalArmyPursuersToStack` | `army.IsTraveling == true` 时 **只** 调 `ClampArmyPursuitToStackAnchor`，**从不** 在 RouteId/目标变化时 `MoveArmyToStackAnchor` 整段改道 |
| 2 | `ClampArmyPursuitToStackAnchor` | 要求 `army.RouteId == stack.RouteId`；我方在 **节点跳/跨路** 行军时直接 return，追击冻结 |
| 3 | `ClampArmyPursuitToStackAnchor` | 移动敌军时 `RouteSegmentEndProgress` 与目标差 ≤0.02 即早退，敌人继续前进后可能 **长期不 retarget** |
| 4 | `TryContinueQueuedTravel` | 队列中 `__route_progress__` leg 的 progress **开拔时写死**；敌人在 node hop 期间移动，到站 leg 仍追旧进度 |
| 5 | `ArmyStack` vs `FormalArmy` | 链接栈（Bandit Scout）与 FormalArmy **双轨 Advance**；追击读 Stack 锚点，若未同步可能追到 stale 位置 |
| 6 | 单人 vs 军团不对称 | 单人 `WorldTravelService.StartTravelToStackAnchor` 每 tick 可整段改道；FormalArmy 路径更保守，移动敌差距更大 |

### 3.3 建议修复方向（未实施 · 供下轮）

1. **`SyncFormalArmyPursuersToStack` 三分支：**
   - 同路且 `RouteId` 一致 → `Clamp`（保留 tick 不重置）
   - 跨路 / 目标节点变化 / 队列不对准 → `MoveArmyToStackAnchor`（仅当 macro target 实质变化，避免每 tick Prepare）
   - 有 pending 且末段 route leg 对准该栈 → **刷新 leg 进度**，勿整段重开
2. **`Clamp` 对 `stack.IsTraveling`：** 敌人 progress **前移** 时强制 retarget，不用 0.02 早退
3. **`TryContinueQueuedTravel`：** 消费 route leg 时用 **当前 PursueStack  live progress**
4. **`ArmyStackAdapter.SyncStackTravelFromFormalArmy`：** 每 tick 追击前把链接栈位置跟 FormalArmy 对齐
5. **EditMode 测试：** 敌栈每 tick 推进 / 换路线时 FormalArmy 目标更新（`ArmyPhaseDTests` 扩展）

### 3.4 相关测试（现有 · 未覆盖移动敌全流程）

| 测试 | 覆盖 |
|------|------|
| `Pursuit_RetargetsWhenStackMovesAlongRoute_ThenOffersBattle` | 驻路锚点 **手动挪** progress（非 IsTraveling tick 推进） |
| `ArmyPursuit_SameRoute_ClampDoesNotResetToFromNodeEachTick` | 同路 Clamp 不每 tick 拽回 FromNode |
| `ArmyPursuit_CrossNode_StartsFormalArmyTravel` | 跨节点开拔 |
| **缺** | 敌 `IsTraveling` + 每 tick `StrategicTravelDriver` 推进 + 我方 FormalArmy 持续改道 |

---

## 4. 追击架构速查（现行代码）

```text
玩家右键攻击
  → HostWorldTravelDeparture.BeginFormalArmyPursuit
  → StrategicPursuitService.BeginPursuitArmy
  → ArmyTravelCommandService.MoveArmyToStackAnchor（首段开拔）

每 WorldTick（StrategicTravelDriver.AfterTravelTick）
  → ArmyStackService.AdvanceAll（敌军栈 tick）
  → ArmyTravelService.AdvanceAll（FormalArmy tick）
  → StrategicPursuitService.AfterTravelTick
       → SyncPursuersToStack
            → ArmyPursuitCommandService.SyncFormalArmyPursuersToStack
                 · IsTraveling → ClampArmyPursuitToStackAnchor ONLY
                 · HasPendingLegs → TryContinueQueuedTravel
                 · else → MoveArmyToStackAnchor
       → CollectPartyReadyToEngageStack → BattleOffer
```

**单人追击（非 FormalArmy 或成员散装）** 仍走 `WorldTravelService.StartTravelToStackAnchor`（行为更激进）。

---

## 5. 关键文件索引

| 文件 | 职责 |
|------|------|
| `HostWorldTravelDeparture.cs` | 攻击开拔；禁止开拔当下 Sync 拽回 FromNode |
| `HostWorldMapPanel.cs` | 选军团、右键、青色路径预览 |
| `ArmyPursuitCommandService.cs` | FormalArmy 追击 tick 同步 |
| `ArmyTravelCommandService.cs` | Move/Clamp/队列/`MoveArmyToStackAnchor` |
| `StrategicPursuitService.cs` | 追击名单、AfterTravelTick、SyncPursuersToStack |
| `StrategicTravelDriver.cs` | Tick 顺序：Stack → FormalArmy → Follow → Pursuit |
| `ArmyStackAdapter.cs` | FormalArmy ↔ ArmyStack 链接视图 |
| `StrategicEncounterSpawner.cs` | 残留/FormalArmy 接战实体 |
| `HostStrategicRosterQueries.cs` | 角色名单（含弥留/尸体） |

---

## 6. 验收状态（2026-08-23）

| ID | 项 | 状态 |
|----|-----|------|
| RTS-01 | 选军团右键移动 | ✅ 已编码 · 部分手操 OK |
| RTS-02 | 选军团右键攻击 = 追击移动 | ✅ v2 已实现 · **EditMode/手操待签收** |
| RTS-03 | 下令后青色路径预览（无悬停） | ✅ |
| RTS-04 | 残留战场不双倍 generic 敌人 | ✅ |
| RTS-05 | 名单含弥留/尸体不可勾选 | ✅ |
| RTS-06 | 追击不弹到站、到站弹接战 | ✅（追上时） |
| RTS-07 | 失去视野自动停止追击 | **DEFERRED** — REQUIRES STRATEGIC VISION / FOG OF WAR（见 §3.4） |
| 153 | Global Strategic 双入口 | IMPLEMENTED · Unity 手操 DEFERRED |

**签注（追击 v2）：** 代码+测试已提交 — _______________　**日期：** 2026-08-23

---

## 7. 下一步（优先级）

1. **Unity EditMode** — `ArmyPursuitMovingTargetTests` + `ArmyPhaseDTests` / `ArmyPhaseETests` / `StrategicPhaseTests`
2. **Host 手操** — CASE A～E（154 §3.3）
3. **通过后** — 更新 141、153 checklist、42-devlog、41-roadmap；RTS-02 标 RUNTIME ACCEPTED

---

## 8. EditMode 测试入口

```powershell
# 需关闭已打开同一项目的 Unity 编辑器
.\tools\run-editmode-tests.ps1
```

相关类：`ArmyPursuitMovingTargetTests`（PUR-01～11）、`ArmyAttackPositionTests`（ATTACK-POS-01～07）、`ArmyPhaseDTests`、`ArmyPhaseETests`、`StrategicPhaseTests`（含 `Pursuit_*`）。
