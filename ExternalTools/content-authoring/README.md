# XianXia Content Studio

桌面内容编辑器：读写仓库内 `Content/BaseGame/Data/*.json`，不改 Unity `Assets/`。

## 环境

- Node.js 18+
- Windows（可打 portable exe）；macOS 可用 `pack:mac` 出目录包

## 开发启动

```bash
cd ExternalTools/content-authoring
npm install
npm run dev
```

会起 Vite（5173）+ Electron。默认打开仓库里的 `Content/BaseGame`。

## 打包

```bash
# Windows：优先产出 dist-pack/win-unpacked/XianXia Content Studio.exe（免安装）
# 若本机无「创建符号链接」权限，portable 单文件可能失败，用解包目录即可
npm run pack:win

# macOS 应用目录 → dist-pack/
npm run pack:mac
```

打包后的程序仍通过「打开包…」选择本机上的 `Content/BaseGame` 目录。

## 四个编辑器

| 导航 | 说明 | 使用文档 |
|------|------|----------|
| 包总览与校验 | 按 type 浏览、运行校验、跳转 | [108](../../docs/40-process/108-content-studio-browser-usage.md) |
| 区域／地点 | 逻辑地图、邻接、产出、摆点 | [109](../../docs/40-process/109-content-studio-region-editor-usage.md) |
| 任务 | quest 条件／奖励 | [110](../../docs/40-process/110-content-studio-quest-editor-usage.md) |
| 事件 | contentEvent 触发／选项 | [111](../../docs/40-process/111-content-studio-event-editor-usage.md) |

## 与游戏的关系

保存后到 Unity 重新 Play（如 `DemoParityHost`）。Loader 会重新扫 `Data/**/*.json`。
