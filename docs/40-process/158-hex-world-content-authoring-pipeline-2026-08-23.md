# 158 — Hex World Content Authoring Pipeline + WorldGraphEditor WYSIWYG

> **日期：** 2026-08-23  
> **状态：** IMPLEMENTED · Unity 手操验收 **DEFERRED**  
> **关联：** [ADR-0025](43-decisions/ADR-0025-strategic-spatial-model-hexgrid.md) · [155](155-hex-strategic-worldmap-migration-2026-08-23.md)

---

## 摘要

战略 Geography（地形 / 道路 / WorldSite 布局）正式从 **C# 硬编码** 迁移为 **HexWorld Content JSON**。  
`WorldGraphEditor` 彻底重做：**不再编辑 Node / Route**，改为 Hex WYSIWYG 编辑器。  
Editor 与 Runtime 共用 **Odd-R offset + Pointy-Top** 布局公式（Runtime `HexWorldLayout` 为真源）。

---

## Content Pipeline

```
WorldGraphEditor（ExternalTools）
    ↓ Save
Content/BaseGame/Data/Worlds/*.json   （type: hexWorld, SchemaVersion: 1）
    ↓ ContentPackageLoader
DefinitionRegistry.RegisterHexWorldContent
    ↓ PlayableDayBootstrap
HexStrategicMapContentBootstrap.TryApplyToSession
    ↓ HexWorldContentLoader.Apply
HexWorld + WorldSite
    ↓ HostHexWorldRenderer / WorldSitePresentationLayer
Runtime WorldMap
```

### 正式 Content 文件

| 项 | 值 |
|---|---|
| 路径 | `Content/BaseGame/Data/Worlds/ch01_hex_world.json` |
| DefinitionId | `base:hex_world_ch01` |
| Scenario 引用 | `openingHexWorldId` on `base:scenario_ch01_reference` |
| 尺寸 | 200×100（20000 cells；原 100×50 备份见 `Content/BaseGame/_backups/`） |

### Content JSON vs Snapshot

| | Content JSON | Snapshot |
|---|---|---|
| 职责 | 世界**初始**长什么样 | 玩家玩到**现在**什么样 |
| 含 | Terrain / Road / Site / Footprint | Army 位置 / 占领 / 战争 / 角色状态 |
| 编辑工具 | WorldGraphEditor | SaveGame（非本工具） |

---

## JSON Schema（v1）

顶层 `hexWorld` definition 字段：

- `id`, `type`, `name`
- `width`, `height`, `hexSize`
- `defaultTerrain`, `defaultPassable`
- `cells[]`：`q`, `r`, `terrain`, `passable?`, `isRoad`
- `sites[]`：`siteId`, `displayName`, `siteType`, `anchorQ`, `anchorR`, `footprint[]`, `localMapId`, `ownerFactionId`, `legacyNodeId`

**坐标语义：** `OddROffsetPointyTop` — `q` = 列，`r` = 行（与 Runtime `HexWorldLayout` 一致）。  
**禁止** NodeId / Route 作为 Geography 真源。

---

## WorldGraphEditor（Hex World）

- **入口：** `Tools → XianXia → WorldGraphEditor`（Release：`ExternalTools/ContentAuthoring/.build/WorldGraphEditor/`）
- **标题：** `WorldGraphEditor — Hex World`
- **工具：** Select / Terrain / Road / Site / Erase；笔刷半径；Footprint 编辑
- **能力：** New / Open / Save / Save As / Validate / Fit / Undo / Redo
- **默认打开：** `ch01_hex_world.json`
- **CLI 迁移：** `dotnet run --project WorldGraphEditor -- --migrate-ch01`

### Editor Performance Architecture（2026-08-23）

| 项 | 实现 |
|---|---|
| Renderer | **Chunked DrawingVisual cache**（非 per-Hex `Canvas.Polygon`） |
| Chunk Size | **16×16**（对齐 Runtime `HexWorldScale.RenderChunkSize`） |
| Geometry | 世界坐标烘焙；Pan/Zoom 只更新 `MatrixTransform` + visible cull |
| Dirty rebuild | Terrain/Road 笔刷只 rebuild 触及的 chunk |
| Hover / Select | Overlay DrawingVisual；**不** dirty terrain chunks |
| Repaint | 无定时强制刷新；仅 pan/zoom/hover-change/content-edit |
| Undo | 一次 Brush Stroke = 一次 JSON snapshot（非每格） |
| Validate | 手动「校验」；不在每次绘制时跑全图连通性 |

状态栏显示：`World / Cells / Visible Chunks / Dirty / Rebuild ms / Sync ms`。

### SUPERSEDED

- Node 可视化编辑
- Route 连线编辑
- `WorldGraphEditor` 作为 Node/Route Authoring 工具的旧描述（见 [128](128-worldgraph-editor-2026-08-18.md) 部分 supersede）

---

## Hex Layout Single Source

| 层 | 真源 |
|---|---|
| Runtime 投影 / Picking | `Assets/Scripts/Core/World/Hex/HexWorldLayout.cs` → `HexMetrics` |
| Editor 投影 / Picking | `ExternalTools/.../Shared/HexWorld/HexWorldLayoutShared.cs`（**公式必须与 Runtime 一致**） |

**Odd-R 公式（Pointy-Top）：**

```
worldX = sqrt(3) * hexSize * (col + 0.5 * (row & 1))
worldY = 1.5 * hexSize * row
```

其中 `col = q`, `row = r`。

### 2026-08-23 WYSIWYG 修复

**根因：** Editor 曾使用错误公式（`1.5*Q` / `Q` 奇偶偏移），导致同 JSON 在 Editor 与 Runtime **拓扑错位**、道路「看起来连通」但数据不相邻。

**修复：** `HexWorldLayoutShared` 对齐 Runtime；增加道路连通性 Validate（孤立 Road / 多 connected components）。

---

## 代码入口

| 组件 | 路径 |
|---|---|
| Loader | `Assets/Scripts/Data/Content/HexWorldContentLoader.cs` |
| Exporter | `Assets/Scripts/Data/Content/HexWorldContentExporter.cs` |
| Playable Bootstrap | `StrategicContentBootstrap` + `HexStrategicMapContentBootstrap` |
| Test fixture only | `Ch01HexPrototypeMapBuilder`（无 Content 包时 EditMode） |
| Editor Shared | `ExternalTools/ContentAuthoring/Shared/HexWorld/*` |
| Editor UI | `ExternalTools/ContentAuthoring/WorldGraphEditor/*` |

---

## 验收清单

### EDITOR-HEX

| ID | 项 | 状态 |
|---|---|---|
| EDITOR-HEX-01 | WorldGraphEditor 打开当前 Hex 世界 | IMPLEMENTED |
| EDITOR-HEX-02 | WYSIWYG 地形 | IMPLEMENTED |
| EDITOR-HEX-03 | Terrain brush | IMPLEMENTED |
| EDITOR-HEX-04 | Road brush | IMPLEMENTED |
| EDITOR-HEX-05 | Site create/edit/delete | IMPLEMENTED |
| EDITOR-HEX-06 | Multi-Hex footprint | IMPLEMENTED |
| EDITOR-HEX-07 | Save JSON | IMPLEMENTED |
| EDITOR-HEX-08 | Load JSON | IMPLEMENTED |
| EDITOR-HEX-09 | Runtime reads same JSON | IMPLEMENTED |
| EDITOR-HEX-10 | Editor/Runtime layout match | **FIXED · Unity 手操 DEFERRED** |
| EDITOR-HEX-11 | Validation | IMPLEMENTED |
| EDITOR-HEX-12 | Undo / Redo | IMPLEMENTED |

### WYSIWYG 自动测试

| ID | 文件 |
|---|---|
| WYSIWYG-02 | `HexWorldWysiwygLayoutTests` — 5000 cell roundtrip |
| WYSIWYG-05~07 | `HexWorldContentPipelineTests` |
| WGE-RUNTIME-01 | Loader 应用 Forest + Site |

---

## 制作人手操验收（待签）

1. WorldGraphEditor 打开 `ch01_hex_world.json`，与 PlayMode 对比 Site / Road 拓扑  
2. 刷 Forest → Save → PlayMode 同格变 Forest  
3. 新增测试村 → Save → Runtime 出现  
4. Editor **校验** — 若有 `ROAD DISCONNECTED` 为 JSON 内容问题，非投影 bug

---

## 非目标（本轮）

- Army / Pursuit / Battle 规则变更  
- Scenario 重排 Geography（Ch01 仅剧情 / 山匪位置，不改 JSON 地形）  
- 程序生成新世界工具（DEFERRED）
