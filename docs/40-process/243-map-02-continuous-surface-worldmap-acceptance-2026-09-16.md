# MAP-02 — Continuous Surface WorldMap Strategic View 验收记录

> 日期：2026-09-16
> 状态：**Producer Accepted / Sealed**
> 前置：[MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)｜系统真源：[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)｜决策：[ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)、[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)

## 1. 验收范围

MAP-02 将主 Continuous Surface 的 WorldMap 切换为同源的战略缩放视图。其目标是让 Surface terrain、森林、Site、party 与路线目标使用同一份连续世界坐标，而不是再以 Hex center 作为画面或点击的中间真源。

已验收行为：

- 打开主 Surface WorldMap 时，画面由 `SurfaceWorldMapViewportProjection` 按 exact `WorldPosition` 投影与反投影。
- `HostSurfaceWorldMapRenderer` 缓存并绘制 Surface terrain/forest；cache 缺失时仅降级为既有 Surface navigation 数据，不伪造 Hex 地图。
- 左键/右键点选在 Surface mode 直接解析连续 world-space 坐标；重新选点时使用当前起点与当前目标。
- MAP-02 的 compatibility publish 生成 `continuousSurfaceWorldMap` runtime definition，Content loader 与 registry 能验证、加载并按 surfaceId 查询。
- 旧 Hex WorldMap 分支只服务尚未迁出的 compatibility world，不覆盖主 Surface view。

## 2. 数据与职责闭环

```text
WorldComposer / FineEditor authoring source
        ↓ compatibility publish
continuousSurfaceWorldMap runtime JSON
        ↓ ContentPackageLoader + DefinitionRegistry
HostSurfaceWorldMapRenderer + SurfaceWorldMapViewportProjection
        ↓
WorldMap strategic view / exact WorldPosition input
```

此 cache 只保存 Surface 地图展示所需的基础 terrain 与森林覆盖、尺寸、原点、cell size 与 source hash。它不是第二套地理、路线或 position authority；实际 world authority 仍是 Continuous Surface / Canonical WorldPosition。

## 3. 明确保留的兼容边界

MAP-02 不等于完全去 Hex，也不重写实际 travel、河桥规则、streaming 或 LocalMap。以下仍为后续 MAP-03 的逐 consumer 迁移项：

- `PlayerPartyHexTravelService.BeginContinuousSurfaceTravel()` 仍需要 `HexWorld.HasGrid`，Surface travel 仍持有 HexPath compatibility plan。
- `PlayerPartyWorldMotion.CurrentHex`、`DestinationHex`、`FinalDestinationHex` 仍存在。
- New Game 的 Site `PresenceHex` 仍是 compatibility data。
- `WorldSitePhysicalRegionQuery` 仍有 `WorldPosition → Hex → OccupiedHexes` 路径。
- FactionFlag 仍有 strategic anchor；Outdoor Site/Wilderness 的 LocalMap adapter 仍在。

这些状态不得被描述为 MAP-02 已清理，也不得因本次验收而删除、改写拓扑或改变存档兼容。

## 4. 验收结论与后续

制作人已人工验收通过 MAP-02。该检查点只封板 Continuous Surface WorldMap strategic view；**不启动 MAP-03**。后续若进入 MAP-03，必须另行授权，并以逐个 consumer 的 authority、存档与产品行为审计为前置。
