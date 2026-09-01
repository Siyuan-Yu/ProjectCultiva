# 186 · Phase 5S Final Architecture Closure（2026-09-01）

> 状态：**Authoritative Closure / 封板** ｜ 日期：2026-09-01
> 上级：[185 Phase 5S-B2-3 实现与迁移记录](185-phase-5s-b2-3-world-combat-in-place-aftermath-and-population-migration-2026-08-31.md) ／ [2K RPG-First 真源](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)
> 决策链：[ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) ／ [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) ／ [ADR-0023](../40-process/43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)
> 本文 = Phase 5S 关于 **WorldMap↔LocalMap 连续世界、WORLD_COMBAT spatial authority、FormalArmy／Strategic Residual → Loaded LocalMap population migration** 的**最终权威规则**。
> 与本文冲突的旧文档规则必须标记 SUPERSEDED（见 §4）；历史 diagnosis 一律保留、不删除、不改写。
> 本文不新增实现；若与当前 C# 不一致，以代码现状为准并回报。

---

## 1. 适用范围与一句话

Phase 5S 关闭时，战略世界与战术 LocalMap 是**同一连续世界的两种 presentation**。FormalArmy 与 PlayerParty 的世界位置由各自权威持有；任何 Loaded 的 surface LocalMap（Wilderness Hex 或 WorldSite footprint）都通过普通 materialization 桥看到这些战略人口，不依赖 battle-only visibility，也不依赖「进入残留战场」等特殊 gateway。

---

## 2. Final Invariants（19 条，编号沿用任务清单 1370–1388）

### 1370. WorldMap 与 LocalMap 是同一连续世界的不同 presentation，不是两个独立空间。

### 1371. PlayerParty strategic physical authority = `PlayerPartyWorldMotion`。
- WorldPosition / CurrentHex / LocationKind 是主控队伍战略位置的唯一权威；`WorldPresence` 的成员行、`PartyWorld` 的 focus 均由它派生 / 与之对齐。
- 战斗中把 PlayerParty commit 到 BattleHex 时，写的是 `PlayerPartyWorldMotion`（含 `CaptureTravelingMembers` / `SetAtWorldPosition` / WorldSite 保留 WorldPosition + `AlignCurrentHex`），**不**只改成员 WorldPresence。

### 1372. FormalArmy strategic physical authority = `FormalArmy.WorldMotion`。
- `WorldMotion.HasPosition / CurrentHex / WorldPosition / LocationKind` 是军队位置的唯一权威；成员 WorldPresence 由 `FormalArmyMemberPresenceSync` 从 `WorldMotion` 派生。
- `ArmyPresenceAdapter` 只附加 `CombatPursuitStackId` 等 pursuit metadata，**不再**根据 legacy `army.CurrentHex` 自行决定 physical position。
- 上一轮 `CommitArmyAtExactBattleHex` 之后成员不会再被 legacy adapter 降回错误 AtHex authority。

### 1373. BackgroundCharacter 继续使用自己的 WorldPresence / travel materialization 链，与 FormalArmy population 分离。
- `LoadedDestinationArrivalMaterializer` 是 Background Character 专用，带 background travel side effects；`PlayerPartyLocalMapMaterializationService` 管 PlayerParty；`LoadedStrategicPopulationMaterializer` 管 FormalArmy living member 与 Strategic Residual。三者不合并 movement authority。

### 1374. FormalArmy 初始 authored source 已迁移为 Content。
```text
CharacterDefinition + FormalArmyDefinition + OpeningScenario.initialFormalArmyIds
→ FormalArmyContentBootstrap（Data/Bootstrap）
→ ArmyService.CreateAuthoredArmy（Core/World/Strategic/ArmyService.cs）
```
- `TestStrategicBootstrap` / `EnsureBandit*` production generation 已删除（当前工作区代码已无此类生成入口）。

### 1375. Bandit / 普通 NPC 共用同一个 `CharacterDefinition` schema。
- Bandit **不是**特殊 Character 类型；`strategic_bandits.json` 只是文件组织方式，不是另一种 schema。
- Faction membership 在 runtime army bootstrap 中赋予；hostility 由 Faction / Diplomacy 决定。

### 1376. WORLD_COMBAT frozen participant authority = `BattleParticipantSnapshot`。
- 参战者、BattleAnchorHex、`LocalMapResolutionKind`、`EncounterLocalMapId` 在 Offer 创建 / 接战时冻结；不重新 scan SupportArea、不重新 gather。

### 1377. WORLD_COMBAT 无论 Manual 或 Auto，只要 unit 实际参战，都 commit 到 frozen `BattleAnchorHex`：
- selected / mandatory FormalArmy（Friendly + Enemy primary + reinforcement）→ BattleHex；
- PlayerParty **只有实际参战**（snapshot 含 PlayerParty member）→ BattleHex；
- third-party battle **不移动 PlayerParty**。

### 1378. Manual 与 Auto 的 world-state semantics 相同，区别仅在 combat execution。
```text
Manual → tactical LocalMap
Auto   → mathematical resolution
```
- 同一套 world commit（`ManualBattleWorldCommitService.CommitWorldCombatParticipants`，Auto 复用，不复制第二套实现）。

### 1379. Manual WORLD_COMBAT 使用真实 Wilderness / WorldSite LocalMap，不依赖 dedicated EncounterMap。
- `BattleLocalMapResolver` 消费 frozen `PendingEngagement.BattleLocation` / defender WorldMotion SiteId；WorldSite 分支保留 exact BattleHex（multi-hex Site 不被吸回 AnchorHex / PresenceHex / StartLocation）。

### 1380. Auto settlement 后，若 PlayerParty 实际参战，关闭 WorldMap 时应显示 BattleHex 对应 authoritative LocalMap，而不是旧 LocalMap。

### 1381. Real FormalArmy Character 不属于 `BattlefieldSpawnScope` lifetime ownership。
- `BattlefieldSpawnScope` 只保留 synthetic / legacy encounter-owned entities；真实 Army member 用原始 EntityId 作普通 LocalMap 人口。

### 1382. WORLD_COMBAT End Battle 不 restore participant 到 pre-battle strategic position。
- `RestoreParticipantsAfterBattle` 仅保留 legacy ExplicitEncounter / compatibility；real WORLD_COMBAT（WorldSite / Wilderness）禁止调用。

### 1383. Living FormalArmy survivor 留在 BattleHex。

### 1384. FormalArmy non-living member detach 后：
```text
Incapacitated / visible corpse
→ StrategicResidualPresence
→ WorldPresence.AtHex(BattleHex)
```

### 1385. Residual marker 从真实 Character residual 聚合产生，而不是 ArmyStack casualty counter。
- `StrategicResidualPresentationQuery` = PURE DERIVED 聚合（Hex × Relation × DEAD/DOWNED）。

### 1386. Residual-only Hex 通过普通世界移动进入并由 `LoadedStrategicPopulationMaterializer` materialize；不再提供「进入残留战场 / 攻击残留战场」production gateway。

### 1387. `LingeringBattlefieldRegistry` 只剩 legacy / compatibility 职责，不是新 WORLD_COMBAT physical authority。

### 1388. `ExplicitEncounterMap` / 部分 legacy compatibility 可以继续存在，但必须明确**不是**当前 WORLD_COMBAT 主路径。

---

## 3. Character Content Authoring Convention

- 所有普通人物、NPC、山匪、军队成员共用 `type:"character"`；`CharacterDefinition` 是唯一 Character schema。
- `strategic_bandits.json`（`Content/BaseGame/Data/Characters/`）只是文件组织方式，不是另一种 schema；山匪在 runtime army bootstrap 中获得 Faction membership。
- `baseAttributes` / `spiritRoots` / `personalityTags` / `backgroundTags` … 均为 optional；正式 production Character **推荐**使用统一 authoring template 补齐（缺省字段走默认值）。
- faction / hostility **不写成 Character subtype**：归属在 `FormalArmyDefinition`（组织）与 runtime bootstrap（Faction 赋予）层表达；敌我关系由 Faction / Diplomacy 决定。

---

## 4. SUPERSEDED 文档清单

| 文档 | 被取代的规则 | 处置 |
|---|---|---|
| [23-combat](../20-systems/23-combat.md) §2:14 | 「战略遭遇 = Modal Encounter LocalMap」模型（WORLD_COMBAT 主路径走 EncounterMap） | 顶部 banner：部分 SUPERSEDED（正文战斗规则仍有效） |
| [147-battlefield-linger-no-teleport](147-battlefield-linger-no-teleport-2026-08-21.md) | 残留再进：敌弥留→攻击再入、我方弥留→查看再入（`EnterLingeringBattlefield`）作为 production gateway | 顶部 banner：SUPERSEDED |
| [149-lingering-battlefield-batch2](149-lingering-battlefield-batch2-2026-08-21.md) | 「进入残留战场」菜单 / 探望到站自动衔接再入 | 顶部 banner：SUPERSEDED |
| [150-lingering-battlefield-batch3-offer](150-lingering-battlefield-batch3-offer-2026-08-21.md) | 敌方残留栈再进 → 接战 Offer（`LingeringLocalMapId` 产品入口） | 顶部 banner：SUPERSEDED |
| [153-lingering-remnant-macro-presentation](153-lingering-remnant-macro-presentation-2026-08-22.md) | 「BattlefieldLingering 再入保留」 | 顶部 banner：部分 SUPERSEDED（derived marker 部分保留，见 1385/1386） |
| [159-encounter-scoped-lingering-and-worldmap-path-preview](159-encounter-scoped-lingering-and-worldmap-path-preview-2026-08-24.md) | Encounter 作用域 Lingering 再入（`LingeringBattlefieldParticipantService` 等）作为主路径 | 顶部 banner：SUPERSEDED（Registry 仅 legacy，见 1387） |

> 处置规则：banner 加在文档顶部，指向本文 §2；**不删除、不改写**历史 diagnosis 与问题记录。

---

## 5. 旧规则处置说明（用户指定搜索项 × 实际证据）

| 旧规则表述 | docs 中的实际证据 | 处置 |
|---|---|---|
| PlayerParty 留在 support hex | 无独立文档声明；为旧代码 policy，已由 Phase 5S-B2-3.2 正式改变（185 §1 有历史根因记录） | 由 1371/1377 取代；185 保留历史记录 |
| support Army 战后 restore pre-battle position | [182](182-phase-5s-b2-real-localmap-manual-battle-architecture-2026-08-30.md) §已明确 **no pre-battle snapshot restoration**（与本文一致） | 无冲突文档；182 不需标记 |
| WORLD_COMBAT 必须进入 EncounterMap | [23](../20-systems/23-combat.md) §2:14（已标记）；[183](183-phase-5s-b2-1-battle-localmap-resolution-contract-2026-08-30.md) 已声明 ExplicitEncounterMap 为 legacy 边界 | 23 顶部 banner；183 一致不标 |
| Auto Battle 不移动 Player | 无独立文档声明；[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) §8 的「仅 Auto」是参与资格（仍有效） | 由 1377 精化（PlayerParty 仅实际参战时 commit） |
| Lingering Battlefield 是特殊 attack/enter target | 147/149/150/153/159（已标记） | 见 §4 |
| prototype Bandit 由代码生成 | 代码已删（`EnsureBandit` / `TestStrategicBootstrap` 在当前 Assets/Scripts 无命中）；docs 中无正式生成规则（2A/2J 仅有 Prototype 注记） | 由 1374/1375 取代 |

---

## 6. 与当前代码的一致性核对（2026-09-01 grep 实证）

- `ArmyService.cs:98` `CreateAuthoredArmy`；`Data/Bootstrap/FormalArmyContentBootstrap.cs:100` 调用之；`OpeningScenarioDefinition.InitialFormalArmyIds`（`Data/Content/OpeningScenarioDefinition.cs`）→ 1374 成立。
- `Data/Content/CharacterDefinition.cs`、`FormalArmyDefinition.cs`；`Content/BaseGame/Data/Characters/strategic_bandits.json`（type 同为 character）→ 1375 成立。
- `Core/World/Strategic/StrategicResidualPresenceService.cs`、`StrategicResidualPresentationQuery.cs`、`LoadedStrategicPopulationMaterializer.cs`、`LoadedStrategicPopulationQuery.cs` → 1384/1385/1386 成立。
- `Core/World/Strategic/BattleEngagementSupportArea.cs` 仍存在 —— 参与资格（1377 的「实际参战」判定）仍使用；与位置 authority 分离，不冲突。
- `EnsureBandit*` / `TestStrategicBootstrap` 在当前 Assets/Scripts 无任何命中 → 1374「已删除」成立。
- 未发现文档与当前代码的新冲突（除 §4 已标记的旧 gateway 语义外）。

---

## 7. 验证（本轮）

- docs grep：仅本文与 185 为 Phase 5S 现行记录；§4 六个冲突文档已加顶部 SUPERSEDED banner。
- markdown consistency：banner 使用既有 `> **⚠️ …**` 引用块风格；链接相对路径正确。
- `git diff --check`：通过（exit 0）。
- 未修改任何 C# runtime logic、未修改 Content JSON gameplay values、未跑 Unity tests。

---

## 8. 引用链（真源）

- [2K RPG-First](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)（控制 / PlayerParty / 连续 Hex / FormalArmy 高层真源）
- [ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) ／ [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)
- [185 Phase 5S-B2-3 实现与迁移记录](185-phase-5s-b2-3-world-combat-in-place-aftermath-and-population-migration-2026-08-31.md)（实现细节 / 验收清单）
- 本文 §2 是 Phase 5S 上述领域的最终权威；与本文冲突的旧规则以本文为准。
