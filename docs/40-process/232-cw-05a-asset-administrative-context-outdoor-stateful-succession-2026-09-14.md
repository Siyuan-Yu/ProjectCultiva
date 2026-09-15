# CW-05A：Asset Administrative Context + Outdoor Stateful Asset Succession

> 状态：Producer Accepted / Sealed（2026-09-15 正常玩法人工验收）
> 日期：2026-09-14
> 前置：CW-04／CW-04.5 Producer Accepted / Sealed
> 范围：Farm Plot administrative manager、Outdoor stateful object 存续与 farm WorldTick；不进入 CW-05B

> 验收处理：底层 Probe 路线已退役；当前 authority 由 [CW-05 Closing 正常建田闭环](234-cw-05c-constructible-farm-administrative-lifecycle-2026-09-14.md)完成正常玩法验收并封板。

## 目标与 authority

CW-05A 使用 Farm Plot 闭合首个 Administrative Asset 纵切。架构复核在制作人验收前纠正了早期实现曾将全部 Outdoor stateful kinds 临时视作 administrative assets 的问题。正式规则是 **Stateful World Object != Administrative Asset**：`OutdoorStatefulObjectBoard` 继续保存树／墙的 HP、Destroyed 和农田的 CropId、CropStage、Growth；只有显式进入行政资产语义的 farm cells 才登记到 `OutdoorAdministrativeAssetAnchorBoard`，并由 `WorldAdministrativeAssetControlResolver` 调用唯一的 `WorldSiteAdministrativeControlResolver`。`placement.SiteId` 仅是 authoring/source grouping，不参与 current manager 判断。

Physical Asset Identity、Property Ownership 与 Administrative Manager 是三个独立概念。本轮只实现 Administrative Manager；manager 是 `WorldSite`，Faction 仅从 `managingSite.OwnerFactionId` 派生。本轮不建立 Property Ownership，也不把管理接续解释为产权转移。

## Identity 与 anchor source

- Data bootstrap 枚举 checked-in `OutdoorWorldSurfaceDefinition.SitePlacements`，不读取 GameObject、Host View、loaded chunk、Hex center、LocalMap 坐标或 nearest Site。
- Core `OutdoorStatefulObjectSemantics` 管理 farm／tree／wall 的持久 identity、destructible 和 per-cell 语义；`OutdoorAdministrativeAssetSemantics` 是独立显式准入层，当前只接受 herbField／grainField。
- herbField／grainField 按 baked rectangle 的 `SourceCellsW/H` 计算逐格中心，并调用既有 `OutdoorStatefulObjectId.ForCell(placement.StableId,x,y)`。每个 farm cell 是一个 stable administrative asset 和一个 anchor。treeS/M/L 与 authored wall 仍是 stateful world objects，但不登记行政 anchor。
- anchor board 在 NewGame ContentRuntime bootstrap 与 Snapshot content-shell rehydrate 重建，不写 Snapshot。

## Manager、succession 与 unmanaged

查询链固定为 `AdministrativeAsset StableId → AdministrativeAssetAnchorBoard → SurfaceId + exact WorldPosition → WorldSiteAdministrativeControlResolver → Site + winning TerritoryClaim`。已知行政资产返回成功但 Site/Claim 为 null 表示合法 unmanaged。tree／wall 或其他未显式准入的 stateful ID 返回 `false / NotAdministrativeAsset`；其所在领土只能称为 Territorial Context，本轮不增加该查询 API。

Site A 有效时解析 A；B 后建立且重叠时，Claim history 仍使 A 获胜；A 核心失效后现有 resolver 忽略 A，查询同一 anchor 得到 B；B 失效后得到 none。该调用链从不写 `OutdoorStatefulObjectBoard`，所以 StableId、HP／Destroyed、CropId／Stage／Growth 均不改变。

## Farm world-time migration

正式 enum 为 Core `OutdoorFarmCropStage`，整数映射保持 Empty=0、Growing=1、Mature=2、Ruined=3，Snapshot schema 不变。`SimulationLoop.TickOnce` 每次 WorldTick 增加后调用 `OutdoorFarmGrowthService.Advance`；Growing 每 tick 增加 0.012，达到 1 时设 Mature/Growth=1。服务枚举完整 Core FarmPlots board，不查询 manager、Host registry 或 streaming loaded set。

`HostFarmFieldLabor` 不再用 PresentationDeltaTime 改自然状态；它仅在观察到新 WorldTick 时，让已加载的 `HostMapPlotCell` 从 Core board 刷新 presentation。卸载期间 Core 照常推进，重新加载时 `Configure` 读取最新状态。

## Persistence boundary

本 CW-05A 阶段 Snapshot 保存 `OutdoorDestructibleSnapshotDto`、`OutdoorFarmPlotSnapshotDto` 与 TerritoryClaim history。Asset anchors 由 Content 重建；Current Managing Site/Faction/Claim 每次查询派生，本阶段未新增 snapshot 字段或 schema version；Closing 的运行时农田 placement/序列增量见234。现有 Host 恢复流程先让 `SnapshotService` 建立候选 world 并恢复 state/claims，再由 `RuntimeContentShellBootstrap` 对该 world 重建 Content anchor；发布后的 resolver 只从这两类已恢复真源派生 manager，不依赖二者的先后写入。

## Diagnostics 已退役

CW-05A 阶段专用 ID 输入、最近资产查询、复制管理诊断、Growing Farm Probe 及无消费者 helper 已在 CW-05 Closing 删除。只保留长期通用运行诊断。制作人验收统一见 [234](234-cw-05c-constructible-farm-administrative-lifecycle-2026-09-14.md)，无需 Debug／ID／Claim。

## 修改文件

- `Assets/Scripts/Core/Exploration/OutdoorAdministrativeAssetAdministration.cs`
- `Assets/Scripts/Core/Exploration/LocalMapSession.cs`
- `Assets/Scripts/Core/Simulation/SimulationWorld.cs`
- `Assets/Scripts/Core/Simulation/SimulationLoop.cs`
- `Assets/Scripts/Core/World/Strategic/WorldAdministrativeAssetControlResolver.cs`
- `Assets/Scripts/Data/Bootstrap/OutdoorAdministrativeAssetAnchorBootstrap.cs`
- `Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs`
- `Assets/Scripts/Data/Bootstrap/RuntimeContentShellBootstrap.cs`
- `Assets/Scripts/Data/Content/OutdoorWorldSurfaceDefinition.cs`
- `Assets/Scripts/Unity/Host/HostDemoTileMap.cs`
- `Assets/Scripts/Unity/Host/HostMapPlotCell.cs`
- `Assets/Scripts/Unity/Host/HostFarmFieldRegistry.cs`
- `Assets/Scripts/Unity/Host/HostFarmFieldLabor.cs`
- `Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs`
- 状态与入口文档

## Validation

- `tools/offline-compile.ps1`：Core 466、Data 79、Unity Host 143、Unity Editor 6、Tests 194、PlayModeTests 3、Assembly-CSharp 51、Assembly-CSharp-Editor 1 全部 0 error；保留既有 warning。
- Content 静态枚举只按 `OutdoorAdministrativeAssetSemantics` 生成 260 个 farm-cell administrative anchors，260 unique／0 duplicate；grainField 6 placements、herbField 4 placements。语义矩阵定向检查为 grainField/herbField=true，treeS/treeM/treeL/wall=false；Content 中 41 个 tree/wall placements 均不准入行政 AnchorBoard。
- 定向搜索确认 manager 查询唯一调用 `WorldSiteAdministrativeControlResolver`；administrative asset state/anchor 未新增 manager/owner 字段；tree/wall destructible persistence 与 Snapshot 路径仍保留；Host 已无 `TickPassiveGrowth`、`PassiveGrowthPerSecond` 或 Host-only `PlotCropStage`；Core growth 不引用 PresentationDeltaTime、Host registry、GameObject 或 loaded chunk。
- `git diff --check` 通过（仅显示仓库既有 LF→CRLF 工作树提示）。
- 尝试执行现成 `PlayableDayBootstrapPhaseATests` headless filter；runner 共报告 1 pass／6 fail，6 项均在进入产品 bootstrap 前因本机 Mono 与 Unity 2022.3.6f1 `UnityEngine.CoreModule` 不匹配而无法解析 `UnityEngine.Application.dataPath`，因此只记为测试环境失败，不作为功能失败或通过证据。
- 未启动 Unity、Test Runner、EditMode、PlayMode、batchmode 或 Bake。

## 制作人验收已合并

旧 Probe／StableId／Claim 验收路线已退役。统一执行 [234 CW-05 Closing](234-cw-05c-constructible-farm-administrative-lifecycle-2026-09-14.md) 的五步正常玩法；本阶段未标记 Producer Accepted。

## 后续延期

玩家组织农作授权已由 CW-05B 实现，可建造农田与其建设权限已由 Closing 实现。NPC schedule economy、SettlementProduction、public stock routing、普通房屋/工坊与产权仍归后续 Economy / Automated Settlement Production migration。


## 2026-09-15 制作人封板

CW-05A、CW-05B 与 CW-05 Closing Slice 正式 **Producer Accepted / Sealed**。制作人已通过正常可建农田、管理接续、拆旗保留资产、重新取得管理及 Save/Load 验收。Subsequently Producer Accepted after normal gameplay validation. 历史记录中当时未运行 Unity 验证的事实保持不变。
