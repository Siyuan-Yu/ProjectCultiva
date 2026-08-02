# 101 · NPC Simulation Foundation Milestone 计划 v0.1

> 状态：**已完成（自动化）**｜验收：[102](102-npc-simulation-foundation-acceptance-report.md)｜日期：2026-08-03  
> 前置：Navigation Foundation（[99]/[100]）；Schedule 直接产 Labor／Observe；Host NPC 走位硬编码地点 id  
> **目标：策划可用配置定义 NPC 身份、工作、活动范围与日程行动；Data 驱动，无硬编码坐标。**

## 0. 完成标准

1. Location 扩展：类型（既有 kind）、标签、可用活动。  
2. WorkArea：可配置活动范围（绑定 Location＋可选偏移）。  
3. JobDefinition：农夫／矿工／巡卫／管事，含工作目标区域。  
4. 管线：`Schedule Block → Activity Resolver → Activity → MoveAction → WorkAction`（Schedule 不直接产劳动订单）。  
5. NPC 移动走统一 `MoveAction`＋既有 Host 寻路。  
6. 样例：药农→药田、矿工→矿洞、巡卫→路线巡逻、主管→区域检查。  
7. EditMode 测＋验收报告。

## 1. 不做

- 战斗 AI、复杂决策 AI、大世界 AI  
- Snapshot 强制升版（Move／Work／TargetRef 仅软兼容附加字段）  
- 改 Freeze：Core 仍禁 `UnityEngine`

## 2. 分层

| 层 | 职责 |
|---|---|
| **Content** | `work_areas.json`／`jobs.json`；Location `tags`／`allowedActivities`；spawn `jobId` |
| **Core.Npc** | WorkArea／Job 运行时定义、JobComponent、MovementIntent、ActivityResolver、NpcActivityDriver、Move／Work Action |
| **Host** | 读 MovementIntent 寻路；删除硬编码地点启发式 |
| **Docs** | 本计划＋验收＋Devlog |

## 3. Phase

| Phase | 交付 | Commit 前缀 |
|---|---|---|
| NPC-0 | 本计划 | `docs(npc)` |
| NPC-A | Location／WorkArea／Job Data＋样例 | `feat(data): npc job` |
| NPC-B | Activity Resolver＋Move／Work＋Driver | `feat(core): npc activity` |
| NPC-C | Host 接 MoveAction；去硬编码 | `feat(host): npc activity move` |
| NPC-D | 测＋验收＋Devlog＋飞书 | `docs(npc): accept` |

## 4. 管线示意

```text
ScheduleDefinition.Block
        ↓
NpcActivityDriver（仅 JobComponent）
        ↓
ActivityResolver（Job × Activity → WorkArea／Location）
        ↓
不在目标地点 → OrderType.Move → MoveAction → Host 寻路
在目标地点   → OrderType.Work → WorkAction（劳动／休息／巡逻耗时）
```

无 Job 的 NPC 仍走旧 `ScheduleDriver`（兼容）；样例 NPC 均绑 Job。
