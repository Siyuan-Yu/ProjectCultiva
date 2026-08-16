# 129 · WorldGraph Host 出行／进场景／场景隔离（2026-08-16）

> 状态：**已落地**｜日期：2026-08-16  
> 相对：[113](113-world-graph-local-map-architecture-revision-v0.1.md)｜编辑器用法 [128](128-world-graph-editor-usage.md)  
> 飞书：https://my.feishu.cn/docx/KbFRdzob3o4ndMxsmlbcRV9inng

---

## 1. 一句话

宏观 30 节点出行 + 确认离场 + 进场景关大地图；不同 LocalMap **禁止**荒村药畦／麦垄等串景。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **大地图 RTS** | 头像选人、右键节点确认出行、缩放／中键拖；途中不可进场景 | `HostWorldMapPanel` |
| **离场** | 确认 → 走边缘 → Despawn → 上路；未走出可打断 | `HostWorldTravelConfirmPrompt`／`HostWorldTravelDeparture` |
| **进场景** | 头像菜单「进入」→ `EnterNodeScene` → 刷 LocalMap → **自动关大地图** | `WorldTravelService.EnterNodeScene`／`ApplyPartyWorldNodePresentation(closeWorldMap: true)` |
| **占位节点** | 无专属图 → `base:map_world_node_stub`＋歇脚处地点 | `world_node_stub_map.json`／`world_node_stub_places.json` |
| **场景隔离** | Preferred 图禁止回落荒村；无 place set 清空地点表；Legacy 药畦仅荒村 ActiveMap；色带同门禁 | `MapLayoutPick`／`ActivatePlacesForMapLayout`／`HostInteractSpots`／`HostZoneQuery`／`HostDemoTileMap` |
| **可见性** | 途中隐藏；地点不在当前表则藏 NPC | `LocalMapVisibility` |

---

## 3. 操作流（制作人）

```text
顶栏「地图」／M → 选头像 → 右键相邻节点 → 确认出行
  → 场景内走到边缘消失 → 大地图上路 → 到站
右键头像「进入 XXX」→ 本地图刷出、大地图自动关闭
荒村 ↔ 青云路（保底图）：不得残留药畦／麦垄标签
```

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 通行令 | **无**；旅行不检查 `traversalRequirements` |
| 进场景 | 成功后必须 `Close()` 大地图（含 bootstrap 引用实例） |
| Legacy 交互点 | 仅 `map_ch01_reference`（或开局未写 ActiveMap 且地点表含农田） |
| Preferred 缺失 | 空图，不 `BuildLegacyDemoTiles` |

**明确未做：** 路上遭遇 LocalMap（E）；正式 UI 框架。

---

## 5. 下一步建议

1. 手操确认：进入后大地图必关  
2. 路上遭遇（E）  
3. 更多节点绑真实 LocalMap  
