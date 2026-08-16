# 130 · 场景地点登记（LocalPlaceEditor）

> 状态：**可用（WPF／Windows）**｜日期：2026-08-16  
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

旧 `RegionEditor` 仍编 `worldRegion`（青石等遗留）；新场景用本工具。
