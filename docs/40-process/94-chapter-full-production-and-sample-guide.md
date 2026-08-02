# 章节关卡完整制作指南 + 样例关／第一章对照

> 状态：**现行制作真源**｜日期：2026-08-02  
> 场景入口：`Assets/Scenes/DemoParityHost.unity`（Scenario：`base:scenario_ch01_reference`）  
> 字段权威：`Content/BaseGame/Data/SCHEMA.md`｜命名：[84](84-chapter-content-naming-standards.md)  
> 流程草案：[2G](../20-systems/2G-first-chapter-flow.md)｜时间：[21](../20-systems/21-core-loop-and-time.md)  
> 旧文合并：本页覆盖 [80](80-chapter-content-production-guide.md)／[88](88-chapter-01-reference-level-production-guide.md) 的实操部分

---

## 0. 总原则

1. **内容在 Data，规则在 Core，表现在 Host。** 新关卡优先只改 `Content/BaseGame/Data/*.json`。  
2. **样例关 ≠ 最终剧情。** 用参考文案／Flag 串流程；正式第一章可换 ID／文案复用结构。  
3. **先骨架后正文。** 先挂 ID／条件／奖励／Flag，再填长文案。  
4. **校验：** 菜单 `XianXia/Content/Validate BaseGame Package`；EditMode 全绿。  
5. **硬停（需人工确认）：** 改 Freeze、升 Snapshot、战斗、产品级 UGUI／对话框大改、新 Core 条件／指令种类。

---

## 1. 工程入口（你要打开什么）

| 用途 | 路径／操作 |
|---|---|
| 样例关手操 | Unity 打开 `DemoParityHost` → Play |
| 框架调试 | `PlayableHost`（可挂 F1–F4 调试面板） |
| Demo 视觉参考（只读） | `Demo_v0_1` |
| 重建样例场景 | `XianXia/Demo Parity/Create Or Update Sample Level Scene` |
| 内容目录 | `Content/BaseGame/Data/`（`ch01_reference_*.json` 等） |

---

## 2. 一份关卡由哪些文件组成

按依赖顺序配置：

```text
1. characters*.json          人物／NPC 定义
2. cultivation.json / sites  功法／机缘点
3. resources / settlements   资源／据点库存（按需）
4. worldRegion（区域 JSON）  地点图、邻接、坐标、探索产出、任务挂点
5. quests*.json              任务
6. content_events*.json      事件（选项／Flag）
7. chapters*.json            章节：任务链／日 beat／事件清单
8. scenarios.json            开局：区域＋章节＋spawns＋关系
9. Host 场景                 只选 Scenario；不写玩法规则
```

Loader 会扫描 `Data/` 下所有 JSON；**拆文件不影响加载**（靠 `id` 全局唯一）。

---

## 3. 如何配置地图（WorldRegion）

**文件示例：** `ch01_reference_region.json`  
**Scenario 字段：** `openingWorldRegionId`／地点 `startLocationId`

每个地点必填：

| 字段 | 含义 |
|---|---|
| `id`／`name`／`kind` | 稳定 ID、显示名、类型 |
| `adjacentIds` | 邻接＝可 Travel 的边 |
| `presentationX`／`presentationZ` | Host 2D 坐标（XY 平面用 X/Z 语义） |

常用选填：

| 字段 | 含义 |
|---|---|
| `resourceOnExploreId`／`Amount` | 探索／劳动产资源 |
| `opportunitySiteId` | 挂机缘（发现后可修炼／学功法） |
| `residentNpcDefinitionId` | 驻地 NPC（开局落点提示） |
| `enterConditions` | 进入门槛（条件数组） |
| `questOfferIds` | 抵达／开局可挂任务 |

Host：`HostDemoTileMap` 按 Demo 规则铺砖；`HostMapGraybox` 画区域色块。  
**不要**为了关卡去改 Demo Runtime。

---

## 4. 如何配置角色／NPC

### 4.1 定义（`character`）

`characters.json` 或 `ch01_reference_characters.json`：

- `id`／`name`／`baseAttributes`
- `personalityTags`／`backgroundTags`／`talentTags`

### 4.2 开局生成（`scenarios.json` → `spawns[]`）

| 字段 | 建议 |
|---|---|
| `entityKind` | 主角 `character`；路人 `npc` |
| `factionRole` | 劳役 `LaborDisciple`；主管 `Supervisor` |
| `bindSchedule`／`scheduleId` | `schedule_mortal_day`／`cultivator_day`／`supervisor_day` |
| `aiRole` | `Mortal`／`Cultivator`／`Supervisor`（影响日程意图映射） |
| `bindDailyTask`／`workRole` | 主角日课；NPC 可关 |
| `recruitable` | 可招募路人 |

**第一章前期建议：** 三名主角都用 `Mortal`＋`schedule_mortal_day`（被压迫劳役感）。  
外门修士／主管／守卫用另外两套日程做世界氛围。

### 4.3 日程实际语义（当前 Core 内建，非 JSON）

逻辑：`1 Tick = 15 游戏分钟`，`1 日 = 96 Tick`，`1 时 = 4 Tick`。  
表现：Host 默认约 `secondsPerAutoTickAt1x ≈ 3s` → **约 5 分钟现实时间 = 1 游戏日**（1x）；可用暂停／2x／5x。

凡人日（对齐 [21] 骨架）：深夜休息 → 清晨／白天劳役 → 正午吃饭 → 下午劳役 → **入夜 Explore（自由缝）** → 再休息。  
主管：巡查／检查。修士：探索／修炼。

NPC **不会**像 Demo 那样满地图走路寻路；日程驱动的是 **Core Order／头顶活动字**。玩家角色靠右键移动＋指令。

---

## 5. 如何配置任务（Quest）

**文件：** `quests.json`／`ch01_reference_quests.json`

| 步骤 | 做什么 |
|---|---|
| 1 | `id`／`name`／`description` |
| 2 | `offerConditions`：何时可接 |
| 3 | `completeConditions`：何时完成 |
| 4 | `rewards`／`failResults`：Flag／库存／进度／开下一任务等 |
| 5 | 挂到：章节 `questChainIds`、或 `dayBeats.questOfferIds`、或地点 `questOfferIds`、或 `autoOffer` |

**可用 condition.kind：**  
`atLocation`｜`hasFlag`｜`missingFlag`｜`realmAtLeast`｜`knowsSite`｜`stockAtLeast`｜`questActive`｜`questCompleted`｜`exploredLocation`｜`hasManual`

**可用 outcome.kind：**  
`setFlag`｜`clearFlag`｜`addStock`｜`startQuest`｜`relationDelta`｜`grantProgress`｜`discoverSite`

任务链：`questChainIds` 有序；**上一环 Completed 后自动 TryStart 下一环**。

---

## 6. 如何配置事件（ContentEvent）

**文件：** `content_events.json`／`ch01_reference_events.json`

| 字段 | 说明 |
|---|---|
| `trigger` | `onExplore`／`onArrive`／`onQuestCompleted`／`manual` |
| `locationId`／`questId` | 过滤上下文 |
| `conditions` | 含 `storyFlag` 做分支 |
| `choices[]` | `id`／`text`／`outcomes` |
| `once` | 默认 true |

**正式打断（CIF）：** `HostContentInterruptPresenter` 在事件激活时中央弹层＋强制暂停；选项走 `ResolveContentChoice`。  
计划：[95](95-content-interrupt-system-plan-v0.1.md)。调试可用 F3 **Force Present**（`PlayableHost`）。

### 6.1 内容打断配置拆分（制作人）

| 目标体验 | 配什么 | 何时弹 |
|---|---|---|
| 探索弹出选项对话 | `contentEvent`：`onExplore`＋地点＋`choices` | 探索该地 |
| 走到某地弹出 | `contentEvent`：`onArrive`＋地点 | Travel 或表现抵达区中心 |
| 任务完成后对话 | `contentEvent`：`onQuestCompleted`＋`questId` | 任务完成 |
| 某日强制剧情 | `chapter.dayBeats[].contentEventIds` | 跨日 beat |
| 接任务提醒 | `quest.name`／`description`（无需新类型） | `QuestStarted` →「知道了」 |
| 完成任务提醒 | 同上 | `QuestCompleted` →「知道了」 |

优先级：**事件选项 ＞ 任务提醒 ＞ RTS**。打断期间不可移动／派工。

---

## 7. 如何配置章节（Chapter）

**文件：** `ch01_reference_chapter.json`  
**Scenario：** `openingChapterId`

| 字段 | 作用 |
|---|---|
| `plannedDays` | 制作参考天数 |
| `questChainIds` | 主线任务链 |
| `eventChainIds` | 计划内事件清单（触发仍靠 explore／beat／条件） |
| `dayBeats[]` | `dayIndex`＋`setFlags`／`questOfferIds`／`contentEventIds`／`conditions` |

日 beat 在跨日时评估；用 Flag 把「第几天发生什么」钉死。

---

## 8. 样例关操作（ACS 风格 Host 底栏 · IMGUI）

| 输入 | 作用 |
|---|---|
| 左键／框选 | 只选**我方三人** |
| 右键地面／区域 | **只移动**到鼠标点（打断当前活；不自动劳动／入定） |
| 上方「交互」后左键工区 | 走到区中心 → **抵达后才劳动／采药** |
| 上方「修炼」后左键灵地 | 走到区中心 → **抵达后才入定** |
| 已在工区时点「劳动」／F4 | 当场劳动（无需再走） |
| 右键灵泉／洞府 | 走到区中心 → **抵达后入定** |
| 右键空地 | 只移动 |
| 上方移动／交互／战斗／修炼 | 进入点选（绿可点／红不可）；战斗仅占位 |
| 底栏宣纸面板 | **点选任意角色打开查看**；上方指令钮**仅己方**；己方**不跟课表自动行动** |
| Q／F1／E／F8／F6 | 移动／停止／交互／战斗占位／修炼（G＝敛息草；F5 存档；F12 重建表现） |
| 顶栏时间 | `第N天 HH:mm`（每 Tick＝15 游戏分） |
| 可交互点 | 农田／树林／药田／矿／灵泉／洞府各多点（黄／青球标记） |
| 左附属格 | 停止／敛息／劳动（同上，仅焦点角色） |
| 右页签 况／系 | 该角色概况／关系（课表／任务／事件在**右侧全局栏**） |
| 顶栏 | 暂停／倍速／库存／主管愤怒（全局） |
| 框选多人 | 指令仍只控焦点那一人；群体位移用右键 |
| C／X／G／S | 入定／出定／敛息／停止 |
| F10 | 显隐 FormalHud |
| F5／F9 | 存／读（按钮默认隐藏） |

**刻意未做（需新系统）：** 食物／饮水／娱乐真需求、炼丹炼宝画符、突破专用 UI、装备栏。条目标签只绑现有 Core 字段。

---

## 9. 第一章流程 ↔ 样例关一一对应

对照 [2G](../20-systems/2G-first-chapter-flow.md)，样例关用 **可运行 Data** 近似完整第一章体验（文案仍为参考腔）。

| # | 2G 阶段 | 样例关交付（Data／手操） | 状态边界 |
|---|---|---|---|
| 1 | 白天劳役／生存压力 | 开局压迫事件＋巡视／伐木／采药日课；主管日程＋日 beat 施压 | ✅ Data＋Host |
| 2 | 晚上自由缝 | 凡人日程入夜 `Explore`；玩家可夜探 | ✅ 日程调参 |
| 3 | 神识看见异常 | `quest_spirit_sense`＋灵泉 `onExplore` 事件 | ✅ 打断弹层 |
| 4 | NPC 机缘 | 砍柴老人（树林 `onArrive`）／行商密语（枢纽替代） | ✅ 双路线 |
| 5 | 第一份功法 | 洞府机缘→青云诀（Cultivate Gate） | ✅ |
| 6 | 找修炼环境 | `quest_visit_cave` | ✅ |
| 7 | 暗修→第一次炼气 | 夜缝入定→引气入体事件→`realmAtLeast QiRefining` | ✅ 无独立突破 UI |
| 8 | 隐藏修士 | 房屋区敛息约定＋敛息草；权力伏笔收束 | ✅ |
| 9 | 对话／任务打断 | `HostContentInterruptPresenter`（事件选项＋任务提醒，强制暂停） | ✅ A |
| 10 | 战斗／夺据点 | 明确未做 | ❌ 另开切片 |

推荐试玩顺序（手操 · `DemoParityHost`）：

1. 开局弹层「杂役晨点」（含操作说明）→ 任务「知道了」→ 走开再走回农田（首次入区自动勘察）  
2. 点「交互」再点「树林」交差 → 药田同理  
3. 走进「灵泉」听异响 → 再进树林遇砍柴老人（替代：枢纽再按一次探索找行商）  
4. 进洞府后点「入定」学青云诀 → 再进洞府选「引气入体」→ 多次入定至炼气  
5. 进房屋区立约隐藏 → 回枢纽听制度伏笔  

常驻提示：顶栏下方操作条；调试面板 **F11**（F1＝停止）。  

---

## 10. 推荐验收清单

- [ ] Content Validate 通过  
- [ ] `DemoParityHost` Play：三主角可选、NPC 可见、地图砖不漏天  
- [ ] 任务链可推进到炼气→隐藏→`story:ch01_ref_arc_complete`（EditMode：`ReferenceLevel_FullAwakeningArc_ToEpilogue`）  
- [ ] 凡人／修士／主管头顶活动随时间变化  
- [ ] 1x 下一天体感约数分钟级（非数十秒刷完）  
- [ ] 未改 Freeze；未升 Snapshot  

---

## 11. 明确不做／需你拍板的新功能

| 需求 | 判断 | 建议 |
|---|---|---|
| 了不起的修仙模拟器式 HUD（宣纸底栏／F 键／页签） | **B 已做**（IMGUI 近似；真需求条未做） | 产品 UGUI／贴图另开 |
| 模态对话／任务弹窗打断并暂停 | **A 已做**（FormalHud 事件弹层＋暂停） | 多段对话树另开 |
| ACS 式角色操作 | **B 已映射**到既有指令 | 炼丹／装备等无系统项灰区未做 |
| NPC 真走路寻路 | **新表现／规则** | 仍用头顶日程；确认后再做 |
| 战斗／夺主管据点 | **新玩法切片** | 第一章后段再开 |

旧短文 [80]／[88] 仍可作历史参考；**新关卡请以本页为准。**
