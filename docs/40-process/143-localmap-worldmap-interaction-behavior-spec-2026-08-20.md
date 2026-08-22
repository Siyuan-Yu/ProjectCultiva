# 143 · LocalMap／大地图进出交互行为方案（2026-08-20）

> 状态：**部分 superseded（2026-08-21）**｜原日期：2026-08-20  
> **战略战斗时间与 Modal 遭遇真源已改为：** [ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)／[144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)  
> 上级碎片：[139](139-world-map-rts-orders-2026-08-17.md)／[140](140-world-map-rts-battle-return-rollup-2026-08-18.md)／[129](129-world-graph-host-travel-scene-isolation-2026-08-16.md)／[113](113-world-graph-local-map-architecture-revision-v0.1.md)  
> 飞书：https://my.feishu.cn/docx/RF0lduLt5oEmVUxUdqecQquhnHL

---

## 0. Superseded 声明（必读）

下列 **143 原稿目标已废弃**，勿再实现：

| 废弃项 | 原因 |
|--------|------|
| P1「清场后战场仍活、回战场」 | 改为 FieldCleared → PostBattle → Resolve → 销毁普通遭遇 |
| 战斗中切到其他 LocalMap／一人进村一人留战场 | Modal Encounter：锁 ActiveMap |
| FieldCleared 后世界继续跑、InEncounter 挂起 | ADR-0023：Tick 冻结至 Resolve |
| 多个可同时活跃的手动战场 | Queue 串行；开战前勾选增援 |

**仍有效：**

- 非战斗时：一次只有一个 ActiveMap  
- Node LocalMap 与 Encounter LocalMap **语义分离**  
- WorldPresence 是 Core 真源；LocalMap 只负责实体表现  
- 非 Modal 时：进节点门槛＝有我方在场；全员上路可不卸视线（P3 非战斗语境）

> **2026-08-22 补充（target-model）：** 正式产品目标：Character 不能直接跨 Node 战略移动，须经 Army（[ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)／[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)）。当前 `PartyWorldPresence` 直上路为 **Prototype**。

下文保留为历史草案；冲突处以 ADR-0023／144 为准。

---

## 1. 现行（ADR-0023）进出摘要

```text
非战斗：
  大地图下令 → Despawn → Travel
  进节点 → 装 Node LocalMap
  全员上路 → 可不卸图、不挪镜

战略接战：
  BattleOffer → 冻结 WorldTick
  Manual → 锁 Encounter ActiveMap（Modal）
  禁：切村图、战略派参战者离开、并行第二场 Manual
  FieldCleared → PostBattle（仍冻结）→ 结束战斗 Resolve
  → 恢复 pause／倍速 → 普通遭遇实例销毁
```

增援：开战前按 ReinforcementRange 勾选（Phase B/C）；**不做**战中「以后再回战场」。

---

## 2～12.（历史草案 · 归档）

> 原 1A／2A、「回战场」决策表、§7.1 泳道等已整体 superseded。需要对照旧讨论时查 git 历史 `143` 初版；**不要按旧表开工。**
