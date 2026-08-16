# 128 · WorldGraph 编辑器用法（WorldGraphEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-16  
> 工程：`ExternalTools/ContentAuthoring/WorldGraphEditor/`  
> 编辑：`type = worldGraph`（节点＋道路）  
> 真源：[113](113-world-graph-local-map-architecture-revision-v0.1.md)

## 怎么打开

- `启动-WorldGraphEditor.cmd` 或 `Apps\WorldGraphEditor\WorldGraphEditor.exe`（首次需 `publish.ps1`）

## 日常操作

1. 打开包 → 选 `base:graph_ch01`
2. 改 name／startNodeId
3. 上表改 **节点**（含 `localMapId`／worldX/Y／kind=Pass 关隘）
4. 下表改 **道路**（cost／state；通行条件字段可填但运行时暂不检查）
5. **保存到磁盘**

## 注意

- 村内地点不在这里编 → `localPlaceSet`／RegionEditor（旧）  
- 格点地图 → MapEditor  
- 通行条件格式：`kind:id`（数据可填；**运行时旅行当前不检查**，无令牌门槛）
