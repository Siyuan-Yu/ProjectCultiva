# Phase 3：FormalArmy Continuous World + RPG-First Authority（2026-08-27）

> 状态：**实现入仓 · 待人工验收**｜最后更新：2026-08-27  
> 产品契约真源：[2K §7–8 FormalArmy](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`（`PlayableHostBootstrap`；**不用** PlayableHost）

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
| **3I** Debug + EditMode | F11 `HostFormalArmyDebugPanel`；`FormalArmyPhase3AuthorityTests` | ✅ 入仓 · **待跑通／手操** |
| **3J** 收口（167） | A2 第二轮 Authority；PP-Follower 跨图；主角营地；试炼三军 + 伤亡夹具 | ✅ 入仓 · **待手操** |

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

**待确认：** Unity EditMode 全绿（本地编译已通过 CS 修复）

---

## 7. 人工验收清单（F11 · LevelTester · 待签收）

1. **TEST 1** 在 friendly Site 选 Follower → Create Army → 确认 Army 位于 Site、`WorldMotion.AtWorldSite`
2. **TEST 2** Active 在候选列表 → Create Army 应失败
3. **TEST 3** Travel To Hex（Wilderness）→ Advance Ticks → `AtWorldPosition` 连续变化
4. **TEST 4** Travel To Hex（敌方 Footprint 内格）→ 到达 `AtWorldSite`（非 Footprint 内浮点坐标）
5. **TEST 5** Travel To Site → Advance → 到达目标 Site
6. **TEST 6** 从 Site 出发 Travel → 需 Advance 才离开（非 instant snap）
7. **TEST 7** 旅行中 Disband → 应失败
8. **TEST 8** Site 上 Disband → 成员回 `AtSite` Presence
9. **TEST 9** Save/Load 中途旅行 → 位置与路径延续
10. **TEST 10** 成员 WorldPresence 跟 Army 移动（Host HUD／Debug 可查）

**Deferred：** FormalArmy WorldMap Marker 连续表现、Autonomous AI Order、Battle Authority Phase 4

---

## 8. 已知问题 / 编译修复（2026-08-27）

| 问题 | 修复 |
|------|------|
| `StrategicSnapshotHelper` CS0136 `motion` 重名 | Capture 循环内改为 `armyMotion` |
| `FormalArmyContinuousTravelService` CS1501 | `TryGetActiveSegmentWorld(hexSize, …)` 三参数签名 |
| `HostFormalArmyDebugPanel` CS0246 | 补 `using XianXia.Core.World` |

---

## 9. Git / 下一里程碑

- **本提交：** Phase 3 实现 + PresenceHex 收口 + 文档（**未封板**）
- **封板条件：** 上节 F11 TEST 1–10 人工通过 + EditMode 全绿
- **封板后：** 更新本文档状态 → `人工验收封板`；roadmap Phase 3 勾选；可选更新 2K §13 Deferred 列表
- **下一 Phase：** Phase 4 Manual Battle Permission（远方 Auto；≤1 Hex 介入）

---

## 10. 相关文档

- [167 Phase 3 收口](./167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md) — A2 重验、PP-Follower、试炼敌军、伤亡夹具
- [165 Phase 2D Background Travel](./165-phase-2d-background-character-world-travel-2026-08-26.md) — Site Departure／Canonicalization 先例
- [164 Phase 2C Surface Exit](./164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)
- [163 RPG-First 迁移计划](./163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)
- [41-roadmap](./41-roadmap.md)
