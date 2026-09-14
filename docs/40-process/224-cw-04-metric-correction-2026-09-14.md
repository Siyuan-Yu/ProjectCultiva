# CW-04 Metric Correction（2026-09-14）

## 结论

此前把制作人给出的“500×500”误当成 Domain WorldPosition 尺寸。其单位确为 Continuous Surface/editor ground cells；随后制作人最终锁定 Site Level 1 为 150×150 cells，Main Surface 解析为 4.2×4.2 world／3×3 chunks。Wilderness Encounter 独立保留 500×500 cells，即14×14 world／约10×10 chunks。

## 权威链

- Content 使用 `controlWidthCells/controlHeightCells` 与 `wildernessEncounterWidthCells/wildernessEncounterHeightCells`。旧 `*World` 名称仅作旧 Content alias；同一定义混用新旧字段直接校验失败。
- Core 不硬编码 0.028，也不使用 Unity presentation 尺度。`WorldSpatialRules` 必须从目标 Surface 已注册的 `SurfaceGroundNavigation.CellSize` 解析；metric 缺失即明确失败，不以 1 回退。
- `WorldSite.CoreRangeWidth/Height`、Encounter 冻结尺寸与 `TerritoryClaim.Width/Height` 都是解析后的 Domain world 值。理论范围、精确行政解析、建设许可与 Encounter 共用该结果。
- Claim Snapshot 正式格式为 V3。已知开发期 V1 raw-cell 500/250 与 V2 world-size 14/7，仅在可确认是 Level 1 baseline/initial 且 Site/Surface 匹配时迁移到当前 4.2 world；Expansion 或来源不明则 `SnapshotInvalid`。

本轮不修改 Surface cellSize/chunk、坐标、建筑 footprint、W2A geography、控制算法或 CW-05。制作人随后于 2026-09-14 完成人工验收；状态：**CW-04 Producer Accepted / Sealed**。
