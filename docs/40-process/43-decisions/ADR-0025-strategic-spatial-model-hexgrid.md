# ADR-0025：战略空间模型 = HexGrid

- **状态：** 已采纳（取代 ADR-0006 中 Route 作为战略移动拓扑的部分）
- **日期：** 2026-08-23
- **决策者：** 制作人

## 背景

Node + Route 图结构导致编辑器工作流笨重（手工拉 Route）、移动语义复杂（RouteProgress / RouteAnchor），且与「连续战略地图」产品方向不符。

## 决策

1. **HexTile（Axial `q,r`）** 是战略空间基本单位；Unity WorldPosition 仅为 Presentation 投影。
2. **StrategicSite** 坐落于 Hex，承接原 Node 的地点职责（名称、Owner、LocalMap、Capture）。
3. **Road** 是 Hex 属性（`IsRoad` / Terrain modifier），不是 Route Entity。
4. **FormalArmy** 通过 **HexPath** 移动；`CurrentHex` + `StepProgress` 为位置真源。
5. **禁止** Route Entity 作为正式战略移动模型（legacy 仅作 H8 前过渡）。
6. **Character** 不独立战略跨 Hex 移动；FormalArmy 成员位置由 Army 派生。
7. **LocalMap** 与 StrategicSite 关联，不与每个 Hex 一一对应。
8. Faction / Diplomacy / Capture 等上层规则 **保留**，仅适配位置引用。
9. **玩家命令语义保留**（选军、右键移动、右键攻击、路径预览、接战）；**战略移动引擎整体替换为 Hex**（`ArmyHexCommandService` / `ArmyHexPursuitService`）。

## Superseded

- ADR-0006 中「WorldMap 节点间 Route 连线移动」作为**正式战略移动**的部分 → **SUPERSEDED**
- 文档 113 / 153 中 Node→Route movement 验收 → 标记 SUPERSEDED，见 155

## 影响

- 新增 `docs/40-process/155-hex-strategic-worldmap-migration-2026-08-23.md`
- Snapshot schema 将升级 v3（H7）
- WorldGraphEditor Route 工作流废弃；Hex WorldMap Editor 替代（H5）

## 非目标（本轮）

Fog of War、领土染色、AI 战争节奏、贸易、外交 UI、最终地形数值。

## 2026-08-23 Presentation Pass 1 补充

1. **Domain 位置真源** 仍为 `HexCoord`（Axial 存储；Compact 网格 Q=列 0..99、R=行 0..49）。
2. **Odd-R offset 矩形布局**（`HexWorldLayout`）仅用于世界平面坐标、视口 fit、渲染与 Picking 投影；**不**引入第二战略位置真源。
3. **WorldSite 图标**（`WorldSitePresentationLayer`）为 Presentation；`WorldSite.AnchorHex` 仍为位置真源。
4. **Terrain Palette V1**（`HexTerrainPresentation`）统一图例与配色；不改变 `HexTerrainCatalog` 移动规则。
5. Ch01 程序化散点地形标注为 **TEST TERRAIN DISTRIBUTION**，非最终区域生成。

## 2026-08-23 World Content Authoring Pipeline

1. **Geography 真源** = `hexWorld` Content JSON（`Content/BaseGame/Data/Worlds/`），非 C# Builder / Scenario 硬编码。
2. **WorldGraphEditor** 重做：编辑 HexCell / Terrain / Road / WorldSite；**SUPERSEDED** Node/Route 编辑路径。
3. **Loader 单一入口：** `HexWorldContentLoader`（Data 层）；Playable 经 `openingHexWorldId` 加载。
4. **Editor WYSIWYG：** `HexWorldLayoutShared` 必须镜像 Runtime `HexWorldLayout`（Odd-R, Pointy-Top）。
5. Content JSON **≠** Snapshot；Editor 不编辑 SaveGame 动态状态。

详见 [158](158-hex-world-content-authoring-pipeline-2026-08-23.md)。

