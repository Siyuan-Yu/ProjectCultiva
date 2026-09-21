# ADR-0038：Continuous World Legacy Migration Final Seal

> 日期：2026-09-21  
> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 前置：LEGACY-FINAL-A、LEGACY-FINAL-B、LEGACY-FINAL-C 均已 Producer Accepted / Sealed  
> 关联：[247 Project Handoff](../247-project-handoff-current-state-2026-09-18.md)、[250 LEGACY-FINAL-C](../250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)、[ADR-0035](ADR-0035-unified-squads-and-encounter-scope.md)、[ADR-0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)

## 1. 决策

正常 New Game、现代存档与正常 Current BaseGame runtime 只能使用下列 authority：

| 领域 | 唯一现代 authority |
|---|---|
| Character | `SurfaceId + exact WorldPosition`，或 Separate Space / Independent Battle 的 Interior `EntityLocation` |
| PlayerParty | `PlayerPartyTravel` 的 `SurfaceVisible + SurfaceId + exact WorldPosition` |
| NPC group | `Squad + SquadWorldMotion` |
| Territory | `WorldSite + TerritoryClaim + Actual Administrative Control` |
| Combat | `CharacterEncounter + real Character participants + exact Surface anchor` |
| Separate Space | `LocalMapSession + Interior EntityLocation` |

CharacterEncounter 内部位置进一步冻结为：`Origin`＝source canonical authority，`Return`＝开战前实际主世界 EntityView 位置，`Tactical`＝独立战场当前位置。`Return` 在 Active 前冻结后不可变，Tactical 永不成为战后 world position；Encounter 在 `CommitAndReturn` 前独占 participant spatial state，弥留／死亡只在现有 View 原地更新。普通 residual handoff 从返回主世界后才开始。

## 2. Legacy quarantine

`FormalArmy`、`ArmyStack`、`TerritoryRegion`、`AtHex`、Outdoor LocalMap 与 Hex travel
只能作为旧 Content、旧 Snapshot、单向 migration 或显式 compatibility adapter。任何新玩法禁止依赖这些
API、字段或命名作为 runtime authority。旧数据必须先迁移为上表中的现代状态，之后才能进入正常 runtime。

合法保留输入包括 `FormalArmyDefinition`、`FormalArmySnapshotDto`、`ArmyMembershipSnapshotDto`、
legacy pending engagement DTO、`TerritoryRegionControllerSnapshotDto`、`RetreatingArmySnapshotDto`、
BattleAnchorHex 数值字段、`AtHex` enum value、`HexCoord`、旧 Outdoor LocalMap definitions，以及
`LegacyPlayerParty*Compatibility`／`LegacyWildernessLocalMapFallback`。这些类型的存在不授予现代写入权。

## 3. Source boundary

- **A — Modern runtime：** Continuous Surface、WorldPresence exact position、PlayerParty Surface travel、
  Squad/SquadWorldMotion、WorldSite/Claim/Actual Control、CharacterEncounter、Separate Space。
- **B — Compatibility runtime adapter：** `LegacyPlayerPartyHexTravelCompatibility`、
  `LegacyPlayerPartyLocalVisibleTravelCompatibility`、`LegacyPlayerPartyOutdoorLocalMapCompatibility`、
  `LegacyWildernessLocalMapFallback`。入口必须由 LocalVisible、active Outdoor LocalMap 或无 SurfaceId 的
  World-mode Hex plan 明确 gate；SurfaceVisible 永远不得进入。
- **C — Legacy serialization/content input：** 上述 DTO、parser、旧 enum numeric values、旧 Content schema；
  读取后单向转换，现代 Save 不重新输出 retired authority。
- **D — Removed dead code：** zero-caller WorldTravel order wrappers、旧 Hex FactionFlag construction chain、
  dead WorldSite access wrappers，以及此前 C 已删除的 TerritoryRegion／StrategicEncounter／
  RetreatingArmy／LingeringBattlefield runtime。

## 4. Development guards

`LegacyRuntimeInvariant` 只在 New Game bootstrap、Snapshot capture 等边界执行，检查：

- normal Continuous WorldPresence 不含 AtHex；
- PlayerParty 不使用 legacy execution；
- Continuous Site 不使用 AtWorldSite；
- Current opening scenario 不声明 `InitialFormalArmyIds`；
- normal Outdoor 不激活 Outdoor LocalMap；
- modern snapshot 不输出 TerritoryRegion、legacy residual、RetreatingArmy、old StrategicEncounter、
  Ch01 formation compatibility 或 AtHex Character authority。

该 guard 不做每帧全世界扫描。

## 5. Freeze rule

后续 Gameplay／Content 开发若需要读取旧数据，必须在 Data／Serialization／LegacyMigration 边界先转成现代
authority。禁止在 normal Gameplay 新增 FormalArmy、ArmyStack、TerritoryRegion、AtHex、Outdoor LocalMap 或
Hex travel 依赖；禁止把 compatibility adapter 作为新功能捷径。

## 6. Seal state

代码与文档已达到 Final Seal 候选状态，仍需制作人最终 smoke。通过前项目状态保持
**Implementation Complete / Producer Acceptance Pending**，暂不宣称 Continuous World Legacy Migration Complete。

## 7. Final dead-runtime residue purge

全仓 caller audit 后删除以下零调用 residue：

- `BattleEngagementKinds`（旧 Battle initiator／decision option 类型）；
- `FormalArmyLocationKinds`（旧 FormalArmy location／movement／order／route enum）；
- `HexStrategicRuntime`（旧 Hex runtime mode switch）；
- `PlayerPartyWorldMotion` 中退役的 FormalArmy pursuit metadata；
- `PartyWorldPresence` 中从未产生非空值的 FormalArmy focus metadata；
- 零 caller 的 `HexTerrainPresentation` 与 `HexTerrainVisualInset` 表现辅助类型。

`HexMetrics` 与 `HexWorldMapRenderBounds` 仍有测试 caller，继续保留。旧 FormalArmy／ArmyMembership
snapshot DTO、AtHex 与 owner／command enum numeric value、TerritoryRegion input、Ch01 compatibility、
`LegacyPlayerParty*Compatibility` 等读取／迁移边界也继续保留。本次只收口死代码与注释，现代 runtime
authority、CharacterEncounter、PlayerParty travel、NPC Squad 与 Separate Space 行为均未改变。
