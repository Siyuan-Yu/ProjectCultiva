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

## 常用交互（同类地图编辑器）

| 操作 | 方式 |
|------|------|
| 缩放 | **Alt** 或 **Ctrl** + 滚轮；`100%`／`Ctrl+0`；`适应`／`Ctrl+1` |
| 平移 | **中键拖**，或 **空格 + 左键拖** |
| 放置 | 左侧选设施 → 空白单击 |
| 选择／拖移／缩放物件 | 选择工具；拖移；右下角红点调大小 |
| 取消／回选择 | **Esc**；画布 **右键** |
| 删除 | **Delete**／Backspace；或工具栏删除 |
| 复制 | **Ctrl+D** |
| 微调 | **方向键** 1 格；**Shift+方向键** 5 格 |
| 撤销／重做 | **Ctrl+Z**／**Ctrl+Y** |
| 保存 | **Ctrl+S** |
| 光标格坐标 | 右下角实时显示 |

## 日常操作

1. 打开包（默认 Content/BaseGame）；选 `base:map_ch01_reference`（已有样例）
2. **整图尺寸**：改「宽／高」后点 **应用地图尺寸**（或回车）；也可点预设 `80×50`／`200×100`／`400×200`
3. **Alt/Ctrl+滚轮**：缩放画布（25%～400%）；不按修饰键时滚轮仍滚动视口
4. 左侧先选工具：
   - **选择**：点选／拖移已有设施，空白处取消选中
   - **设施**：十字光标，在画布空白处单击放置（超出边界会自动夹入）
5. 拖设施移动；右下角红点拖缩放；右侧改属性／`boundLocationId`／是否挡路
6. **保存到磁盘**（Ctrl+S）→ Unity Play 会用该 mapLayout 生成寻路网格

## 验证建议

1. MapEditor 打开样例，拖一堵墙保存  
2. Unity `DemoParityHost` Play，角色移动是否绕开新墙  
3. 把药田调到更大（如 50×50），确认不挡路区域仍可走

## 注意

- 挡路只看 `blocksMovement: true` 的矩形  
- 逻辑地点坐标仍在 RegionEditor；可用 `boundLocationId` 关联  
- 当前样例地图约 80×50；要更大屏幕感，直接改 width／height
