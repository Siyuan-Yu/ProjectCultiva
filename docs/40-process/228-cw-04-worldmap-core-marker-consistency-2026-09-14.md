# CW-04 WorldMap Core Marker Consistency

> 状态：Implementation Completed / Producer Acceptance Pending  
> 日期：2026-09-14  
> 范围：WorldMap Core marker 与 Legacy/debug-only flag 隔离；不修改 Actual Control

## Marker authority

`WorldSitePresentationLayer` 不再对全部 Site 无差别遍历 footprint 画房屋。Marker 分类只读取正式 Core identity：

```text
Site.CoreAssetId
→ FactionFlagSiteCoreQuery
→ flag.IsSiteCore && flag.SiteId == site.SiteId
→ FactionFlag marker @ Site.CoreWorldPosition
```

旗杆／旗面由 `FactionFlagWorldMapPresentation.DrawFlagMarker` 单点复用。旗 Site 只画一面旗和一份 Site label，不再同时产生 footprint house 或 legacy flag icon。非旗 Continuous Core（当前议政厅）继续画原房屋；普通 Site 也保持既有房屋表现。

玩家新建旗天然满足同一 identity，因此无需第二套 marker 路径。Core 建立成功后的 Development invariant 验证 Site／CoreAsset／Flag 反向身份、Claim，以及 Core center 不得由其它势力管理；同势力旧 Site 可继续作为重叠中心的 actual manager。

## Legacy/debug-only isolation

正式开局唯一未迁移旗为 `test:flag_fisher_east`，它属于测试 fixture，不是正式产品漏迁。Content 现显式声明 `legacyDebugOnly=true`；正常 WorldMap 不绘制，诊断模式以后可选择显式请求旧旗层。

非开局 `base:hex_world_ch01` 的 `base:flag_xijin_westroad`、`base:flag_nan_yan_centralroad`、`base:flag_fisher_eastroad` 同样是无法落入正式 Continuous Surface 的旧兼容数据，已显式标记 debug-only。Reference validation 规定：非 `createsWorldSite` flag 必须显式 debug-only；正式产品旗若缺少 Site-Core metadata 会在 Content load 阶段失败。

## Diagnostics and deferred presentation

LevelTester CW-04 输出增加：

- 每个 active Continuous Site 的 `MarkerKind`、`MarkerWorldPosition`、`ActualOverlayPieces`、legacy marker 是否重复；
- 每面 flag 的 authored/debug-only 状态与正常产品 marker 可见性。

同 Faction 相邻 Actual Control 区域的视觉 union 延期：未来 fill 仍来自各 Site actual data，正常产品 border 可按 Faction union 隐藏同势力内部边界，diagnostics 仍可显示 Site management boundary。不得因此合并 Site identity、Claim 或 Administrative resolver。

## Validation boundary

- 仅运行现有非 Unity Core／Data／Unity Host offline compile。
- 仅执行 BaseGame Content/schema/reference 与轻量启动链检查、`git diff --check`。
- 未运行 Unity、EditMode、PlayMode、TestRunner、batchmode、Bake 或新增测试。
- 修改保持未提交，等待制作人人工验收。
