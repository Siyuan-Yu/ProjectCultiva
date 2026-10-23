# 实木墙返修与农田阶段表现

> 状态：Implementation Complete / Producer Acceptance Pending / Committed·Sealed｜优先级：P1｜最后更新：2026-10-19

## 制作人反馈与范围

2026-10-23 后续复用见 [322](322-huangcun-compact-housing-and-cell-wood-walls-2026-10-23.md)：沿用本页 ART-ROOM-002 贴图，新增 `Assets/Prefabs/Environment/Tiles/WoodWallTile.prefab`，由 `MapKindCatalog.wall` 及木材质运行时墙段使用，打包目录同步。该 prefab 单 SpriteRenderer、无旧占位行为／物理组件，Host 直接裁框适配而不创建第二覆盖层；本轮村界使用 844 段一格墙，仍待视觉及性能人工验收。旧 WallTile 保留供原入口使用，不宣称石墙美术已完成。

制作人提供 298 实际运行截图：旧 `ART-ROOM-002_wall_straight_v02` 是狭窄的木框浅墙段，逐格组合呈栅栏感；目标是整块、封闭的木墙。制作人同时同意继续下一步。本轮针对这一明确画面反馈替换墙面视觉图，保持旧候选在 ArtSource 中；不重开人物动画。随后把黄村既有麦田、药田的四阶段图片接到真实农作物状态，使上一轮同屏样板中的田地不再只有旧色块。

## 表现边界

- 墙：只更换 Host 的 `wall` 显示 Sprite。新增一张正俯视实木板切件，四边封闭且完整填满现有一格墙的视觉范围；旧木框切件继续作为历史源图。墙的稳定 ID、逻辑格、碰撞、破坏、门洞及人物遮挡权威不改。
- 田：复用已有 `ART-GROUND-010` 麦田与 `011` 药田四阶段原图，按现有 `HostMapPlotCell.CropStage/Growth01` 展示空田、初生、长成与成熟；Ruined 使用空田图的衰败染色。图片只消费状态，不产生或修改生长、收获和库存。原始互动框、WorldRect、Source Cell 与导航不依赖图片尺寸。
- Unity Host/Resources 与 ArtSource 原图接线；不改 Core、Data、Content、Packages、ProjectSettings、Freeze，不生成其它美术批次。资产登记和 Devlog 同步。

## 验收口径

基础离线编译与最少资源检查不等于游戏内画面通过。制作人在 LevelTester 用正常缩放看：住宅整圈墙不漏草地且门洞仍空；麦田/药田从空田到成长、成熟、收获回空田，图片对应状态且可点选；人物行经田边、墙边时遮挡和寻路保持。未有现场结果前状态为 Producer Acceptance Pending；普通实施不自动提交或上传。

## 实际实施

- 用内置 `image_gen` 按现有地板和旧墙候选配色生成单张透明实木墙图，原图、最终提示、SHA-256 和运行裁框见 `ArtSource/Batches/Batch_EnvironmentWall_001/manifest.json`。未覆盖旧 `Batch_Codex_002/Static/ART-ROOM-002_wall_straight_v02.png`。运行 Sprite 只取新图不透明方形范围并铺满原墙格；原临时墙角拼片随之移除，门洞由原墙格缺席决定。
- 既有 `Batch_Codex_001/Originals` 麦田/药田四阶段图直接复制到 Resources。`HostStepAArt` 按每个图集固定的四列取相同视觉画框，`HostMapPlotCell` 用 Empty→第 1 张、Growing 前/后半程→第 2/3 张、Mature→第 4 张、Ruined→第 1 张加衰败染色。原 SpriteRenderer 位于 prefab 的 Visual 子物体，只改变其 Sprite/视觉子缩放，不改变根变换和 `HostMapVisualGeometry.WorldRect`。
- ART-ROOM-002、ART-GROUND-010／011 的 273 TSV 更新为已接入待验；`XianXia.Unity` 离线基础编译 `ALL_OK`。没有运行 Unity、自动测试或正式 Build；现场墙体连续性、田地状态读图和同屏性能仍待制作人验收。
