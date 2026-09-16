# MAP-01A：Continuous Surface Authoring Foundation（2026-09-15）

> 状态：已实施，待制作人人工验收
> 优先级：P1
> 最后更新：2026-09-15

## 新项目结构

`ExternalTools/ContentAuthoring/` 新增 `SurfaceAuthoring.Core`、`SurfaceAuthoring.EditorCommon`、`WorldComposer` 与 `FineEditor`。Core 只包含新的 Source model、JSON、validation 与坐标定义；两个 Editor 只引用 Core 和极小的 viewport common，不引用 `Shared`、`Shared/HexWorld`、`WorldGraphEditor`、`MapEditor`、`mapLayout` 或旧 Hex model。

## Source documents

- `WorldCompositionDocument`：`*.worldcomposition.json`，保存 Continuous Surface 尺寸、固定 `worldEditorCellSize=10`、coarse terrain source、Blueprint placement 与 Detail Patch placement collection。
- `WorldSiteBlueprintDocument`：`*.worldsiteblueprint.json`，保存任意正整数 `widthCells` / `heightCells` 与 fine terrain source collection。
- `DetailPatchDocument`：`*.detailpatch.json`，保存任意正整数 `widthCells` / `heightCells` 与 fine terrain override collection。

三种文件都使用 UTF-8、human-readable deterministic JSON、`schemaVersion=1`，并在 Open/Save 时验证不支持的 schemaVersion 和非法尺寸。它们不会自动写入 `Content/BaseGame/Data`。

## 固定空间定义与坐标

- Surface Cell 是 1×1 Gameplay terrain 单位。
- World Editor Cell 只属于 WorldComposer，固定为 10×10 Surface Cells。
- Runtime Chunk 当前为 50×50 Surface Cells，只是 Runtime streaming partition，未进入任何新 Source schema。
- Blueprint 与 Detail Patch 使用任意正整数 Surface Cell 尺寸，不要求 10、50 或 150 的倍数。

Source JSON 使用 Runtime-aligned 坐标：`(0,0)` 是 Surface 左下，X 向右/东增长、Y 向上/北增长。现有 `OutdoorSurfaceCoordinateMapper` 也是直接以 worldY 增长；WPF viewport 仅为屏幕绘制反转 Y，不把屏幕方向写入 JSON。

## 当前能力边界

WorldComposer 可 New/Open/Save/Save As WorldComposition，并显示默认 1900×850 Surface Cells 对应的 190×85 World Editor Cell grid，具备 pan、Ctrl+wheel zoom、Zoom、100%、Fit、cursor coordinate 和基础 metadata Undo/Redo。

FineEditor 可 New/Open/Save/Save As WorldSite Blueprint 或 Detail Patch，显示任意尺寸的空白 1×1 Surface Cell grid，并提供同样的 viewport、cursor coordinate 与 metadata Undo/Redo。

MAP-01B 尚未开始。本轮没有 terrain painting、placement、road、river、Bake、Content 迁移、青石荒村迁移或任何 Gameplay/Unity Runtime 改动。
