# 140 · 大地图纯 RTS／接战／清场回程收束（2026-08-18）

> 状态：**已落地／手操基本通过；时间纪律 2026-08-21 起以 ADR-0023 为准**｜日期：2026-08-18；**2026-08-22 正式 Army 目标见 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)／[ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)**  
> 相对提交：`238c279` 之后 → 本轮 `main`  
> 计划真源：[139](139-world-map-rts-orders-2026-08-17.md)｜[138](138-world-strategic-battle-offer-plan-2026-08-17.md)  
> **战略战斗冻结：** [ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)／[144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)  
> **143「回战场」路径已 superseded**（不再作为默认）  
> 飞书：https://my.feishu.cn/docx/MEzIdolEBonQCExqQGjcsmxGnsf

> **historical vs target-model（2026-08-22）：** 本文记录的纯 RTS 出行、接战、清场回程等为 **已验收 Prototype 行为**，继续有效。正式 Faction／Army（真实 Character 成员）／外交／占点模型以 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 为准；`ArmyStack` 整数成员为 Prototype 简化。

---

## 1. 一句话

把大地图钉成 **纯 RTS 宏观层**：下令即上路、遇敌进 LocalMap、后到可加入、打完无结算可回程；本轮修清场后回不了青石荒村、后到误弹到站，并暂关节点势力染色。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **纯 RTS 出行** | 确认后立刻 Despawn＋宏观上路；删边缘离场整链 | `HostWorldTravelDeparture`／`HostWorldTravelConfirmPrompt` |
| **视线保留** | 全员上路不卸图、不挪镜头 | `PlayableHostBootstrap`／`LocalMapVisibility` |
| **到站弹窗** | 非追击最终目的地 →「是否打开大地图查看」 | `ArrivalNoticeService`／`HostStrategicInterruptPresenter` |
| **追击接战** | `CombatPursuitStackId`；到位只弹 BattleOffer，不弹到站查看；敌军挪位持续贴 | `StrategicPursuitService`／`WorldTravelService` |
| **后到加入** | 先到手动战后保留路上增援标记；到后「是否加入战斗」 | `ClearPursuitForEngagedKeepEnRoute`／`BattleOfferService` |
| **打完离场** | ~~敌清空 FieldCleared；无结算、不弹大地图；可宏观下令~~ → **改：** FieldCleared → PostBattle → Resolve；Tick 冻结至结束（ADR-0023） | 见 144 Phase D |
| **清场回程** | 路中／InEncounter：当前道路两端可直达（修「回不了荒村」） | `WorldTravelPathService.CanAgentReachTarget` |
| **路进度保留** | 进战／删栈前快照路锚，避免进度归零像瞬移 | `SnapshotEngagedRouteFromStack`／`PreserveRouteProgressForEncounter` |
| **进场景** | 有我方在场即可进；不做战略敌对封锁 | `StrategicNodeAccessService` |
| **暂关势力 UX** | 清演示 Owner；节点不按帮派染色；去掉大地图外交面板 | `StrategicBootstrap`／`HostWorldMapPanel` |
| **追击贴敌＋多选近战** | 每 tick 贴敌军栈；LocalMap 多选一起打 | 见 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md) |

---

## 3. 操作流（制作人手操）

```text
【出行】大地图选人 → 右键节点／道路 → 确认 → 人立刻从 LocalMap 消失并上路
【视线】全员上路后画面仍停在当前 LocalMap（不强制切大地图）
【攻击】右键敌军栈 → 攻击 → 持续贴敌军当前宏观位置；追上弹接战（自动／手动／撤退）
【群殴】LocalMap 多选己方右键敌人 → 全员一起打
【增援】第二人攻击同栈 → 到位弹「是否加入战斗」，不弹「到了要不要查看」
【清场】打光遭遇敌军 → 无胜利结算、不自动弹图；开 M 可下令回青石荒村
【进村】人回到荒村节点后 → 左键节点「进入场景」
```

---

## 4. 规则摘要（冻结）

| 规则 | 现行 |
|------|------|
| 宏观移动 | 纯 RTS；路上随时可改目标 |
| 遇敌 | 仅攻击／追击贴上敌军栈才弹 BattleOffer；敌军挪位持续改道；过路不暗雷 |
| 到站 | ArrivalNotice 仅非追击最终目的地 |
| 清场 | FieldCleared 解锁宏观移动；LocalMap 可仍留战场 |
| 进场景 | 有我方即可；暂不做外交／Owner 封锁 |
| 节点色 | 仅焦点高亮；暂不按势力 |

**明确暂缓：** LocalMap 边缘离场演出；战略外交／占点正式玩法；击杀自动胜利结算弹窗；交谈 ContentEvent。

---

## 5. 本轮修过的坑

| 现象 | 根因 | 修复 |
|------|------|------|
| 后到仍弹「要不要查看」 | 先到手动战 `ClearPursuit` 清掉路上人标记 | 只清进场者；同栈合并追击名单 |
| 打完回不了青石荒村 | 清场后仍 InEncounter，BFS 用较近端当起点，点回原端判「已在」 | 路中两端可直达；`ResolveAnchorNodeId` 覆盖 InEncounter |
| 清场后像瞬移／丢路进度 | 进 Encounter 清 TravelTicks；删栈前未快照 | Preserve／Snapshot 路锚 |
| 误以为外交挡进村 | 旧敌对门槛／节点染色误导 | 准入只看我方在场；清 Owner、停染色 |
| 攻击后敌军跑了追不上 | 只追下令时的锚点／Dest 节点 | 每 tick `SyncPursuersToStack` |
| LocalMap 多选只有一人打 | 近战只记一名攻方，后 `Begin` 顶掉前人 | 多名攻方同时打同一目标 |

---

## 6. 测试

- `StrategicPhaseTests`：追击不弹到站、加入战斗、FieldCleared 解锁、清场后点回荒村、敌军挪位再贴上弹窗等

---

## 7. 下一步建议

1. 手操再验：追击贴敌、LocalMap 多选群殴、增援加入、清场回荒村  
2. 跟随菜单／交谈仍暂缓（攻击＝贴上再打，不是另做「只跟不打」）  
3. 战略外交／占点正式启用前再开刀（当前刻意关掉）
