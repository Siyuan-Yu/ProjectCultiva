# Phase 5A — Travel Code Cleanup & Authority Consolidation

- Date: 2026-08-29
- Baseline: `dev_1 @ 47b3f89`（phase4真正完整的版本）
- Status: behavior-preserving cleanup only（待 LevelTester 人工验收；未 commit）

## 做了什么

1. `HostWorldMapPanel` Close Expand 路径：委托已有 Core API `PlayerPartyHexTravelService.CloseWorldMapTakeover`（Cancel + EnterLocal）；`ExpandLocalMap` 仍在 Host。
2. **删除死链** `WorldTravelService.AdvanceTravel`（no-op）及其在 `SimulationLoop` 的唯一调用；**不**改为转发真实 Advance（避免双推进）。
3. **删除** `StrategicTravelDriver.BeginArrivalCapture` / `ArrivedScratch`（仅 Clear、无消费者）。
4. 注释改为标明真推进链：`SimulationLoop → StrategicTravelDriver.AfterTravelTick → PlayerPartyHexTravelService.AdvanceAll`。
5. 删除全仓库零引用死类型 `HexTravelPlan`。

## 明确没做什么

- 未新增 Movement Executor / LocalVisible / TravelPlan / Coordinator
- 未改 `PlayerPartyWorldMotion` 字段与生命周期
- 未改 BeginTravel / Pathfinding / Destination
- 未改 Marker / Route Preview / Hex Math / WorldLocationQuery
- 未改 Phase 2C Edge / Wilderness Transition
- 未动 `ArrivalNoticeService.AfterTravelTick` 等未证明安全的孤儿 API
- Close 契约（5A 当时）：AutoTravel 中关 WorldMap → Cancel Travel → Expand LocalMap（**已被 Phase 5B supersede**）

## 后续 Phase 5（未实现）

Phase 5B 已完成 View Takeover（Preserve + LocalVisible）；Executor / Local A* 仍属后续。见 [173](173-phase-5b-worldmap-localmap-travel-view-takeover-2026-08-29.md)。
