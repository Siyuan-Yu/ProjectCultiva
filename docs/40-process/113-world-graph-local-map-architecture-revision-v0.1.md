# 113 · World Graph + Local Map 架构修订 v0.1

> 状态：**阶段 A～D／F 已落地／Host 出行与隔离见 [129](129-world-graph-host-travel-scene-isolation-2026-08-16.md)／E 待做／G 战略接战见 [138](138-world-strategic-battle-offer-plan-2026-08-17.md)**｜日期：2026-08-17  
> 一句话：**宏观世界是 Civilization／RimWorld 式节点图；实体玩法只发生在按需加载的 LocalMap 上。**  
> 取代：`24` 中「Region = 较大连续区域、路途也在同一张连续地图上走」的体验模型。  
> 保留：`mapLayout`／MapEditor、WalkGrid、RTS、Job／Schedule、内容包 Loader。  
> 相关：[24 世界与据点](../20-systems/24-world-and-settlements.md)｜[112 MapEditor](112-map-editor-usage.md)｜[109 RegionEditor](109-content-studio-region-editor-usage.md)｜VS0.9 Travel [71](71-vertical-slice-0.9-world-interaction-plan-v0.1.md)

---

## 1. 为什么改

当前实现把「逻辑地点」摊在**同一张** `worldRegion.locations[]` + 一张 `mapLayout` 上：

- `adjacentIds` 是瞬间 Travel 邻接，没有路、没有旅行时间、没有路权  
- 药田／房屋／树林既是战略点，又是同一张格点上的色块  
- 放大地图尺寸 ≠ 做出一个世界；只会把 LocalMap 撑成假大地图  

新方向：世界层是**图**，实体层是**一张张独立小地图**。道路不是常驻连续地形。

---

## 2. 三层结构（新）

```text
WorldGraph
  WorldNode  ──WorldRoute──  WorldNode
       │
       └──（可选）localMapId → LocalMap (mapLayout)
                                    │
                                    └── 进入后才加载：NPC／寻路／工区／RTS／交互／修炼／（未来）战斗

路上（WorldRoute）
  默认：时间推进 + 危险检定 + 事件
  需要实体交互时：临时生成／加载 Route Encounter LocalMap，结束即卸
```

### 2.1 WorldNode（宏观地点）

一个战略点，**不是** LocalMap 里的一格药田。

典型 kind：城镇、荒村、宗门、矿山、森林、灵地、山口、遗迹、渡口、其他战略点。

建议字段（内容 JSON，可分期落地）：

| 字段 | 含义 |
|------|------|
| `id`／`name`／`kind` | 标识 |
| `localMapId` | 可选。有则进入时加载该 `mapLayout` |
| `worldX`／`worldY` | 宏观地图摆点（不是 Local 格点） |
| `ownerId`／`state` | 归属／可见／废弃／沦陷… |
| `tags[]` | 探索／任务过滤 |

**没有 `localMapId` 的 Node**：宏观上可到达，进入时可以是摘要界面／营地，不必刷实体地图。

### 2.2 WorldRoute（边）

连接两个 Node。**不是**一张常驻大地图。

| 字段 | 含义 |
|------|------|
| `id` | 边 id |
| `fromNodeId`／`toNodeId` | 端点（默认可双向；需要单向再加 `directed`） |
| `kind` | `Road`／`Trail`／`Bridge`／`RiverCrossing`／`MountainPass` |
| `travelCost` | 时间／体力代价（与统一时钟对齐） |
| `danger` | 遭遇权重／检定 |
| `ownerId` | 路权 |
| `state` | 畅通／损毁／封锁／施工 |
| `traversalRequirements[]` | 数据保留；**运行时旅行暂不检查**（无令牌门槛） |
| `encounterPoolId` | 可选。触发临时 Encounter LocalMap |

未来允许对 Route：**建造、升级、摧毁、封锁、修复**。第一期只读内容数据，不做建造 UI。

### 2.3 LocalMap（实体地图）

**就是现有 `mapLayout` + MapEditor。** 重要 Node 绑一张。

例：

```text
WorldNode  id = base:node_huangcun
localMapId = base:map_huangcun
```

进入 Node：

1. 卸载上一张 LocalMap 的实体／网格／交互点  
2. 加载 `mapLayout` → WalkGrid + prefab 刷子 + 该图 WorkArea／NPC  
3. 运行现有 Local 循环：导航、Job、Schedule、RTS、交互、修炼  

离开：

- 卸载实体  
- **保留必要持久状态**（作物、仓库、任务、地点所有权、已触发 once 事件等）  
- 队伍宏观位置记在 Node 或 Route 进度上  

LocalMap **内部**仍可有逻辑地点／工区（今日 `locations[]`／`workArea` 的职责收缩到「这一张图里的点」，不再充当世界邻接图）。

### 2.4 路上／Wilderness

Node 之间**没有**持续存在的大型连续荒野地图。

队伍在 Route 上旅行：

- 推进 `travelCost` 对应的世界时间  
- 按 `danger`／事件表检定  
- 需要真打／真捡／真对话 → 进入 **临时 Route Encounter LocalMap**（山谷伏击、断桥、野摊…）  
- Encounter 结束：卸图，回到 Route 进度或进入目标 Node  

---

## 3. 和旧模型怎么映射（Ch01 不推倒重做）

| 旧 | 新 |
|----|----|
| `worldRegion` 一整区 | 一张 `WorldGraph`（或第一章一张子图） |
| `locations[]` 里的荒村／农田／树林挤在同一层 | **战略点**升为 WorldNode；**村内设施**留在 LocalMap |
| `adjacentIds` | WorldRoute |
| `presentationX/Z`（世界摆点） | Node 用 `worldX/Y`；Local 内摆点仍用 mapLayout／工区 |
| 一张 `map_ch01_reference` 当全世界 | 先当作 **荒村 LocalMap**（`base:map_huangcun`），世界层另做 3～6 个 Node 的小图 |
| `ExplorationService.Travel` 瞬间换 locationId | 改为：有 Route 则进入 Traveling 状态；到站再 `EnterNode`／加载 LocalMap |
| RegionEditor | 演进为 **WorldGraph 编辑器**（点＋边）；或暂时 JSON |
| MapEditor | **只编 LocalMap**（已有职责，保持） |

第一章建议最小图（内容可后填）：

```text
node_huangcun  --Trail-->  node_kuangshan
       |                      |
     Trail                  Trail
       v                      v
node_linjian  --Road-->  node_dukou（或山口）
```

`huangcun.localMapId =` 现有 `base:map_ch01_reference`（可改名，非必须）。

---

## 4. 运行时状态（概念）

```text
PartyWorldPresence
  mode: AtNode | Traveling | InEncounter
  nodeId
  routeId + progress 0..1     （Traveling）
  localMapId                  （已加载的实体图，可空）
  encounterId                 （临时）
```

- `AtNode` 且 Node 有 `localMapId` → Host 加载该图  
- `Traveling` → Host 显示宏观旅行／时间流逝，不刷大荒野  
- `InEncounter` → 加载遭遇 LocalMap  

Snapshot：第一期可只存 `nodeId` + 任务／仓库；Route 进度与 Encounter 中盘后补。

---

## 5. 明确不做（本修订）

- 无缝开放世界、NavMesh 跨 Node  
- 把药田／一堵墙做成 WorldNode  
- 第一期道路建造玩法（数据预留即可）  
- 改 Architecture Freeze 历史正文的逐字删除；以**本文为世界结构新真源**，`24` 顶部挂修订说明  
- 一口气重写全部 `worldRegion` 消费方；分阶段替换 Travel

---

## 6. 落地阶段

| 阶段 | 交付 | 验收 |
|------|------|------|
| **A 数据** | `worldGraph`／`worldNode`／`worldRoute` SCHEMA + Loader；Ch01 小图 JSON | **已落地** |
| **B 旅行** | Core：组队 `StartTravel`／到站；无通行令 | **已落地** |
| **C Host 宏观** | 顶栏「地图」／M；节点上显示角色；勾选组队移动 | **已落地** |
| **D 卸载／持久** | 离开 Node 卸实体表现；按 localMapId 换图；库存／任务保留 | **已落地**（作物持久未单测；占位 Node 清空表现） |
| **E Encounter** | 一条 Trail 触发临时 LocalMap，结束回 Route | 手操山谷遭遇一次 |
| **F 工具** | WorldGraph 编辑器 | **已落地**（`启动-WorldGraphEditor.cmd`） |
| **G 战略层／接战** | 帮派占点、外交、ArmyStack、BattleOffer 弹窗（自动／手动 LocalMap） | 见 [138](138-world-strategic-battle-offer-plan-2026-08-17.md) VS-WorldStrategic-0.1 |

当前代码冻结线：**A～D／F 已落地**；E 路上遭遇未做；**G 设计已定、待实现**。村内地点正式类型＝`localPlaceSet`；`worldRegion` 仅旧 VS（青石）。

---

## 7. 对现有文档的效力

| 文档 | 效力 |
|------|------|
| 本文 113 | **世界结构新真源** |
| `24` World→Region(连续)→LocalMap | Region「连续大区」作废；改为 Graph；LocalMap 定义加宽（据点本体，不只山洞） |
| VS0.9 Travel | 命令可保留，语义改为走 Route，不再瞬间换点（B 阶段） |
| 112 MapEditor | 仍只编 LocalMap，正确 |
| 109 RegionEditor | 将改为编 Graph，或降级为「Local 内地点表」 |

---

## 8. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-17 | G：战略层／接战弹窗设计见 [138](138-world-strategic-battle-offer-plan-2026-08-17.md) |
| 2026-08-16 | D／F：换 Node 卸装 LocalMap；localPlaceSet；WorldGraphEditor |
| 2026-08-16 | 阶段 B／C：PartyWorldPresence＋Travel／关隘；Host 地图按钮／M；废 Y 宏观 Travel |
| 2026-08-16 | 阶段 A：SCHEMA／Loader／`ch01_world_graph.json`／EditMode；Demo 仍走 worldRegion |
| 2026-08-11 | 初版：确认 WorldNode＋WorldRoute＋按需 LocalMap＋路上 Encounter |
