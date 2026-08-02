# 100 · Navigation Foundation 验收报告

> 状态：**自动化已验收／手操待签收**｜计划：[99](99-navigation-foundation-milestone-plan-v0.1.md)｜日期：2026-08-03  
> 场景：`DemoParityHost`（`base:scenario_ch01_reference`）

## 1. 范围对照

| 需求 | 交付 | 结果 |
|------|------|------|
| 可行走区域 | `WalkGrid`＋`Ch01ReferenceWalkGrid`（80×50，cell=1，房屋／岩块障碍） | 通过 |
| Grid／A* | `GridPathfinder`（4 邻接 A*，世界路径 API） | 通过 |
| Move 接路径 | `HostMoveController` 航点跟随；不可达失败／snap | 通过 |
| NPC Schedule 移动 | `HostNpcScheduleMover` 按活动去房屋／工区／枢纽／灵泉 | 通过（表现） |
| 障碍检测 | 障碍格不可走；路径不穿障碍测 | 通过 |
| 多单位避让 | 移动中软分离（separation） | 通过（基础） |

**不做（确认未做）：** 战斗寻路、大地图、复杂编队、飞行、NavMesh／Physics。

## 2. Phase／Commit

| Phase | Commit | 说明 |
|-------|--------|------|
| NAV-0 | `8339af4` | 计划 [99] |
| NAV-A/B | `41acdf1` | Core 网格＋A*＋测 |
| NAV-C/D | `b025c9a` | Host 路径移动＋NPC 日程走位＋分离 |
| NAV-E | （本提交） | 本验收＋Devlog |

## 3. 自动化测

- `NavigationFoundationTests`：障碍、A* 绕障、Ch01 农田→树林世界路径、最近可行走  
- `HostNavigationPathFollowTests`：绕房屋区路径点可行走  
- 既有 EditMode 套件应保持可编译（Unity 本机占用时以编辑器编译为准）

## 4. 手操签收

1. 选中己方，右键空地：沿弯路到达，不穿房屋障碍块。  
2. 右键点进障碍深处：snap 到附近可行走或不动（不瞬移穿模）。  
3. 解除暂停并加速：杂役／守卫等 NPC 会在课表地点间走动。  
4. 两人近距离同向移动：有轻微挤开，不完全重叠。

## 5. 已知限制

- 障碍为手写矩形，非地砖自动烘焙。  
- NPC 目的地为地点中心启发式，非精细工位。  
- 避让为局部排斥，非 ORCA／流量场。  
- 路径不进 Snapshot。

## 6. 结论

**Navigation Foundation Milestone：核心验收项已落地。** 制作人手操 [§4](#4-手操签收) 后即可视为 Demo 0.1 导航底座完成。
