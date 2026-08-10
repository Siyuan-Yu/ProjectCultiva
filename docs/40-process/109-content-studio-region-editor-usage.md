# 109 · 区域／地点编辑器用法（RegionEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10  
> 工程：`ExternalTools/ContentAuthoring/RegionEditor/`  
> 编辑：`worldRegion` + `locations[]`（**逻辑地点表**，不是格点画布）  
> 格点设施地图请用：[112 MapEditor](112-map-editor-usage.md)  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

编辑逻辑地图：地点、邻接、标签、活动、摆点坐标、探索产出、驻点 NPC／机缘／挂任务。不是地砖美术编辑器。

## 怎么打开

- VS：启动项目 `RegionEditor` → F5  
- 或：`publish\RegionEditor\RegionEditor.exe`（先跑 `publish.ps1`）

## 日常操作

1. 确认包路径；下拉选区域（如 `base:region_ch01_reference`）
2. 改区域 name／startLocationId
3. 表格改各地点；邻接字段是 **`adjacentIds`**（逗号分隔）
4. **+ 地点**／删除选中行
5. **保存到磁盘** → 写回该 region 所在 JSON

## 注意

- 不要用已废弃的 `linkedLocationIds`  
- 删地点前清掉任务／事件引用  
- Unity 重新 Play 验证连通与勘察
