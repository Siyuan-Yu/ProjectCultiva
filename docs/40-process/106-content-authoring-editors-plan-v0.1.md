# 106 · 编辑器工具

> 状态：**第一期已交付（WPF 五独立 exe，Windows）**｜日期：2026-08-11  
> 一句话：**五个独立 Visual Studio／WPF 程序，分别编辑关卡 Data JSON；游戏仍用现有 Loader 读这些文件。**  
> 工程：`ExternalTools/ContentAuthoring/`（`ContentAuthoring.sln`）  
> 用法：[108 总览](108-content-studio-browser-usage.md)｜[109 逻辑地点](109-content-studio-region-editor-usage.md)｜[112 格点地图](112-map-editor-usage.md)｜[110 任务](110-content-studio-quest-editor-usage.md)｜[111 事件](111-content-studio-event-editor-usage.md)  
> 相关：[94 制作指南](94-chapter-full-production-and-sample-guide.md)｜[107 收束](107-recent-milestones-rollup-2026-08-10.md)｜`Content/BaseGame/Data/SCHEMA.md`

---

## 1. 要做哪些编辑器（清单）

**五个独立程序、五个文件夹、五个 exe**（不是一个应用里多个分支）。  
**不放** `Assets/`。当前只做 **Windows**。

### 第一期（已交付）

| # | 工程文件夹 | exe | 你用它做什么 | type | 用法文档 |
|---|------------|-----|--------------|------|----------|
| **1** | `PackageBrowser/` | `PackageBrowser.exe` | 看全包条目；一键校验 | 全部（只读＋校验） | [108](108-content-studio-browser-usage.md) |
| **2** | `RegionEditor/` | `RegionEditor.exe` | 逻辑地点／邻接／产出／NPC／任务挂接 | `worldRegion` | [109](109-content-studio-region-editor-usage.md) |
| **2b** | `MapEditor/` | `MapEditor.exe` | **格点地图**：设施板＋画布拖拽缩放；挡路写入寻路 | `mapLayout` | [112](112-map-editor-usage.md) |
| **3** | `QuestEditor/` | `QuestEditor.exe` | 新建／改任务 | `quest` | [110](110-content-studio-quest-editor-usage.md) |
| **4** | `EventEditor/` | `EventEditor.exe` | 新建／改事件 | `contentEvent` | [111](111-content-studio-event-editor-usage.md) |

共享库：`Shared/`（读盘、写盘、SCHEMA 字段白名单、校验）。

### 第二期再做

| # | 编辑器 | type |
|---|--------|------|
| **5** | 章节编排器 | `chapter` |
| **6** | 开局 Scenario 编辑器 | `openingScenario` |
| **7** | 工区／职业编辑器 | `workArea`／`job` |
| **8** | WorldGraph 编辑器（点＋路） | `worldGraph`／`worldNode`／`worldRoute` — 见 [113](113-world-graph-local-map-architecture-revision-v0.1.md) |

### 明确不做

战斗关卡编辑、对话树 IDE、在编辑器里改 Core／Snapshot、把玩法写进 Unity 场景。  
**已废弃：** 早期 Electron「一应用四页」方案（`ExternalTools/content-authoring`），已删除。

---

## 2. 工程方案

```text
ExternalTools/ContentAuthoring/
  ContentAuthoring.sln
  Directory.Build.props  ← bin/obj 改到 .build/
  Shared/                 ← 类库
  PackageBrowser/         ← WPF 源码
  RegionEditor/
  MapEditor/
  QuestEditor/
  EventEditor/
  publish.ps1             ← 发布到 Apps/
  启动-*.cmd              ← 日常双击入口
  Apps/<App>/*.exe        ← 发布产物（gitignore）
  .build/                 ← 编译中间产物（gitignore，勿当启动入口）
  README.md
```

### 技术选型

| 层 | 选什么 | 为什么 |
|----|--------|--------|
| 桌面 | **WPF + .NET 8** | Visual Studio 调试／Release；Windows 优先 |
| 数据 | `System.Text.Json` 读写现有 JSON | 与 `ContentPackageLoader` 同契约 |
| 打包 | `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` | 双击 exe 即可，可不装运行时 |

### 和游戏怎么接

```text
编辑器保存 JSON → Unity 停掉再 Play DemoParityHost
→ ContentPackageLoader 扫 Data/**/*.json
→ mapLayout 建 WalkGrid + 按 kind 刷 Environment prefab
```

制作人**不要**在 Inspector 里换地图 JSON。`Playable Host Bootstrap` 默认读 `Content/BaseGame`。详见 [112](112-map-editor-usage.md)。

---

## 3. 怎么运行／打包

### Visual Studio

1. 安装 VS 2022（.NET 桌面开发）或单独装 .NET 8 SDK  
2. 打开 `ExternalTools/ContentAuthoring/ContentAuthoring.sln`  
3. 右键某个编辑器工程 → 设为启动项目 → F5  
4. 或选 **Release** → 生成；输出在 `.build/<工程>/bin/...`（调试用，日常请用 `Apps\`）

### 一键出五个 exe

```powershell
cd D:\UnityProjects\XianXia\ExternalTools\ContentAuthoring
.\publish.ps1
```

得到（也可双击同目录 `启动-*.cmd`）：

- `Apps\PackageBrowser\PackageBrowser.exe`
- `Apps\RegionEditor\RegionEditor.exe`
- `Apps\MapEditor\MapEditor.exe`
- `Apps\QuestEditor\QuestEditor.exe`
- `Apps\EventEditor\EventEditor.exe`

---

## 4. 每个编辑器要点

### 编辑器 1 — 包总览与校验台

**用法文档：** [108](108-content-studio-browser-usage.md)

按 type 浏览定义；运行校验（未知字段、重复 id、地点引用等）。

### 编辑器 2 — 区域／地点

**用法文档：** [109](109-content-studio-region-editor-usage.md)

表格编辑 `locations[]`：`adjacentIds`、tags、activities、presentationX／Z、产出、NPC／机缘／questOfferIds。

### 编辑器 3 — 任务

**用法文档：** [110](110-content-studio-quest-editor-usage.md)

表单 + 条件／奖励 JSON 数组（`offerConditions`／`completeConditions`／`rewards`／`fail*`）。字段对齐 SCHEMA（`autoOffer`，无 objectives）。

### 编辑器 4 — 事件

**用法文档：** [111](111-content-studio-event-editor-usage.md)

`body`／`trigger`／`locationId`／`conditions`／`choices` JSON。

---

## 5. 制作人流程

```text
1. 发布或 VS 打开对应 exe（日常用 启动-*.cmd／Apps\）
2. RegionEditor 摆逻辑地点并保存
3. MapEditor 摆格点设施；boundLocationId 绑到地点；Ctrl+S
4. QuestEditor / EventEditor 填剧情并保存
5. PackageBrowser 跑校验
6. Unity DemoParityHost → 停掉再 Play（看 Console WalkGrid from mapLayout）
```

---

## 6. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-10 | 初版：Electron 单应用计划 |
| 2026-08-10 | Electron 第一期交付后又废弃 |
| 2026-08-10 | 改为 WPF 四独立工程 + VS／publish.ps1；只要 Windows |
| 2026-08-10 | 新增 MapEditor（mapLayout 格点设施地图）；Host 优先读内容网格 |
| 2026-08-10 | 发布目录改为 `Apps/`；编译产物改到 `.build/`；增加 `启动-*.cmd` |
| 2026-08-11 | MapEditor 缩放／自由平移／UTF-8 保存；Host 按 mapLayout 刷 prefab＋地点对齐；完整清单见 [112](112-map-editor-usage.md) |
