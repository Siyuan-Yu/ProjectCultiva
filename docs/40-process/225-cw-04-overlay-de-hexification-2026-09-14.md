# CW-04 Overlay De-hexification（2026-09-14）

## 产品绘制真源

WorldMap 的“显示势力范围”默认开启，直接消费 `WorldSiteActualControlOverlayBuilder` 输出的 world-space Actual Administrative Control 几何。构建器用所有有效 Claim 的 X/Y 矩形边界形成有限精确分区，每个分区单元只调用既有 `WorldSiteAdministrativeControlResolver` 确认归属，再输出按 Site 分组的矩形片段与外边界段。它没有读取 Hex 控制、没有重新定义优先级，也没有把理论核心方框冒充实际控制。

Host 将片段顶点直接经 `HexMapViewportProjection.ProjectWorld` 投影，绘制半透明 fill 与 outline；视口裁剪仅影响可见绘制，不修改 Claim。相同 Surface、Level 且未被裁剪／抢占的 Level 1 Site 都是同一 150×150 cells／4.2×4.2 world 几何，经同一线性投影后外接框等大，不再受 Hex 相位影响。

## 三层职责

- Theoretical Core Coverage：当前矩形玩法上限，Level 1 Content 为 150×150 cells，Main Surface 解析为 4.2×4.2 world。
- Actual Administrative Control：Claim 历史经正式 resolver 得到的唯一管理权；产品 overlay 的唯一真源。
- Strategic Hex Projection：`StrategicTerritoryCoverageResolver`、`HexCell.ControlFactionId` 与 `TerritoryRegion.Hexes` 继续服务 legacy/strategic/diagnostics；产品 overlay 不消费。`DebugShowHexTerritorySummary` 默认关闭，可显式用于开发对照。

Site diagnostics 同时报告 theoretical cells/world、actual piece count/bounds 和 strategic effective hex count。本轮未修改控制算法、Claim、Encounter 或 CW-05 系统。

状态：CW-04 Overlay De-hexification Implementation Completed / Producer Acceptance Pending。
