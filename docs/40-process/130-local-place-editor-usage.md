# 130 · 场景地点登记（LocalPlaceEditor）

> ## ⚠️ LEGACY / TRANSITIONAL（2026-09-15）
>
> - 普通 Outdoor WorldSite 未来退出 `LocalPlaceSet`。
> - 当前为兼容旧 Content 保留。
> - 未来可能收窄为 Interior / Cave / Dungeon authoring（是否改名 `InteriorPlaceEditor` 为 Open）。
> - **不再推荐用于创建新的普通 Outdoor Site**；新 Outdoor 制作方向见 [ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。

> 状态：**可用（WPF／Windows）**｜日期：2026-08-16
>
> 生命周期：**Legacy / Transitional**（[ADR-0037 §6](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)）
>
> 工程：`ExternalTools/ContentAuthoring/LocalPlaceEditor/`  
> 编辑：`type = localPlaceSet`  
> 相关：[112 MapEditor](112-map-editor-usage.md)｜[128 WorldGraph](128-world-graph-editor-usage.md)｜[109 RegionEditor（旧）](109-content-studio-region-editor-usage.md)

---

## 和 MapEditor 的区别

| | LocalPlaceEditor | MapEditor |
|--|------------------|-----------|
| 编什么 | 本场景有哪些**逻辑地点**（落点／洞口进哪／任务锚点） | 格点外观／寻路 |
| 数据 | `localPlaceSet` | `mapLayout` |

种植／隐藏洞府等**玩法跟类型走**；本工具只做实例登记。

---

## 怎么打开

- `启动-LocalPlaceEditor.cmd` 或 `Apps\LocalPlaceEditor\LocalPlaceEditor.exe`（先 `publish.ps1`）

## 日常

1. 选地点表（如 `base:places_ch01_reference`）
2. 填 `mapLayoutId`／`startLocationId`
3. 表格改地点；洞口填 `机缘 site`／`进洞 map`／`洞内落点`；神识门槛默认 0
4. 「+ 隐藏洞口模板」可预填一行
5. **保存到磁盘**

旧 `RegionEditor` 仍编 `worldRegion`（青石等遗留）。本工具当前可继续用于**兼容既有 Content** 以及 Interior / Cave / Dungeon 类局部地点登记；但**新的普通 Outdoor WorldSite 不再走 LocalPlaceSet → LocalMap 这条路线**。
