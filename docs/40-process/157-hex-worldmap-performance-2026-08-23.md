# 157 — Hex WorldMap 性能审计与重构

> 日期：2026-08-23  
> 状态：**IMPLEMENTED（第一版批处理渲染 + 100×50 地图）**

## 制作人反馈

1. 单格视觉太大 → 已改为 `HexOuterRadius = 1.0` + 默认 ~80 Hex 横向可见
2. WorldMap 卡顿 → 根因已定位并重构

## 性能审计结论（重构前）

### 三大 Bottleneck

| # | 问题 | 严重度 |
|---|------|--------|
| 1 | **每 Hex 独立 `GL.Begin/End`**（`HostHexGridDrawing.FillHex`） | 致命 |
| 2 | **每 Hex 6 次 `GUI.DrawTexture` 描边** | 高 |
| 3 | **每帧 `new List` + 全图排序 + `new HashSet` 路径集** | 中高 |

### 已排除（当前实现无此问题）

- 0 GameObject / MonoBehaviour / Collider / Update per Hex
- 无 UGUI Image/Button per Hex
- 无 SpriteRenderer per Hex
- 点击：O(1) `WorldToHex` 数学查询

## 重构措施

### `HostHexWorldRenderer`（新）

- **单次 `GL.TRIANGLES`** 批绘全部可见地形
- **单次 `GL.LINES`** 批绘边框（带 LOD：格半径 &lt; 3.5px 隐藏边框）
- **视口裁剪**：仅迭代可见 q/r 范围（紧凑网格）
- **零每帧分配**：复用静态 `float[]` 顶点缓冲（上限 200k verts）
- **路径高亮**：复用 `bool[] pathMask`，`Array.Clear` 无 new
- Site 标签 LOD：格太小时只画点不画字

### `HexWorld` 数据层

- **紧凑数组** `HexCell[width*height]`，O(1) `CoordToIndex`
- `ChunkCountX/Y` 供渲染分块（16×16 logical chunk 元数据）
- 可扩展至 10万+ cells（不绑死 100×50）

### 地图规格

| 规格 | 尺寸 | Cells |
|------|------|-------|
| Playable V1 | 100×50 | 5,000 |
| Stress Dev | 200×100 | 20,000 |

### 镜头默认

- 战略视角：~**80 Hex** 横向可见（`HexWorldScale.DefaultHexesAcross`）
- 最近：~28 Hex 横向（`CloseHexesAcross`）
- Hex 逻辑半径：**1.0** world unit（视觉由 zoom 决定）

## 目标指标（5000 Hex）

| 指标 | 目标 |
|------|------|
| Hex GameObjects | 0 |
| Hex MonoBehaviours | 0 |
| Terrain rebuild / frame | 0 |
| Steady-state terrain GC | ~0 |
| Draw calls (terrain) | 2（fill + lines） |

## Stress Test

`HexWorldStressMapBuilder.Build(world)` → 200×100，Dev-only 验证架构。

## 后续（未做）

- 世界空间 Chunk Mesh 持久缓存（当前每帧投影但已批处理）
- Control/Faction overlay 独立 Layer
- Unity Profiler 实机 FPS 报告（需制作人本地跑）

## 2026-08-23 验收修复（交互 + 浅色视觉）

### 鼠标偏移根因

1. **`MapPad` 内框缩放与全屏 `mapRect` 混用**：scale 按 inset 48px 计算，但早期 center/拾取未统一 → 已移除，改为全 `mapRect` 真源。
2. **`ComputeViewBounds` 曾用错误二分 `Unproject`** → 改为 `HexMapViewportProjection.ScreenToWorld`。
3. **GL 单次提交超过 ~65535 顶点** → 地形批绘静默失败（仅 overlay 可见）→ `FlushTriangles/Lines` 分批 60000。

### 坐标真源

新增 `HexMapViewportProjection`（`Assets/Scripts/Unity/Host/HexMapViewportProjection.cs`）：

- `ProjectWorld` / `ScreenToWorld` / `TryPickHex`
- `HexCoordToWorldCenter` → `HexMath.ToWorldPosition`
- `WorldToHexCoord` → `HexMath.WorldToHex`
- Hover / Click / Render / Path / Site 全部经此类型

EditMode 自检：`HexMapViewportProjectionTests`（H → center → H round-trip）。

### 视觉

- 地图底：浅米黄 `(0.93, 0.89, 0.78)`
- 普通边框：暖灰褐 `(0.52, 0.46, 0.38)` alpha ≥ 0.88
- Hover：淡黄填充 + 金色边；Selected：橙金填充 + 深橙边
- 情报面板：羊皮纸底 + 深褐字

## 2026-08-23 返工（鼠标拾取 + 全图 Grid）

### 错误「长三角形 / wedge」根因

不是单独的 pointer beam 代码，而是 **Hover/Selected 用半透明三角扇填充 + scale 1.06/1.10**，再叠加 **拾取不准** 时，错误格子的填充块看起来像指向鼠标的楔形。已全部删除填充，改为 **仅描边、scale=1.0、画在 Hex 自身位置**。

### Picker 唯一入口

`HexMapMousePick.TryResolveMouseHex` — Hover / Click / 右键 共用。

### Renderer 与 Picker 共用 HexMetrics

`HexMetrics`（几何）+ `HexMapViewportProjection`（屏幕投影）— 同一 `HexCoordToWorldCenter` / `WorldToHexCoord`。

### 普通 Grid Layer

`HostHexWorldRenderer.EmitGridEdges`：批处理 **~1.7px 屏幕四边形边**，暖灰褐 `(0.62,0.54,0.42)` alpha 0.82，与地形同批 GL 提交；非 per-cell GameObject。

### 测试

`HexMapMousePickTests`：HEX-PICK-01～06（100 格 round-trip、扇区采样、平移/缩放不变性）。

