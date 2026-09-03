# 190 · Local Combat 弥留者 Ownership 统一规则：Residual Handoff + 精确落点 封板（2026-09-03）

> 状态：**已封板（本轮修复完结）** ｜ 日期：2026-09-03
> 上级：[189 Phase 5S CLOSED Checkpoint](189-phase-5s-closed-world-local-continuity-v1-checkpoint-2026-09-03.md)／[186 Phase 5S 权威封板](186-phase-5s-final-architecture-closure-2026-09-01.md)
> 本文 = 2026-09-03 两个修复批（Local Combat 弥留者随队消失 → residual handoff；弥留者重进 LocalMap 位置漂移 → precise WorldPosition）的**封板归档**。devlog 已按两轮主题逐条记录（2026-09-03 顶部两条），本文固化最终 ownership invariant 与 authority 收口，不重写历史细节。

---

## 1. 修了什么（两个相连 bug）

### Bug 1 — 普通 Local Combat 弥留 Follower 离开 LocalMap 再返回后消失
- 触发：主控在 WorldSite LocalMap 对 FormalArmy 宣战并结束 Manual Battle（留下双方弥留者）→ 不离开地图对普通 Character 发起普通近战 → 我方成员被打成弥留 → 离开再返回：战略战斗弥留者仍在，普通 Local Combat 倒下者消失。
- root cause：`DispatchDrainedEvents` 的 `CombatantDefeated` fallback 只完整覆盖 **FormalArmy casualty**（detach → `FormalArmyMemberPresenceSync` → `PlaceCharacterAtResidualHex`）。PlayerParty/Follower/普通 LocalCharacter 无第三层 handoff → presence 未钉 hex。重进时 `PlayerPartyLocalMapMaterializationService` 遍历 `party.Members` 全集把弥留者当活人随队重生；`LoadedStrategicPopulationMaterializer` residual loop 又无条件排除所有 party member → 两头都不归 → 消失。

### Bug 2 — 弥留者重进 LocalMap 出现在"重新计算的位置"而非原位
- root cause：handoff 只保存 `ResidualHex`；重进时 `TryResolveResidualLocalPlacement` 执行 `ResidualHex → Hex 中心 → WorldToLocal → ApplyFormationOffset`，任何精确落点都被丢弃。Save/Load 侧 `JsonSnapshotSerializer.SerializeStrategic` 根本没写 characterWorldPresences 的 worldX/worldY，读档同样丢。
- 连带 bug：WorldSite 下上一版 handoff 用**主控** `PlayerPartyWorldMotion.WorldPosition` 派生 Follower 的 residual hex —— Follower 的战略格按主控位置决定，不是角色自己的位置。

---

## 2. 最终 Ownership Invariant（本批固化）

任何角色一旦进入 **Incapacitated / visible Corpse**：

| 角色 | PlayerParty.IsMember | TravelingWithParty | Lifecycle | WorldPresence | Materializer |
|---|---|---|---|---|---|
| 活 Follower | true | true | Alive | 随 PlayerParty motion | PlayerParty materializer |
| 倒地 Follower | true（保留，不 TryRemoveMember） | **false** | Incapacitated | **AtHex + precise WorldPosition**（自己倒下格） | StrategicResidual materializer |
| FormalArmy 倒地成员 | —（不再 living member） | false | Incapacitated | AtHex（现有 Army handoff） | StrategicResidual materializer |

一句话：**ResidualHex 只回答"尸体/弥留者属于哪个战略格"；它不回答"尸体在 LocalMap 的哪里"。普通 Local Combat 倒下时捕获该角色自己的 EntityView 精确位置，经当前 Surface mapping 保存为 residual precise WorldPosition，重进 LocalMap 反向映射回原位。**

三层 fallback 互斥（只有一个 owner 处理）：
```
StrategicEncounter → FormalArmyCasualtyService → LocalCombatCasualtyHandoffService
```

---

## 3. Authority 收口（本批新增/修改的事实）

### 3.1 双层 presence 模型（WorldPresenceBoard）
- `WorldAgentPresence.SetAtResidualWorldPosition(residualHex, precise)`：Mode **保持 AtHex**（`UsesHexPresence` 语义不变，不改成 BackgroundCharacter AtWorldPosition），清 SiteId，写 HexQ/R + `HasContinuousWorldPosition`/`WorldPosX/Y`，ClearFollow/Pursuit。
- `SetAtHex` 继续表示"仅 Hex、无精确 continuous position"：旧数据 / Auto Battle / 无法取得具体 local point 时使用。
- `StrategicResidualPresenceService.PlaceCharacterAtResidualWorldPosition`（IsResidualLifeCandidate 后保存）；原 `PlaceCharacterAtResidualHex` 保留未动（战略战斗 fallback 零破坏）。

### 3.2 倒下瞬间捕获真实 View 位置（Host 层）
- `PlayableHostBootstrap.TryGetCurrentLocalPresentation(id, out localX, out localZ)`：从 `entityViewSpawner.Registry` 读**真实 transform** → `HostPresentationSpace.ToPresentation`。**不读 `EntityLocationComponent.PresentationOverride`**（`HostMoveController.SyncLocation` 并非每帧回写，远离 WorldRegion 地点会漏采 → 可能是 stale）。
- 捕获后立即 `SetPresentationOverride(localX, localZ)` 让 Domain presentation 与真实 View 对齐——但长期 authority 是 precise WorldPosition，不是 override。
- `ResolveLoadedStrategicBounds` 复用既有 `_loadedStrategicWildernessBounds/_loadedStrategicSiteBounds`（与 `LoadedStrategicPopulationMaterializer` 同一套 bounds authority，不在 Core 重猜）。

### 3.3 Local → World 映射（LocalCombatCasualtyHandoffService）
- **Wilderness**：ResidualHex = `context.WildernessHex`（不重新 WorldToHex）→ `TryProjectLocalToWorld(WildernessHex, localX, localZ, bounds, hexSize)` → `PlaceCharacterAtResidualWorldPosition`。
- **WorldSite**：用**角色自己的** localX/localZ → `WorldSiteSpatialMapping.TryLocalToWorldSurface` → `WorldToHex` → **`site.OccupiesHex(derivedHex)`**（footprint 边界浮点歧义走 neighbor resolution）。**绝不用主控位置派生 Follower 的 hex，绝不拿 AnchorHex 当 authority。**
- 无 view / 无 bounds：走 hex-only fallback（WorldSite 下明确失败不猜，由调用方保留原态）。

### 3.4 重进 LocalMap 位置回放（LoadedStrategicPopulationMaterializer）
- `TryResolveResidualLocalPlacement` 改读整个 presence：`HasContinuousWorldPosition` → precise WorldToLocal（Wilderness `TryProjectWorldToLocal` / WorldSite `TryWorldSurfaceToLocal`），**绝不再 ApplyFormationOffset**（最多极轻 safety clamp，正常 roundtrip 零改动）。
- 无 precise → legacy fallback（Hex center → WorldToLocal → stable formation offset）保留：老存档 / 旧 Strategic Battle residual / Auto Battle / 只有 BattleHex 无 Local position。

### 3.5 移动 ownership gate
- `PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty` 加 `CanFight` gate（非 Alive 不随队）——自动辐射所有 capture/reconcile/materialize 过滤；**绝不 TryRemoveMember**（逻辑 membership 与 physical traveling membership 分离）。
- 6 处 `CaptureTravelingMembers(party.Members)` 直传（HexTravelService）/ WorldLocationQuery / PreEngagementLegalLocation → `CaptureTravelingMembersForPartyTransition`。保留：`HexStrategicSessionBootstrap`（New Game 全 living）、`ManualBattleWorldCommitService`（battle 进入时刻 living participant 快照语义，注释说明）。
- `PlayerPartyLocalMapMaterializationService`：materialize 循环跳过非 Alive；末尾 `CaptureTravelingMembers(materializedIds)` 只含实际生成者。
- `LoadedStrategicPopulationMaterializer` residual loop：排除条件改为**仅排除 transitionable member** → 弥留/尸体 party member（AtHex residual）允许走 StrategicResidual 重生成；`MaterializeResidual` occupant 查重防 double。
- `HostPlayerPartyController.TickFollowers/OrderFollowerTowardActive` 加 CanFight gate（弥留/尸体绝不发 follow movement）。

### 3.6 Save/Load 全链（precise 持久化）
- `CharacterWorldPresenceSnapshotDto.HasWorldPosition`（显式字段，**不用 WorldX==0 判断**，因为 (0,0) 合法）。
- Capture：AtHex/AtWorldPosition 都带 `HasWorldPosition/WorldX/WorldY`。
- Restore：AtHex + HasWorldPosition → `SetAtResidualWorldPosition`；AtHex 无 → `SetAtHex`（legacy）。
- `JsonSnapshotSerializer.SerializeStrategic/ReadStrategic` 补 `hasWorldPosition/worldX/worldY`（JsonValue.FromBool 对应 Read `hwp.Bool`）；旧存档无字段 → false → legacy fallback。
- **二次覆盖防护**：Restore 顺序先 CharacterWorldPresences 后 ResidualCharacterPresences；记录 `restoredCharacterWorldPresenceIds`，旧 Residual DTO 遇到已恢复者 `continue` —— 旧 DTO 不覆盖新 authority。

### 3.7 WorldMap 图标同一 physical truth（WorldAgentMapPositionResolver）
- `UsesHexPresence` 且有 precise → 用 `WorldPosX/Y`（LocalMap 与 WorldMap 同一 corpse physical truth）。若产品希望 corpse icon 吸 Hex 中心可只改这一处，LocalMap materialization 不受影响。

### 3.8 ReleaseManagedEntity 不动
- 离图继续清 LocalMap occupant + PresentationOverride（presentation 层）；authority 已在 WorldPresence（ResidualHex + WorldPosX/Y）里，回来从 precise 重建。

---

## 4. 验证

- **PreciseResidualCheck（10 项 PASS）**：SetAtResidualWorldPosition 行为 / Capture 保留 HasWorldPosition+WorldX/Y / Restore 还原 precise / 旧 Residual DTO 不覆盖新 authority / legacy 无 HasWorldPosition 走 hex-only。
- **LocalCombatHandoffCheck（23 项 PASS 回归）**：living 跟随正常；follower 弥留 → AtHex → traveling 排除 → reconcile 不拖走 → 返回 rematerialize → party materialize 不生成弥留者；FormalArmy member 拒绝非 army handoff。
- Core/Data/Unity 三程序集编译 **0 error**；`git diff --check` 干净。

## 5. 人工验收路径（Unity，用户执行）
1. WorldSite LocalMap：Follower 站非中心位置（如房子右侧）被打弥留 → 记录视觉位置 → 离开 → 返回 → **回原位（非 Hex 中心 / 非 formation 位）**。
2. 同 footprint 两 Follower 倒在不同区域 → 返回各回各位不聚团。
3. Wilderness 同测一次。
4. Save → Load → 返回 → 仍原位。
5. Regression：战略 Manual Battle 弥留正常；Auto Battle 仅 Hex 信息仍 fallback materialize；Living Follower 正常随队；Corpse decay 正常；老存档无 hasWorldPosition 不报错；WorldSite multi-hex 不再用主控位置决定 Follower residual hex。

---

## 6. 主要文件
- Core：`WorldPresenceBoard.cs`、`StrategicResidualPresenceService.cs`、`LocalCombatCasualtyHandoffService.cs`(新)、`LoadedStrategicPopulationMaterializer.cs`、`PlayerPartyTransitionMembership.cs`、`PlayerPartyHexTravelService.cs`、`PlayerPartyLocalMapMaterializationService.cs`、`PlayerPartyWorldLocationQuery.cs`、`PreEngagementLegalLocation.cs`、`ManualBattleWorldCommitService.cs`(注释)、`WorldAgentMapPositionResolver.cs`、`StrategicSnapshotHelper.cs`、`WorldSnapshot.cs`
- Data：`JsonSnapshotSerializer.cs`
- Unity Host：`PlayableHostBootstrap.cs`、`HostPlayerPartyController.cs`
- 文档：`42-devlog.md`（2026-09-03 顶部两轮条目）、本文
