# 112 · MapEditor 用法（格点地图）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/MapEditor/`  
> 编辑：`type = mapLayout`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 和 RegionEditor 的区别

| | RegionEditor | MapEditor |
|--|--------------|-----------|
| 编辑什么 | 逻辑地点、邻接、任务挂接 | 格点设施／墙／区域大小 |
| 数据 | `worldRegion.locations[]` | `mapLayout.placements[]` |
| 交互 | 表格 | 画布拖拽、缩放 |

做「第一章地图长什么样、药田多大、墙在哪」→ 用 **MapEditor**。  
做「地点之间怎么走、接什么任务」→ 用 **RegionEditor**。

## 怎么打开

- VS：`ContentAuthoring.sln` → 启动项目 `MapEditor` → F5  
- 或：`.\publish.ps1` 后双击 `publish\MapEditor\MapEditor.exe`

## 日常操作

1. 打开包（默认 Content/BaseGame）；选 `base:map_ch01_reference`（已有样例）
2. 可改地图 **宽／高**（格数）、origin、cellSize（默认 1）→ **应用尺寸重绘**
3. 左侧选设施（药田默认 50×50、墙、房子…）→ 画布空白处单击放置
4. 拖设施移动；右下角红点拖缩放；右侧改属性／`boundLocationId`／是否挡路
5. **保存到磁盘** → Unity Play 会用该 mapLayout 生成寻路网格

## 验证建议

1. MapEditor 打开样例，拖一堵墙保存  
2. Unity `DemoParityHost` Play，角色移动是否绕开新墙  
3. 把药田调到更大（如 50×50），确认不挡路区域仍可走

## 注意

- 挡路只看 `blocksMovement: true` 的矩形  
- 逻辑地点坐标仍在 RegionEditor；可用 `boundLocationId` 关联  
- 当前样例地图约 80×50；要更大屏幕感，直接改 width／height
