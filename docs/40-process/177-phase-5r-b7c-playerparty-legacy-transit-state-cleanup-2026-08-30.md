# Phase 5R-B7C — PlayerParty 遗留经过状态清理

- 日期：2026-08-30
- 状态： **验证完成，待封板；本轮不提交**

## 使用者关系图

`MandatoryWaypointSiteId` 仅存在于 PlayerPartyWorldMotion 的旧字段、setter、reset/clear，以及 HostWorldMapPanel 的旧状态显示分支；无 runtime writer/decision consumer、无测试引用、无 JSON/save DTO/serializer/migration/reflection persistence。

## 本轮删除

- PlayerPartyWorldMotion：字段、setter 及全部清理点。
- HostWorldMapPanel：旧 Mandatory Gateway 状态显示分支。

判定为纯 dead state；无 save compatibility impact。

## 延后清理

Host `GatewayConfirm` dead scaffold 暂留。当前没有 PlayerParty 开启入口，不参与 Travel authority；因尚未证明无共享 UI 依赖，本轮不删除，后续另做纯 Host UI dead-code cleanup。

FormalArmy 的 `WorldSiteTransitPolicy` 及其现役 blocked-footprint/MandatoryTransit 逻辑未修改。

## 剩余 Site 特殊分支

保留的 `AtWorldSite`、`SiteId`、footprint、`SiteDeparture` 分支均属于 Site context/materialization、正式 ingress/egress、目标到达、Transit context switch 或 presentation/query；不再保留 PlayerParty Site-as-obstacle、Gateway confirmation 或 Anchor/Presence route authority。

## 验证

- 全仓 `MandatoryWaypointSiteId` / `SetMandatoryWaypoint`：0 runtime、0 test、0 serializer/docs-code 引用（历史文档描述除外）。
- 未重新执行非 Unity Core+Data+Runtime Host 编译；当前环境缺少先前临时编译工程源码。
- 未运行 Unity、PlayMode 或 Test Runner。
- 已完成 `MandatoryWaypointSiteId` / `SetMandatoryWaypoint` runtime/test/serialization 0 引用审计。
- `git diff --check` 通过；无 save compatibility impact。
- 因仅删除 dead state/presentation 分支，不要求额外 LevelTester 验收。
