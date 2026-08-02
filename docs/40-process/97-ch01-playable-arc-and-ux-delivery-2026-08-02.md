# 第一章可玩弧＋内容打断＋RTS 引导 · 交付总结（2026-08-02）

> 状态：**自动化已验收；制作人手操签收中**｜日期：2026-08-02  
> 场景入口：`Assets/Scenes/DemoParityHost.unity`（Scenario：`base:scenario_ch01_reference`）  
> 制作指南：[94](94-chapter-full-production-and-sample-guide.md)｜流程草案：[2G](../20-systems/2G-first-chapter-flow.md)  
> 打断计划／验收：[95](95-content-interrupt-system-plan-v0.1.md)／[96](96-content-interrupt-system-acceptance-report.md)  
> 手感对齐：[91](91-demo-v0.1-to-formal-gap-audit.md)／[93](93-demo-parity-level-acceptance-report.md)

---

## 1. 一句话

在 **不复活 Demo Runtime、不升 Snapshot、不改 Freeze** 的前提下，把样例关做成可手操的 **2G 凡人觉醒弧**（劳役→神识→机缘→功法→暗修→炼气→隐藏→权力伏笔），并补上 **内容打断暂停** 与 **RTS 可理解操作引导**。

---

## 2. 本轮交付清单

### 2.1 场景与 Host 交互

| 项 | 说明 |
|---|---|
| 样例关场景 | `DemoParityHost`；菜单 `XianXia/Demo Parity/Create Or Update Sample Level Scene` |
| 框架场景 | `PlayableHost` 不再默认绑样例 Scenario |
| RTS 移动／派工 | 右键只移动；劳动／入定需 F4／F6 或 W 点选工区后抵达下达 |
| 点选模式 | W／底栏劳役·入定 → 左键确认工区（`HostWorkTargetMode`） |
| 指令范围 | 底栏 `IssueOne`：**只令焦点一人**；群体移动仍可用右键多选 |
| 首次入区勘察 | `HostMoveController.ApplyPresentationArrival`：换区 NotifyArrived＋**首次** `ExploreHere`（对齐「走进区域」） |
| ACS 风格 HUD | `HostFormalHud`：点选己方开宣纸板；顶栏资源／时间；右栏课表·任务·事件；F10 显隐 |
| 操作引导 | 顶栏下常驻操作条；按当前任务提示下一步；开局事件含怎么玩 |
| 热键整理 | F1–F4／F6–F8 行动；**G 敛息**；调试 HUD 改 **F11**（不再与 F1 休息冲突）；去掉重复意图条 |

### 2.2 内容打断（CIF）

| 项 | 说明 |
|---|---|
| 组件 | `HostContentInterruptPresenter` |
| 行为 | ContentEvent 选项弹层＋Quest 接取／完成／失败「知道了」；强制暂停；优先级 事件＞任务提醒＞RTS |
| Core | `ExplorationService.NotifyArrived`；Bootstrap `DispatchDrainedEvents` 供 Interrupt／EventFeed 共用 |
| FormalHud | 已去掉内嵌事件模态 |

### 2.3 第一章参考 Data（对齐 2G，无战斗）

文件：`Content/BaseGame/Data/ch01_reference_{quests,events,chapter}.json`（及既有 region／characters／scenario）。

| 阶段 | 任务／事件要点 |
|---|---|
| 开局压迫 | `event_ch01_ref_opening`＋日课巡视 |
| 生存压力 | 伐木／采药日课；主管日 beat 施压 |
| 神识 | 灵泉事件（首次入区勘察或 F3） |
| NPC 机缘 | 砍柴老人（树林 `onArrive`）；行商密语（枢纽 `onExplore` 替代） |
| 功法／洞府 | 探洞府→入定学青云诀 |
| 暗修→炼气 | 夜缝入定→引气入体→`realmAtLeast QiRefining` |
| 隐藏／伏笔 | 房屋敛息约定→枢纽制度伏笔；`story:ch01_ref_arc_complete` |
| 明确未做 | 战斗、夺主管据点、多段对话树 |

任务链靠 `autoOffer`＋`offerConditions` 在日课中途衔接（不仅依赖换日 `ApplyQuestChain`）。

### 2.4 测试门禁

- EditMode：**194/194**（含 `ReferenceLevel_FullAwakeningArc_ToEpilogue`、`HostRtsFirstVisitSurveyTests`、打断／HUD 相关测）
- Snapshot schema 仍为 **v1**（未升版）

---

## 3. 手操验收路径（约 25～40 分钟）

1. 开局弹层（含操作说明）→ 任务「知道了」  
2. 已预选第一人：F3／底栏「探索」巡视农田（或先走开再走回触发首次勘察）  
3. 走到树林后点「劳动」→ 药田同理  
4. 右键进灵泉听异响 → 再进树林遇老人（或枢纽再探索找行商）  
5. 进洞府入定学诀 → 再进洞府「引气入体」→ 多次入定至炼气  
6. 房屋区隐藏约定 → 枢纽权力伏笔收束  

---

## 4. 已知残留（验收时请知情）

| 项 | 说明 |
|---|---|
| 工区无「可右键」高亮 | 靠地图 TextMesh 地名＋操作条 |
| 假读数 | 体魄／心境条非完整需求系统 |
| V 行动菜单／F11 调试 | 框架残留；正式手操以 FormalHud 为准 |
| 行商线 | 须在枢纽**再点探索**（路过自动勘察若已发生则不会重复触发 onExplore） |
| 战斗／夺权／产品 UGUI | Out；另开切片 |

---

## 5. 主要代码／内容路径

| 区域 | 路径 |
|---|---|
| Host | `Assets/Scripts/Unity/Host/HostFormalHud.cs`、`HostContentInterruptPresenter.cs`、`HostMoveController.cs`、`HostWorkTargetMode.cs`、`HostZoneQuery.cs`、`HostCommandBridge.cs`、`PlayableHostBootstrap.cs` |
| Core | `ExplorationService.NotifyArrived` |
| Data | `Content/BaseGame/Data/ch01_reference_*.json` |
| 测试 | `Chapter01ReferenceLevelAcceptanceTests`、`HostRtsFirstVisitSurveyTests`、`ContentInterruptSystemAcceptanceTests` 等 |
| 文档 | [94](94-chapter-full-production-and-sample-guide.md)、[95](95-content-interrupt-system-plan-v0.1.md)、[96](96-content-interrupt-system-acceptance-report.md)、本页 |

---

## 6. 下一步建议

1. 制作人按 §3 手操签收 `DemoParityHost`（必要时菜单重建场景挂最新组件）。  
2. 按 [94] 换正式第一章文案／ID（结构可复用）。  
3. 战斗／夺据点／多段对话／产品 UGUI／Snapshot 入档：硬停确认后再开。
