# Phase 5S-B2 — 手动战斗真实 LocalMap 架构设计

- 状态：仅设计；未改运行时代码，未运行 Unity，未提交。

## 当前生命周期

`BattleOfferService` assigns `EncounterLocalMapId`; `HostStrategicInterruptPresenter.EnterManualEncounter` falls back to `StrategicEncounterCatalog.DefaultEncounterLocalMapId`, calls `StrategicEncounterSpawner.PlanManualEncounter`, clears Site focus and writes that map to `PartyWorld.LocalMapId`. `LocalMapVisibility` and `PlayableHostBootstrap` then load the encounter map. `StrategicEncounterSpawner` owns tracked battlefield spawn/reuse/prune/clear and remnants. Completion clears the encounter session and its scoped presentation before normal location visibility resumes.

## 目标生命周期

`WorldBattleLocation` resolves a real `BattleLocalMapId` → reuse current `LocalMapSession` if ids match, otherwise enter that real location through existing 5R Site/Wilderness materialization → create a `CombatContext` over the session → materialize only missing participants → resolve combat → remove only encounter-scoped presentation and CombatContext, retaining the session, nonparticipants and real character state.

## 权威

Introduce a Core `BattleLocalMapResolver` (not implemented): input is battle identity plus explicit resolution kind. `WORLD_SITE` resolves `SiteId → WorldSite.LocalMapId`; `WILDERNESS` resolves `BattleHex → existing Wilderness LocalMap resolver`; `EXPLICIT_ENCOUNTER_MAP` retains the current special-map path for arena/dream/dungeon/scripted cases. The resolver, not Host UI, owns the location choice. `PartyWorld.LocalMapId` remains session state, not battle identity.

Current-session reuse belongs at the Host materialization/bootstrap boundary: compare active loaded map and `PartyWorld.LocalMapId` with resolved `BattleLocalMapId`; equal means no reload, no party rematerialization and no Canonical rewrite. Different means invoke existing 5R location materialization, never duplicate Site/Wilderness mapping inside encounter code.

## 参战者与实体规则

CombatContext owns membership (`Attacker`, `Defender`, `FriendlyReinforcement`, `EnemyReinforcement`), allegiance and result. It is not LocalMap population. Every participant refers to one `EntityId`/Character identity. Existing `StrategicEncounterSpawner` may retain scoped presentation, reuse, remnants, pruning and cleanup, but must stop assuming that a scoped encounter map is the whole LocalMap. `ClearSpawned` must only remove entities proven encounter-scoped; it must not clear original map NPCs/facilities/nonparticipants.

Reinforcements are selected by frozen EngagementArea/SupportRing eligibility, then materialized into the same map at a direction-derived edge. The future resolver should consume a battle-specific ingress descriptor derived from SurfaceExitConnection / BoundaryContact / SafeLanding concepts, but must not reuse or mutate Travel state.

## 已决的普通世界手动战斗资格与结束规则

Ordinary `WORLD_COMBAT` is manual only when PlayerParty / current controlled character is actually included in that battle's participant range. A distant friendly FormalArmy battle is background auto-battle: it does not load a battle LocalMap, move PlayerParty/Active Character, or offer ordinary remote manual control.

Consequently ordinary world combat has no “temporarily enter distant battle location, then return” flow. It begins and ends in the same real LocalMap: `CombatContext Begin → Combat → CombatContext End`. On completion PlayerParty remains at the battle location; there is no settlement screen, victory/defeat modal, return confirmation, automatic WorldMap opening, map reload, or pre-battle snapshot restoration. World state results (HP/injury/death/down, Army losses/destruction, and future capture/loot/prisoner rules) still write normally. Survivors, corpses/remnants and nonparticipants remain as the post-battle LocalMap population.

Only explicit `EXPLICIT_ENCOUNTER_MAP` cases—dreams, secret realms, arenas and scripted dungeons—may have an independent map, completion, return policy or settlement presentation.

## 迁移

| Phase | Scope | Risk / acceptance |
|---|---|---|
| B2-1 | Add resolver data contract with legacy explicit-map fallback; no call-site behavior change | Snapshot compatibility; Core tests |
| B2-2 | Route WorldSite/Wilderness manual-entry resolution through resolver; keep explicit map path | Existing manual encounter smoke test |
| B2-3 | Add current-session reuse gate in bootstrap/materialization | Same-map battle: no reload/Canonical change |
| B2-4 | Separate CombatContext membership from scoped spawn lifecycle | Nonparticipants remain visible; identity checks |
| B2-5 | Real-location completion policy and reinforcement edge placement | Unity LevelTester required |

Largest risks are snapshot compatibility around `EncounterLocalMapId`, incorrectly removing original LocalMap entities, and treating generated Army combatants as duplicate persistent Characters. First implementation scope is B2-1 only: resolver contract plus legacy-compatible explicit-map classification and tests; no combat resolution or materialization rewrite.
