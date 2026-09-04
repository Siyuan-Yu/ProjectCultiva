# 势力／外交只读总览 V0

**日期：**2026-09-05  
**状态：**实现完成，待 Unity 人工验收  
**范围：**WorldMap 运行时势力与外交可见性

## 目标与边界

本轮提供 `WorldMap → 战略 → 势力` 的只读总览，用于查看当前会话内势力、外交、领地与正式军队状态。它不提供外交 mutation：不含宣战、议和、结盟、解除联盟、建立／解除附庸、外交 AI 或谈判 UI。

现有 WorldMap 的角色、军队和势力入口共用同一战略工具栏与侧栏生命周期；切换入口互斥，关闭 WorldMap 时全部关闭。

## 正式真源

当前势力集合由 `FactionDiplomacyOverviewQuery` 从 `SimulationWorld.Strategic` 的正式运行时引用汇总：

- `PlayerFactionId`；
- 活动 `WarBoard`；
- `AllianceBoard`；
- `VassalageBoard`；
- `FormalArmyBoard`；
- `WorldSiteBoard` 的所属势力；
- `TerritoryRegionBoard` 的控制势力。

展示名与排序读取 `StrategicFactionCatalog` 已安装的 faction Content 元数据；没有元数据的运行时 ID 使用既有回退显示。该页面不读取 `strategicOpening`，因此 Save/Load 后继续反映恢复出来的当前 Board 状态。

## 外交关系查询

`FactionDiplomacyRelationQuery` 是 Host 以外的正式只读关系入口，返回：自己、战争、联盟、宗主、附庸、普通。

优先级固定为：

`自己 → 战争 → 联盟 → 直接附庸 → 普通`

方向以观察者为准。若 A 是 B 的宗主，则 A 看 B 为「附庸」，B 看 A 为「宗主」。这使第一章起事前玩家查看旧宗门时显示「宗主」，起事后战争优先显示「战争」。

## V0 面板内容

- 势力列表：正式展示名与其相对玩家的当前关系；
- 势力详情：名称、势力 ID、与玩家关系、控制领地区域数、FormalArmy 数、宗主与附庸；
- 外交关系：选中势力相对其他所有当前运行时势力的关系。

领地统计直接遍历 `TerritoryRegionBoard`；军队统计直接遍历 `FormalArmyBoard`。没有第二份缓存或 UI 侧关系规则。

## 验收与后续

最小纯 Core 测试覆盖附庸方向和战争优先级。当前环境没有可用 Unity Editor、Unity Test Runner 或 `Assembly-CSharp.csproj`，Unity 编译与人工验收由用户执行。

建议人工步骤：打开 WorldMap，依次点击「战略 → 势力」；起事前确认玩家对旧宗门为宗主关系，确认起事后刷新为战争；占领荒村后确认双方领地区域数变化；存档读档后再次确认战争关系保持。

动态外交玩法是后续阶段，不能由本页扩展实现。
