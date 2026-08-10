# 106 · 编辑器工具

> 状态：**计划确认稿（待开工）**｜日期：2026-08-10  
> 一句话：**在 `ExternalTools/` 做一套「内容制作桌面应用」，可视化编辑关卡 Data JSON；游戏仍用现有 Loader 读这些文件。**  
> 相关：[94 制作指南](94-chapter-full-production-and-sample-guide.md)｜[107 收束](107-recent-milestones-rollup-2026-08-10.md)｜`Content/BaseGame/Data/SCHEMA.md`

---

## 1. 要做哪些编辑器（清单）

做成 **一个应用「XianXia Content Studio」**，左侧导航切换下面这些编辑器。  
**不是**五个独立 exe；**不放** `Assets/`。

### 第一期必须做（够你做地图逻辑＋任务＋事件）

| # | 编辑器名称 | 你用它做什么 | 读写的内容 type | 典型落盘文件 |
|---|------------|--------------|-----------------|--------------|
| **1** | **包总览与校验台** | 看全包有哪些条目；一键查错；点进对应编辑器 | 全部 | 只读＋校验，不单独 invent 格式 |
| **2** | **区域／地点编辑器** | 做「逻辑地图」：地点位置、邻接、产出、挂任务／机缘／NPC | `worldRegion`＋其 `locations[]` | 如 `ch01_reference_region.json`、`world_regions.json` |
| **3** | **任务编辑器** | 新建／改任务：描述、何时接取、完成条件、奖励 Flag | `quest` | 如 `ch01_reference_quests.json`、`quests.json` |
| **4** | **事件编辑器** | 新建／改事件：何时触发、选项、每个选项改什么 Flag／库存 | `contentEvent` | 如 `ch01_reference_events.json`、`content_events.json` |

### 第二期再做（关卡骨架与 NPC 班组）

| # | 编辑器名称 | 你用它做什么 | type |
|---|------------|--------------|------|
| **5** | **章节编排器** | 任务链顺序、事件清单、第几天触发什么 | `chapter` |
| **6** | **开局 Scenario 编辑器** | 开哪张图、开哪章、刷哪些人、绑什么 Job／日程 | `openingScenario` |
| **7** | **工区／职业编辑器** | WorkArea 绑地点＋偏移；Job 每天活动去哪 | `workArea`／`job` |

### 第三期（可选）

| # | 编辑器名称 | 说明 |
|---|------------|------|
| **8** | 角色／功法／机缘点编辑器 | `character`／`cultivation`／`opportunitySite` |
| **9** | 视觉地砖／障碍编辑器 | 与逻辑地点分离；另开里程碑 |

### 明确不做

战斗关卡编辑、产品对话树 IDE、在编辑器里改 Core 规则／Snapshot、把玩法写进 Unity 场景。

---

## 2. 整体怎么做（工程方案）

### 2.1 放哪

```text
D:\UnityProjects\XianXia\
  Content\BaseGame\Data\*.json     ← 真源（编辑器读写这里）
  ExternalTools\
    content-authoring\             ← 本工具工程（新建）
      package.json / 或 tauri.conf
      src\                         ← 前端页面（各编辑器）
      shared\                      ← 读盘、写盘、schema、校验
      README.md
  Assets\                          ← 不放这套 UI
```

### 2.2 技术选型（默认）

| 层 | 选什么 | 为什么 |
|----|--------|--------|
| 桌面壳 | **Tauri 2**（优先）或 Electron | 本机读写文件；Tauri 体积更小 |
| UI | **Web（React + TypeScript）** | 地点画布、表单、列表都好做 |
| 数据 | 直接读写现有 JSON | 与 `ContentPackageLoader` 同契约 |
| 校验 | TypeScript 实现字段白名单＋交叉引用 | **不**引用 Unity asmdef（外挂易碎） |

备选：若坚持纯 C# → Avalonia；地图画布会慢一截，默认不走这条。

### 2.3 应用骨架（所有编辑器共用）

```text
┌─────────────────────────────────────────────────────────┐
│  XianXia Content Studio     [打开包…] Content/BaseGame  │
├──────────┬──────────────────────────────────────────────┤
│ 总览校验  │                                              │
│ 区域地点  │           当前编辑器工作区                    │
│ 任务      │                                              │
│ 事件      │                                              │
│ (二期…)   │                                              │
├──────────┴──────────────────────────────────────────────┤
│ 未保存* ｜ [校验] [保存] [在资源管理器打开文件]            │
└─────────────────────────────────────────────────────────┘
```

公共能力（`shared/`）：

1. **PackageStore**：扫描 `Data/**/*.json`，解析全部 `definitions[]`，按 `id`／`type` 索引。  
2. **FileWriter**：按「该 definition 来自哪个文件」写回；保留 `schemaVersion`；禁止未知字段。  
3. **Validator**：对齐 SCHEMA 字段表 + 引用是否存在（地点／资源／角色／quest／flag…）。  
4. **Id 生成器**：`base:` + 类型前缀 + 制作人输入的 local 段。

### 2.4 和游戏怎么接

```text
编辑器保存 JSON
    ↓
你在 Unity 里 Play DemoParityHost
    ↓
PlayableHostBootstrap → Content/BaseGame
    ↓
ContentPackageLoader 扫 Data/**/*.json
    ↓
进 DefinitionRegistry → 开局 Scenario 应用进 SimulationWorld
```

编辑器 **不**往 Unity 进程塞数据；只改磁盘上的包。改完重新 Play（或以后再做热重载，非第一期）。

---

## 3. 每个编辑器怎么做（界面＋操作＋实现）

### 编辑器 1 — 包总览与校验台

**界面**

- 左：按 `type` 分组的树（quest／contentEvent／worldRegion…）  
- 中：当前 type 的表格（id、name、来源文件）  
- 右：只读摘要；按钮「在对应编辑器打开」  
- 顶：「运行校验」→ 下方错误列表（点错误可跳转）

**操作**

1. 启动后选／记住包路径 `Content/BaseGame`  
2. 点校验 → 看未知字段、重复 id、悬空引用  
3. 双击一条 quest → 跳到任务编辑器并选中该 id  

**实现要点**

- `PackageStore.loadAll()`  
- `validatePackage()` 输出 `{ level, message, definitionId, file }`  
- 路由：`/browser`、`/quest/:id` 等  

---

### 编辑器 2 — 区域／地点编辑器（逻辑地图）

**界面**

```text
┌────────────┬─────────────────────┬──────────────────┐
│ Region 列表 │     2D 画布          │ 选中地点属性      │
│ · ch01_ref  │  · 圆点=地点         │ id / name / kind │
│ · qingshi   │  · 拖拽改坐标        │ tags / activities│
│ [+新建区域] │  · 拖线=邻接         │ 产出资源／数量    │
│             │  · 滚轮缩放          │ enterConditions  │
│             │                     │ questOfferIds    │
│             │                     │ opportunitySite  │
│             │                     │ residentNpc     │
└────────────┴─────────────────────┴──────────────────┘
```

**操作**

1. 选一个 `worldRegion`  
2. 画布上拖地点 → 改 `presentationX`／`presentationZ`  
3. 从地点 A 拖到 B → 写入双方或单方 `adjacentIds`（UI 提供「双向／单向」）  
4. 右侧改表单 → 点保存 → 写回该 region 所在 JSON  

**实现要点**

- 画布：简单 SVG／Canvas；坐标与游戏一致（X／Z 语义）  
- 数据模型：`WorldRegionDefinition` + `locations[]`  
- 条件编辑器复用公共组件（kind 下拉 + id／amount）  
- **不做**第一期地砖美术铺装  

---

### 编辑器 3 — 任务编辑器

**界面**

```text
左：任务列表 [+新建] 过滤搜索
右：表单
  id*  name  description（多行）
  autoOffer ☑
  offerConditions[]     ← 条件行编辑器
  completeConditions[]  ← 条件行编辑器
  rewards[]             ← 结果行编辑器
  failConditions[] / failResults[]
```

**条件行编辑器（公共组件）**

每行：`kind` 下拉 + 按 kind 显示字段：

| kind（与游戏一致） | 额外字段 |
|--------------------|----------|
| `storyFlag`／`hasFlag`／`missingFlag` | id |
| `exploredLocation`／`atLocation` | id（地点下拉） |
| `stockAtLeast` | id（资源）+ amount |
| `realmAtLeast` | realm |
| `hasManual`／`knowsSite`／`questActive`／`questCompleted` | id |

**结果行（rewards／outcomes）**

| kind | 字段 |
|------|------|
| `setFlag`／`clearFlag` | id |
| `addStock` | id + amount |
| `grantProgress` | amount |
| `discoverSite` | id |
| `relationDelta` | from／to definitionId + amount |
| `startQuest` | id |

**操作**

1. 新建 → 生成 id → 填描述与条件  
2. 保存 → 写入选定的 quests 文件（默认跟同章节文件，或「另存到…」选文件）  

**实现要点**

- 表单受控组件；保存前跑单条 schema 校验  
- 引用字段用 combobox 搜 `PackageStore`  

---

### 编辑器 4 — 事件编辑器

**界面**

```text
左：事件列表 [+新建]
右：
  id / name / body（多行剧情）
  trigger: manual | onArrive | onExplore | onQuestCompleted | …
  locationId（可选，地点下拉）
  questId（可选）
  once ☑
  conditions[]
  choices[]:
    ┌ choice id / text
    │ outcomes[]   ← 同结果行编辑器
    └ [+选项]
```

**操作**

1. 写 body 与选项文案  
2. 选 trigger＋地点（如 onArrive + 灵泉）  
3. 每个选项挂 setFlag／discoverSite 等  
4. 保存写回 events JSON  

**实现要点**

- 与任务编辑器共用 ConditionRow／OutcomeRow  
- trigger 枚举与 Loader／SCHEMA 一致  

---

### 编辑器 5～7（第二期，此处只定界面意图）

| 编辑器 | 怎么做（摘要） |
|--------|----------------|
| **5 章节** | 左章节列表；右：拖拽排序 `questChainIds`／`eventChainIds`；`dayBeats` 表格（dayIndex＋条件＋挂任务／事件／setFlags） |
| **6 Scenario** | 选 openingWorldRegionId／openingChapterId；spawns 表格（definitionId、entityKind、jobId、scheduleId、aiRole…）；openingRelations 表格 |
| **7 WorkArea／Job** | WorkArea：选 locationId + offset 预览点；Job：activityBindings 表（activity、workAreaIds、mode single／route） |

---

## 4. 制作人完整使用流程

```text
1. 打开 Content Studio，绑定仓库里的 Content/BaseGame
2. 用【区域／地点】摆好逻辑地图并保存
3. 用【任务】【事件】填剧情链并保存
4. 回【总览】点校验，清零错误
5. Unity 打开 DemoParityHost → Play 手操
6.（二期）用章节／Scenario 串开局与日 beat
```

手写 JSON 仍可用，但是兜底；**正式生产以本工具为准**。

---

## 5. 实施 Phase（开工顺序）

| Phase | 做什么 | 完成标准 |
|-------|--------|----------|
| **TOOL-0** | 本文档写清＋飞书同步 | 制作人能按本文说出「四个编辑器各自干什么」 |
| **TOOL-1** | 脚手架：Tauri/Electron + React；打开包；读全 definitions | 能列出 BaseGame 全部 id |
| **TOOL-2** | 编辑器1：总览＋校验 | 对现有 BaseGame 跑校验有结果 |
| **TOOL-3** | 编辑器2：地点画布＋保存 | 改坐标／邻接后 Unity Play 可见 |
| **TOOL-4** | 编辑器3＋4：任务／事件表单＋保存 | 新建一条任务／事件能被 Loader 加载 |
| **TOOL-5** | README＋与 [94] 互链；可选 Unity 菜单「打开 Content Studio」 | 制作人可独立打开使用 |
| **TOOL-6+** | 编辑器5～7 | 另开小里程碑 |

硬停：改 Snapshot／Freeze／新增 condition kind → 先人工确认。

---

## 6. 验收（第一期）

制作人无需手写 JSON，能完成：

1. 在地点编辑器加一个地点并连边  
2. 新建一个任务＋一个到达触发事件  
3. 校验通过  
4. Unity Play 能接到该任务／触发该事件  

---

## 7. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-10 | 初版确认稿 |
| 2026-08-10 | **重写**：补「要做哪些编辑器」点名表＋每个编辑器的界面／操作／实现／Phase |
