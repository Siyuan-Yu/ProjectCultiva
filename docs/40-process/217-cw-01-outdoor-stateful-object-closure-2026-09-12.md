# CW-01 Outdoor 动态物件状态闭环交接（2026-09-12）

## 状态与范围

- **Implementation：Completed for CW-01**。Outdoor 墙／树的已有状态现同时约束物件表现与 Continuous Outdoor WalkGrid；农田已有状态在重建时恢复到对应地块表现。
- **Producer Acceptance：Pending**。本轮完成离线编译和定向自动检查，没有启动 Unity，不能据此宣称 Play 验收通过。
- 本轮没有新增 Snapshot 字段、没有提升 schema version，也没有改运行时 JSON、Content、场景、Prefab、地图或 Bake。现有 `WorldSnapshot.OutdoorDestructibles`／`OutdoorFarmPlots` 及正式 JSON Capture/Restore 继续作为持久化真源。
- 本轮不包含 SiteCore、独立遭遇、战争授权、接管、继承、飞舟、WorldMap、Auto Travel 或全局导航扩展。

## 已确认根因与修复

### 1. 状态此前没有进入 Continuous blocker 合成

`HostMapDestructible` 已经把 HP／Destroyed 写入 `OutdoorStatefulObjectBoard` 并销毁 GameObject，但 `ContinuousOutdoorSurfaceRuntime.BuildSiteBlockerGrid` 仍无条件栅格化 authored placement。因此墙在画面上消失后，WalkGrid 仍把原格视为阻挡。

现在使用同一 authored stable ID 解析有效状态：

`authored placement + stable ID → OutdoorStatefulObjectBoard → presentation + Site blocker grid`

墙仍按 checked-in placement 的 source cell 拆分，单元 ID 为 `<placementStableId>:<localX>:<localY>`。一个单元的 tombstone 只跳过该单元，其他墙段保持阻挡。水面、固体地理层和其他 blocker 仍由各自输入参与 Composite WalkGrid，不会因为墙体销毁而被清除。

### 2. 销毁后没有导航失效信号

`OutdoorStatefulObjectBoard` 新增只表达碰撞拓扑改变的 revision。普通 HP 下降不触发重组；物件首次进入或离开 Destroyed 状态才改变 revision。Host 的实际销毁路径同时通知 Continuous runtime，runtime 在下一次 Update 合并执行一次 WalkGrid 重组，不做每 Tick 全量重建，也不改变普通 chunk crossing 的 staged streaming。

### 3. 同 Surface 读档可能保留旧 World 的瞬态对象

正式读取会替换 `Session.World`。旧路径在 SurfaceId 未变时可能直接返回，导致保留的地块、注册表、SurfaceGround 或导航仍绑定旧 World。

现在 Snapshot restore 是明确的 hard rebind 边界：清理旧 Surface presentation 时不把旧 View 坐标捕获进刚恢复的新 World，然后从恢复后的 canonical WorldPosition 重新激活 Surface、loaded chunks、地块、动态物件、实体 materialization、SurfaceGround 和 WalkGrid。普通跨 Chunk 仍走已有增量路径，不触发 hard rebuild。

## 兼容边界

- 没有 Board override 的 authored 物件保持原始表现和碰撞。
- Destroyed／`hp <= 0` 统一视为 tombstone；读回后不重新实例化该墙／树，也不恢复其 authored blocker。
- 旧 JSON 缺少两个 Outdoor 数组时按空 override board 读取，继续使用 authored 默认状态。
- Legacy LocalMap 的历史下划线墙单元 ID 未迁移；CW-01 只统一 Continuous Outdoor 已使用的冒号 stable ID，避免扩大兼容面。
- Farm plot 的 stable cell state 继续由 `HostMapPlotCell.Configure` 读取；hard rebind 确保读档和 chunk 重建产生的新 Plot 绑定恢复后的 World。

## 修改入口

- Core 状态与稳定 ID：`Assets/Scripts/Core/Exploration/LocalMapSession.cs`
- Data 有效状态解析：`Assets/Scripts/Data/Content/OutdoorWorldSurfaceDefinition.cs`
- Continuous blocker 合成、拓扑失效和 restore hard rebind：`Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs`
- Continuous placement tombstone 消费：`Assets/Scripts/Unity/Host/HostDemoTileMap.cs`
- 销毁通知：`Assets/Scripts/Unity/Host/HostDestructibleAssault.cs`
- Snapshot 读取后的表现重建入口：`Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`
- 定向回归：`Assets/Tests/EditMode/OutdoorStatefulObjectClosureTests.cs`

## 自动检查结果

- Core／Data／Unity Host／Tests 离线编译：通过，0 error；输出仍含仓库既有 warning。
- `OutdoorStatefulObjectClosureTests`：4/4 通过：
  1. 正式 JSON 首次保存读取及读后继续修改再保存，保留 wall HP／Destroyed 与 farm crop／stage／growth；
  2. 缺少 Outdoor 数组的旧 JSON 可读取为空 override；
  3. 三段墙只销毁中段时，仅中段 blocker 失效且 stable ID 不依赖 chunk 加载顺序；
  4. HP-only 改变不增加 topology revision，碰撞状态改变才增加。
- checked-in Main Surface stable ID 审计：75 个 placement 无重复；按当前逐格规则展开 405 个逻辑 ID，无重复。
- 本文及 devlog 本地 Markdown 链接检查通过；`git diff --check` 通过。
- Unity Play：未运行，制作人验收待完成。

## 制作人最短 Play 路线

1. **单格墙表现与通行**：正常 New Game 到荒村，找到 north horizontal wall placement `base:site_huangcun:place_wall_235031705`（chunk `(4,8)`，source grid `(25,48)`，`9×1`）。打掉其中一格；该格 GameObject 应消失并可通行，相邻墙格仍存在且阻挡。可用 vertical wall `base:site_huangcun:place_wall_235035500`（source grid `(24,39)`，`1×10`）复核纵向情况。
2. **Chunk 往返**：保持被打掉墙格状态，移动到足以让 chunk `(4,8)` 离开 radius-1 loaded neighborhood，再返回。该格不得复活，缺口仍可通行；相邻墙、河流和其他 blocker 不变。
3. **同 Surface 新旧档**：顶部 `LevelTester 开发工具` → `存档` → `保存存档`。先保存破坏前档 A，再打墙／种植并保存档 B；不退出游戏，在同一 Surface 反复读取 A／B，各自应恢复对应墙体、HP 与作物状态。读取 B 后再改一个物件并保存 C，读取 C 应保留这次新修改，以排除旧 World 引用。
4. **地理 blocker 隔离**：河仍不可走、桥仍可走；破坏邻近物件不得抹平河流或其他障碍。沿 [W2A Surface Geography 路线](215-surface-geography-w2a-handoff-2026-09-11.md#focused-producer-route) 冒烟一次原有跨桥自动旅行，不重新扩大 W2A 验收范围。
5. **身份、农田与兼容**：对 `base:site_huangcun:place_grainField_235504495` 或 `base:site_huangcun:place_herbField_234905155` 的实际格子操作作物后保存／读取，作物种类、阶段和成长表现应恢复，且无重复格子。确认角色当前位置、Follow 和精确旅行目标未被读档重建改写；现有无 Outdoor 数组的旧档应载入 authored 默认物件状态，而不清空其他存档内容。

若 Play 中出现状态不一致，记录物件 `stableId`、操作前后 HP／Destroyed 或 crop/stage/growth、所在 chunk、是否刚读档／跨 chunk，以及表现与通行各自的实际结果。CW-01 只有上述正常 Gameplay 路线通过后才能标记 Producer Accepted。
