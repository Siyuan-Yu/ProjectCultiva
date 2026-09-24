# MAP-COORD-01 — WorldMap World Coordinate Readout

> 状态：**Producer Accepted / Sealed**
> 制作人验收日期：2026-09-24
> 封板记录日期：2026-09-25

## 正式规则

- WorldMap 坐标就是当前 Continuous Surface 的 exact `WorldPosition`，不建立 Hex、Grid index、UI local coordinate 或 GameObject transform authority。
- 鼠标位于地图有效区域时显示反投影后的 X/Y；离开后显示“坐标 —”。玩家坐标只来自 `PlayerPartyWorldLocationQuery`，不在当前可映射 Outdoor Surface 时显示不可用。
- `SurfaceWorldMapViewportProjection` 以同一 map rect、center、scale 与 Y 方向同时完成 world→map 和 map→world；zoom／pan 后 marker 中心反算仍回到同一 world position。UI 固定一位小数，runtime float 不舍入。
- X 下边缘与 Y 左边缘显示淡色 major ticks；间隔按 visible span 从 1／2／5×10ⁿ 选择，约保持 5～10 个区间。
- 坐标参照不产生隐藏内容知识。未发现 `hiddenUntilDiscovered` Opportunity 仍无 marker、名称、Activity、Toast、locator、View 或位置泄漏。

## 实现与验证

- `HostWorldMapPanel` 绘制 pointer／player readout 与 major ticks；不改变 WorldMap 选点、移动、关闭、Camera 或 Activity locator 语义。
- `SurfaceWorldMapViewportProjection` 提供有效区域反算和自适应 major interval。
- 定向 tests 4/4：exact round-trip、zoom／pan、区域外失效、1／2／5 interval。
- 全程序集离线编译 `ALL_OK`，OpportunityEditor／EventEditor Release build 均 0 warning／0 error，BaseGame Content reference validation 与 `git diff --check` 通过；未启动 Unity。

本项随 DYNAMIC-DISCOVERY-01 完成制作人人工验收并封板。下一里程碑为 `KNOWLEDGE-DELAY-01`，状态 Planned。
