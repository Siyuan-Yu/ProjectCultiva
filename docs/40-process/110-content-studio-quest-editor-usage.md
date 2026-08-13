# 110 · 任务编辑器用法（QuestEditor）

> 状态：**可用（WPF v2 · 可视化条件）**｜日期：2026-08-14  
> 工程：`ExternalTools/ContentAuthoring/QuestEditor/`  
> 编辑：`type = quest`  
> 计划：[106](106-content-authoring-editors-plan-v0.1.md)

---

## 干什么

新建／改任务：**基本信息、发放方式、接取／完成／失败条件、奖励、是否可放弃**。  
条件与奖励用 **可视化列表**（`+ 添加`）编辑，不必手写 JSON。  
NPC 对话发任务：在本编辑器创建关联事件，**台词在 EventEditor 改**；局内走事件弹层。  
游戏内：**J**／右上角「任务」打开日志（可接｜进行中｜已完成）；目标达成后 **待领奖＋红点**，点「领取奖励」才发奖。

---

## 怎么打开

- 推荐：`ExternalTools/ContentAuthoring/启动-QuestEditor.cmd` 或 `Apps/QuestEditor/QuestEditor.exe`  
  首次打开若缺 exe，脚本会自动跑 `publish.ps1`（约 1 分钟）；也可先双击 `发布-所有编辑器.cmd`
- 调试：VS 打开 `ContentAuthoring.sln` → 启动项目 `QuestEditor` → F5
- **不要**打开各工程 `bin\` 或 `.build\` 里的 exe（易过期）

顶部路径栏显示当前打开的 **Content/BaseGame** 包目录；任务 JSON 在 `Content/BaseGame/Data/Quests/`（其它 type 见 `Data/README.md`）。

---

## 工具栏

| 按钮 | 作用 |
|------|------|
| **打开包…** | 选择 `Content/BaseGame` 目录，加载 `Data/*.json` 里所有 `type=quest` |
| **新建…** | 输入任务 Id → 选保存文件 → 输入显示名称；写入新 JSON（或追加到已有文件） |
| **保存** | 把当前编辑写回**原文件**（覆盖该条 definition） |
| **另存为…** | 复制当前任务到新 JSON 文件，可改 Id（原文件保留） |
| **删除…** | 从包中删除当前任务（确认后不可撤销；会清地点挂接，不删关联事件） |

左侧 **任务列表** 点选条目；右侧按 ①～⑥ 区块编辑。

---

## 界面区块详解

### ① 基本信息

| 字段 | 含义 | 注意 |
|------|------|------|
| **id** | 全局唯一任务 Id，如 `base:quest_ch01_ref_inspect_yard` | 改 id 后需检查 Region `questOfferIds`、关联 Event 是否仍指向旧 id |
| **name** | 任务标题（HUD／日志显示） | |
| **description** | 任务说明（多行） | 给玩家看的引导文案写这里 |

---

### ② 发放方式

决定 **任务怎么交到玩家手里**（不是完成条件）。选方式后，下方灰色小字会显示当前关联摘要，例如：

- `地点 questOfferIds: base:loc_ref_labor_yard` — 某地点挂了此任务
- `autoOffer = true` — 满足接取条件时自动接
- `保存文件: ch01_reference_quests.json` — 本条存在哪个 JSON

| 方式 | 玩家侧体验 | 编辑器写入什么 |
|------|-----------|----------------|
| **自动接取（条件满足）** | ③ 里条件全满足 → 任务自动进列表 | `autoOffer=true` + `offerConditions[]` |
| **前置任务完成后自动接** | 指定上一环完成后自动接上 | `autoOffer=true`，接取条件固定一条「任务已完成」 |
| **到指定地点可领** | 探索／到达某地点时 offer | `autoOffer=false`；Region 地点 `questOfferIds` 写入本任务 id |
| **NPC 对话发放（关联事件）** | 到地点弹对话，选「接下任务」才接 | 创建/更新 `contentEvent`（outcome `startQuest`）；台词去 **EventEditor** |
| **自定义（保留当前 JSON 逻辑）** | 按手写复杂规则 | 不自动改结构，只编辑 ③ 列表 |

**NPC 对话：** 选「对话地点」→ 点 **创建/更新关联事件** → 到 **EventEditor** 改 `body`／选项文案。

---

### ③ 接取条件（autoOffer 时生效）

勾选 **「条件满足时自动接取 (autoOffer)」** 且发放方式为「自动接取」或「自定义」时，下面列表即 `offerConditions`：**全部满足**才自动接任务。

点 **+ 添加** 增加一行；左侧下拉选条件类型，右侧填参数；**删除** 去掉该行。

| 条件类型 | 含义 | 典型用途 |
|----------|------|----------|
| **已有 Flag** | 某剧情标记已设置 | 某事件发生后才开放任务 |
| **缺少 Flag** | 某标记尚未出现 | 首次／未做过某事 |
| **已探索地点** | 玩家曾进入过某 location | 到过某区域才开放 |
| **当前在地点** | 玩家此刻站在某 location | 人在现场才接 |
| **库存 ≥** | 某资源数量够 | 持有道具／货币门槛 |
| **境界 ≥** | 修炼境界达到（凡人／炼气） | 修为门槛 |
| **任务已完成** | 指定任务已完成 | 任务链后续环（也可直接用「前置任务完成后」方式） |
| **任务进行中** | 指定任务已在进行 | 并行／子步骤 |
| **已知机缘点** | 已发现某 opportunitySite | 探索进度 |
| **已学功法** | 已拥有某 cultivation | 功法门槛 |
| **角色在地点劳动秒数 ≥** | `laborAtLocation`：累计劳动 | 单人劳动时长门槛 |
| **指定角色在地点** | `characterAtLocation` | 集合／护送 |
| **不同角色劳动人数 ≥** | `uniqueLaborAtLocation` | 多人到场劳动 |
| **不同角色采集人数 ≥** | `uniqueHarvestAtLocation` | 多人各采到 ≥1 |

**库存 ≥** 读的是 **小队共用背包**，不是聚落仓库。

选「到指定地点可领」或「NPC 对话」时，③ 列表通常为空（由地点 offer 或事件控制）。

---

### ④ 完成条件

`completeConditions`：**全部满足**则进入 **待领奖（ReadyToClaim）**；玩家在任务日志点「领取奖励」后才发奖并标完成。类型与 ③ 相同。

**示例（ch01 当前三环）：**

1. **不同角色采集人数 ≥** → 农田地点，`amount: 3`  
2. **库存 ≥** → 粗粮／灵药／粗木各 3（背包）  
3. **指定角色在地点** ×3 → 集合点 `base:loc_ref_party_rally`

---

### ⑤ 奖励

`rewards`：任务完成时执行的 **结果**（不限于发物品，也包括写 Flag）。

| 结果类型 | 含义 |
|----------|------|
| **设置 Flag** | 写入剧情／进度标记（如 `quest:ch01_ref_yard_done`） |
| **清除 Flag** | 去掉某标记 |
| **增加库存** | 给资源＋数量 |
| **开始任务** | 自动开启另一任务 id |
| **发现机缘点** | 解锁 opportunitySite |
| **修炼进度** | 增加 cultivation 进度值 |
| **关系变化** | 两角色之间好感 ±N |

**示例（同一 ch01 任务）：**

1. 设置 Flag → `quest:ch01_ref_yard_done`（本环完成标记）  
2. 设置 Flag → `story:ch01_phase_labor_seen`（章节阶段标记，供后续任务／事件引用）

---

### ⑥ 失败（可选）

| 区块 | 字段 | 说明 |
|------|------|------|
| **失败条件** | `failConditions` | 满足则任务失败（条件类型同 ③） |
| **失败结果** | `failResults` | 失败时执行的结果（类型同 ⑤） |

Demo 第一章多数任务留空即可；限时／互斥任务再用。

---

## 日常操作（推荐顺序）

1. **打开包…**（默认 `Content/BaseGame`）或 **新建…** 建任务  
2. 左侧选任务；填 ① 基本信息  
3. 选 ② 发放方式；按需配 ③ 接取条件  
4. ④ 完成条件、⑤ 奖励用 **+ 添加** 配齐  
5. **保存**（改原文件）或 **另存为…**（复制到新 JSON）  
6. **PackageBrowser** 跑校验 → Unity **Level Tester** Play 验证  

---

## 与 EventEditor 分工

| 编辑器 | 负责 |
|--------|------|
| QuestEditor | 任务本体、发放方式、接取／完成／失败、奖励 |
| EventEditor | 弹窗正文 `body`、`trigger`、选项与 `outcomes` |

---

## 注意

- 不要用 `autoAccept`／`objectives` 等非 SCHEMA 字段  
- 改 **id** 后检查 Region `questOfferIds`、关联 Event 的 `startQuest`  
- 好感解锁任务：暂用 Event 置 flag + Quest 条件「已有 Flag」（`relationAtLeast` 引擎未做）  
- 保存只改磁盘 JSON；Unity 需重新 Play（或 F12 重载）才生效  

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-14 | 补劳动／采集／角色在地点条件；库存读背包；待领奖说明；ch01 三环示例 |
| 2026-08-13 | v3.1：Data 按 type 分子目录（Quests/Maps/…）；新建/另存默认进对应子目录 |
| 2026-08-13 | v2：可视化条件/奖励、发放方式、NPC 事件向导 |
| 2026-08-10 | 初版 |
