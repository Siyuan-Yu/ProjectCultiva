# 115 · 近期更新收束（Content Studio／Level Tester／Data 目录）— 2026-08-13

> 状态：**已推送**｜日期：2026-08-13（末轮补录）  
> 相对提交：`104af44` 之后 → 本轮 `main` 推送  
> 相关：[110 QuestEditor](110-content-studio-quest-editor-usage.md)｜[112 MapEditor](112-map-editor-usage.md)｜[114 Level Tester](114-level-tester.md)｜[113 World Graph](113-world-graph-local-map-architecture-revision-v0.1.md)

---

## 1. 一句话

**内容真源统一到 `Content/BaseGame/Data/` 并按 type 分子目录**；QuestEditor v2 可视化编辑任务；Level Tester 支持编辑模式 prefab 预览；ContentAuthoring 启动脚本可自动打包；Host 缺 prefab 用洋红占位而不再偷换 kind。

---

## 2. 交付对照（104af44 之后）

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **Data 分子目录** | `Quests/` `Maps/` `Events/` `Characters/` … 共 15 类 | `Content/BaseGame/Data/README.md`、`SCHEMA.md` |
| **内容真源迁移** | 地图／任务不再以 `Assets/DynamicData/GameData/Levels/` 为真源 | `ContentPathRules`、Level Tester 默认 `Data/Maps/ch01_reference_map.json` |
| **QuestEditor v2** | 发放方式向导、接取/完成/奖励可视化、`JsonArrayEditor` | [110](110-content-studio-quest-editor-usage.md)、`启动-QuestEditor.cmd` |
| **QuestEditor 新建/另存** | 选 Id + 目标 JSON；另存为复制到新文件 | QuestEditor 工具栏 |
| **EventEditor 条件** | `conditions` 改用 `JsonArrayEditor` | EventEditor |
| **JsonArrayEditor 修复** | 修复 ComboBox 递归导致 Stack overflow 闪退 | `Shared/JsonArrayEditor.xaml.cs` |
| **编辑器启动** | `Apps/` 缺 exe 时自动 `publish.ps1`；新增 `发布-所有编辑器.cmd` | `ExternalTools/ContentAuthoring/` |
| **Level Tester 预览** | Inspector **Import / Clear Preview**：编辑模式用游戏 prefab 预览 mapLayout | `LevelTesterMapPreview.cs` |
| **Prefab 严格对应** | 去掉 Wall→Road 等偷换；缺 prefab → 洋红棋盘格占位 | `MapLayoutPrefabResolver`、`MissingPrefabPlaceholder` |
| **LevelTester 编译** | `DefaultMapLayoutPath` 跨类引用加类名前缀 | `LevelTesterSceneTool.cs` |
| **文档** | 110 用法 v3（含发放方式/条件表）；112/114 路径更新 | 飞书已同步 |

---

## 3. Data 目录结构（新）

```text
Content/BaseGame/Data/
  Characters/  Quests/  Events/  Maps/  Regions/  Chapters/
  Scenarios/  Cultivation/  Items/  Sites/  Resources/
  Facilities/  Settlements/  WorkAreas/  Jobs/
  SCHEMA.md  README.md
```

- Unity `ContentPackageLoader` 与 `PackageStore.Load` 均 **递归扫描** `Data/**/*.json`，子目录不影响加载。  
- 各编辑器 **新建/另存** 默认进对应子目录（任务→`Quests/`，地图→`Maps/`）。  
- `Assets/DynamicData/GameData/Levels/` 下旧 JSON 已删，留废弃 README。

---

## 4. QuestEditor v2 要点

### 4.1 发放方式（②）

| 方式 | 含义 |
|------|------|
| 自动接取 | `autoOffer` + 接取条件列表 |
| 前置任务完成后 | 固定 `questCompleted` 上一环 |
| 到指定地点可领 | Region 地点 `questOfferIds` |
| NPC 对话发放 | 自动创建/更新 `contentEvent`（`startQuest`） |
| 自定义 | 保留手写 JSON 逻辑 |

NPC 台词仍在 **EventEditor** 改。

### 4.2 可视化条件/奖励（③～⑥）

接取/完成/失败条件与奖励用 **+ 添加** 列表编辑，类型见 [110](110-content-studio-quest-editor-usage.md) 全文。

### 4.3 共享组件

- `ContentFieldCatalog` — 条件/奖励 kind 与字段  
- `QuestOfferService` — 发放方式检测与写入  
- `EditorPrompts` — 新建/另存对话框  

---

## 5. Level Tester 增量

| 项 | 说明 |
|----|------|
| 默认地图 | `Content/BaseGame/Data/Maps/ch01_reference_map.json` |
| Import | 编辑模式把 mapLayout 刷成 Host prefab 预览（无需 Play） |
| Clear Preview | 清预览实例 |
| 缺 prefab | 洋红棋盘格 `MissingPrefabPlaceholder`，Console 警告 |

---

## 6. MapEditor／Host（延续 104af44 前批次）

- 物件四角/四边缩放；分区 vs 物件两页工具板  
- 新建空图／另存为／打开地图（现默认 `Data/Maps/`）  
- Host 缩放后包围盒中心对齐；墙加深可见  

详见 [112](112-map-editor-usage.md)。

---

## 7. ContentAuthoring 使用提醒

1. 日常只双击 `启动-*.cmd` 或 `Apps/<Editor>/<Editor>.exe`  
2. 首次缺 exe 会自动打包（约 1 分钟），或先跑 `发布-所有编辑器.cmd`  
3. `Apps/` 不在 Git 里；改 Shared 后需重新 publish  

---

## 8. 已知未做

- EventEditor **choices** 可视化  
- 好感 `relationAtLeast` 引擎 + 编辑器  
- **ChapterEditor**（dayBeats 发任务）  
- World Graph 阶段 A→C  
- Ch01 手操签收（105）  

---

## 9. 建议手操检查

1. `启动-QuestEditor.cmd` → 打开 ch01 任务 → 改完成条件 → 保存  
2. Level Tester → 选 `Maps/ch01_reference_map.json` → **Import** → Scene 里见 prefab  
3. 故意删一个 prefab 引用 → 见洋红占位、无 kind 偷换  
4. PackageBrowser 校验 → Unity Play  

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 末轮：Data 分子目录、QuestEditor v2、Level Tester 预览、启动脚本、真源迁移 |
| 2026-08-13 | 初版：MapEditor／分区物件／Host 对齐／Level Tester 场景 |
