# Phase 5R-B7B — WorldSite 经过与目的地权威清理

- 日期：2026-08-30
- 基线：`d551ea0`（B7A，已存在于 `origin/dev`）
- 状态：**审计完成；本轮不提交**

## 权威矩阵

| 场景 | DestinationSiteId | 允许正式 ingress / CompleteMove | 权威入口 |
|---|---|---|---|
| 普通 Wilderness→Wilderness | 空 | 否 | `PlayerPartyWorldMotion.BeginAutoTravel` + executor |
| 经过非目标 WorldSite | 最终目标保持不变 | 否 | `TryCommitThroughSitePassage` / formal egress |
| 目标 WorldSite | 目标 Site S | 是，仅一次 | `FinishArrival` → `EnterWorldSiteAsParty` |
| WorldMap 开/关 | 不变 | 否 | takeover 只切换 ExecutionMode |

## 审计结论

- `FinishArrival` 是 PlayerParty 的唯一目标到达完成点；其 `DestinationSiteId == site.SiteId` 守卫保留。
- `EnterWorldSiteAsParty` 与 `TrySetAtWorldSitePreservingWorldPosition` 仅用于目标 Site 或明确的本地查看，不被 transit 分支调用。
- 非目标 Site 通过 `TryCommitThroughSitePassage` 保持 HexPath、SegmentIndex、DestinationSiteId 与移动顺序，随后由正式 egress 继续。
- `PlayerPartyWorldLocationQuery` 的 Site departure 预览从 `SiteDepartureExitHex` 开始，和执行器一致。
- FormalArmy 的 `WorldSiteTransitPolicy.BuildBlockedFootprintHexes` 保留；PlayerParty 不再消费 MandatoryTransit/Gateway 规则。
- `MandatoryWaypointSiteId` 及 Host GatewayConfirm scaffold 仍是遗留死状态，未改变运行时权威；建议后续独立清理，避免与 FormalArmy policy 混改。

## 特殊情况

1. `BeginAutoTravel` 对空路径或已在目标 hex 的请求直接 `CompleteMove`；这是终点幂等保护，需保持。
2. `AtWorldSite` + `IsSiteDeparturePending` 只表示当前 Site 上下文/出境瞬态，不得被当作目标到达。
3. Anchor、Presence、CurrentHex 的修正只用于连续位置解析，不得触发 CompleteMove 或改写 DestinationSiteId。

## 验证

本轮仅进行静态审计与文档记录；未运行 Unity、未修改 Travel/Exit/Camera/Repath 行为、未提交 B7B。
