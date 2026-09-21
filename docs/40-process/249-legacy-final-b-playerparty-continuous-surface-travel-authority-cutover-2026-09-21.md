# LEGACY-FINAL-B — PlayerParty Continuous Surface Travel Authority Cutover

> 日期：2026-09-21  
> 状态：**Producer Accepted / Sealed**（2026-09-21）  
> 前置：LEGACY-FINAL-A 已 Producer Accepted / Sealed  
> 后续：LEGACY-FINAL-C

## 1. 正式 authority

正常 Current BaseGame 的 PlayerParty Outdoor 位置由 `PlayerPartyWorldMotion` 的
`SurfaceId + exact WorldPosition` 唯一表达。`LocationKind` 始终为 `AtWorldPosition`；
进入 WorldSite 的 Actual Control 区域只更新 `CurrentOutdoorWorldSiteId`，不切换为
`AtWorldSite`，也不创建 Outdoor LocalMap。

`CurrentHex` 与 destination Hex 只由精确世界位置单向派生，供旧 DTO、旧查询和诊断兼容。
它们不能反推、吸附或改写现代位置、路线与到达判定。Runtime Chunk 与旧 Hex 边界只是
普通 streaming seam。

## 2. 现代旅行链

正常 WorldMap 右键目标链固定为：

`exact WorldPosition / SiteArrival`
→ `PlayerPartySurfaceTravelService.BeginTravel`
→ `SurfaceGroundNavigation.TryFindRoute`
→ `PlayerPartyWorldMotion.BeginSurfaceAutoTravel`
→ `SurfaceVisible`
→ `HostPlayerPartyController.TickContinuousSurfaceAutoTravelMovement`
→ `ContinuousOutdoorSurfaceRuntime`
→ `PlayerPartySurfaceTravelService.TrySyncWorldPosition`
→ `CompleteSurfaceArrival`。

现代 plan 保存 exact destination、arrival radius、Surface route 与 waypoint index；不创建
`HexPath`，也不读取 Hex leg、Wilderness exit、SurfaceExitConnection 或 Outdoor LocalMap
边界推进。WorldMap 打开只暂停 Host path，关闭后重发当前 Surface subgoal，不切换 Domain
executor。

## 3. ExecutionMode 边界

- `SurfaceVisible = 3`：正常 Continuous Outdoor，由可见 Continuous Surface 表现执行；
- `LocalVisible = 2`：仅旧 Outdoor LocalMap / Hex edge compatibility；
- `World = 1`：仅旧 Hex world executor。

`StrategicTravelDriver` 只在 `World + nonempty HexPath + empty SurfaceId` 时推进旧 PlayerParty
Hex travel。现代 `SurfaceVisible` 不由 WorldTick 改写物理位置。

## 4. Presentation sync 与成员 presence

`ContinuousOutdoorSurfaceRuntime.SyncPartyPresentation` 只调用
`PlayerPartySurfaceTravelService.TrySyncWorldPosition`。该服务验证 SurfaceId、registered
navigation、coverage 与 segment walkability，随后提交 exact WorldPosition、派生 CurrentHex、
实时 WorldSite context，并单向 reconcile 随队成员。

正常 Continuous Surface 中，每个 PlayerParty member 都是
`PartyWorldPresenceMode.AtWorldPosition + PersonalSurfaceId`。WorldSite membership 是空间／行政
context，不是 PlayerParty location kind。

## 5. Snapshot 与迁移

`PlayerPartyTravelSnapshotDto` additive 保存 `SurfaceId` 与
`CurrentOutdoorWorldSiteId`，schema version 不变。新移动存档保存 `SurfaceVisible`、exact position、
exact destination 与 arrival radius；HexPath 不是 snapshot authority。Load 按 position + destination
重算 Surface route。

旧档单向规则：

- 缺 SurfaceId：用 saved exact WorldPosition 查询 registered Surface；
- continuous Site 的旧 `AtWorldSite`：保留合法 exact point，或以 authored SiteArrival 作一次迁移，
  恢复为 `AtWorldPosition + SurfaceId + CurrentOutdoorWorldSiteId`；
- 旧 `LocalVisible`／`World` 且带 exact continuous destination：重算路线并恢复为 `SurfaceVisible`；
- 无合法 Surface 位置的真实旧 Hex／Outdoor LocalMap 档继续进入 legacy compatibility。

## 6. Compatibility quarantine

`PlayerPartyHexTravelService`、`PlayerPartyLocalVisibleAutoTravelService`、
`PlayerPartyWildernessTransitionService` 与 `WildernessLocalMapFallback` 只服务旧 Hex、旧 Site／
Wilderness LocalMap、旧 SurfaceExit 与旧存档／旧内容。现代 WorldMap→arrival 链及 Continuous
WASD position sync 对这些服务的静态调用为零。

无生产调用且与当前 exact WorldMap 规则相反的 `WorldMapPartyTravelCommand` 已从 runtime 删除；
旧 Hex 测试夹具留在 Tests assembly。无调用的 Hex→continuous begin wrapper、旧
`BeginContinuousAutoTravel`、旧 close／resume takeover 与旧 hold wrapper 也已从 runtime 删除；
旧行为只留在 Tests assembly 的 compatibility fixture。

## 7. 输入、相机与保留边界

WASD、右键点走和 Stop 会同时识别现代 `SurfaceVisible` 与旧 `LocalVisible`，并调用
`CancelTravelPreservePosition`，不会让旧 WorldMap destination 在后台继续执行。相机保持既有
session 语义：新 AutoTravel 默认跟随，中键平移后 detach，打开／关闭 WorldMap 不重置，只有
新 travel session 才重新跟随。

Separate Space、CharacterEncounter 与 LEGACY-FINAL-A 的 NPC Squad authority 未改变。

## 8. 验证与验收状态

完成 Core／Data／Unity Host／Tests assembly 最小离线编译、BaseGame content load/reference
validation、New Game invariant、现代 route、snapshot JSON round-trip、旧 current-save migration、
真实 legacy fallback、现代链 caller audit 与 `git diff --check`。未打开 Unity，未运行 Unity Test、
PlayMode 或 batchmode。

制作人已于 2026-09-21 完成人工验收，本阶段正式 **Producer Accepted / Sealed**。
LEGACY-FINAL-C 随后开始；C 完成并验收前不得宣称 Legacy Finalization Complete。

## 9. Producer Acceptance 修复：Surface route 到 Composite physical execution

制作人验收发现 WorldMap 可以成功建立 `SurfaceVisible` travel plan，但 Active Character 可能完全不动。
根因是 Host 把 global Surface route 的当前 waypoint 当作必须逐点踩中的物理 checkpoint：该点在
`SurfaceGroundNavigation` 中可行，却可能被当前 loaded Composite WalkGrid 的议政厅、储藏室或其他
runtime blocker 占据。旧 resolver 会把当前位置返回为最近合法 approach；Host 因已在 approach 而不发
path，Domain 又因永远到不了原 waypoint 而不推进 route index，形成永久等待。

正式分层保持为：`SurfaceGroundNavigation` 提供大陆尺度地理 corridor；当前 loaded Composite
WalkGrid 是局部物理执行 authority。Surface waypoint 只是 guidance，不是必须踏入的 checkpoint。
现代 Host 现在对有限 route window 做一次 Composite flood，保留可达的当前 waypoint；当前点不可达时，
选取同一真实连通分量内最远的后续 waypoint。角色仍由 `HostMoveController` 的 Composite A* 行走，
实际抵达所选目标后才单调推进 Domain route index，因此不会 teleport、穿墙、穿建筑或回退索引。

当路线离开 loaded Composite grid 时，resolver 在当前连通分量中选择最接近第一个 outside waypoint 的
reachable frontier。抵达边界后由现有 Runtime Chunk streaming 更新 navigation generation，再重新解析；
frontier 等于当前位置则明确进入 `SurfaceLocalRouteNoProgress`，不再静默空转。最终 destination 被局部
blocker 占据时，只允许使用同一连通分量内、距离受限的合法 approach 完成物理到达，同时保留原始
WorldMap destination 作为 intent 与诊断。

现代 Surface Host transient 已独立为 Surface execution arm/resume、route target、subgoal、retry 与
diagnostic 状态，不再消费 LocalVisible takeover、SurfaceEdgeGate 或 legacy last-target 状态。Snapshot
仍只保存 Domain authority；load 后按 exact position/destination 重建 route，Host local target/path 在首个
execution tick 重新解析。`BeginTravel` 同时规范化明确可迁的旧 Continuous state：empty SurfaceId 使用
唯一覆盖 exact position 的 Surface；旧 continuous `AtWorldSite` 优先保留合法 exact position，否则只用
正式 SiteArrival，禁止 PresenceHex center fallback。

`SurfaceVisible` stall 诊断现记录 plan/surface/canonical 与 presentation position、route index、local target、
subgoal、Host path、chunk、navigation generation、grid revision 与 waiting reason；1.5 秒无物理和 canonical
进展且无 Host path 时只输出一条 `[SurfaceAutoTravelStalled]`。Host HUD 对现代路径改为 Surface-first；
legacy `LocalVisible` 继续显示既有 Hex diagnostics。

本项修复已纳入 2026-09-21 制作人人工验收，状态随本阶段正式 **Producer Accepted / Sealed**。

## 10. Producer Acceptance 修复：post-content Snapshot travel restore

制作人验收确认 idle Continuous 存档读档后主控无法移动。根因是 Snapshot DTO 第一阶段恢复发生在
`RuntimeContentShellBootstrap.Rehydrate` 注册 SurfaceGround 之前；当时 Continuous policy 不成立，
PlayerParty travel 会落入 `SetAtWorldPosition`，而该入口按契约清空 `SurfaceId`。原 Host workaround 只在
`IsMoving + HasContinuousPhysicalDestination` 时二次恢复，因此 moving save 有机会恢复 provenance，idle
save 则留下 `AtWorldPosition + exact WorldPosition + empty SurfaceId`。Continuous presentation 仍可按坐标
激活，但每次 movement sync 都因 Domain provenance 缺失失败并把 Active transform 拉回，看起来像完全冻结。

PlayerParty travel restore 现明确分为两阶段：第一阶段只恢复 serialized/raw strategic state；第二阶段严格
位于 SurfaceGround rehydrate 与 authored Site shell 建立之后，对 idle 和 moving snapshot 都执行。
Idle 恢复 `AtWorldPosition + saved/唯一推导 SurfaceId + exact position + derived CurrentHex`，不创建路线；
moving 在同一 canonicalization 后调用 `TryResumeAfterRestore` 重算 Surface route，失败直接返回
`SnapshotInvalid`，不留下半移动状态，也不回退 Hex/LocalVisible。明确保存的 SurfaceId 与 exact position
不一致时严格失败；只有旧 empty-SurfaceId 存档允许从唯一覆盖 Surface 推导。旧 Continuous AtWorldSite
优先保留可信 exact point，否则只使用正式 SiteArrival，禁止 PresenceHex/Core center fallback。

TravelingMembers 在第二阶段后按真实 PlayerParty membership 与生命状态重建。普通 Continuous Outdoor 才
从 PlayerPartyWorldMotion 单向同步成员 presence；active Separate Space 与 CharacterEncounter 只修复
PlayerParty outdoor return/source motion，不覆盖 interior occupant 或 tactical participant authority。
Background Surface travel 也移到 Site shell 之后恢复，按 CharacterId 先 replace 再建 route，并检查每次
结果；失败包含 CharacterId、DestinationSiteId、SurfaceId 与原因，不再 silent continue。

Host 在消费 pending snapshot 前强制检查 PlayerParty LocationKind、SurfaceId、registered navigation、
coverage、derived CurrentHex，以及 moving route/SurfaceVisible 完整性。Presentation rebuild 只消费已经合法
的 Domain authority；发现非法状态会报告
`[SnapshotRestore.PlayerPartySurfaceAuthorityInvalid]` 并拒绝激活。运行期 sync 仍保护 canonical position，
同时用一次性 `[PlayerPartySurfaceAuthorityInvalid]` 区分 provenance 错误和普通地形阻挡。Snapshot active
control trace 增加 Surface authority、route、active Surface、WalkGrid、manual/modal pause 与 input gate。

本项修复已纳入 2026-09-21 制作人人工验收，状态随本阶段正式 **Producer Accepted / Sealed**。
