# Content Interrupt System Plan v0.1（内容打断／对话选项）

> 状态：**已完成**｜验收：[96](96-content-interrupt-system-acceptance-report.md)｜最后更新：2026-08-02  
> 前置：Content Ready／Chapter Production；Host A 事件弹层原型  
> 制作入口：[94](94-chapter-full-production-and-sample-guide.md) §打断  
> **目标：把「事件选项＋任务提醒打断」做成可配置、可验收的正式 Host 功能，规则仍在既有 ContentEvent／Quest。**

## 0. 完成标准

1. **事件打断**：`contentEvent` 激活时强制暂停，中央弹层显示正文与选项，选项走既有 `ResolveContentChoice`。  
2. **任务打断**：`QuestStarted`／`QuestCompleted` 弹出提醒（标题＋描述＋「知道了」），确认前时间暂停。  
3. **呈现层独立**：`HostContentInterruptPresenter` 负责打断；FormalHud 不再内嵌事件模态。  
4. **抵达触发**：表现层走到地点后调用 Core `NotifyArrived` → `onArrive` 事件／地点挂任务可弹。  
5. **制作可配**：制作人只用 Data（quest／contentEvent／chapter dayBeat／地点 questOfferIds）配置打断内容；无新玩法系统。  
6. EditMode 验收测全绿；样例关 `DemoParityHost` 挂载组件。

## 1. 不做（本切片）

- 多段对话树／立绘／语音  
- 产品级 UGUI 皮肤（仍用 IMGUI 宣纸弹层）  
- Snapshot 升版（Quest／Event／Flags 会话态）  
- 改 Freeze；复活 Demo Runtime  

## 2. 分层

| 层 | 职责 |
|---|---|
| **Core** | 既有 ContentEvent／Quest／Chapter；补 `ExplorationService.NotifyArrived`（抵达钩子） |
| **Data** | 既有 JSON 字段；SCHEMA／制作指南写清「何种内容会打断」 |
| **Host** | `HostContentInterruptPresenter`：暂停策略、事件模态、任务提醒队列、吞掉世界点击 |
| **Docs** | 本计划＋[94] 配置拆分＋验收 |

## 3. 优先级与暂停策略

```text
ContentEvent（有选项，必须点选）  >  Quest 提醒（知道了）  >  正常 RTS
```

- 任一打断占用时：`IsPaused = true`；移动／工区点选不响应。  
- ContentEvent 与 Quest 提醒不同时叠两层：Event 优先；Event 清后才出队列中的 Quest。  

## 4. 内部 Phase

| Phase | 交付 |
|---|---|
| CIF-0 | 本计划 |
| CIF-A | `HostContentInterruptPresenter`＋从 FormalHud 迁出事件模态 |
| CIF-B | QuestStarted／Completed 提醒队列 |
| CIF-C | `NotifyArrived`＋MoveController 抵达接线 |
| CIF-D | 制作指南／SCHEMA 注记＋样例场景挂载 |
| CIF-E | EditMode 验收＋状态页 |

## 5. 制作人配置拆分（写入 [94]）

| 想要的打断 | 配置位置 | 触发 |
|---|---|---|
| 探索弹出选项对话 | `content_events.json`：`trigger=onExplore`＋`locationId`＋`choices` | 探索／劳动区探索 |
| 走到某地弹出 | `trigger=onArrive`＋`locationId` | 表现抵达或 Travel |
| 任务完成后对话 | `trigger=onQuestCompleted`＋`questId` | 任务完成 |
| 日 beat 强制事件 | `chapters.json` → `dayBeats[].contentEventIds` | 跨日 |
| 接任务时提醒 | `quests.json` 的 `name`／`description`（Host 听 QuestStarted） | 接取／dayBeat／地点 offer |
| 完成任务提醒 | 同上（Host 听 QuestCompleted） | 完成条件满足 |

## 6. 硬停

Freeze／Snapshot 升版／对话树引擎／战斗打断。
