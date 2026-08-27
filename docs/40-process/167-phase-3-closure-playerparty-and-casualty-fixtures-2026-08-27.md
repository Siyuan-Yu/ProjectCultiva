# Phase 3 收口：Authority 重验、PlayerParty 跨图、试炼敌军与伤亡夹具（2026-08-27）

> 状态：**实现入仓 · 部分人工验收待签收**｜最后更新：2026-08-27  
> 前置：[166 Phase 3 FormalArmy Continuous World](./166-phase-3-formal-army-continuous-world-2026-08-27.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`

---

## 0. 本批目标

在 Phase 3 主体入仓（166）基础上，完成：

| 编号 | 内容 | 状态 |
|------|------|------|
| **A2** | FormalArmy Authority 第二轮：禁止静默抢 PlayerParty | ✅ 入仓 |
| **G16–G18** | Moving Army 成员 Presence 同步 + Debug 伤亡工具 | ✅ 入仓 |
| **H19** | Save/Load 中段 FormalArmy 恢复（沿用 Snapshot） | ✅ 已有 · 待手操 |
| **Legacy** | 旧 RTS 战斗入口标记未删 | ✅ 标记 |
| **PP-Follower** | PlayerParty Follower 跨 LocalMap 同步 | ✅ 入仓 |
| **Player Camp** | 主角临时营地独立 LocalMap | ✅ 入仓 |
| **Casualty Fixture** | 第三支试炼强匪 + 自动战必伤亡夹具 | ✅ 入仓 |

---

## 1. A2 Authority（第二轮 · 最终规则）

### 问题

第一轮实现 `activeControlledCharacterId` 后，`ArmyService` 仍在 Join/Create 路径上调用 `TryRemoveMember` + `ClearFollow`，会**静默把 Follower 踢出 PlayerParty**——与 RPG-First「玩家主动 Leave Party」冲突。

### 最终规则

| 规则 | 行为 |
|------|------|
| Active 角色 | **禁止** CreateArmy / AddMember |
| PlayerParty 成员（Active + Follower） | **禁止**被 FormalArmy 自动征召；须先 **Stop Follow / Leave Party** → Background → 再组军 |
| FormalArmy 服务层 | **删除**所有 `TryRemoveMember` / `ClearFollow` 自动转移 |
| 校验入口 | `ArmyAuthorityRules.TryValidateNotPlayerPartyMember` + `TryValidateNotActive` |

### 关键文件

- `ArmyAuthorityRules.cs` — `TryValidateNotPlayerPartyMember`、`ResolveActiveControlledCharacterId`
- `ArmyService.cs` — `TryValidateMemberCanJoinFormalArmy`（仅校验，不 mutating Party）
- `ArmyUiCommands.cs` — 候选列表过滤 `IsEligibleFormalArmyCandidate`
- `HostArmyFormPanel.cs` / `HostStrategicRosterQueries.cs` — UI 侧一致过滤

### EditMode

`FormalArmyPhase3AuthorityTests.cs` 增补：

- PlayerParty Follower 不可直接入军
- Leave Party 后可入军
- CreateArmy 含 Party 成员失败
- Active 不可入军

---

## 2. G16–G18：FormalArmy 旅行中成员 Presence 与 Debug

| 项 | 实现 |
|----|------|
| **G16** | `FormalArmyContinuousTravelService.AdvanceAll` 对 Moving Army 调用 `ArmyService.SyncNonLivingMembers` |
| **G17** | `HostFormalArmyDebugPanel`（F11）：Incap Leader/Member、Sync Casualties、成员 Presence 显示 |
| **G18** | 同上 Debug 面板 + EditMode 覆盖 |

---

## 3. H19：Save/Load

沿用 Phase 3 既有 `FormalArmySnapshotRestore` + Snapshot DTO Phase 3 字段。  
**手操步骤（LevelTester）：** F5 存档 → 旅行中途 F9 读档 → 确认 Army 位置/路径/成员 Presence 延续。

---

## 4. Legacy 战斗入口（仅标记）

以下路径仍为 Legacy Prototype，**未删除**，待 Phase 4 统一：

- `HostWorldMapPanel` — Attack/Enter 旧入口
- `ArmyHexPursuitService`
- `BattleOfferService` 部分旧链

---

## 5. PlayerParty Follower 跨 LocalMap（PP-Follower）

### 根因

`TravelingMembers` 仅含 Active；Follower 在 LocalMap 边缘跨格时不同步。

### 修复

| 类型 | 职责 |
|------|------|
| `PlayerPartyTransitionMembership` | 边界 Transition 前 `CaptureTravelingMembers`（完整 `party.Members`，排除 FormalArmy 成员） |
| `PlayerPartyHexTravelService` | Site path + `AdvanceDistanceBudget` 集成 |
| `PlayerPartyWorldMotion` | SiteDeparture + TravelPresentation |

### 测试

`PlayerPartyFollowerLocalMapTransitionTests.cs`

---

## 6. 主角临时营地独立 LocalMap

共用荒村 LocalMap 为 **bug/临时 workaround**，非设计。

| 项 | 值 |
|----|-----|
| SiteId | `test:site_player_camp` |
| LocalMapId | `base:map_player_camp` |
| Content | `Content/BaseGame/Data/Maps/player_camp_map.json` |
| Places | `Content/BaseGame/Data/LocalPlaces/player_camp_places.json` |
| 绑定 | `Ch01HexPrototypeMapBuilder.EnsureLevelTesterPlayerCampSite` — 营地固定独立 map，不再复用荒村 `localMapId` |

---

## 7. Prototype 三支测试山匪

LevelTester 使用 `base:hex_world_travel_mvp_30x15`（荒村 anchor 约 `(3,7)`）。

| 显示名 | StackId | FormalArmyId | 人数 | 位置（相对荒村） | 自动战 |
|--------|---------|--------------|------|------------------|--------|
| 荒村山匪 | `army:bandit_patrol_1` | `army:formal_bandit_patrol_1` | 4 | 南侧 `(Q+2, R+4)` | 正常 |
| 试炼弱匪（自动必胜） | `army:bandit_patrol_weak` | `army:formal_bandit_patrol_weak` | 1 | 东侧 `(Q+6, R)` | 夹具必胜 |
| 试炼强匪（自动伤亡） | `army:bandit_patrol_casualty_test` | `army:formal_bandit_casualty_test` | 3 | 西北（小图出界时回退边界内西北格） | 夹具必胜 + **必 1 人弥留或阵亡** |

### 放置修复（2026-08-27 晚）

**问题：** 第三支设计坐标 `(Q-4, R-2)` 在 30×15 小图上为 `(-1,5)` **出界** → Stack 注册但大地图不绘制。

**修复：** `Ch01HexPrototypeMapBuilder.ResolvePrototypeTestBanditHexesBelowHuangcun` 增加 `NorthWest` 方向解析 + 边界内回退；`Ch01FullHexMapTests` 增补 travel_mvp 用例。

### 伤亡夹具（试炼强匪）

| 项 | 说明 |
|----|------|
| 入口 | `AutoBattleCasualtyService.ApplyCasualtyTestFixtureDamage` |
| 触发 | `ArmyStackAdapter.IsCasualtyTestEnemyStack` + `BattleOfferService` 强制高胜率/必胜 |
| 效果 | 从接战名单随机选 **1 人**；约 35% 优先阵亡、65% 优先弥留；失败则 fallback 另一种状态 |
| 敌军属性 | 3 人筑基：攻 36 / 防 24 / 血 150 / 速 16 |
| 测试 | `AutoBattleCasualtyFixtureTests.CasualtyTestBandit_AutoWin_GuaranteesOnePlayerIncapacitatedOrKilled` |

---

## 8. 新增 / 主要改动文件索引

### Core

- `PlayerPartyTransitionMembership.cs`（新）
- `FormalArmyManagementSitePolicy.cs`（新）
- `WorldSiteFootprintLocationAuthority.cs`（新）
- `Ch01HexPrototypeMapBuilder.cs` — 三支山匪 Hex + 主角营地
- `Ch01ScenarioStrategicSetup.cs` — Seed 三支 + Position
- `ArmyStackAdapter.cs` — `EnsureBanditCasualtyTestArmy`
- `TestStrategicBootstrap.cs` — 强匪角色生成
- `AutoBattleCasualtyService.cs` — 伤亡夹具
- `BattleOfferService.cs` — 试炼敌军必胜路径

### Content

- `Content/BaseGame/Data/Maps/player_camp_map.json`（新）
- `Content/BaseGame/Data/LocalPlaces/player_camp_places.json`（新）

### Host

- `HostFormalArmyDebugPanel.cs` — G16–G18 Debug
- `HostBackgroundTravelDebugPanel.cs` — 旅行 Debug 扩展
- `HostWorldMapPanel.cs` — Legacy 标记相关

### Tests

- `FormalArmyPhase3AuthorityTests.cs`
- `PlayerPartyFollowerLocalMapTransitionTests.cs`
- `AutoBattleCasualtyFixtureTests.cs`（新）
- `Ch01FullHexMapTests.cs`
- `PlayerPartyContinuousWorldPhase2CTests.cs`

---

## 9. 人工验收清单（待签收）

### A2 Authority

1. Active 在组军候选 → **不可选/失败**
2. Follower **未** Leave Party → CreateArmy/AddMember **失败**
3. Follower Stop Follow → Background → 同 Site 组军 **成功**
4. 组军过程 **不** 静默改 PlayerParty 成员列表

### PP-Follower

5. 2+ 人 Party（1 Active + Follower）Wilderness 跨 Hex / 进 Site → Follower 同图可见

### 主角营地

6. TEST 主角营地点「进入近景」→ 加载 `base:map_player_camp`，**非**荒村 map

### 试炼敌军

7. 大地图可见 **三支** 紫色敌军（重进 Play 刷新）
8. 打「试炼强匪（自动伤亡）」→ 自动战胜利 + **我方恰好 1 人** 弥留或阵亡
9. 打「试炼弱匪」→ 自动必胜、无夹具伤亡

### G16–G18 / H19

10. F11：Moving Army Advance → 成员 Presence 跟 Army
11. F11：Incap Leader/Member、Sync Casualties 可用
12. F5/F9 中途存读 FormalArmy 旅行状态

---

## 10. Git / 下一里程碑

- **本提交：** Phase 3 收口 + 文档 + Content（**未整体封板**）
- **封板条件：** 166 F11 TEST 1–10 + 本节验收 1–12
- **下一 Phase：** Phase 4 Manual Battle Permission

---

## 11. 相关文档

- [166 Phase 3 FormalArmy Continuous World](./166-phase-3-formal-army-continuous-world-2026-08-27.md)
- [165 Phase 2D Background Travel](./165-phase-2d-background-character-world-travel-2026-08-26.md)
- [2K RPG-First](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)
- [114 LevelTester](./114-level-tester.md)
