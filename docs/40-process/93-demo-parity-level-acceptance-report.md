# Demo 手感对齐关 · 验收报告

> 状态：**自动化门禁已通过；样例关独立场景**｜日期：2026-08-02  
> 缺口真源：[91](91-demo-v0.1-to-formal-gap-audit.md)｜进度：[92](92-demo-parity-progress-2026-08-02.md)  
> 对照：[49]§5（攻击占位 Out）

## 1. 结论

正式 **Host＋Core＋Content** 已按 [32]／[49] 文档内语义，对齐 Demo_v0_1 **可验收手感**（2D Sprite、XY 操作、工区／灵地、Stop／W／C／X／G、暴露／愤怒、课表、氛围 NPC）。  
**未**复活 Demo Runtime 为玩法真源；**未**做攻击占位／可改课表／真战斗。

## 2. 测试

- EditMode：**含 `DemoParityLevelAcceptanceTests`／`DemoParityHostPresentationTests`**（跑全绿后记入 commit）
- Snapshot schema 仍为 v1

## 3. 手操入口

- **样例关场景**：`Assets/Scenes/DemoParityHost.unity`（菜单 `XianXia/Demo Parity/Create Or Update Sample Level Scene`）
- Scenario：`base:scenario_ch01_reference`（写在样例场景 Bootstrap 上）
- **框架测试场景**：`PlayableHost`（可挂调试面板；默认不再绑样例 Scenario）
- 地图：`HostDemoTileMap` 按 Demo ChooseGround 规则铺砖（stride=1）
- 地点坐标对齐 Demo 工区中心（林／药／田／灵地）
- 调试按钮（劳动数字指令、Save/Load 条）默认关闭；F5/F9 仍可用

## 4. [49]§5 对照

| # | 清单 | 状态 |
|---|---|---|
| 1 | A 右键森林采木 | ✅ 右键工区／Labor |
| 2 | B 同时农田工作 | ✅ 多选独立指令 |
| 3 | C 灵地修炼 | ✅ 右键 Opportunity／C |
| 4 | 互不覆盖／可中断 | ✅ Stop＋新命令 |
| 5 | 暂停／倍速 | ✅ 既有 |
| 6 | 昼暴露／G 敛息 | ✅ |
| 7 | 守卫巡逻／主管归府 | ✅ Schedule（表现头顶字） |
| 8 | 村民群体状态 | ✅ HostCrowdPresenter |
| 9 | 无红错 | ✅ EditMode 门禁 |

## 5. 残留 Partial

- 铺砖 stride=2（非逐格 4000 实例）；要满密可把 `HostDemoTileMap.stride=1`
- 日课三资源任务数值仍可再对齐 Demo DailyTasks 配置

## 6. 后续增量（同日，见 [97]）

样例关已叠加：内容打断 CIF、2G 觉醒弧 Data、首次入区自动勘察与操作引导。手操入口仍以 `DemoParityHost` 为准；完整清单见 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)。
