# 106 · 编辑器工具（Content Authoring）计划 v0.1

> 状态：**已确认待开工**｜日期：2026-08-10  
> 前置：Demo 0.1 可玩弧／内容包管线已可用；制作人手写 JSON 成本高  
> **目标：在仓库外 `ExternalTools/` 提供可视化编辑器，读写 `Content/BaseGame/Data`，不进 `Assets/`。**

---

## 0. 结论（已拍板）

| 项 | 决定 |
|----|------|
| 要不要做编辑器 | **要做**；正式关卡生产以编辑器为主，手写 JSON 仅作兜底 |
| 放哪 | `D:\UnityProjects\XianXia\ExternalTools\content-authoring\`（**不进 Assets**） |
| 形态 | **一个桌面应用 + 多工作区**（不是五个独立 exe） |
| 技术倾向 | **Electron 或 Tauri + Web UI**（地点画布友好）；备选 Avalonia |
| 数据真源 | 仍为现有 `Content/BaseGame/Data/*.json`；**不另发明运行时格式** |
| 第一期范围 | 模块 **A～D**（总览校验／地点／任务／事件） |

未开工实现前，本页即为交互与范围确认稿。

---

## 1. 游戏如何读取 JSON（编辑器必须对齐）

```text
Unity Play（DemoParityHost／PlayableHost）
  → PlayableHostBootstrap 解析包目录
       默认：仓库根/Content/BaseGame（Assets 上一级）
  → ContentPackageLoader.Load
       读 manifest.json → contentFolders: ["Data"]
       扫描 Data/**/*.json
  → 文件形态：{ "schemaVersion": 1, "definitions": [ { "id", "type", ... } ] }
  → 按 type 进入 DefinitionRegistry
  → PlayableDayBootstrap／OpeningScenario 接到 SimulationWorld
```

约束：

- 只加载 `Data/`；`Authoring/`、模板、ExternalTools **不进运行时**。  
- **严格字段**：未知字段 → 加载失败（`DefinitionSchema`）。  
- `id` 全局唯一（`namespace:local`，如 `base:quest_…`）。  
- 编辑器写出的文件必须能被现有 Loader **原样**加载。

字段权威：`Content/BaseGame/Data/SCHEMA.md`；制作流程：[94](94-chapter-full-production-and-sample-guide.md)。

---

## 2. 目录约定

```text
XianXia/
  Assets/                      # 仅 Unity 运行时；可保留「打开工具／Validate」菜单
  Content/BaseGame/            # 内容真源（编辑器读写）
    manifest.json
    Data/*.json
  ExternalTools/
    content-authoring/         # 新建：内容编辑器工程
      README.md
      apps/ 或 src/            # UI
      shared/                  # JSON IO、schema 字段表、交叉引用校验、包路径
```

---

## 3. 模块清单

### 第一期（P0）— 开工范围

| 模块 | 名称 | 职责 |
|------|------|------|
| **A** | 包总览／校验台 | 列出 definitions；按 type 过滤；一键校验；报错跳转编辑器 |
| **B** | 区域／地点编辑器 | 画布摆 `presentationX/Z`；邻接；产出／门槛／挂任务／机缘／驻地 |
| **C** | 任务编辑器 | 名称／描述／autoOffer／条件／奖励／失败 |
| **D** | 事件编辑器 | 触发、地点过滤、条件、选项与 outcomes |

### 第二期（P1）

| 模块 | 名称 | 职责 |
|------|------|------|
| **E** | 章节＋日 Beat | questChain／eventChain／dayBeats／Flag |
| **F** | 开局 Scenario | 区域／章节、spawns、jobId、关系 |
| **G** | WorkArea／Job | 工区偏移、职业活动绑定 |

### 第三期（P2）

| 模块 | 名称 | 职责 |
|------|------|------|
| **H** | 角色／功法／机缘点 | characters／cultivation／sites |
| （后） | 地砖／障碍／可行走网格 | 真正「画视觉地图」；与逻辑地点分离 |

**明确不做（本里程碑）：** 战斗关卡编辑、产品对话树 IDE、改 Core／Snapshot 协议、把玩法规则写进编辑器。

---

## 4. 交互逻辑（制作人路径）

```text
打开应用 → 绑定包根 Content/BaseGame
  → A 总览看条目／跑校验
  → B 摆逻辑地图并保存
  → C／D 填任务与事件并保存
  → 再校验
  → Unity 打开 DemoParityHost → Play 手操
```

### A 校验台

- 左：type 树；中：条目；右：摘要／「在编辑器打开」  
- 校验：字段白名单 + 交叉引用（地点／资源／quest／flag／character 等是否存在）  
- 可选二次门禁：提示制作人跑 Unity 菜单 `XianXia/Content/Validate BaseGame Package`

### B 地点

- 画布拖拽改坐标；两点连边改 `adjacentIds`  
- 右侧表单对齐 SCHEMA 地点字段  
- 保存写回对应 `worldRegion` JSON 文件

### C 任务／D 事件

- 条件与奖励 **kind 下拉**（与现有 ContentCondition／Outcome 一致）  
- 引用类字段可搜索包内已有 id  
- 保存写回 quests／events JSON（可按章节拆文件）

保存纪律：保留 `schemaVersion`；不写未知字段；校验 id 格式。

---

## 5. 技术做法（摘要）

| 项 | 做法 |
|----|------|
| UI | Web（React/Vue 任选）+ 桌面壳（Tauri 优先体积，或 Electron） |
| IO | Node／Rust 侧读写仓库相对路径下的 JSON |
| 校验 | `shared` 内维护与 SCHEMA 对齐的字段表与引用检查（**不**硬链 Unity asmdef） |
| 与 Unity | 数据契约共享；程序集隔离。Unity 内最多加「打开 ExternalTools」快捷方式 |

---

## 6. Phase（确认后执行）

| Phase | 交付 |
|-------|------|
| TOOL-0 | 本页＋Devlog＋飞书（本文档） |
| TOOL-A | ExternalTools 脚手架＋包路径＋读全集 definitions |
| TOOL-B | 模块 A 校验台 |
| TOOL-C | 模块 B 地点编辑器 |
| TOOL-D | 模块 C＋D 任务／事件 |
| TOOL-E | README／制作人用法＋与 [94] 交叉链接 |

硬停：若要改 Snapshot／Freeze／新 condition kind → 先人工确认。

---

## 7. 相关文档

- 内容制作指南：[94](94-chapter-full-production-and-sample-guide.md)  
- Demo 0.1 手操：[105](105-demo-0.1-producer-playbook-30min.md)  
- 近期里程碑收束：[107](107-recent-milestones-rollup-2026-08-10.md)  
- SCHEMA：`Content/BaseGame/Data/SCHEMA.md`
