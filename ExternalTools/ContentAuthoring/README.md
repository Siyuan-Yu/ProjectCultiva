# ContentAuthoring（Windows / WPF）

本目录有 12 个独立编辑器工程，以及共享库。Editor metadata 的唯一真源是 [EditorManifest.json](EditorManifest.json)：它定义工程路径、生命周期、默认发布集合、未来替代工具与提示说明。

## 日常编译与启动

制作人日常只需双击 `编译-所有编辑器.cmd`。它会用 Release、win-x64、self-contained、single-file 构建 manifest 中全部默认编辑器；无需直接运行 PowerShell，也无需去 `.build/` 或工程 `bin/` 找 exe。

成功后，从以下任一入口启动：

1. `启动-<Editor>.cmd` 快捷脚本；
2. `Apps/<EditorName>.exe`，例如 `Apps/MapEditor.exe`。

若启动脚本报告 exe 不存在，请先运行 `编译-所有编辑器.cmd`。启动器不会自行触发隐藏的完整编译。

如果 WorldComposer 或 FineEditor 双击后立即退出，请提供 `Apps/WorldComposer-crash.log` 或 `Apps/FineEditor-crash.log`。日志只会在发生未处理错误时创建或覆盖，正常启动不会生成。

`Apps/` 是最终交付 exe，属于 generated output，不是 Content source 或版本真源。`.build/` 只是 Build All 执行期间的临时工作目录，保存 bin、obj、staging 与 backup；正常结束后会自动移除。两者都不纳入 Git。

## Recommended / Active Editors

| Editor | 职责 |
|---|---|
| `PackageBrowser` | 包总览与校验 |
| `CharacterNpcEditor` | 人物与 NPC 规则 |
| `ManualArtEditor` | 功法与斗技 |
| `QuestEditor` | 任务 |
| `EventEditor` | 事件 |
| `WorkAreaEditor` | 工区规则、容量与权限 |
| `WorldComposer` | Continuous Surface 大世界拼装、路径、链接放置、预览与 Bake |
| `FineEditor` | WorldSite Blueprint / Detail Patch 的逐 Surface Cell 精修与对象放置 |

### WorldComposer

MAP-01 Production V1 大世界拼装工具。schema v3 的正式基础地形只有平原、山地、水域，森林是独立且可通行的生态覆盖层；支持河流/道路曲线路径、显式世界物件、道路河流交叉口、WorldSite Blueprint 与 Detail Patch 相对链接、选择/移动/旋转/精确坐标、预览、验证和 `FinalContinuousSurfaceBake`。Preview 与 Bake 共用 `CompositionEngine`。

MAP-01.5 已将世界预览改为后台生成的 `1 Surface Cell = 1 pixel` 缓存位图：pan、zoom、选择和 hover 只重绘位图与轻量矢量 overlay；宏观地形笔刷提交只更新带邻格 padding 的 dirty region，路径拖动在 mouse-up 后才重新合成。制作人可见的按钮、工具、状态、弹窗、验证与枚举显示统一为中文，JSON schema 与 enum value 仍保持英文稳定值。

“导入当前项目世界…”从现有 Main Surface、W2A geography、参考地图和 LocalPlace 数据生成 `Content/BaseGame/Authoring/ContinuousSurface/` 下的新制作源，不改运行时 `Data/**`。“导出运行时兼容候选包…”只写入制作人选择的独立目录，生成 geography 与 main surface 候选文件，不会自动安装。

### FineEditor

MAP-01 Production V1 `1×1 Surface Cell` 局部精修工具。WorldSite Blueprint 与 Detail Patch 都以 sparse override 表示未设置即继承；支持基础地形、特征与密度、1/2/4/8 笔刷、框选/填充/擦除。Blueprint 另支持通用对象放置的选择、拖动、旋转、复制、删除以及 `kindId`、Content/Asset 引用、精确坐标和 footprint 编辑。WorldComposer 可从框选区域创建并链接 Detail Patch，也可把链接源直接交给 FineEditor 打开。

MAP-01.5 为 sparse terrain 建立 `(x,y)` 索引与缓存 raster，绘制不再逐格线性搜索；大尺寸 Blueprint 的浏览与笔刷更新只处理缓存位图和实际 dirty cells。用户界面同样统一为中文。

## Legacy Compatibility Editors

以下工具仍能正常启动，且仍由 Build All 构建；它们只用于维护或迁移既有 Content，不应用于制作新一代地图 Content。

| Editor | 旧 Content 范围 | 未来替代 |
|---|---|---|
| `WorldGraphEditor` | Hex / WorldGraph Content | `WorldComposer` |
| `MapEditor` | mapLayout / LocalMap / Outdoor compatibility Content | `FineEditor` |
| `RegionEditor` | worldRegion / navigation Content | 尚未锁定 |
| `LocalPlaceEditor` | LocalPlace Content | 尚未锁定 |

`WorldComposer` 与 `FineEditor` 是新的 Authoring Source 工具，不继承旧 Hex、WorldGraph、mapLayout 或 LocalMap document model。`WorldGraphEditor` 与 `MapEditor` 仍只维护既有兼容 Content。Legacy Editor 启动时会在控制台显示兼容用途与已知替代方向。

## 发布语义

`publish.ps1` 是 `编译-所有编辑器.cmd` 调用的内部实现。它先将 manifest 中全部 `PublishByDefault=true` 的 Editor 发布到 `.build/` 下的临时 staging，只有全部成功后才整体替换 `Apps/`。任一 Editor 发布失败时，已有 `Apps/` 不会被改动；切换失败会尽力 rollback。完成或可安全恢复的失败后，整个 `.build/` 都会自动移除。因此，下一次完整构建也会移除已从 manifest 退出的旧 exe。

每次 Build All 覆盖 `build-all.log`，其中记录 preflight、manifest、每个 Editor 的 publish、Apps switch 和最终 exit code，便于诊断双击构建失败。

正式 `Apps/` 只保留平铺的最终 exe 和启动说明，例如：`Apps/PackageBrowser.exe`、`Apps/WorldGraphEditor.exe`。不要使用旧的 `Apps/<Editor>/<Editor>.exe` 路径。

## 用 Visual Studio（调试）

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 VS 2022 勾选 .NET 桌面开发）。
2. 打开 `ContentAuthoring.sln`，选择一个 Editor 工程后按 F5。
3. 调试输出在 `.build/<工程>/bin/...`，不作为日常启动入口。

## 使用要点

- 启动后默认尝试定位仓库里的 `Content/BaseGame`；找不到就点「打开包…」。
- 保存只改磁盘 JSON；Unity 重新 Play 后生效。
- 详细用法见仓库 `docs/40-process/108`～`112`、`128`、`130`。
