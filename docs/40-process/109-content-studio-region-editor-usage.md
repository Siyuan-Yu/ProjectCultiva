# 109 · Content Studio · 区域／地点编辑器用法

> 状态：**可用（Studio v0.1）**｜日期：2026-08-10  
> 编辑对象：`type = worldRegion` 及其 `locations[]`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 这个编辑器干什么

编辑**逻辑地图**：地点 id／名称／kind、标签、允许活动、邻接、探索产出、摆点坐标、驻点 NPC／机缘／挂接任务。  
**不是**地砖／障碍美术编辑器（那是后续里程碑）。

## 怎么打开

左侧 **区域／地点**，或从总览对某 `worldRegion` 点「打开地图」。

## 日常操作

1. 顶栏下拉选区域（如 `base:region_ch01_reference`）。
2. 可改区域 **name**、**startLocationId**（玩家开局落点）。
3. 表格编辑每个地点：
   - **adjacentIds**：邻接地点 id，逗号分隔（字段名必须是 `adjacentIds`）
   - **tags**／**allowedActivities**：逗号分隔（活动如 `Labor`／`Patrol`／`Cultivate`）
   - **presentationX／Z**：Host 摆点坐标
   - **resourceOnExploreId／Amount**：首次勘察产出
   - **residentNpcDefinitionId**／**opportunitySiteId**／**questOfferIds**
4. **+ 地点** 追加空行；删按钮移除地点。
5. 下方「连通预览」检查邻接是否写对。
6. 点 **保存到磁盘**（或顶栏保存）→ 写回该 region 所在 JSON（如 `ch01_reference_region.json`）。

## 建议工作流

1. 先定地点列表与 `adjacentIds`（保证能走通）。
2. 再填 tags／activities（给 NPC Job／工区用）。
3. 再挂 `questOfferIds`、机缘、驻点 NPC。
4. 总览跑一遍校验 → Unity Play 验证移动与勘察。

## 注意

- 不要手写已废弃的 `linkedLocationIds`；游戏只认 `adjacentIds`。
- 删除地点前先清掉任务／事件里对该 id 的引用。
- v0.1 表格编辑为主；计划中的拖拽画布尚未做，坐标用数字改即可。
