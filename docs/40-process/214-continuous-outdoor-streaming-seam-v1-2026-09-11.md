# Continuous Outdoor Streaming Seam V1（2026-09-11）

状态：IMPLEMENTED / PENDING PRODUCER PLAY ACCEPTANCE

## 目标与边界

制作人已验收 Opening NPC authored spawn、NPC Schedule realtime movement、Continuous Outdoor Follow。本轮只消除主控跨 rectangular `SurfaceChunkCoord` 边界时，旧同步 neighborhood refresh 造成的单帧尖峰；不修改 Hex 战略拓扑、WorldPosition authority、Follow、Schedule、Camera 或 movement command。

逻辑 gameplay neighborhood 继续严格保持 radius-1（通常 3×3）。额外存在的 chunk 只属于 transition 期间的 transient presentation cache。

## 实现

`ContinuousOutdoorSurfaceRuntime` 将普通 chunk crossing 改为可合并到最新 center 的 staged transition：

Boundary 被检测到的当帧只记录/coalesce transition，不执行任何 heavy phase；工作从后续 `Update` 开始。

1. `BuildIncoming`：每帧最多构建 1 个缺失 incoming chunk；已构建且仍在最新 desired set 的 presentation 直接复用。
2. `CommitLogicalNeighborhood`：所有 incoming presentation 就绪后，单独一帧把 `_loaded` 提交为最终 radius-1 desired set，并只调用一次 `RecomposeWalkGrid()`。
3. `RefreshGameplayScope`：后续一帧执行 `RefreshLoadedOutdoorPlaces()` 与 `ReconcileOutdoorEntityMaterialization()`。
4. `RefreshOverlay`：再下一帧调用一次 `RefreshContinuousOutdoorOverlaysOnce()`。
5. `RetireTrailing`：最后每帧最多移除 1 个 outgoing presentation。

新增 `_presentedChunks`，使 logical `_loaded` 与临时 presentation cache 不再混用。中心在 transition 未完成时再次变化，会重算最新 desired；仍需要的 incoming 保留，不重新 activation、不清空 surface、不生成 duplicate owner。

Surface activation / hard handoff 仍允许同步初始化首个 neighborhood。普通相邻 crossing 已不存在同步 add3/remove3/reconcile/overlay 路径，也不调用 `AlignPartyPresentationToWorld`、`FrameCameraOnActiveCharacter`、`InvalidatePartyLocalMovement` 或任何 path cancel。

## 诊断与验证预算

Editor / Development Build 通过 `[ContinuousStream]` 在每个实际 phase action 完成后记录 center、phase、可选 chunk 与耗时。Release 不产生这些调用；空闲帧不打印。

制作人首次复验在远端 chunk 的 `ComposeWalkGrid` 暴露了 float lattice 误报：理论 150-cell extent 会累计为 `150.00012` / `149.99988`，超过旧的 presentation-space 绝对阈值 `0.0001`。`WalkGridComposer` 现统一按 cell-space 检查，容差为 `0.001 cell`；这只接纳约万分之一格的数值漂移，真实非整数格 placement 继续抛错。

本轮只做 Core/Data/Host offline compile 与 `git diff --check`，不运行 Unity Test Runner、PlayMode、Full EditMode 或 headless 大规模 suite。Unity 体感由制作人验收。
