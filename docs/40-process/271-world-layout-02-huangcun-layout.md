# WORLD-LAYOUT-02 — 荒村布局与周边道路重整

> **2026-10-23 后续覆写：** 世界和荒村的当前尺寸及间距见 [320](320-world-expand-05-7600x3400-2026-10-23.md)。本页保留此前布局时点：当前荒村已扩至 240×200，房屋／床尺寸与归属保留，设施及各区域重新展开；当前含棋盘共 99 个对象。

> 状态：**Implementation Complete / Producer Acceptance Pending**。本轮只保留工作区修改，未暂存、未提交、未推送。

## 实施范围

- 世界保持 **3800×1700**，荒村蓝图保持 **120×100**，作者放置起点保持 Surface Cell **(486,844)**。
- `road_001` 保留原有两端连通关系，中段改从荒村南侧经过，再从东南侧接回后续道路；删除九条 `huangcun:lane:*` 村内道路。
- 普通住宅保持 4×6、床保持 1×2、稳定 ID、父房间与初始分配不变。19 间普通住宅排成三排，同排起点相差 5 格，因此净横向间隔为 1 格；相邻排净纵向间隔为 1 格。
- 农田、药田、伐木区、采矿区维持各自内部结构与数量，只调整功能区整体位置。住宅至农田、住宅至药田、农田至药田、住宅至矿区、伐木区至矿区的边缘距离分别为 32、32、14、约 21.02、20 格。
- 医疗房、牢房、主管住宅、储藏室、议政厅、全部开局人物锚点和荒村 SitePlace 坐标与新布局同步。没有修改人物、职业、任务、小队、建筑数量、田块数量、产量或玩法数值。

## 作者真源与正式输出

作者真源：

- `Content/BaseGame/Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json`
- `Content/BaseGame/Authoring/ContinuousSurface/Blueprints/huangcun.worldsiteblueprint.json`

通过现有 `CompatibilityPublisher` 正式发布：

- `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`
- `Content/BaseGame/Data/Worlds/main_world_surface_map_v1.json`
- `Content/BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json`

发布结果为 7 条道路、1 座既有桥、98 个荒村对象和 2584 个 50×50 Chunk。运行地图 `sourceHash` 与当前作者源一致。

## 静态核对

- 荒村 120×100 的 12000 格全部为 Plain；范围内道路格为 0。
- 最近道路格位于荒村南侧，中心至 Site 边界 28 格；扣除 MajorRoad 3.5 格半宽后，净空约 24.5 格。
- 22 间房、27 张床、98 个对象全部在蓝图边界内；房间之间无重叠，所有床仍位于自己的 `ParentRoomId` 房间内。
- 九条 `huangcun:lane:*` 在作者源和正式地理输出中均为 0。
- Surface Authoring 验证为 0 error／0 warning；正式发布完成，三份运行 JSON 均可解析。
- 按项目 `AGENTS.md` 约定，本轮未新增或运行自动测试，未启动 Unity、PlayMode 或 Runner。

## 制作人人工验收

在 `Assets/Scenes/LevelTester.unity` 新开局核对：

1. 世界主路从荒村南侧、东南侧绕行，仍连接原道路；村内无黄色道路覆盖。
2. 普通住宅同排净间隔为 1 格，门口、床边与三排之间可以正常通行。
3. 农田、药田、伐木区和矿区明显分散，同类设施仍集中。
4. NPC 能前往新工作区，并在休息时回到原分配房间与床位。
5. 议政厅、医疗房、牢房、主管住宅和储藏室的显示、点击与业务位置一致。
6. 新开局和保存读取后位置一致；再次正式发布不会恢复旧道路或旧布局。
