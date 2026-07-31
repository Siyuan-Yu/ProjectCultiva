# Demo v0.1 原型现状（2026-07-31）

> **用途**：Demo 可玩版本的冻结快照。换设备、交接、对照语义时查阅。  
> **阶段：Demo 功能开发已结束；项目进入架构冻结阶段。**  
> 主契约：`../30-tech/33-architecture-core-rules-freeze-v0.1.md`  
> 桥接：`../30-tech/32-prototype-to-product-bridge.md`  
> 仓库：https://github.com/Siyuan-Yu/ProjectCultiva  
> Unity：**2022.3.6f1 Built-in** | 场景：`Assets/Scenes/Demo_v0_1.unity`

## 1. 一句话现状

Demo v0.1 灰盒原型已可验证 **「白天劳动 → 夜晚偷修 → 多角色分工 → 荒村自主运转」** 的基础循环；尚未进入突破、真战斗伤害、夺府与潜行判定。

## 2. 已落地里程碑

| 里程碑 | 验证内容 | 状态 |
|---|---|---|
| 开工 | 可替换 Sprite、三人 RTS 移动、荒村灰盒 | ✅ |
| 灰盒 + 时钟 | 80×50 地图、镜头缩放、GameClock 暂停/1x/2x/5x | ✅ |
| M3 生活循环 | 每日任务、木材/粮食/草药、工作区产出、主管愤怒（只显示） | ✅ |
| M3.5 统一行动 | 右键下令 → 走近 → 工作/修炼 → 进度/中断/失败 | ✅ |
| M4 秘密修炼 | 灵地、修为 0～1000、暴露风险、敛息草 | ✅ |
| M5 NPC 日程 | 守卫 Patrol/Rest、主管昼夜巡视、村民群体状态 | ✅ |

## 3. 明确未做（下一 Milestone 前勿擅自扩展）

- 第一次突破、功法、灵根
- 战斗伤害、技能、妖兽战、主管战
- 夺府、占领控制核心
- 发现玩家、追捕、潜行判定
- 暴露/愤怒的真实惩罚演出
- 正式 UGUI、正式美术

## 4. 核心玩法（当前版本）

### 4.1 角色与选择

- 左键点选；拖拽框选；Shift 多选/取消
- 双击己方单位 = 全选三人
- 选中后底部状态栏常驻；点 UI 按钮不丢选中

### 4.2 移动与工作（M3.5）

| 操作 | 效果 |
|---|---|
| 右键地面 | 移动（中断当前工作/修炼） |
| 右键工位 | 采集木材 / 草药 / 耕作（自动走近后开工） |
| `W` | 黄指针选工位（兼容旧流程） |
| `S` | 停止当前指令 |
| 离开工位 / 新命令 | 中断旧行动 |

工位数量：农田 5、森林 4、草药 3。产出数值在 `WorkZone` / `ActionSettings`，不写死在角色脚本。

### 4.3 修炼（M4）

| 操作 | 效果 |
|---|---|
| 右键灵地 / `C` | 前往灵地后入定，修为持续增长 |
| `X` | 出定 |
| `G` | 消耗敛息草降低暴露（初始 3） |
| 移动 / 新工作命令 | 中断修炼 |

- 修为：`CultivationProgress` 0～1000
- 暴露：`ExposureRisk` 0～100；夜晚低、白天高、靠近主管额外增加；**只显示不惩罚**
- 一人修炼时，其他角色可独立移动/工作

### 4.4 攻击（占位）

- `A` → 红指针选 NPC → 追击交战（无伤害，仅状态/视觉）
- 离开/移动则停战

### 4.5 劳役表与愤怒

- **全村一张劳役表**（右侧「课表」竖栏）；测试模式可改格子
- 愤怒：工时内未工作 **且** 靠近主管/守卫才累计；本阶段无惩罚事件

### 4.6 NPC 日程（M5）

| NPC | 行为 |
|---|---|
| 主管 | 白天沿路线巡视，晚上回住所 |
| 守卫 ×2 | 巡逻点列表 + 路线；Patrol / Rest 切换 |
| 村民 | 群体状态标签（工作中/休息中），不逐人全模拟 |
| 商人 | 简单游荡（占位） |

NPC 头顶显示：巡视中 / 工作中 / 休息中。无发现、追捕、潜行判定。

## 5. 验收清单（Play Mode）

1. 角色 A 右键森林 → 自动走近并持续采集木材  
2. 角色 B 同时在农田工作  
3. 角色 C 右键灵地修炼，修为随游戏时间增长  
4. 三人行动互不覆盖；新命令可中断旧行动  
5. 暂停/倍速正确影响行动与修炼进度  
6. 白天修炼暴露上升；`G` 用敛息草降低  
7. 守卫白天巡逻、夜间回休息点；主管夜间归府  
8. 住宅旁可见村民群体状态  
9. Console 无红色报错  

## 6. 关键代码入口

| 系统 | 路径 |
|---|---|
| 场景生成 | `Assets/Editor/DemoPrototypeBuilder.cs` |
| 统一行动 | `Assets/Scripts/Runtime/Actions/` |
| 输入/选中/右键 | `Assets/Scripts/Runtime/Input/PartyCommandController.cs` |
| 单位控制 | `Assets/Scripts/Runtime/Presentation/DemoUnitController.cs` |
| 修炼 | `Assets/Scripts/Runtime/Cultivation/` |
| 工作/工位 | `Assets/Scripts/Runtime/World/WorkSystem.cs`、`WorkSpot.cs` |
| 劳役表/愤怒 | `Assets/Scripts/Runtime/Time/`、`Obligation/` |
| NPC 日程 | `Assets/Scripts/Runtime/Npc/NpcScheduleConfig.cs` |
| 氛围 NPC | `Presentation/AmbientNpcActor.cs`、`AmbientWorldBootstrap.cs` |
| 村民群体 | `Presentation/VillageCrowdPresenter.cs` |
| HUD | `Assets/Scripts/Runtime/UI/DemoPrototypeHud.cs` |

## 7. 配置资产

| 资产 | 路径 |
|---|---|
| 劳役表 | `Assets/Configs/Schedules/DaySchedule_Laborer.asset` |
| 每日任务 | `Assets/Configs/Tasks/DailyTasks_Supervisor.asset` |
| 愤怒 | `Assets/Configs/Obligation/SupervisorAnger_Default.asset` |
| 修炼 | `Assets/Configs/Cultivation/Cultivation_Default.asset` |
| 守卫日程 | `Assets/Configs/Npc/NpcSchedule_Guard.asset` |
| 主管日程 | `Assets/Configs/Npc/NpcSchedule_Supervisor.asset` |
| 村民群体日程 | `Assets/Configs/Npc/NpcSchedule_VillagerGroup.asset` |

## 8. 设计约束（别走偏）

- 修炼 = **停下就地入定** / 走近灵地后修炼，不是工作式选目标战法  
- 工作 = **显式开工**（右键工位或 W→点工位），到区不等于 Working  
- 劳役表 = **全村村规**，不是三人分别排班  
- 原型 HUD 为 **IMGUI 调试层**，不是正式 UGUI  
- 暴露/愤怒本阶段只显示数值  

## 9. 相关文档

| 文档 | 用途 |
|---|---|
| `44-session-handoff-2026-07-31.md` | 会话交接、快捷键速查 |
| `42-devlog.md` | 按时间倒序的改动记录 |
| `45-demo-v0.1.md` | Demo 范围与设计目标（冻结范围） |
| `41-roadmap.md` | 里程碑总览 |

## 10. 建议下一步

1. **不要继续扩展 Demo。**  
2. 在架构冻结阶段展开 `2C`／`2E`／第一次突破事件规格。  
3. 规则确认后再进入正式 Core 实现（见 `32` 重构顺序）。
