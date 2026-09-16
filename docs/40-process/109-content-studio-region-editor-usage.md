# 109 · 区域／地点编辑器用法（RegionEditor）

> ## ⚠️ LEGACY COMPATIBILITY（2026-09-15）
>
> - 仍用于旧 `worldRegion` / `locations` graph Content。
> - Continuous Outdoor 新内容**不再采用**这套空间模型。
> - 等 Outdoor `worldRegion`/`location` consumers 全部迁完后退休；**当前不立即物理删除**。
> - 方向与生命周期见 [ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

> 状态：**可用（WPF／Windows）**｜日期：2026-08-10
>
> 生命周期：**Legacy Compatibility**（[ADR-0037 §6](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)）
>
> 工程：`ExternalTools/ContentAuthoring/RegionEditor/`  
> 编辑：`worldRegion` + `locations[]`（**逻辑地点表**，不是格点画布）  
> 格点设施地图请用：[112 MapEditor](112-map-editor-usage.md)  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

编辑逻辑地图：地点、邻接、标签、活动、摆点坐标、探索产出、驻点 NPC／机缘／挂任务。不是地砖美术编辑器。

## 怎么打开

- 推荐：`启动-RegionEditor.cmd` 或 `Apps\RegionEditor\RegionEditor.exe`（先跑 `publish.ps1`）  
- 调试：VS 启动项目 `RegionEditor` → F5

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
