# Phase 5R 最终收口 — WorldSite／Wilderness 旅行权威

- 日期：2026-08-30
- 状态：**总结完成，待制作人确认；不含运行时代码改动**
- 关键提交：`d551ea0`（B7A）、`9099cd0`（B7B）、`5da552a`（B7C）

## 最终权威图

- Physical truth：`PlayerPartyWorldMotion.WorldPosition`。
- Context truth：`LocationKind + SiteId`；`AtWorldSite(S)` 不替代物理位置。
- World executor：WorldMap open；LocalVisible executor：WorldMap closed 的 Character Local Transform 与 Canonical mapping。
- WorldMap 开关只转移 executor ownership，不改变 Destination、HexPath 或 Travel Order。
- Site/Wilderness 使用统一 Surface passability；PlayerParty 不因 SiteId block、加隐藏 cost 或触发 MandatoryTransit。
- Target Site 以 whole-footprint legal ingress goal-set 为目标；Transit Site 只切换 context/materialize/egress，不 CompleteMove。
- 仅 `DestinationSiteId == current Site` 且正式 ingress 完成后允许 `FinishArrival / CompleteMove`。
- Formal egress 由 `SourceHex / DestinationHex / BoundaryContactWorld` authority 决定；Canonical crossing point 与 route destination hex 分离。
- Preview 与 executor 共享 HexPath 与 departure connection，不绘制未执行的 Site 内部 prefix。
- Anchor / Presence 不是 PlayerParty physical truth。

## 暂停规则

打开 WorldMap 的 false→true 强制 ManualPause；WorldMap 与 LocalMap 均可由 Space/UI Pause/Resume；Travel Order 不修改 Pause；关闭 WorldMap 保留 Pause/Running；Modal 是强制暂停 authority，普通 Resume 不得解除。

## 保留／移除／延期

保留：SiteId/OccupiedHexes、近景实体化、正式进入／离开、目标到达、Transit 上下文切换、占领／归属上下文，以及世界与近景映射、选择、标记／路径表现。

移除（PlayerParty）：地点即障碍、MandatoryTransit/Gateway 确认、Anchor/Presence 路径权威、`MandatoryWaypointSiteId`。

延期：CurrentHex 遗留迁移；Host GatewayConfirm 死代码框架；FormalArmy `WorldSiteTransitPolicy`（footprint 阻挡／MandatoryTransit）；战斗／SupportRing；外交／Gate／脚本限制。

## 已知问题

当前 5R PlayerParty 旅行范围内没有已知阻塞问题；未声称完成 Unity 自动验收。

## 后续禁止重新引入

不得恢复 non-target Site 默认阻挡、PlayerParty Gateway/MandatoryTransit fallback、以 Anchor/Presence 改写 physical truth、或让 Preview 绘制 executor 不执行的 route prefix。
