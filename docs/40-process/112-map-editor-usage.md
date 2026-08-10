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
2. **整图尺寸**：改「宽／高」后点 **应用地图尺寸**（或回车）；也可点预设 `80×50`／`200×100`／`400×200`
3. 左侧先选工具：
   - **选择**：点选／拖移已有设施，空白处取消选中
   - **设施**：十字光标，在画布空白处单击放置（超出边界会自动夹入；药田 50×50 在小地图上会缩到能放下）
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
