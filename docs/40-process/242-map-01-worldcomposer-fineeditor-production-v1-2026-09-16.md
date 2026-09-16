# MAP-01 — WorldComposer + FineEditor Production V1

> 日期：2026-09-16  
> 状态：**Implementation Completed / Producer Acceptance Pending**  
> 范围：External Content Authoring 工具与 authoring/bake schema；未迁移 Content，未接入 Gameplay/Runtime。

## 1. 交付能力

`WorldComposer` 以 10×10 Surface Cells 的 World Editor Cell 制作大陆级 source。Production V1 支持 PlainGrass、Dirt、Sand、Rock、Water 基础层，Forest、Mountain 特征与密度，1/2/4/8/16 编辑格笔刷、连续 stroke、Fill、Eraser、框选、pan/zoom、grid 与 Runtime Chunk debug overlay。边缘展开由 world seed、全局 Surface 坐标和周边 source intent 确定，不依赖随机调用顺序。

河流和道路使用 Surface 坐标控制点与 Catmull-Rom 采样；河流保存宽度，道路保存 Trail、SmallRoad、Road、MajorRoad 等级。控制点可增加、拖动、删除；道路河流交叉口可保持 Unresolved 或标记 Bridge/Ford。

Composer 可链接、放置、拖动、精确定位、quarter-turn 旋转和删除 WorldSite Blueprint，并显示 footprint 与 Blueprint 对象 overlay。Surface 框选可直接创建 Detail Patch、写入相对 `sourcePath`、链接到当前 composition 并在 `FineEditor` 打开。刷新链接会把缺失、损坏、id 不匹配、越界和重叠问题送入 Problems。

`FineEditor` 同时编辑任意正整数尺寸的 WorldSite Blueprint 与 Detail Patch。地形使用 sparse override，未设置格等于 Inherit；支持基础地形、特征/密度、1/2/4/8 Surface Cell 笔刷、连续 stroke、框选、Fill 和 Erase-to-Inherit。Blueprint 支持通用对象 placement 的选择、拖动、旋转、复制、删除，以及 kind、Content/Asset 引用、精确坐标和 footprint 编辑。

两个编辑器都有 New/Open/Save/Save As、dirty `*`、Save/Discard/Cancel 关闭流程、Undo/Redo 与常用快捷键。它们使用自绘 canvas，不为每个 Surface Cell 创建 UIElement。

## 2. Source schema v2

- `WorldCompositionDocument`：Surface 尺寸、固定 World Editor Cell 尺寸、seed、默认基础地形、macro terrain/feature、river/road/crossing、Blueprint 和 Detail Patch placement。
- `WorldSiteBlueprintDocument`：尺寸、sparse fine terrain source、通用对象 placements。
- `DetailPatchDocument`：尺寸与 sparse fine terrain overrides，只覆盖地形层。
- 链接 placement 保存稳定 placement id、目标 source id、相对 composition 的 source path、世界 Surface 坐标；Blueprint 另保存 quarter turns。
- loader 接受 schema v1 并在内存升级到 v2；新保存写 v2。schema 仍保留后续 additive migration 空间。

## 3. Composition 与 Bake

Preview 和 Bake 共用 `CompositionEngine`。Production V1 固定顺序为：基础地形 → 特征 → 河流 → 道路 → Detail Patch terrain override → Blueprint terrain override → Blueprint object overlay。

`FinalContinuousSurfaceBake` 包含确定性 source hash、Surface 尺寸与 seed、逐行 RLE terrain/feature/river/road、道路河流 crossing、Blueprint instance 以及解析到世界坐标的对象 placement。Bake 前阻断 schema、尺寸、重复 source cell/id、路径、对象、链接和越界错误；未解决 crossing 与 placement overlap 作为可见 warning。

## 4. 边界与验证

本轮只编译 `SurfaceAuthoring.Core`、`SurfaceAuthoring.EditorCommon`、`WorldComposer` 与 `FineEditor`，进行 manifest/工程路径静态检查、source JSON round-trip 和相同输入 bake 确定性检查，并在末尾最多执行一次正式 Build All。没有打开 Unity，没有运行 Unity Test 或 Shared.Tests。

本轮没有修改 `Content/BaseGame`、Gameplay、Runtime、Scene、Prefab、ProjectSettings、Packages、旧 `MapEditor`/`WorldGraphEditor`，没有创建青石荒村迁移内容。Runtime 仍未消费新 bake；正式 Content pilot 属后续单独授权范围。
