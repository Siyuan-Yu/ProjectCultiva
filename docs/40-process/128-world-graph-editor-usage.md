# 128 · WorldGraph 编辑器用法（WorldGraphEditor）

> 状态：**可用（WPF／可视化节点）**｜日期：2026-08-16  
> 工程：`ExternalTools/ContentAuthoring/WorldGraphEditor/`  
> 编辑：`type = worldGraph`（节点＋道路）  
> 真源：[113](113-world-graph-local-map-architecture-revision-v0.1.md)

## 怎么打开

- `启动-WorldGraphEditor.cmd` 或 `Apps\WorldGraphEditor\WorldGraphEditor.exe`（首次／改代码后需 `publish.ps1`）

## 日常操作

1. 打开包 → 选 `base:graph_ch01`
2. **画布**：拖节点改 `worldX`／`worldY`；外形与游戏大地图一致（128×44 灰底标签、棕线道路、Y 向上）
3. **连线**：点「连线」→ 依次点两个节点建路；点道路可选中改 cost／条件
4. 右侧改 id／名称／kind／`localMapId` 等；「设为起点」写 `startNodeId`
5. **保存到磁盘**（Ctrl+S）

### 镜头

| 操作 | 方式 |
|------|------|
| 平移 | 中键拖，或空格＋左键拖 |
| 缩放 | **画布上滚轮**（同游戏，鼠标为锚）；顶部滑条／＋－；「邻站」＝最大放大；「适应」＝全图 |
| 删除 | Delete（节点会连带删路） |

## 与游戏对齐

编辑器投影与 `HostWorldMapPanel` 相同：世界坐标 → 屏幕（Y 翻转）；站点框固定屏上尺寸，不随世界单位拉伸。拖完进 Play 开大地图，相对位置应一致。

## 注意

- 村内地点不在这里编 → LocalPlaceEditor／`localPlaceSet`  
- 格点地图 → MapEditor  
- 通行条件格式：`kind:id`（数据可填；运行时旅行当前不检查）
