# 106 · 编辑器工具

> 状态：**早期第一期已交付（WPF 独立 exe，Windows）**；后期逐步增加至当前工具集｜日期：2026-08-11（本页 2026-09-15 做生命周期对齐）
>
> 一句话：**多个独立 Visual Studio／WPF 程序，分别编辑关卡 Data JSON；游戏仍用现有 Loader 读这些文件。**
>
> **生命周期真源：[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) §5～§6**（Active／Legacy Compatibility／Planned Replacement）。本页只做导航，不硬编码工具数量。
>
> 工程：`ExternalTools/ContentAuthoring/`（`ContentAuthoring.sln`）  
> 用法：[108 总览](108-content-studio-browser-usage.md)｜[109 逻辑地点](109-content-studio-region-editor-usage.md)｜[112 格点地图](112-map-editor-usage.md)｜[110 任务](110-content-studio-quest-editor-usage.md)｜[111 事件](111-content-studio-event-editor-usage.md)  
> 相关：[94 制作指南](94-chapter-full-production-and-sample-guide.md)｜[107 收束](107-recent-milestones-rollup-2026-08-10.md)｜`Content/BaseGame/Data/SCHEMA.md`

---

## 1. 要做哪些编辑器（按生命周期）

> 本页**不再硬编码「几个编辑器」**：数量会随工具增删过期。生命周期真源见 [ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) §5～§6，本节只做导航。
> **不放** `Assets/`。当前只做 **Windows**。

### Active（正式推荐生产工具）

| 工程文件夹 | 你用它做什么 | 主要 type | 用法文档 |
|---|--------------|------|----------|
| `PackageBrowser/` | 看全包条目；一键校验 | 全部（只读＋校验） | [108](108-content-studio-browser-usage.md) |
| `CharacterNpcEditor/` | 人物规则／工区偏好／可控制性／场景挂载 | `character` 等 | [118](118-npc-behavior-editor.md)／[119](119-npc-character-vs-role-template-editors.md) |
| `ManualArtEditor/` | 功法／斗技 | `manual` 等 | [133](133-manual-art-editor-and-cleanup-2026-08-16.md) |
| `QuestEditor/` | 新建／改任务 | `quest` | [110](110-content-studio-quest-editor-usage.md) |
| `EventEditor/` | 新建／改事件 | `contentEvent` | [111](111-content-studio-event-editor-usage.md) |
| `WorkAreaEditor/` | WorkArea 规则编辑（activity／capacity／privileges／resident tags） | `workArea`／`job` | — |

> `WorkAreaEditor` 未来「WorkArea 在世界哪里」可能由 FineEditor 的 Blueprint placement 吸收，但其**规则属性编辑仍有正式消费者**：当前不得删除，也不是 Planned Removal。

### Legacy Compatibility（只为维护／迁移既有 Content 保留）

| 工程文件夹 | 当前用途 | Planned Replacement | 用法文档 |
|---|--------------|------|----------|
| `WorldGraphEditor/` | 旧 Hex Content；含 Faction／Opening Diplomacy 功能（退休前须迁出） | `WorldComposer` | [128](128-world-graph-editor-usage.md) |
| `MapEditor/` | 旧 `mapLayout`／LocalMap 格点地图 | `FineEditor` | [112](112-map-editor-usage.md) |
| `RegionEditor/` | 旧 `worldRegion`／`locations` graph | Outdoor consumers 迁完后删除 | [109](109-content-studio-region-editor-usage.md) |
| `LocalPlaceEditor/` | 旧 `localPlaceSet`；将收窄为 Interior／Isolated Surface authoring | 可能改名 `InteriorPlaceEditor`（Open） | [130](130-local-place-editor-usage.md) |

> `Lifecycle = Legacy ≠ 不能运行`：这些工具当前仍由 Build All 编译发布，直到替代工具可用、源 Content 迁完、生产路径不再需要，才按 ADR-0037 §9 的顺序删除。

共享库：`Shared/`（读盘、写盘、SCHEMA 字段白名单、校验）。

### 早期计划中仍未交付的条目（仅作历史）

章节编排器（`chapter`）、开局 Scenario 编辑器（`openingScenario`）、WorldGraph 编辑器（`worldGraph`／`worldNode`／`worldRoute`，见 [113](113-world-graph-local-map-architecture-revision-v0.1.md)）——当时的「第二期」清单，现已经历多轮增删；以 ADR-0037 生命周期表与实际 `ExternalTools/ContentAuthoring/` 为准。

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
  …                       ← 后续追加的编辑器工程（完整列表见 ADR-0037 §6）
  publish.ps1             ← 发布到 Apps/（方向：收敛为唯一 Build All 入口）
  启动-*.cmd              ← 日常双击入口
  Apps/<App>/*.exe        ← 发布产物（gitignore；方向为 Apps/<App>.exe 平铺）
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

### 一键出全部 exe

```powershell
cd D:\UnityProjects\XianXia\ExternalTools\ContentAuthoring
.\publish.ps1
```

得到（也可双击同目录 `启动-*.cmd`；下例只列早期五个，完整列表以 ADR-0037 §6 为准）：

- `Apps\PackageBrowser\PackageBrowser.exe`
- `Apps\RegionEditor\RegionEditor.exe`
- `Apps\MapEditor\MapEditor.exe`
- `Apps\QuestEditor\QuestEditor.exe`
- `Apps\EventEditor\EventEditor.exe`

> **方向（未实现）：** [ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 锁定正式发布为唯一 Build All 入口 + `Apps/<EditorName>.exe` **平铺** + staging all-or-nothing。当前实际仍是 `Apps/<Editor>/<Editor>.exe` 多层目录。

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
