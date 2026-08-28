# 169 — Snapshot Faction / Test Entity 生命周期审计（2026-08-28）

> **场景：** LevelTester · `vs04_slot0.json` · Save → Stop Play → Play → Load  
> **范围：** 主角团 Faction Membership、Prototype 测试山贼、ArmyStack 展示链、LevelTester Test Fixture  
> **前置：** [162 Snapshot v6 JSON](162-pure-hex-final-audit-and-snapshot-v6-json-2026-08-24.md)、[114 LevelTester](114-level-tester.md)

---

## 背景

人工验收发现两类高度相关的跨 Play Session 问题：

1. **主角 / 同伴甲 / 同伴乙**：Save 前 Faction = 主角团，Load 后变为 **无归属 / None**。
2. **LevelTester 测试山贼**（BanditLeader 等）：Save 时存在，Load 后 **从世界地图 / 运行时消失**。

本轮要求：**不做**「山贼 special case」「主角团 special case」补丁，而是统一审计 **Snapshot Dynamic / Test Entity 生命周期**。

---

## 一、「主角团」是什么

| 项 | 值 |
|---|---|
| **FactionId** | `base:faction_player`（`StrategicFactionCatalog.PlayerFactionId`） |
| **DisplayName** | Catalog 硬编码 → **主角团** |
| **正式 Faction JSON** | **无** — `PLAYER FACTION IS NOT CONTENT-DEFINED` |
| **Definition 来源** | `StrategicFactionCatalog` + `scenarios.json` / `level_tester_roster.json` 的 `factionId` |
| **Runtime 创建** | Opening / `Ch01ScenarioStrategicSetup.ApplyPlayerFactionAndVassalage` 写入 `world.Strategic.PlayerFactionId`；角色通过 `FactionMembershipComponent.Assign` |
| **分类** | **CASE A 语义**：稳定字符串 ID + Catalog 显示名；**不是** Dynamic Faction 对象 Registry |
| **Stop→Play 后** | 同一 FactionId **稳定**（常量 + Scenario），不存在随机 Id |

### 正式 Faction Catalog（无独立 JSON 定义库）

| FactionId | DisplayName | Source |
|---|---|---|
| `base:faction_player` | 主角团 | Catalog + Scenario/Roster |
| `base:sect_huangcun_labor` | 压迫宗门 | Catalog |
| `base:faction_fisher_village` | 沧澜渔盟 | Catalog |
| `base:faction_nan_yan` | 南堰庄盟 | Catalog |
| `base:faction_shuofeng` | 朔风堡 | Catalog |
| `base:faction_donglin` | 东林海会 | Catalog |
| `base:faction_xijin` | 西津渡帮 | Catalog |
| `base:faction_bandits` | 山匪 | Catalog |

---

## 二、测试山贼是什么

| 项 | 值 |
|---|---|
| **来源** | `TestStrategicBootstrap`（Phase C 测试夹具） |
| **创建链** | `StrategicContentBootstrap.ApplyCh01Defaults` → `Ch01ScenarioStrategicSetup.SeedPrototypeBanditArmies` → `ArmyStackAdapter.EnsureBandit*Army` |
| **创建时机** | Fresh Play 的 Ch01 战略初始化（**非** Cheat 按钮） |
| **CharacterId** | **非固定常量**；`EntityStore` 顺序分配（示例存档 id **19–26**） |
| **DefinitionId** | `test:test:bandit_leader` / `test:test:bandit_a` 等（`CreateNpc` + 双重 `test:` 前缀） |
| **DisplayName** | BanditLeader / BanditA / WeakBandit / StrongBanditLeader 等 |
| **FactionId** | `base:faction_bandits` |
| **正式 Character JSON** | **无** |
| **分类** | **Test Fixture Character** + **Test Fixture FormalArmy** |
| **Stop→Play** | 按名称查找可复用实体，但 **EntityId 不保证与上次相同**；Load 后以 Snapshot 保存的 Id 为准 |

### Prototype 三支测试山匪 FormalArmy（稳定 ArmyId）

| FormalArmyId | StackId | 成员示例 |
|---|---|---|
| `army:formal_bandit_patrol_1` | `army:bandit_patrol_1` | BanditLeader + A/B/C |
| `army:formal_bandit_patrol_weak` | `army:bandit_patrol_weak` | WeakBandit |
| `army:formal_bandit_casualty_test` | `army:bandit_patrol_casualty_test` | StrongBanditLeader + A/B |

---

## 三、`vs04_slot0.json` 实查

**路径：** `%USERPROFILE%\AppData\LocalLow\DefaultCompany\XianXia\vs04_slot0.json`

| 指标 | 值 |
|---|---|
| Snapshot entity 数 | 27 |
| `strategic.playerFactionId` | `base:faction_player` |
| 主角/同伴 entity `factionId` | 修复 Serializer **后** 有；旧档可能缺失 |
| 山贼 entity（id 19–26） | **在 Snapshot 中** |
| `strategic.formalArmies` | 4（3 bandit + 1 player） |
| `strategic.armyMemberships` | bandit 19–26 均有绑定 |
| `strategic.characterWorldPresences` | **仅 3 条**（主角/甲/乙）；山贼位置依赖 FormalArmy hex + 成员 sync |
| `strategic.worldSiteOwners` | 含 `test:site_player_camp` → `base:faction_player` |

### 主角团 Save JSON 示例（修复前旧档）

| Character | Runtime（Save 前） | Snapshot entity |
|---|---|---|
| 主角 id=1 | 主角团 | **无 `factionId` 键** |
| 同伴甲 id=2 | 主角团 | **无 `factionId` 键** |
| 同伴乙 id=3 | 主角团 | **无 `factionId` 键** |

→ **Capture DTO 有字段，JsonSnapshotSerializer 未落盘** → Restore 空 Membership → UI「无归属」。

### 山贼 Save JSON 结论

→ **CASE B**：山贼 **已在** Snapshot Character records；Load 后消失是 **Restore / 链接层** 问题，不是 Capture 漏实体。

---

## 四、Snapshot Character Restore 模型

**混合 MODEL 2：**

```
Snapshot entities[]     → 重建 Domain Entity（含 test bandit）
        ↓
Strategic Restore       → FormalArmy / Membership / WorldPresence
        ↓
FinalizeRuntimeLinks    → ArmyStack 展示链 + 成员 Presence 二次同步
        ↓
Host Rehydration        → Hex Content Shell 就绪后再 Finalize 一次
```

| 分类 | Fresh Play | Load |
|---|---|---|
| Content-defined 角色 | Opening Scenario 创建 | Snapshot entities 覆盖 |
| Test Fixture 山贼 | `TestStrategicBootstrap` 创建 | **必须**从 Snapshot entities 重建 |
| Dynamic Runtime 角色 | 运行时 spawn | Snapshot **可以**保存 entity 记录并重建 |

### 核心问题回答

| 问题 | 答案 |
|---|---|
| 能否恢复 Save 时存在、Fresh Content 不存在的 **Character**？ | **YES**（entities[] 全量重建） |
| 能否恢复 Save 时存在、Fresh Content 不存在的 **Faction**？ | **N/A / 等价 YES** — 势力是字符串 ID + Membership，无 Faction Domain 对象 |

---

## 五、Root Cause

### 问题 A — 主角团变 None

| 层 | 结论 |
|---|---|
| Faction Registry | **不存在**需 resolve 的 Faction 对象 |
| Character Capture | DTO 正确 |
| JSON Serialize | **`JsonSnapshotSerializer` 漏写 entity `factionId`** |
| Restore | 空 `FactionMembershipComponent` → 「无归属」 |

**修法（CASE A）：** 补齐 `JsonSnapshotSerializer` entity 读写（`factionId` / `factionRole` 及同批 vitals 字段），**不**新增 Faction JSON。

### 问题 B — 山贼 Load 后消失

| 层 | 结论 |
|---|---|
| Entity Restore | **正常**（Snapshot 含 id 19–26） |
| ArmyMembership Restore 顺序 | **错误**：原先在 `FormalArmySnapshotRestore.Apply` **之后** → `FormalArmyMemberPresenceSync.SyncAll` 静默失败 → **无 WorldPresence** |
| ArmyStack | Snapshot **未持久化** Stacks；Load 后 `Strategic.Armies.Stacks` **空** → 世界地图 `DrawArmyStacks` 不绘制 |
| Faction | `base:faction_bandits` 为 Catalog ID，**不是** Registry 丢失 |

**修法（统一链接层，非山贼 if）：**

1. `StrategicSnapshotHelper.Restore`：先 Register FormalArmy → Restore ArmyMembership → 再 `FormalArmySnapshotRestore.Apply`
2. `StrategicSnapshotHelper.FinalizeRuntimeLinks`：从 FormalArmy 重建 ArmyStack + 全员 Presence 二次 sync
3. `ArmyStackAdapter.EnsurePresentationStacksFromFormalArmies`：已知 Prototype StackId 映射 + 通用 fallback（`stackId = formalArmyId`）
4. `HostSnapshotSessionRehydration`：Hex Shell 就绪后再 `FinalizeRuntimeLinks`
5. Debug：`[SnapshotCharacterRestore] ... FAILED: ...`（Membership / Presence 目标缺失）

---

## 六、临时主角营地

| 项 | 值 |
|---|---|
| **SiteId** | `test:site_player_camp` |
| **Content** | `travel_mvp_hex_world_30x15.json`（正式写入，非纯 Runtime inject） |
| **Owner** | Snapshot `strategic.worldSiteOwners` |
| **分类** | Content Site + Snapshot Owner；**不是**与山贼相同的 Entity 重建问题 |

---

## 七、Restore 依赖顺序（目标态）

```
Content Definitions（Hex / Site Shell）
        ↓
Snapshot entities[]（重建全部 Character Domain Entity）
        ↓
Strategic FormalArmy Register
        ↓
ArmyMembership Restore
        ↓
FormalArmySnapshotRestore.Apply（Motion + Member Presence）
        ↓
CharacterWorldPresences / SiteOwners / Diplomacy ...
        ↓
FinalizeRuntimeLinks（ArmyStack + Presence resync）
        ↓
PlayerParty / Presentation Rehydration
```

**禁止：** 先 Restore Membership 但 Faction 不存在（本项目中 Faction 为 ID 字符串，无此问题）；**禁止** Load 后重新 `SpawnBandits` Cheat 掩盖链接断裂。

---

## 八、代码改动摘要

| 文件 | 改动 |
|---|---|
| `JsonSnapshotSerializer.cs` | entity `factionId` 等字段 + root inventory/relationship 读写 |
| `SnapshotService.cs` | Debug 警告：玩家阵容缺 membership |
| `StrategicSnapshotHelper.cs` | Restore 顺序 + `FinalizeRuntimeLinks` |
| `ArmyStackAdapter.cs` | `EnsurePresentationStacksFromFormalArmies` |
| `HostSnapshotSessionRehydration.cs` | Shell 就绪后 Finalize |
| `SnapshotRuntimeCoverageTests.cs` | `SNAP_COV_01` Faction roundtrip；`SNAP_COV_05` bandit + stack |

---

## 九、人工验收（LevelTester）

### TEST 1 — Character Faction + 山贼

1. Play → 确认主角/甲/乙 = **主角团**，山贼栈可见  
2. **重新 Save**（旧档无 entity `factionId` 必须新存）  
3. Stop Play → Play → Load  

**PASS：**

- 三人仍 **主角团**
- 山贼仍在（CharacterId 与 Save 一致）
- Faction = 山匪；世界地图 bandit stack 可见

### TEST 2 — Army / Camp 一致性

Save 时存在 player FormalArmy + 主角营地 Owner = 主角团 → Load 后：

- Player Faction / Camp Owner / Army.FactionId / Member Faction **同一** `base:faction_player`
- 不能出现 Army=主角团、Member=None 的半恢复态

---

## 十、与「主角团 / 山贼 / 营地」分类对照

| 对象 | 类型 | 跨 Play 依赖 |
|---|---|---|
| 主角团 | Catalog Faction ID | entity `factionId` JSON + strategic `playerFactionId` |
| 测试山贼 | Test Fixture Entity + Army | entities[] + formalArmies + **FinalizeRuntimeLinks** |
| 临时主角营地 | Content Site + Snapshot Owner | `worldSiteOwners` + Content hex JSON |

**结论：** 主角团与山贼 **同属 Snapshot 持久化链路问题**，但根因层不同（JSON 漏字段 vs Restore 链接顺序 + ArmyStack）；**不是** Dynamic Faction Registry 消失。

---

## 十一、未做 / 禁止项

- 未新增 Faction Content JSON 作为 workaround  
- 未启用 Territory 系统  
- 未做 `if (山贼) respawn` / Load 后重跑 Spawn Cheat  
- 未改已通过 Snapshot 功能（Active Control / LocalMap / Party 等）的行为语义  

---

## 相关文件

- `Assets/Scripts/Core/World/Strategic/StrategicFactionCatalog.cs`
- `Assets/Scripts/Core/World/Strategic/TestStrategicBootstrap.cs`
- `Assets/Scripts/Core/World/Strategic/Ch01ScenarioStrategicSetup.cs`
- `Assets/Scripts/Core/Persistence/SnapshotService.cs`
- `Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs`
- `Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs`
- `Assets/Tests/EditMode/SnapshotRuntimeCoverageTests.cs`
