# CW-05 Closing：可建造农田与行政资产生命周期

> 状态：Implementation Completed / Producer Acceptance Pending
> 日期：2026-09-14
> 授权：制作人 CW-05 Closing Slice，基于当前未提交工作树继续；禁止自动提交。

## 已批准范围

新增 grainField 建筑，5×4 Surface cells，粗木5；reference scenario NewGame 通过 startingInventory 获得粗木20。创建独立 Core OutdoorConstructedAssetBoard，以持久序列生成 root identity，并复用 ForCell identity。资产只存物理矩形、Surface、建筑类型、格数和独立 LocationId，不存行政管理者。

全 footprint 逐格调用 WorldSiteAdministrativeControlResolver；允许跨同势力 Site 边界，任何无人或他方管理格均拒绝。Host 负责 loaded-area、障碍、地形、距离、预览和输入，Core 重验权限并事务扣料、注册资产和 anchors。

Snapshot v6 添加可选 runtime placements 与序列；旧档缺字段视为空，新字段损坏须报错。恢复 placement、crop 后由 Content shell 重建 authored＋runtime anchors；startingInventory 仅 NewGame 发放。Streaming 复用 BuildOutdoorPlacementInstance 生成同一批田格。

移除 CW-05A Probe／ID 验收 UI。CW-05A/B 验收合并入本产品闭环，未标记 Accepted。NPC schedule economy、SettlementProduction、generic buildings、产权、建筑战争、农田拆除等继续延期。

## 原有缺口与实现链

之前只存在 authored farm placement 和作物状态，建筑目录只有 FactionFlag；因此玩家无法正常创建待验收农田，技术 Probe 不能证明完整产品闭环。本轮链路为 Buildings JSON → ConstructionCatalog.FarmField → HostConstructionController → HostFarmFieldConstructionPresenter → ConstructionService.TryConstructFarmField → OutdoorConstructedAssetBoard＋20格 anchors。

每次成功使用 `asset:runtime:farm:<sequence>`，sequence 只在事务成功后递增并保存；BoundLocationId 为 `location:runtime:farm:<rootId>`。物理矩形左下角按 Surface origin/grid 吸附，尺寸乘该 Surface 的 CellSize；初始缺少 crop override 等价于 Empty/Growth0。同地重复建造被拒绝且不扣料。序列、矩形、重复 root/LocationId/重叠等错误新档报错；旧 v6 缺这组字段则为空。

## Host 与 Streaming

放置仅限 Continuous Outdoor、非 Interior、无战斗冻结；以当前主控12 presentation units为距离上限。每格检查有效 Surface、已加载范围和 composite walk grid，物体矩形检查已有农田、树/墙/核心，旗占地继续来自 composite blocker。确认时重新执行 preflight 和 Core permission，拒绝显示中文原因。独立 named HostInputGate owner 在成功、取消、禁用、世界替换时释放。

BuildChunk 同时创建 baked 与 runtime placements；runtime adapter 的 SiteId 为空，仅传物理矩形、kind、格数和 BoundLocationId，复用 HostDemoTileMap.BuildOutdoorPlacementInstance。既有 stamping 按每格中心选择 owner chunk，因此跨 chunk 的农田仍只有20个唯一格。建造后的刷新只物化缺失 owner，不销毁已有劳动目标。

卸载按 chunk owner 清理 GameObject/Registry，Core board、anchors 和 crop 不变。读档已替换 World 时也从旧 owner key 清理旧表现，避免新旧农田残留。再次 materialize 时 HostMapPlotCell 从同一 StableCellId 读取 Core crop state。

## 管理与自然状态

Runtime Farm 使用与 authored farm 完全相同的 CW-05B hover、右键和逐格 Worker authorization。拆旗只使动态查询变为 none/其他有效 Site，不删 placement/crop。same-faction A→B 仍 Allowed；none/foreign 时改选合法格，没有则一次提示并取消剩余移动。已有 Growing 作物继续 Core WorldTick；重新建己方旗后同一资产立即恢复组织权限。

## 存档与开局库存

保存 runtime placement DTO、NextOutdoorConstructedAssetSequence 和既有 OutdoorFarmPlots。Restore 先建立 runtime placement，再恢复 crop；content shell 用 Surface metric 验证物理矩形后重建 authored＋runtime anchors，不能用当前 manager 做恢复门槛。任何 manager/owner/WinningClaim 都不进入该 DTO。

OpeningInventoryBootstrap 仅由 NewGame ContentRuntimeBootstrap.Apply 接收已选 scenario 后调用；RuntimeContentShellBootstrap 不调用它。初始容量不足返回 Bootstrap failure，连失败条目已部分加入的数量也回滚。PartyInventorySlots 始终是读档库存真源。

## 验证结果

- 严格加载完整 BaseGame package 成功；reference NewGame 确认粗木20、FarmField目录5×4/成本5。
- `tools/offline-compile.ps1`：Core467／Data80／Unity Host144／Unity Editor6／Tests195／PlayModeTests3／Assembly-CSharp51／Assembly-CSharp-Editor1，全部0 error，既有 warning 保留。
- `tools/run-headless-tests.ps1 -Filter ConstructibleFarmLifecycleTests`：8/8通过，仅通过 Mono 执行该 Core/Data 定向类，没有启动 Unity。覆盖 footprint 内两己方 Site、单格无人/他方、扣料/失败不扣/重复建造、20个唯一ID和独立LocationId、same-faction接续、无人管理仍WorldTick生长、placement/crop/sequence JSON往返、manager不入DTO、真实NewGame20木、保存13木后消耗再读档恢复13且重复rehydrate不增发、容量不足部分入包回滚、Surface metric/origin及离网拒绝、旧档可选兼容和损坏字段拒绝。
- 定向静态核对 dispatch、presenter自动装配、BuildChunk双来源、unload仅移除表现、runtime农田不写SitePlacements、CW-05 Probe退出、自然生长无manager gate；`git diff --check`通过。
- 未启动 Unity、EditMode/PlayMode Test Runner、batchmode 或 Bake。编译和 Core/Data 验证不替代制作人场景人工验收。

## 制作人五步正常验收

1. New Game：HUD木20，建筑页可见势力控制建筑木10、农田木5。在主角据点己方实际控制内建田，木20→15，立即出现5×4农田。
2. 选中主控，悬停农田显示“右键农作 · 己方管理”；右键后角色前往播种、照料。
3. 劳动中右键据点旗并正常拆除；无其他己方Site接管时，田/作物留存、自动农作停止、hover显示无人管理。尝试再建农田应红色拒绝。已有Growing作物仍随正常时间推进。
4. 正常再建势力控制建筑覆盖旧田；无需重造田，hover恢复己方管理，右键继续劳动。
5. 有crop状态时Save/Load：田仍在原地、crop保留、权限与当前领土一致，背包保持Save数量，没有额外+20木。

五步全部由制作人通过后，CW-05A＋CW-05B＋Closing才能一起Producer Accepted / Sealed。

## Git

No commit created.
Changes remain uncommitted for producer review.
