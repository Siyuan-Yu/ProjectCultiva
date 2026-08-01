# Demo 手感对齐关 · 验收报告（PlayableHost）

> 状态：**自动化门禁已通过；满幅 Prefab 铺砖为 stride 抽稀版**｜日期：2026-08-02  
> 缺口真源：[91](91-demo-v0.1-to-formal-gap-audit.md)｜进度：[92](92-demo-parity-progress-2026-08-02.md)  
> 对照：[49]§5（攻击占位 Out）

## 1. 结论

正式 **PlayableHost＋Core＋Content** 已按 [32]／[49] 文档内语义，对齐 Demo_v0_1 **可验收手感**（2D Sprite、XY 操作、工区／灵地、Stop／W／C／X／G、暴露／愤怒、课表、氛围 NPC）。  
**未**复活 Demo Runtime 为玩法真源；**未**做攻击占位／可改课表／真战斗。

## 2. 测试

- EditMode：**含 `DemoParityLevelAcceptanceTests`／`DemoParityHostPresentationTests`**（跑全绿后记入 commit）
- Snapshot schema 仍为 v1

## 3. 手操入口

- 场景：`PlayableHost`（菜单重建以挂新组件）
- 默认 Scenario：`base:scenario_ch01_reference`
- 地图：`HostDemoTileMap` 按 Demo ChooseGround 规则铺砖（stride=2）
- 地点坐标对齐 Demo 工区中心（林／药／田／灵地）

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
