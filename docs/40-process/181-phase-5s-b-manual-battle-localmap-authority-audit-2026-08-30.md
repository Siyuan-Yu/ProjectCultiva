# Phase 5S-B — 手动战斗真实 LocalMap 权威审计

- 状态：仅审计；未改运行时代码，未运行 Unity，未提交。

## 当前入口

`HostStrategicInterruptPresenter.EnterManualEncounter` receives `EncounterLocalMapId`; an empty value falls back to `StrategicEncounterCatalog.DefaultEncounterLocalMapId`, then writes it to `PartyWorld.LocalMapId`. `BattleOfferService.ResolveOfferEncounterLocalMapId` also defaults to that Encounter map. This is why manual battle enters a dedicated map instead of a WorldSite `LocalMapId` or ordinary Wilderness LocalMap.

`StrategicEncounterSpawner.PlanManualEncounter` manages tracked battlefield spawns/remnants; the flow may reuse, spawn, prune, or clear tracked entities. Existing LocalMap presentation therefore is not currently preserved as one unchanged physical scene. Participant identity is mixed: selected party entities are retained through participant state, while Army/remnant battlefield presence is managed as tracked spawns.

## 架构缺口

目标模型可在不重写战斗结算的前提下实现，但后续需独立改造：从 SiteId／BattleHex 解析战斗 LocalMap；保留原 LocalMap session；在同一 session 中实体化选定参战者；结束时留在同一地点而非 Encounter 图。未发现或新增 PlayerParty→WorldSite 攻击入口。

FormalArmy support selection already exists through frozen `BattleEngagementSupportArea`; it does not yet establish same-real-LocalMap materialization for all reinforcements.
