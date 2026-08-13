# ContentAuthoring (Windows / WPF)

五个**独立** Visual Studio / `dotnet` 工程，各自发布一个 exe。共享库 `Shared` 只负责读写 `Content/BaseGame/Data`。

## 日常怎么打开（重要）

**只从下面两处启动，不要进各工程的 `bin\`：**

1. 双击本目录的 `启动-MapEditor.cmd` 等快捷脚本  
2. 或打开 `Apps\<编辑器名>\<编辑器名>.exe`

`Apps/` 由 `.\publish.ps1` 生成（自包含，可不装 .NET 运行时），**不在 Git 里**。  
首次克隆后若双击 `启动-*.cmd` 提示找不到 exe，脚本会**自动打包**；也可先双击 `发布-所有编辑器.cmd`。  
编译中间产物在隐藏感更强的 `.build/`，**不是**给你双击用的。

| 工程文件夹 | 产物 | 干什么 |
|------------|------|--------|
| `PackageBrowser/` | `PackageBrowser.exe` | 包总览与校验 |
| `RegionEditor/` | `RegionEditor.exe` | 逻辑地点 |
| `MapEditor/` | `MapEditor.exe` | 格点地图（设施／墙） |
| `QuestEditor/` | `QuestEditor.exe` | 任务 |
| `EventEditor/` | `EventEditor.exe` | 事件 |

> 旧的 Electron 单应用已废弃，见 `ExternalTools/_archived-content-authoring-electron/`。

## 用 Visual Studio（调试）

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 VS 2022 勾选 .NET 桌面开发）
2. 打开 `ContentAuthoring.sln`
3. 设启动项目为某一个编辑器 → F5
4. 调试输出在 `.build/<工程>/bin/...`（不要当日常启动入口）

## 命令行一键发布

```powershell
.\publish.ps1
```

产物：`Apps/<编辑器名>/*.exe`。

## 使用要点

- 启动后默认尝试定位仓库里的 `Content/BaseGame`；找不到就点「打开包…」
- 保存只改磁盘 JSON；Unity 重新 Play 后生效
- 详细用法见仓库 `docs/40-process/108`～`112`
