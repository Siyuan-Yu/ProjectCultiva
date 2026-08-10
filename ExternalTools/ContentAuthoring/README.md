# ContentAuthoring (Windows / WPF)

四个**独立** Visual Studio / `dotnet` 工程，各自 Release 出一个 exe。共享库 `Shared` 只负责读写 `Content/BaseGame/Data`。

| 工程文件夹 | 产物 | 干什么 |
|------------|------|--------|
| `PackageBrowser/` | `PackageBrowser.exe` | 包总览与校验 |
| `RegionEditor/` | `RegionEditor.exe` | 区域／地点 |
| `QuestEditor/` | `QuestEditor.exe` | 任务 |
| `EventEditor/` | `EventEditor.exe` | 事件 |

> 旧的 Electron 单应用已废弃，见 `ExternalTools/_archived-content-authoring-electron/`。

## 用 Visual Studio

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 VS 2022 勾选 .NET 桌面开发）
2. 打开 `ContentAuthoring.sln`
3. 设启动项目为某一个编辑器 → F5 调试，或配置 **Release** → 生成
4. 输出一般在 `某工程/bin/Release/net8.0-windows/`

## 命令行一键发布（自包含单文件）

在本目录执行：

```powershell
.\publish.ps1
```

产物在 `publish/<编辑器名>/`，例如 `publish/QuestEditor/QuestEditor.exe`。

## 使用要点

- 启动后默认尝试定位仓库里的 `Content/BaseGame`；找不到就点「打开包…」
- 保存只改磁盘 JSON；Unity 重新 Play 后生效
- 详细用法见仓库 `docs/40-process/108`～`111`
