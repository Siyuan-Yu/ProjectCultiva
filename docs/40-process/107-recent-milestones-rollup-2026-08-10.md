# 107 · 近期里程碑收束（导航／NPC／Demo0.1／热修）— 2026-08-10

> 状态：**文档补录**｜日期：2026-08-10  
> 用途：把此前已合入代码、但总览／现状页尚未收齐的增量一次写清，便于制作人与飞书阅读。  
> 详细计划／验收仍以各专题页为准。

---

## 1. 一句话

自 Demo Parity／可玩弧交付之后，又完成了三块底座与一版可玩弧生产补强，并确认下一步做 **ExternalTools 内容编辑器**（见 [106](106-content-authoring-editors-plan-v0.1.md)）。

---

## 2. 里程碑对照表

| 里程碑 | 计划 | 验收／交付 | 状态 |
|--------|------|------------|------|
| Navigation Foundation | [99](99-navigation-foundation-milestone-plan-v0.1.md) | [100](100-navigation-foundation-acceptance-report.md) | 自动化已验收；手操待签 |
| NPC Simulation Foundation | [101](101-npc-simulation-foundation-milestone-plan-v0.1.md) | [102](102-npc-simulation-foundation-acceptance-report.md) | 自动化已验收；手操待签 |
| Demo 0.1 Production（Ch01 可玩弧） | [103](103-demo-0.1-production-milestone-plan-v0.1.md) | [104](104-demo-0.1-production-acceptance-report.md)／手操 [105](105-demo-0.1-producer-playbook-30min.md) | 自动化已验收；手操待签 |
| 内容编辑器工具 | [106](106-content-authoring-editors-plan-v0.1.md) | [108](108-content-studio-browser-usage.md)～[112](112-map-editor-usage.md) | **第一期可用**；2026-08-11 已接 Host prefab 生成 |

另有过程交付：[98 RTS 手动控制＋HUD](98-rts-manual-control-and-hud-pass-2026-08-03.md)、[97 可玩弧＋打断＋UX](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)。

---

## 3. 各块做了什么（摘要）

### 3.1 Navigation Foundation

- Core：`WalkGrid`、A*、`Ch01ReferenceWalkGrid`  
- Host：移动沿航点；NPC 日程走位；软分离  
- **不做：** 战斗寻路、NavMesh、大地图  

### 3.2 NPC Simulation Foundation

- Location 扩展：`tags`／`allowedActivities`  
- Data：`work_areas.json`／`jobs.json`；样例农夫／药农／矿工／巡卫／管事  
- Core：`Schedule → ActivityResolver → MoveAction → WorkAction`（有 Job 的 NPC）  
- Host：读 `MovementIntent` 寻路，去掉硬编码地点 id  
- **不做：** 战斗 AI、复杂决策 AI、大世界 AI  

### 3.3 Demo 0.1 Production（第一章可玩弧）

体验链：

```text
Day0 → 杂役 → 三人分派（新） → 探索机缘 → 秘密修炼 → 第一次突破 → 隐藏／权力伏笔
```

内容补强要点：

- 新任务 `quest_ch01_ref_dispatch_party`（粮＋木＋药）  
- **砍柴老人**与 **矿工老倔**拆角（避免 Job 样例撞坏主线机缘）  
- 矿洞氛围事件；开局／任务文案对齐约 30 分钟手操  
- 入口：`DemoParityHost` → `base:scenario_ch01_reference`  

### 3.4 热修（此前未单开文档）

| Commit | 说明 |
|--------|------|
| `65f39a5` | `WorkAction.cs` 补回 `using XianXia.Core.Orders`，修复 `OrderId` 编译错误 |

---

## 4. 内容生产判定（2026-08-10）

- **可以开始正式做关卡内容**（任务／事件／剧情 Flag／逻辑地点）：管线与样例关已齐。  
- **手写 JSON 可用但不友好**；已确认用 **ExternalTools 编辑器** 作为主生产路径（[106](106-content-authoring-editors-plan-v0.1.md)）。  
- 仍建议制作人按 [105](105-demo-0.1-producer-playbook-30min.md) 手操签收一遍 Demo 0.1。  

**硬停未变：** 战斗／夺权、产品 UGUI、Snapshot 升版、改 Freeze——需人工确认后再开。

---

## 5. 下一步

1. 开工 [106] 第一期模块 A～D（校验台＋地点＋任务＋事件）。  
2. 制作人手操签收导航／NPC／Demo0.1。  
3. 正式第一章长文案可在编辑器就绪后加速换皮（结构复用 `ch01_reference_*`）。
