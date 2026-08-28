# Phase 3：FormalArmy Continuous World + RPG-First Authority（2026-08-27）

> **状态：Accepted / Sealed（正式封板）**｜封板日期：2026-08-28（用户正式确认）  
> 实现完成 · EditMode 用例已入仓 · 核心能力已在 LevelTester 持续使用及 Phase 4 开发／人工验收中实际验证  
> 产品契约真源：[2K §7–8 FormalArmy](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`（`PlayableHostBootstrap`；**不用** PlayableHost）  
> **说明：** 原计划 F11 TEST 1–10 逐条签字表未单独归档；不再作为阻塞项。封板依据为用户确认 + 后续实际运行验证，**非**本轮 Cursor 独立执行 Unity 人工验收。

---

## 0. 目标与边界

| 项 | 说明 |
|----|------|
| **Goal** | FormalArmy 迁移到 Continuous WorldPosition；成员 Presence 从 Army 派生；RPG-First Authority 互斥 |
| **真源** | `FormalArmy.WorldMotion.WorldPosition`；`CurrentHex` 等 legacy 字段经 `SyncLegacyFromWorldMotion()` 兼容旧读者 |
| **旅行** | 复用 Phase 2C/2D 语义：Site Departure 真实 Travel、Footprint Hex → canonicalize `AtWorldSite` |
| **命令** | **仅玩家 Order**；无 Autonomous AI 驱动 Army 移动 |
| **Must Not Break** | Army vs Army／Site Capture／War 链；Snapshot v6 向后兼容 |
| **Explicitly Out of Scope** | Battle Authority 改造、新战斗系统、Autonomous AI、FormalArmy WorldMap Marker 表现层 |

---

## 1. 子阶段进度

| 子阶段 | 内容 | 状态 |
|--------|------|------|
| **3A** Authority / Membership | Party／Background／Army 互斥；Active 禁止入军；Follower 入军自动退出 Party | ✅ 入仓 |
| **3B** Army WorldLocation | `FormalArmyWorldMotion`：`AtWorldSite`／`AtWorldPosition` + `AutoTravel` | ✅ 入仓 |
| **3C** Continuous Travel | `FormalArmyContinuousTravelService`：距离预算推进、Site Departure、Destination Canonicalization | ✅ 入仓 |
| **3D** Member Presence | `FormalArmyMemberPresenceSync`：成员 `WorldPresence` 从 Army 派生 | ✅ 入仓 |
| **3E** Create / Roster / Disband | `ArmyService`：仅 friendly `AtWorldSite` 组军；Wilderness 禁止 Disband／改编制 | ✅ 入仓 |
| **3F** Casualty / Leader | Leader 自动顺位；伤亡脱离保留当时 Location（沿用既有 Domain） | ✅ 沿用 |
| **3G** Save / Load | `FormalArmySnapshotRestore` + Snapshot DTO Phase 3 字段；旧档 `CurrentHex` 回退 | ✅ 入仓 |
| **3H** Attack Authority 审计 | 未改 Battle Offer／接战链；仅位置真源迁移 | ✅ 审计 |
| **3I** Debug + EditMode | F11 `HostFormalArmyDebugPanel`；`FormalArmyPhase3AuthorityTests` | ✅ 入仓 |
| **3J** 收口（167） | A2 第二轮 Authority；PP-Follower 跨图；主角营地；试炼三军 + 伤亡夹具 | ✅ 入仓 |

**并行收口：PresenceHex == AnchorHex**

- `WorldSite.EnsurePresenceHexValid()` 强制 `PresenceHex = AnchorHex`
- Content 加载／Editor／Save／Validation 同步
- 30×15 测试世界：`base:site_huangcun` (4,7)→(3,7)；`base:site_zhuangyuan` presenceR 4→5
- Ch01 等旧 Content 仅加载时修正，未改 JSON 源文件

---

## 2. 核心类型

| 类型 | 职责 |
|------|------|
| `FormalArmyWorldMotion` | Army 级 `AtWorldSite`／`AtWorldPosition` + `BeginAutoTravel`／`BeginSiteDepartureTravel` |
| `FormalArmyContinuousTravelService` | `MoveArmyToHex`／`MoveArmyToWorldSite`／`AdvanceAll`；Site Departure；Footprint canonicalize |
| `FormalArmyMemberPresenceSync` | 旅行／静止时成员 Presence 跟 Army |
| `FormalArmyWorldLocationQuery` | 位置解析；friendly Site 判定 |
| `ArmyAuthorityRules` | Active 角色禁止编入 Army |
| `FormalArmySnapshotRestore` | Snapshot 恢复 + legacy `CurrentHex` 兼容 |
| `ArmyHexTravelService` | 对外 API 委托连续旅行服务（替代离散 tick 步进） |
| `FormalArmyHexWorldPositionResolver` | 优先 `WorldMotion.WorldPosition` |
| `HostFormalArmyDebugPanel` | **F11** 开发验收：组军／解散／Travel To Hex／Site／Advance Ticks |

---

## 3. Authority 规则（Phase 3A）

| 规则 | 实现 |
|------|------|
| Active 不可入军 | `ArmyAuthorityRules` + `ArmyService.CreateArmy` |
| Follower 入军 | **禁止**静默踢 Party；须玩家主动 Leave Party 后再组军（见 [167](./167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)） |
| Background Travel 中 | `CancelTravelIfAny` 后入军 |
| Party / Army 互斥 | 同一 Character 不可同时在 Party 与 Army |
| Wilderness | 禁止 `DisbandArmy`、禁止改 Roster（沿用 Domain 校验） |
| 组军地点 | 仅 friendly `AtWorldSite`；成员须同 Site |

---

## 4. 旅行语义（对齐 Phase 2D）

- **Site 离开：** `BeginSiteDepartureTravel`；保持 `AtWorldSite` 直至跨过 Boundary；FootprintCenter → BoundaryEntry 真实距离
- **Travel To Hex 命中 Footprint：** canonicalize 为 `AtWorldSite(siteId)`
- **Travel To Site：** 真源 `WorldSiteId`；Approach Hex 确定性解析
- **推进：** `PlayerPartyHexTravelService.WorldUnitsPerTick` 同源距离预算；`ArmyHexTravelService.AdvanceOneTick` 已委托连续服务

---

## 5. Snapshot 扩展（v6 最小扩展）

`FormalArmySnapshotDto` 新增（旧档缺省时从 `CurrentHex` 回退）：

| 字段 | 含义 |
|------|------|
| `LocationKind` | `AtWorldSite` / `AtWorldPosition` |
| `SiteId` / `WorldX` / `WorldY` | 连续位置 |
| `DestinationSiteId` | Site 目的地 |
| `CurrentOrderKind` | Travel 命令类型 |
| `SegmentIndex` / `SegmentProgress` | 中途路径进度 |

恢复入口：`FormalArmySnapshotRestore.Apply`

---

## 6. EditMode 测试

| 测试 | 覆盖 |
|------|------|
| `FormalArmyPhase3AuthorityTests.ActiveCharacterCannotJoinArmy` | Active 拒绝 |
| `FollowerJoinsArmyAfterLeavingPlayerParty` | Follower 可入军 |
| `CannotFormArmyWithMembersAtDifferentSites` | 同 Site 组军 |
| `ArmyCannotDisbandInWilderness` | Wilderness 禁解散 |
| `TravelToHexInsideFootprintCanonicalizesToWorldSite` | Footprint canonicalize |
| `ArmyWorldPositionTravelIsContinuous` | 连续位移非 teleport |
| `SnapshotRoundtripPreservesArmyWorldMotion` | Save/Load 中途旅行 |

用例已入仓；以仓库测试为准。未在本轮单独报告 Editor 全绿数字。

---

## 7. 封板验收记录

### 核心目标（已完成）

| 目标 | 状态 |
|------|------|
| FormalArmy 不再是角色世界旅行的前置条件 | ✅ |
| PlayerParty／普通角色独立世界旅行 Authority | ✅ |
| FormalArmy 收敛为军事层对象 | ✅ |
| Army Continuous WorldPosition / Travel | ✅ |
| Army 成员 Presence 同步 | ✅ |
| Army Save / Load | ✅ |
| Army 组建／解散／Authority 边界 | ✅ |
| Active Character 不可直接被塞入 FormalArmy | ✅ |
| PlayerParty / FormalArmy Authority 分离 | ✅ |
| Army Marker / Strategic Selection / Attack Order 等后续收口 | ✅（Phase 4 前已依赖） |
| Snapshot 生命周期相关问题 | ✅ 已修复 |

### 验证方式

- 核心功能在 **LevelTester 持续使用** 及 **Phase 4 开发／人工验收** 过程中实际验证
- **2026-08-28 用户正式确认** Phase 3 封板
- 原计划 **F11 TEST 1–10** 逐条签字表未单独归档 → **不再作为阻塞项**
- **不**将「用户确认封板」表述为 Cursor 独立完成 Unity 人工验收

### 历史清单（参考 · 非阻塞）

166 原 F11 TEST 1–10 与 167 验收 1–12 仍可作为回归参考，但**不**再阻止封板。

### Backlog / Deferred（不拉回 Phase 3）

- FormalArmy WorldMap Marker 连续表现
- Autonomous AI Order
- 更复杂 Army AI、主动战争、多方战争、Army Capacity 等 → 未来 Phase / Backlog

---

## 8. 已知问题 / 编译修复（2026-08-27）

| 问题 | 修复 |
|------|------|
| `StrategicSnapshotHelper` CS0136 `motion` 重名 | Capture 循环内改为 `armyMotion` |
| `FormalArmyContinuousTravelService` CS1501 | `TryGetActiveSegmentWorld(hexSize, …)` 三参数签名 |
| `HostFormalArmyDebugPanel` CS0246 | 补 `using XianXia.Core.World` |

---

## 9. 封板状态

- **Phase 3 = Accepted / Sealed（2026-08-28）**
- 实现 + 收口（167）已完成；Phase 4 人工验收与实际运行未暴露需重开 Phase 3 的阻塞问题
- **下一 Phase：** Phase 4 已封板；Phase 5 **Not Started**

---

## 10. 相关文档

- [167 Phase 3 收口](./167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md) — A2 重验、PP-Follower、试炼敌军、伤亡夹具
- [165 Phase 2D Background Travel](./165-phase-2d-background-character-world-travel-2026-08-26.md) — Site Departure／Canonicalization 先例
- [164 Phase 2C Surface Exit](./164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)
- [163 RPG-First 迁移计划](./163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)
- [41-roadmap](./41-roadmap.md)
