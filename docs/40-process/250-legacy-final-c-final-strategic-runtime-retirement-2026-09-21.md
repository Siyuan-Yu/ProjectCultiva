# LEGACY-FINAL-C — Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement

> 状态：**Producer Accepted / Sealed — 2026-09-21**  
> 日期：2026-09-21  
> 前置：LEGACY-FINAL-A、LEGACY-FINAL-B 均已 Producer Accepted / Sealed

## 1. 本阶段边界

正常 New Game、现代存档与正常 Continuous runtime 不再拥有旧战略世界 authority。Hex 类型、旧 Content schema
和旧 Snapshot DTO 可以继续存在，但只能作为派生摘要、诊断或单向迁移输入。LEGACY-FINAL-C 已完成人工验收并封板；
后续仅进行 FINAL-SEAL compatibility quarantine、dead API cleanup 与 architecture freeze。

## 2. Territory runtime 退役

`StrategicBoard.TerritoryRegions`、`TerritoryRegionBoard`、`TerritoryControlService`、
`StrategicTerritoryCoverageResolver` 和旧 invariant 已删除。现代行政控制唯一链为：

`WorldSite.OwnerFactionId` + `TerritoryClaim` history + `WorldSiteAdministrativeControlResolver`
+ Actual Administrative Control geometry。

外交概览显示“控制据点”，不再为统计数字重建 Region。旧 `TerritoryRegionContentDefinition`、JSON parser／validator
和 `TerritoryRegionControllerSnapshotDto` 只在 Data／Serialization 边界保留；旧 controller 仅在对应 Site 尚无现代 Owner
时作一次恢复输入，不创建 runtime Region。新存档不输出该字段。

## 3. residual 与位置 authority

现代弥留、尸体和战后留下的真实 Character 使用：

`LifecycleState + AtWorldPosition + SurfaceId + exact WorldPosition`。

`AtHex` 不再表达“战后残留”。精确 Continuous 残留直接保存原位置；旧 `AtHex` 若同时保存 SurfaceId 与精确坐标，
单向迁移为 `AtWorldPosition`。只有 Hex 的旧数据必须通过明确的 legacy Surface mapping 迁移；正常 Continuous session
无法映射时返回 `SnapshotInvalid`。所有仍可写 Hex presence 的方法均显式命名为 `SetLegacyAtHex`，只供旧档、旧 Outdoor
LocalMap、旧 Hex travel 或非连续兼容路径。

## 4. 旧 battle runtime 退役

`StrategicEncounterRuntime`、`RetreatingArmyBoard`、`LingeringBattlefieldRegistry` 及其 record／trace／state 已从
`StrategicBoard` 和 normal runtime 删除。现代 `CharacterEncounter` 只依赖真实 Character、Squad、精确 source-world anchor、
生命周期、`ManualBattleSettlement` 与现代 participant state；结算不再清理旧 StrategicEncounter。

旧 active battle snapshot 只在所有实际参与者都能唯一映射到真实 Character／Squad、共享明确 Surface 且拥有精确位置时，
单向迁移到 `CharacterEncounter`。仅含 synthetic ArmyStack／FormalArmy 且无法唯一还原 Character 的输入返回
`SnapshotInvalid`。旧 RetreatingArmy／LingeringBattlefield 容器不会恢复；可证明的真实 Character 空间和生命状态由现代
presence/lifecycle 恢复。

## 5. Outdoor LocalMap 与 Hex compatibility

旧服务已改为明确 compatibility 名称：

- `LegacyPlayerPartyHexTravelCompatibility`
- `LegacyPlayerPartyLocalVisibleTravelCompatibility`
- `LegacyPlayerPartyOutdoorLocalMapCompatibility`
- `LegacyWildernessLocalMapFallback`
- `WorldTravelService.EnterLegacyWildernessLocalMap`

正常 B 链仍是 `SurfaceId + exact WorldPosition + SurfaceVisible`，不调用这些 compatibility executor。
`StrategicTravelDriver` 先推进现代 Squad／background；只有 `World + nonempty HexPath + empty SurfaceId` 才推进旧玩家 Hex
存档。通用 LocalMap／EntityLocation 保留，因为 Separate Space 与 Independent Battle 仍正式使用。

## 6. CurrentHex、PresenceHex 与合法 Legacy 类型

`CurrentHex` 只可从现代 WorldPosition 单向派生，服务旧 DTO、诊断和明确兼容路径；不得反推正常移动、攻击、可见性、
行政控制或 materialization。`PresenceHex` 只保留在旧 WorldSite／Content 摘要与 fallback 输入，不能覆盖已有精确位置。

可以继续存在的类型包括 `HexCoord`、旧 snapshot numeric fields、旧 Content definitions/parser/validator、legacy import
converter 与 enum numeric compatibility。它们不能注册为 normal runtime Board 或反向成为现代 Gameplay authority。

## 7. Ch01 与 dead historical code

删除零 caller 的 `Ch01RebellionService` 与 `BattleOfferOrigin`。`Ch01FormationScenarioCompat` 仅保留为旧 JSON 可读字段；
新 capture 固定 false 且 serializer 不输出，Ch01 progression 由实际 opening scenario bootstrap 注册。

## 8. Runtime 与 Snapshot 不变量

Development build 在 Current BaseGame bootstrap 后检查：Continuous Character 不处于 AtHex、PlayerParty 使用
`SurfaceVisible`、正常户外不激活 Outdoor LocalMap。现代 capture 后检查：TerritoryRegion controller、legacy residual、
RetreatingArmy、PendingEngagement、Ch01 compatibility 与 AtHex Character presence 均为空。

现代 snapshot authority 固定为：PlayerParty Surface position／route、NPC `SquadWorldMotion`、Character exact presence／
interior EntityLocation、Site + Claim actual control、现代 `CharacterEncounter`。旧字段只读，重新保存时不会回写。

## 9. 验证范围

本阶段只执行最小离线编译、完整 caller audit、BaseGame content／New Game sanity、现代 snapshot round-trip、组合旧档
单向 migration sanity、territory／residual／CharacterEncounter／Separate Space 静态或定向 sanity，以及
`git diff --check`。未打开 Unity，未运行 Unity Test Runner、PlayMode 或 batchmode。

## 10. 人工验收重点

- New Game 正常进入 Continuous Outdoor，PlayerParty 与 NPC Squad 行为保持封板结果；
- WorldMap travel、WASD、Site Actual Control overlay 与外交“控制据点”正常；
- 弥留／尸体保留精确 Continuous 位置，Save/Load 后不吸附 Hex center；
- CharacterEncounter Offer → Start → Battle → End → Report → Close 正常；
- Separate Space 进入、离开、存档恢复正常；
- 旧档能迁移则只产生现代状态，无法唯一恢复真实 Character 的旧 active battle 明确报 SnapshotInvalid。
