# 第一章起事入口与主管府战争门槛修正

**日期：**2026-09-05  
**状态：**实现完成，待 Unity 人工验收  
**范围：**第一章主管府军事占领，不涉及普通 NPC 犯罪式袭击或 Local Combat 重构

## 根因

主管府 `ControlCoreState.LocationId` 保存的是 WorkArea 绑定的 LocalPlace ID，例如 `loc_ref_road_hub`。旧代码把它直接与 `WorldSite.LocalMapId` 比较；两者并非同一层级，导致 CaptureObjective 的 `SiteId` 留空。之后战争门槛无法找到真实 Owner，第一击与占领事务也会错误跳过 WorldSite。

## 正式解析与生命周期

`CaptureObjectiveService.TryResolveControlCoreSite` 统一执行：

`ControlCore.WorkArea.LocationId → WorldRegion.TryGet → WorldLocationState.LocalMapId → WorldSite.LocalMapId → WorldSite.SiteId`

若 CaptureObjective 已有能验证的 SiteId，优先使用。新解析成功会回填 `CaptureObjective.SiteId`。WorkArea 注册可能发生在 WorldRegion 建立之前，因此注册允许解析失败；`WorldRegionBootstrap` 成功建立／切换地点表后调用重绑，`TryBeginMilitaryAssault` 和 `TryCompleteWorldSiteCapture` 仍在操作时懒解析，保证不存在初始化顺序依赖。

## 战争与占领

主管府解析到有 Owner 的 WorldSite 时，攻方不同于 Owner 必须处于 Active War。该规则由 `CaptureObjectiveService` 执行，`ControlCoreService.ApplyStrikeFromAttacker` 继续在真正扣耐久前调用它，故未起事时不能造成第一点伤害。无主 Site 保持旧语义。

占领完成同样先解析并回填 SiteId，再走既有 `WorldSiteTerritoryTransferService.Transfer`，从而同步 WorldSite Owner 与 TerritoryRegion 控制者；没有另造 Capture 路径。

## Host 入口

右键主管府与 F8 Combat targeting 都只调用同一领域预检。F8 失败时不会下移动命令、不会开始 `HostControlCoreAssault`，且会退出 targeting。第一章身处青石荒村、尚未起事时，右键菜单始终显示「起事／反抗宗门」；炼气不足时显示明确门槛。势力总览仍完全只读。

## 人工验收

1. 起事前右键主管府，确认可见起事入口且攻击不可用；F8 Combat 点击主管府也不开始突击。
2. 队伍有炼气成员后右键主管府起事，确认势力页旧宗门从「宗主」变为「战争」。
3. 再攻击主管府并占领，确认 Site Owner 与 Territory 控制者均易主。

未运行 Unity Editor／Test Runner；未提交、未推送。
