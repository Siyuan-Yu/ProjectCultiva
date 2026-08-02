# 98 · RTS 手动控制＋HUD／交互点（2026-08-03）

> 样例关：`DemoParityHost` → `base:scenario_ch01_reference`  
> 本地真源；飞书单向覆盖同步。

## 1. 控制模型（相对了不起的修仙模拟器）

| 项目 | 决策 |
|------|------|
| 己方 Character | **默认完全玩家点选安排**；`ScheduleDriver` **跳过** `EntityTag.Character` |
| 课表 UI | 只读参考；标题注明「己方不自动」 |
| 日后「自动行动」 | 未做；再开时才允许日程注入己方 |
| NPC | 仍跟日程抽象订单；**尚无**走到工区／岗哨的表现 AI |

「巡查中」根因：凡人课表 Explore→`ObserveAction`，HUD 旧标签写成巡查；己方空闲时还会被课表接管。现已堵住己方自动注入，并改标签。

## 2. 指令条（仅己方）

| 按钮 | 热键 | 行为 |
|------|------|------|
| 移动 | Q | 点选地面移动（绿光标） |
| 停止 | F1 | 立即 Stop |
| 交互 | E | 点选工区交互点／人物；工区抵达后 Labor |
| 战斗 | F8 | **占位**（绿/红光标，无战斗 Core） |
| 修炼 | F6 | 点选灵泉／洞府交互点，抵达后 Cultivate |

- 右键仍可快捷移动；点选模式下右键／Esc＝取消  
- 敛息仍 G（道具）  
- 场景 Rebuild 热键改为 **F12**（避免与战斗 F8／旧 R 冲突）

## 3. HUD

- 顶栏时钟：`HH:mm`（每 Tick＝15 游戏分钟 → 分针 00／15／30／45）
- **暴露／主管压**：顶栏全局条，条内 `当前/上限`（暴露＝己方 PersonalRisk 最大值的表现聚合）
- 角色面板：交差／体魄／修为条内数值；功法文案；去掉每人暴露／主管压／「灵机」假条
- 非己方：只开信息面板，无指令钮

## 4. 可交互点

表现层 [`HostInteractSpots`](../../Assets/Scripts/Unity/Host/HostInteractSpots.cs)（不进 Core Freeze）：

- 农田 4、树林 3、药田 3、矿 2、灵泉 2、洞府 2  
- 黄球＝工区、青球＝灵地；[`HostInteractSpotPresenter`](../../Assets/Scripts/Unity/Host/HostInteractSpotPresenter.cs) 生成标记  
- 交互／修炼优先命中具体点并走到该点，再开工

## 5. 已知未做（勿当回归）

- NPC／守卫走动巡逻与战斗 AI  
- 守卫数据仍误挂主管课表／Cultivator（另开）  
- 世界级暴露 Core 字段（仍按人累加、顶栏聚合显示）  
- 真战斗、神识可见性、自动按课表按钮  

## 6. 主要代码路径

- Core：`ScheduleDriver`、`DayClock.MinuteOfHour`  
- Host：`HostFormalHud`、`HostWorkTargetMode`、`HostMoveController`、`HostZoneQuery`、`HostInteractSpots*`、`HostActivityPresenter`  
- Content：开局／任务文案  
- Tests：`HostCharacterManualControlTests`、`HostZoneQueryTests` 交互点  

## 7. 操作签收（手操）

1. 顶栏时间有分钟跳动（加速后可见 15 步进）  
2. 己方空闲不自行变巡查／工作  
3. 点交互 → 农田多个黄点均可绿点 → 走到点后工作  
4. 点守卫：仅查看，无上方指令  
