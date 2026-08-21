# 151 · 战略遭遇 stub 图扩面（2026-08-21）

> 状态：**已落地（Content）**｜日期：2026-08-21  
> 上级：[150 残留再进 Offer](150-lingering-battlefield-batch3-offer-2026-08-21.md)／[138 战略接战](138-world-strategic-battle-offer-plan-2026-08-17.md)  
> 飞书：https://my.feishu.cn/docx/TNcNdQRHyoBFFWxdujOcxRB0nud

---

## 1. 一句话

将默认 Encounter 图 `base:map_world_node_stub` 扩为 **150×80 空场**，去掉歇脚装饰，与节点图（荒村 `base:map_ch01_reference` 200×100）和洞府（40×30）明显区分。

---

## 2. 改动

| 文件 | 说明 |
|------|------|
| `Content/BaseGame/Data/Maps/world_node_stub_map.json` | 24×24 → **150×80**；`origin` (-75,-40)；`placements` 清空 |
| `Content/BaseGame/Data/LocalPlaces/world_node_stub_places.json` | 文案改为「战略遭遇·保底地点／接战空地」（无新地点） |

仍由 `StrategicEncounterCatalog.DefaultEncounterLocalMapId` → `base:map_world_node_stub` 引用；接战／残留再进共用。

---

## 3. 尺寸对照

| 地图 | id | 格点 |
|------|-----|------|
| 荒村（节点） | `base:map_ch01_reference` | 200×100 |
| **Encounter stub** | `base:map_world_node_stub` | **150×80** |
| 洞府 | `base:map_ch01_cave` | 40×30 |

---

## 4. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 初版：扩面 + 空场，暂不加装饰 |
