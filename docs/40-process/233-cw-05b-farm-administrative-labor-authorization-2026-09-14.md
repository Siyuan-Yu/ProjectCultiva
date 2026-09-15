# CW-05B：Farm Administrative Labor Authorization

> 状态：Producer Accepted / Sealed（2026-09-15 正常玩法人工验收）
> 日期：2026-09-14
> 前置：CW-05A Implementation Complete / Acceptance Folded Into CW-05 Closing
> 范围：Player-issued organized farm labor；不进入 NPC schedule、Settlement production 或 generic construction

## 为什么改为产品纵切

CW-05A 已建立 farm cell identity、canonical administrative anchor、动态 managing Site 和独立于 manager/streaming 的 WorldTick 自然生长。制作人认为 StableId／Claim／Probe 路线过于技术化，因此 CW-05A 不单独标记 Producer Accepted；其 authority 改由本轮正常农田 hover、右键农作和管理权丢失行为直接验收。CW-05A／CW-05B 验收现并入 [CW-05 Closing](234-cw-05c-constructible-farm-administrative-lifecycle-2026-09-14.md)，全部正常玩法通过后再一起封板。

## Authorization authority

Core `WorldAdministrativeAssetAuthorizationService.ResolveForFaction` 的固定调用链为：

`Farm StableCellId → WorldAdministrativeAssetControlResolver → OutdoorAdministrativeAssetAnchorBoard → WorldSiteAdministrativeControlResolver → managing WorldSite / winning Claim → actingFactionId comparison`

结果区分 `Allowed`、`Unmanaged`、`ManagedByOtherFaction`、`NotAdministrativeAsset` 与 `Invalid`。只有 `managingSite != null` 且 `managingSite.OwnerFactionId == actingFactionId` 时允许组织劳动。它不缓存 SiteId/FactionId，不复制 Claim winner 算法，也不建立 Property Ownership。

## Player farm labor gating

正常 context click 与 Interact hover 优先拾取真实 `HostMapPlotCell`。授权键是该格 `StableCellId`；`LocationId` 只在授权通过后用于组织整片田区的 job search。无人管理和他方管理均显示正常中文原因与不可执行光标；右键命中后即使拒绝也消费输入，不会降级成普通移动。

`HostFarmFieldLabor.BeginForSelection` 先授权玩家点击的 cell，再以 `world.Strategic.PlayerFactionId` 创建玩家发起的 Worker。拒绝时不创建劳动并向当前选中己方角色显示一次反馈。

## Per-cell reauthorization 与 succession

玩家发起的 Worker 保存 `ActingFactionId` 和需要行政授权的标记。选择下一格时，候选列表逐格以 `StableCellId` 调用 Core authorization；抵达准备开工和完成写入状态前也会再次解析。

- Site A → Site B，OwnerFactionId 仍为玩家势力：重新解析仍为 `Allowed`，劳动继续，不依赖旧 SiteId。
- Site A → none：当前格失权；先尝试同田区其他合法格，没有则停止并提示“农田已失去己方管理，自动农作停止”。
- Site A → foreign faction：与 none 相同停止组织劳动。

Farm identity、CropId、Stage 与 Growth 不因授权变化而重置。

## Natural state boundary

Passive growth 仍只由 `SimulationLoop.TickOnce → OutdoorFarmGrowthService.Advance` 推进，速率为每 WorldTick 0.012。它直接枚举 Core `OutdoorStatefulObjectBoard.FarmPlots`，不查询 manager、Host View 或 loaded chunks。行政授权只约束玩家发起的播种／照料／收获／清理。

## Deferred Economy / Automated Settlement Production

`SyncNpcScheduleFarmers` 及旧 WorkArea/Schedule 路径保持现状，不消费本轮玩家授权；`SettlementState`、`SettlementProductionHandler`、`WorkAssignmentComponent`、public inventory、warehouse、generic building construction 与 Property Ownership 均未迁移。后续统一归入 Economy / Automated Settlement Production migration；本次 CW-05 Closing 仅增加可建造农田。

## 修改文件

- `Assets/Scripts/Core/World/Strategic/WorldAdministrativeAssetControlResolver.cs`
- `Assets/Scripts/Unity/Host/HostFarmFieldRegistry.cs`
- `Assets/Scripts/Unity/Host/HostFarmFieldLabor.cs`
- `Assets/Scripts/Unity/Host/HostWorkTargetMode.cs`
- 当前状态与开发记录文档

## Validation

- `tools/offline-compile.ps1`：Core、Data、Unity Host、Unity Editor、Tests、PlayModeTests、Assembly-CSharp 与 Assembly-CSharp-Editor 全部 0 error；保留既有 warning。
- 定向静态检查 authorization 调用链、StableCellId 输入、逐格过滤、开工/完成前复核、拒绝输入消费、自然生长无 manager gate、Snapshot 无 manager schema、NPC/Settlement deferred 范围。
- `git diff --check`。
- 未启动 Unity、Test Runner、EditMode、PlayMode、batchmode 或 Bake。

## 制作人验收已合并

统一执行 [CW-05 Closing 的五步正常玩法路线](234-cw-05c-constructible-farm-administrative-lifecycle-2026-09-14.md)。CW-05A、CW-05B 和 Closing Slice 均由制作人验收后才能一起封板。


## 2026-09-15 制作人封板

CW-05A、CW-05B 与 CW-05 Closing Slice 正式 **Producer Accepted / Sealed**。制作人已通过正常可建农田、管理接续、拆旗保留资产、重新取得管理及 Save/Load 验收。Subsequently Producer Accepted after normal gameplay validation. 历史记录中当时未运行 Unity 验证的事实保持不变。
