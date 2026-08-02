# 102 · NPC Simulation Foundation 验收报告

> 状态：**自动化已验收／手操待签收**｜计划：[101](101-npc-simulation-foundation-milestone-plan-v0.1.md)｜日期：2026-08-03  
> 场景：`DemoParityHost`（`base:scenario_ch01_reference`）

## 1. 范围对照

| 需求 | 交付 | 结果 |
|------|------|------|
| Location 类型／标签／可用活动 | `kind`＋`tags`＋`allowedActivities`（region JSON→`WorldLocationState`） | 通过 |
| WorkArea | `work_areas.json`／`WorkAreaDefinition`（locationId＋offset 数据） | 通过 |
| JobDefinition | `jobs.json`：农夫／药农／矿工／巡卫／管事＋activityBindings | 通过 |
| Schedule→Activity→Move→Work | `NpcActivityDriver`＋`ActivityResolver`＋`MoveAction`／`WorkAction` | 通过 |
| Navigation 接入 | Host 读 `MovementIntent` 寻路；无硬编码地点 id | 通过 |
| 样例 NPC | 药农→药田、矿工→矿洞、巡卫路线巡逻、主管区域检查 | 通过（Data） |
| 全 Data 驱动／无硬编码坐标 | 目的地仅来自 Location／WorkArea 内容 | 通过 |

**不做（确认未做）：** 战斗 AI、复杂决策 AI、大世界 AI。

## 2. Phase／Commit

| Phase | 说明 |
|-------|------|
| NPC-0 | 计划 [101] |
| NPC-A | Location／WorkArea／Job Data＋样例 |
| NPC-B | Core Activity 管线 |
| NPC-C | Host MovementIntent 移动 |
| NPC-D | 本验收＋Devlog＋飞书 |

## 3. 自动化测

- `NpcSimulationFoundationTests`：Resolver 需移动、Move→Work、巡逻 RouteIndex、矿工工区、内容加载＋样例 Job 绑定  
- 既有 `ContentPackageTests.Load_BaseGame_Succeeds` 须继续通过（含新 type）  
- Unity 本机占用时以编辑器编译／EditMode 为准

## 4. 手操签收

1. 解除暂停并加速：药农走向药田并显示工作；矿工走向矿洞。  
2. 巡卫在枢纽／林缘／房屋／农田间轮转移动（非钉死一点）。  
3. 主管在农田／药田／矿洞／灵泉等检查点间移动。  
4. 己方 Character 仍不自动跟课表。

## 5. 已知限制

- Job／MovementIntent／地点会话态不进 Snapshot v1（Move／Work 订单字段为软附加）。  
- 无 Job 的 NPC 仍走旧 Schedule→Labor／Observe（兼容）。  
- WorkArea offset 默认为 0（中心）；精细工位靠内容填 offset。  
- 到达判定为 Host 半径；超时也会提交地点（防卡死）。

## 6. 结论

**NPC Simulation Foundation Milestone：核心验收项已落地。** 制作人手操 [§4](#4-手操签收) 后即可视为 Demo 0.1 NPC 活动底座完成。
