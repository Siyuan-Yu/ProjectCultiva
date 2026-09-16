# Editor Toolchain Cleanup（2026-09-15）

> 状态：已实施，待制作人人工验收
> 优先级：P1
> 最后更新：2026-09-15
> 范围：`ExternalTools/ContentAuthoring/` 开发与发布工具链；不包含 MAP-01

## 修改前的问题

- `publish.ps1` 自己硬编码 10 个 Editor 名称，和启动器、README、solution 的信息没有单一真源。
- 正式产物按 `Apps/<EditorName>/<EditorName>.exe` 分层；启动器在 exe 缺失时会隐式运行全量 publish。
- Publish 逐个直接覆盖正式 `Apps/`，中途失败会留下新旧混合的半更新状态。
- README 仍写「六个独立工程」，漏列 `WorldGraphEditor`，且将旧的自动构建和分层路径描述为当前行为。

## Manifest 与生命周期

唯一 Editor metadata source 是 `ExternalTools/ContentAuthoring/EditorManifest.json`。每项固定包含：

- `EditorName`
- `ProjectPath`
- `LifecycleStatus`
- `PublishByDefault`
- `Replacement`
- `Notes`

当前 6 个 Active Editor 为 `PackageBrowser`、`CharacterNpcEditor`、`ManualArtEditor`、`QuestEditor`、`EventEditor`、`WorkAreaEditor`。

当前 4 个 Legacy Compatibility Editor 为 `WorldGraphEditor`（未来 `WorldComposer`）、`MapEditor`（未来 `FineEditor`）、`RegionEditor`、`LocalPlaceEditor`。四者仍为 `PublishByDefault=true`，仍可启动并参与 Build All；它们只维护既有兼容 Content。启动器由 manifest 读取 lifecycle，Legacy 启动时输出兼容用途提示。

## Build All 与发布契约

唯一制作人 Build All 入口为 `ExternalTools/ContentAuthoring/编译-所有编辑器.cmd`；旧的等价 `发布-所有编辑器.cmd` 已删除。`publish.ps1` 是该入口的内部实现，不是日常用户入口。

Build All 先验证 manifest 和项目路径，然后把所有默认 Editor 以 Release、win-x64、self-contained、single-file 发布到 `.build/publish-staging-<guid>/Apps/`。只有全部 publish 成功，才将完整 candidate 目录切换为正式 `Apps/`；切换异常会尽力把 `.build/apps-backup-<guid>/` 的旧 `Apps/` 放回原位。完成或可安全回滚的失败后，整个 `.build/` 都会删除；只有 rollback 本身失败时才保留它，以免牺牲原 Apps。因为正式目录每次由完整 candidate 替换，已从 manifest 移除的旧 exe 不会残留。

最终正式输出平铺在：`ExternalTools/ContentAuthoring/Apps/<EditorName>.exe`。`Apps/` 是 gitignored generated output，不是 Content source 或版本真源；`.build/` 仅在 Build All 执行时临时承载编译中间产物及 staging/backup，正常结束后不会保留。`build-all.log` 每次覆盖，记录 preflight、manifest、publish、Apps switch 与最终 exit code。

## 非目标

本轮没有开始 MAP-01；没有创建或改造 `WorldComposer`、`FineEditor`、`Shared.EditorFramework`，没有修改地图 schema、`Content/BaseGame/Data/**`、Gameplay、Unity Runtime、Scene 或 Prefab，也没有迁移青石荒村。
