# 117 · 近期更新收束（NPC 对话／任务失败／时间流速）— 2026-08-14

> 状态：**已推送**｜日期：2026-08-14（含 UX polish 增补）  
> 相对提交：`99e93fb` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/M0q4dQsBdojfxixN0DTcXwlTnCh  
> 相关：[116 上一轮](116-recent-updates-rollup-2026-08-14.md)｜[SCHEMA](../../Content/BaseGame/Data/SCHEMA.md)｜[95 内容打断](95-content-interrupt-system-plan-v0.1.md)

---

## 1. 一句话

把 **NPC 右键对话**做成可换皮的 Host 对话框（UGUI＋打字机），并补上 **任务失败／多人好感惩罚**、**失败任务日志页**、以及 **1 现实秒 = 5 游戏分钟** 的时间流速约定。

---

## 2. 交付对照（本轮）

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **NPC 右键交互** | 右键 NPC → 对话／攻击；走近时 NPC 停下；点菜单外可关闭；结束后释放移动 | `HostNpcContextMenu`、`HostMoveController.HoldNpcForInteraction` |
| **onTalk 对话框** | 仅 `trigger: onTalk` 走新对话框；其它 ContentEvent 仍用中央打断弹窗 | `HostDialoguePresenter`／Controller／View |
| **UGUI View** | 程序化底栏（居中固定宽）；立绘占位；打字机；选项 hover；Esc 关 fallback | `HostDialogueUguiView`、`HostDialogueTypewriter` |
| **换皮路径** | `IHostDialogueView`；Controller／Model 不动；以后换 Prefab／Sprite | `IHostDialogueView`、`PortraitResourceId` |
| **对话时 HUD** | 对话进行中隐藏底部 ACS 角色状态栏 | `HostFormalHud.ShouldHideUnitPanelForDialogue` |
| **任务失败** | `deadlineDays` 超时 → Failed + `failResults`；日志 **J** 增「已失败」页；失败取消追踪 | `QuestDeadlineDayHandler`、`HostQuestJournal` |
| **relationDelta 多人** | `toDefinitionIds[]`；`@party` = 全队；兼容旧 `toDefinitionId` | `ContentOutcome`／Applier／PackageLoader |
| **样例数据** | 主管 `onTalk` 事件；日课失败对 `@party` 降好感 | `ch01_reference_events.json`、`ch01_reference_quests.json` |
| **时间流速** | 1 tick = 5 游戏分；1x：1 现实秒 = 1 tick；5x：1 现实秒 = 25 游戏分 | `SimulationTickPacing`、Bootstrap／FormalHud |
| **UGUI 依赖** | 工程启用 `com.unity.ugui`；`XianXia.Unity.asmdef` 引用 `Unity.ugui` | `Packages/manifest.json` |

---

## 3. 对话框架构

```text
ContentEvent(onTalk) ──► HostDialogueController（建 Model、解析选项）
                              │
                              ▼
                       HostDialogueModel
                              │
                              ▼
                       IHostDialogueView
                    ┌─────────┴─────────┐
                    ▼                   ▼
          HostDialogueUguiView   HostDialogueImguiView（调试回退）
          （默认 · 打字机）
```

| 约定 | 说明 |
|------|------|
| 触发 | NPC 抵达后 `TryTalkToNpc`；有 active onTalk → `TryPresentOnTalk`；无内容 → `ShowFallback` |
| 选项 | 走 `HostCommandBridge.ResolveContentChoice`；无 choices 时「继续」清 Active |
| 打字机 | ~32 字/秒；点正文／Space／Enter 跳过；未完成时选项不可点 |
| 布局 | 宽约 720、水平居中、距底约 72；左侧立绘占位框 |
| 打断分工 | onTalk → 对话框；其它 trigger → `HostContentInterruptPresenter` 中央弹窗 |

场景工具（LevelTester／DemoParity／PlayableHost）与 Bootstrap 会自动挂 `HostDialoguePresenter`＋`HostDialogueUguiView`。

---

## 4. 任务失败与好感

| 项 | 约定 |
|----|------|
| 超时 | Quest `deadlineDays`；换日由 `QuestDeadlineDayHandler` 判失败 |
| 后果 | `failResults`（可含 `relationDelta`） |
| 多目标 | `toDefinitionIds: ["idA","idB"]` 或 `["@party"]` |
| 兼容 | 仅写 `toDefinitionId` 的旧 JSON 仍可用 |
| UI | 任务日志「已失败」与进行中／已完成分栏；失败自动取消追踪 |

QuestEditor：`failResults`／目标 Id 支持逗号分隔多目标（见 Shared `JsonArrayEditor`）。

---

## 5. 时间流速

| 倍速 | 现实时间 | 游戏时间 |
|------|----------|----------|
| **1x** | 1 秒 | **5 分钟**（1 tick） |
| **5x** | 1 秒 | **25 分钟**（5 tick） |
| 1 游戏日 @1x | ≈ **288** 现实秒 | 1440 游戏分 |
| 1 游戏日 @5x | ≈ **57.6** 现实秒 | 同上 |

`secondsPerAutoTickAt1x` 默认 1；Awake 强制同步 pacing；切倍速重置 tick 累加器。顶栏可显示如 `5x·25分/秒`。

---

## 6. 手操验收清单

1. LevelTester／DemoParityHost Play → 右键 **杂役主管** → 对话 → 走近后出现 **居中对话框**（非全宽）  
2. 文字打字机；点正文跳过；选项可点；ACS 角色底栏在对话中应 **消失**  
3. 选「遵命。」／「……（不语）」→ 对话框关闭，NPC 恢复走动  
4. 对无 onTalk 的 NPC → fallback「暂无对话」+「结束」  
5. 探索触发的非 onTalk 事件 → 仍为 **中央** 打断弹窗  
6. （可选）接有 `deadlineDays` 的日课 → 跨日失败 → **J → 已失败**；好感按 `failResults` 下降  
7. 主管选「不语」→ 顶部 toast「新任务 · …（已追踪）」→ **J**／右栏显示 **灵药 x/100** 进度  
8. 惩罚后再对话主管 → 催促台词（`base:event_ch01_ref_supervisor_talk_hurry`），不再重复训话  

---

## 7. 对话／任务 UX polish（`99e93fb` 之后）

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **`stockAtLeast` 进度** | 任务进度与右栏目标显示 **当前数量/目标**（如灵药 37/100），不再只是 0/1 | `QuestService.RefreshProgress`、`QuestJournalQuery`、`HostFormalHud` |
| **对话 `startQuest` 反馈** | 选项 outcome 触发 `QuestStarted` 后：**自动追踪** + 顶部 toast（约 4.5s） | `HostQuestJournal.Ingest`、`PlayableHostBootstrap.DispatchDrainedEvents` |
| **惩罚后对话分支** | 已触发惩罚 Flag 时，主管 onTalk 切到 **催促** 事件 | `base:event_ch01_ref_supervisor_talk_hurry` |

### 样例：对话选项 → 任务（验证用）

| 步骤 | 数据 |
|------|------|
| 事件 | `base:event_ch01_ref_supervisor_talk` → 选「……（不语）」 |
| outcomes | `setFlag` + `relationDelta`（`@party` -12）+ **`startQuest`** |
| 任务 | `base:quest_ch01_ref_supervisor_herb_penalty`：`stockAtLeast` 灵药×100，`deadlineDays: 2` |
| 再对话 | `base:event_ch01_ref_supervisor_talk_hurry`（条件：`penalty_assigned` 且未完成） |

EditMode：`ContentEventSupervisorTalkTests`。

---

## 8. 已知未做／注意

- 正式美术换皮（底图／立绘／字体）未做；`PortraitResourceId` 已预留  
- 主管 **定期发任务** 暂缓  
- 右键「攻击」仍为占位（无战斗）  
- 全部 ContentEvent 统一进对话框：**仅 onTalk 第一刀**  
- 旧场景若缺组件：Play 时 Bootstrap 会 AddComponent；或菜单重建场景  

---

## 9. 主要新增／改动文件

| 文件 | 作用 |
|------|------|
| `HostDialogueModel.cs` | ViewModel |
| `HostDialogueController.cs` | 逻辑 |
| `IHostDialogueView.cs` | View 接口 |
| `HostDialogueUguiView.cs` | UGUI 实现 |
| `HostDialogueImguiView.cs` | IMGUI 回退 |
| `HostDialogueTypewriter.cs` | 打字机 |
| `HostDialoguePresenter.cs` | Host 适配 |
| `HostNpcContextMenu.cs` | 右键菜单 |
| `HostNpcInteraction.cs` | NPC 交互辅助 |
| `QuestDeadline.cs`／`QuestDeadlineDayHandler.cs` | 任务时限失败 |
| `SimulationTickPacing.cs` | 时间流速常量 |
| `ContentEventSupervisorTalkTests.cs` | 主管不语→`startQuest`／催促事件 EditMode |

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-14 | 增补：对话→任务 UX polish、`stockAtLeast` 进度、主管惩罚样例链 |
| 2026-08-14 | 初版：对话框／NPC UX／失败与多人好感／时间流速／文档同步 |
