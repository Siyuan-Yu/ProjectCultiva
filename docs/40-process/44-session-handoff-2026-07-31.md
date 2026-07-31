# 会话交接：2026-07-31（Demo 原型）

> 用途：换设备、换 Cursor 会话时**先读这篇**即可恢复上下文。  
> 仓库：https://github.com/Siyuan-Yu/ProjectCultiva  
> 本地路径（本机）：`F:\ProjectCultiva\ProjectCultiva`（亦可能是 `D:\UnityProjects\XianXia`）  
> Unity：**2022.3.6f1 Built-in**（ADR-0001）

## 30 秒摘要

项目已从 Demo 灰盒进入 **架构冻结阶段**。Demo 验证了劳动／偷修／分工／NPC 日程；**不再扩展 Demo 功能**。

主契约：`33-architecture-core-rules-freeze-v0.2.md`  
桥接：`32-prototype-to-product-bridge.md`  

**本阶段不写代码。** 下一步是展开 `2C`／`2E`／第一次突破细则。

## 本轮改动汇总（2026-07-31 交互验收迭代）

### 操控与镜头
- 框选／点选／Shift 多选
- 中键拖地图；滚轮缩放
- 选中后底部状态栏常驻；点 UI 按钮不取消选中
- `S` 停止指令；`W` 工作；`A` 攻击；`C` 入定／`X` 出定／`G` 敛息

### 角色／NPC 查看
- 可控三人底部栏：修为／暴露／灵气／指令／操作按钮
- NPC（主管／守卫／村民／商人）可点：底部**只读**身份栏
- 主管头顶**红三角**、守卫**黄三角**
- 移除左侧「状态」汇总面板（与底部栏重复）；「详情」留给建筑／区域

### 工作（重要语义）
- **M3.5 统一行动**：右键工位 = 采集／耕作（自动走近）；右键灵地 = 修炼；右键地面 = 移动
- `W` 仍可进入黄指针选工位（兼容）；`A` 攻击选目标保留
- 离开工位／新命令会中断；暂停与倍速影响行动进度
- 农田 5／森林 4／草药 3 个工位；数值在 WorkZone／ActionSettings

### 修炼（M4）
- 右键灵地或 C：前往灵地后入定涨修为（0～1000）
- 暴露：夜晚低、白天高、靠近主管额外增加；**只显示不惩罚**
- G：消耗敛息草降低暴露；初始库存 3；灵地内未修炼可缓慢采草
- X／移动／新工作命令：中断修炼；一人修炼不影响其他人工作

### 劳役表（重要语义）
- **全村一张表**，不是 RimWorld 式三人分别排班
- 含义：主管规定的劳役时段 + 村民按表活动 + 对玩家的「该不该在干活」判定
- UI：右侧竖栏 00～23（类似修仙模拟器）
- 愤怒：工时内未工作 **且被主管／守卫发现** 才涨

### 世界氛围（M5）
- 主管／守卫按 `NpcScheduleConfig`：白天巡视、晚上休息（无发现／追捕）
- 村民群体状态标签（不逐人全模拟）；少量氛围劳工仅作点缀
- NPC 头顶状态：巡视中／工作中／休息中

### 明确未做
突破、战斗、夺府、暴露／愤怒真实惩罚演出、正式 UGUI、正式美术。

## 新设备开工 5 步

1. `git clone https://github.com/Siyuan-Yu/ProjectCultiva.git`（或已有仓库则 `git pull`）
2. 用 **Unity 2022.3.6f1** 打开工程根目录
3. 打开 `Assets/Scenes/Demo_v0_1.unity` → Play
4. 若场景缺组件／过旧：菜单 **XianXia → Build Demo v0.1 Prototype** 重建
5. 再读本文件「操作」与 `docs/40-process/42-devlog.md` 顶部 5～8 条

## 已落地里程碑（代码）

| 里程碑 | 内容 | 提交参考 |
|---|---|---|
| 开工 | 可替换 Sprite、三人控制、占位场景生成器 | 更早 commits |
| 灰盒 + 时钟 | 80×50 图、镜头、GameClock、只读时间表雏形 | `3e224f9` |
| M3 生活循环 | 每日任务、资源、工作区、主管愤怒（只显示） | `dda70b8` 内 |
| M4 秘密修炼 | 灵地、Cultivating、修为、暴露、敛息草 | `dda70b8` 内 |
| M5 NPC 日程 | 可配置日程、守卫 Patrol/Rest、主管昼夜、村民群体状态 | 本轮 |
| 时间表网格 | 24h 网格雏形 | `dda70b8` |
| M3.5 工作交互 | Working 才产资源 | `f3c56cc` |
| RTS 可读性包 | 框选／状态栏／工位／劳役表／威胁标／氛围 NPC | 本提交 |

## 当前怎么玩（验收操作）

- **选择**：左键点选；拖拽框选；Shift 追加／取消
- **可控角色**：底部状态栏；**工作(W)** → 黄指针点工位；**攻击(A)** → 红指针点 NPC 交战；`S` 停止
- **NPC**：左键点主管／守卫／村民 → 底部只读栏；攻击模式下可作为交战目标
- **镜头**：中键拖地图；滚轮缩放
- **移动**：右键空地／工位 = 只移动并停工／停战（有落点 X）；选目标时右键／Esc 取消
- **选择**：双击己方单位 = 全选三人
- **劳役表**：右侧「课表」竖栏 = 全村村规；测试可改
- **愤怒**：工时偷懒且靠近主管／守卫才涨
- **修炼**：先右键走到东南灵地，再按 **C 入定**（停下打坐）；**X 出定**；移动会出定
- **时间**：空格暂停；1／2／5 倍速
- **标记**：主管红三角／守卫黄三角；灵地青色菱形；入定角色略压扁 + 头顶青环

## 关键代码入口

| 系统 | 路径 |
|---|---|
| 场景生成 | `Assets/Editor/DemoPrototypeBuilder.cs` |
| 输入／选中／工作指令 | `Assets/Scripts/Runtime/Input/PartyCommandController.cs` |
| 单位／工位 | `Presentation/DemoUnitController.cs`、`World/WorkSpot.cs` |
| 劳役表／遵守／愤怒 | `Time/ScheduleService.cs`、`ScheduleComplianceTracker.cs`、`Obligation/` |
| 氛围 NPC | `Presentation/AmbientNpcActor.cs`、`AmbientWorldBootstrap.cs` |
| NPC 日程 | `Npc/NpcScheduleConfig.cs`、`Configs/Npc/*.asset` |
| 村民群体 | `Presentation/VillageCrowdPresenter.cs` |
| 统一行动 | `Actions/CharacterActionController.cs` |
| 修炼 | `Cultivation/CultivationSystem.cs` |
| HUD | `UI/DemoPrototypeHud.cs` |
| 威胁标／区域标 | `World/ThreatOverheadMarker.cs`、`ZoneMapLabelOverlay.cs`、`SpiritSiteMapMarker.cs` |

## 重要设计约束（别走偏）

- 原型 **IMGUI** 调试 HUD，不是正式 UGUI
- 劳役表正式版前期应锁定；现 `allowEditForTesting = true`
- **工作必须显式开工**；到区不等于 Working
- 暴露／愤怒本阶段**只显示数值**（愤怒有检测距离，但仍无惩罚事件）
- 点建筑／NPC **只查看，不下指令**（夺府／攻击留给后续）
- 不要擅自开战斗／占领，除非用户明确进入下一 Milestone

## 建议下一步

1. 展开 `2C` Modifier 数据结构与 `2E` 事件账本  
2. 细化第一次突破事件（`25`／`2G`）  
3. **不要**继续加 Demo 功能；确认规则后再编码

## AI／人开工必读顺序

1. **本文件**
2. **`../30-tech/33-architecture-core-rules-freeze-v0.2.md`**（架构主契约）
3. **`../30-tech/32-prototype-to-product-bridge.md`**
4. **`49-demo-v0.1-prototype-status.md`**（Demo 快照）
5. `AGENTS.md`
6. `docs/40-process/42-devlog.md` 顶部若干条
7. `docs/40-process/45-demo-v0.1.md`

## 旧交接

策划向长文仍见：`44-session-handoff-2026-07-30.md`（方向与设定表仍有效；**工程进度以本文件与 devlog 为准**）。
