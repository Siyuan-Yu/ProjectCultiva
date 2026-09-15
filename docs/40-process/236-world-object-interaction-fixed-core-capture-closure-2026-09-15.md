# CW-09.5：World Object Interaction Unification + Fixed SiteCore Capture Authority Closure

> 状态：Implementation Completed / Producer Acceptance Pending｜优先级：P0｜最后更新：2026-09-15

## 问题与正式边界

制作人已通过固定据点战争、攻破、占领、Actual Control／农田管理接续、可拆旗摧毁和 CharacterEncounter 入场主链。后续发现两个收口问题：占领后的议政厅菜单仍无条件显示攻击；Host 左键检视与右键行为分别重复拾取对象。固定核心同时仍借用旧 `CaptureObjective` runtime 保存绑定与物理状态。

正式交互为：左键只选择／检视世界对象，右键执行该对象当前允许的 Gameplay 行为。NPC 继续使用 Entity picker；persistent world object 统一由 `HostWorldObjectPicker` 生成 `WorldObjectInteractionTarget`，precedence 为 ControlCore／FactionFlag → FarmPlot → Destructible → Housing → WorkArea。`HostHousingAreaSelection` 只消费 target 写入现有左上角 `WorldObjectInspectSelection`；`HostNpcContextMenu` 与 `HostWorkTargetMode` 消费同一分类，不再各自决定同一点是什么对象。

## Inspect 与 Context Action

- Fixed ControlCore：详情实时读取绑定 WorldSite，显示据点、Lv、耐久、Owner、己方／他方状态、breach／occupy；权限 ID 映射为住房管理／课表管理。右键实时读取 `WorldSite.OwnerFactionId`，敌方为查看详情＋攻击，己方只有查看详情。
- FactionFlag：新增统一左键详情，显示 HP、Faction、Linked Site、Core Level、理论范围、核心有效状态和己方／他方前哨。右键己方为查看详情＋拆除，敌方为查看详情＋攻击。
- FarmPlot：详情显示作物／阶段／成长，并由 `StableCellId → WorldAdministrativeAssetControlResolver → ManagingSite.OwnerFactionId` 每次动态显示管理 Site／Faction 与己方、他方或无人管理。右键仍直接组织农作。
- Tree／Wall：详情保留类别、名称、HP、产量／摧毁结果，并以物件世界位置查询 `WorldSiteAdministrativeControlResolver` 显示“所在行政区”；树木不是 Administrative Asset。右键继续砍伐／拆毁。
- Housing／WorkArea：保留现有只读详情；未伪造升级或管理动作。Level 2／3 Content 与成本未定义，因此不暴露 Upgrade。

## Fixed Capture Authority

`WorldSite.OwnerFactionId` 是政治 ownership authority；TerritoryClaim 是历史空间 authority；Administrative Asset manager 由 Anchor → Actual Managing Site → Site Owner 动态派生；`ControlCoreState` 只保存固定建筑物理状态与 Content 派生的 `BoundWorldSiteId`。

`ControlCoreBoard` 维护 WorkAreaId → Core 与 SiteId → WorkAreaId 双向索引，重复同对绑定幂等，一 Core 多 Site 或一 Site 多 Core 拒绝。`ContentRuntimeBootstrap` 只从 outdoor controlCore placement 的 `SiteId + StableId + BoundLocationId` 验证并绑定，不从 LocalMap／WorldRegion 猜测。

固定首击资格和占领事务迁入 `WorldSiteCoreWarfareService`：验证 non-removable Site、攻方非 Owner 和 active War；占领完成经 `WorldSiteTerritoryTransferService` 写 Owner，随后恢复 Core 满耐久、重建 `SettlementAuthoritySync`、通知 `OnWorldSiteCaptured` 和完成 CharacterEncounter objective。Claim、Farm 和 NPC identity 不写。可拆旗仍完全走 `FactionFlagService` destruction。

`WorldSiteTerritoryTransferService` 不再要求 `TerritoryRegionId`、Region 实例或 PrimaryWorldSiteId；成功条件只依赖有效 WorldSite 与新 FactionId。Owner 是 cause，coverage rebuild 负责 Region／Hex compatibility projection。authored Region 错误应在 Content validation／development diagnostics 报告。

## Runtime 与 Snapshot 清理

已删除运行时 `CaptureObjectiveState`、`CaptureObjectiveBoard`、`CaptureObjectiveService`、StrategicBoard 的 `CaptureObjectives`、全部完成 hook、Ch01 legacy completed handler、`ControlCore.PlayerControlled`／`AnyPlayerControlled`、LocalMap ownership guessing API、dead ControlCore LocalMap spatial helpers，以及无代码／Content 消费的 breach／capture story flag writes。

WorldSnapshot schema 保持 v6 additive compatibility。新 optional `controlCores` 仅写 `WorkAreaId + CurrentDurability + OccupyProgressSeconds`；MaxDurability、Defense、HoldSeconds 和 Site binding 由 Content shell 提供。新 Save 不写 `captureObjectives`。Deserializer 仍把旧键读取为 `LegacyCaptureObjectiveSnapshotDto` migration input：`Completed=true` 恢复为完整 Core、零进度；否则迁移 HP／进度。旧 SiteId／ObjectiveId／MaxHp／HoldSeconds 不进入新 authority；损坏的新 `controlCores` 拒绝恢复。

## 延期

本轮没有实现 Level 2／3 Upgrade、SettlementProduction／NPC schedule economy 迁移、NPC 自动攻城、普通建筑战争或 property ownership。`SettlementAuthoritySync` 作为当前住房／课表派生权限 bridge 保留。

## 验证

- BaseGame strict loader 与 reference bootstrap 由 SiteCore 定向用例覆盖。
- 定向 Core 用例覆盖双向绑定、无 TerritoryRegion projection 的 Capture、Owner／Claim／Farm／NPC identity、removable flag、CharacterEncounter objective、旧 v6 physical migration 与新 Save key。
- offline compile 覆盖 Core、Data、Unity Host、Unity Editor、Tests、PlayModeTests 与 Assembly-CSharp 系列；不运行 Unity／TestRunner／batchmode／Bake。
- Host persistent object 路径静态核对：左键、右键与 farm direct action 均消费 `HostWorldObjectPicker`；菜单不缓存 Owner。

No commit created.
Changes remain uncommitted for producer review.
