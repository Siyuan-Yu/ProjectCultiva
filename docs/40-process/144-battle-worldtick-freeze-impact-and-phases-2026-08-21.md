# 144 · 战略战斗冻结 WorldTick：影响审计与实施分期（2026-08-21）

> 状态：**Phase A～F 已落地（见 [145](145-adr0023-phases-af-acceptance-2026-08-21.md)）**｜日期：2026-08-21  
> 真源决策：[ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)  
> 产品规则全文见 ADR；本文＝影响面 + superseded 清单 + 分期任务。  
> 飞书：https://my.feishu.cn/docx/Slj6dqeNeoacZUxiellc2gsgn4e

---

## 1. 阻塞级冲突结论

**无阻塞。** 旧「战斗期间世界继续推进」与 143「清场挂起／回战场」为**故意废弃**，不是未决冲突。ADR-0018（唯一 WorldTick）仍然成立；本变更是补充「何时不推进」，不是第二套时钟。

---

## 2. 旧规则 superseded 清单

| 旧规则 | 出处 | 处置 |
|--------|------|------|
| 战斗期间世界时间照常流逝；甲交战乙可别处劳作 | `21` §10、`23` §12 | **废弃** → ADR-0023 |
| BattleOffer 选完即恢复开战前暂停（含进入手动战后） | `138` §3.1 | **修订**：仅 Resolve 后恢复；Manual／PostBattle 保持冻结 |
| FieldCleared 后可宏观离开、世界继续、InEncounter 挂起 | `139`／`140`／代码 | **废弃默认** |
| 一人进村、一人留战场、以后「回战场」 | `143` P1／§6.4／§7.1／D9 | **superseded** |
| 战斗中可切其他 LocalMap | `143` 2A 进村切图（战斗语境） | **废弃**（Modal 锁图） |
| 多个可同时活跃的手动战场体验 | `143`／增援中途加入战中 | **废弃**；开战前勾选增援；Queue 串行 |
| 途中加入进行中手动战（JoinOngoing）作默认 | `138`／`140` | **降级**：改由 Offer 勾选 Optional；战中动态加入另开（不做） |

**保留（143／架构）：**

- 一次只有一个 ActiveMap  
- Node LocalMap ≠ Encounter LocalMap（语义分离；id 债另开）  
- WorldPresence＝Core 真源；LocalMap＝表现  

---

## 3. 数据模型影响

| 概念 | Phase | 说明 |
|------|-------|------|
| `StrategicClockFreezeReason` | A | None／BattleOffer／ManualEncounter／PostBattle／InterruptQueue |
| 冻结快照：SavedPaused／SavedTimeScale | A | Resolve 时恢复 |
| `BattleParticipantSnapshot` | B | Mandatory／Optional／Enemy；PreBattleWorldPresence |
| `ReinforcementRange`／Threshold 配置 | B | 战略 TravelCost，非像素 |
| Offer UI 勾选 Optional | C | |
| PostBattle 结果／「结束战斗」 | D | |
| `BattleInterruptQueue` | E | 同 Tick 多 Offer 确定性顺序 |
| Encounter 实例销毁策略 | D | 普通遭遇 Resolve 后销毁 |

Snapshot schema：Phase A 起建议写入 freeze reason（可容错默认 None）；Participant／Queue 随 B／E 扩。

---

## 4. Core 影响

| 区域 | 变化 |
|------|------|
| `StrategicBoard` | 持有 ClockFreeze 状态 |
| `BattleOfferService.TryBuild*` | 成功创建 Offer 时 `BeginFreeze(BattleOffer)` |
| `ResolveAuto` | 不推进 Tick；结束 `EndFreeze`（或入 Queue 下一场） |
| Manual 开始 | `ContinueFreeze(ManualEncounter)` |
| `SimulationLoop`／Travel 驱动 | 冻结时禁止推进 WorldTick／战略 Travel |
| `WorldTravelService`／Departure | Modal 下拒绝战略出行（除 Retreat 结果） |
| 日后 Spawner／Release／FieldCleared | 不再默认「清场解锁宏观继续跑」 |

---

## 5. Host 影响

| 区域 | 变化 |
|------|------|
| `HostStrategicInterruptPresenter` | Offer 冻结；**进入手动后不得**按旧逻辑恢复 pause |
| `PlayableHostBootstrap` auto-tick／StepTick | 尊重 `IsWorldTickFrozen` |
| 战术 `IsPaused` | 可在 Manual 内切换；**不**驱动 WorldTick |
| `HostNpcMeleeAssault` | WorldTick 冻结时仍可打（跟战术暂停走） |
| `HostWorldMapPanel`／Departure | Modal：只读／禁令／禁进其他场景 |
| PostBattle UI | Phase D；A 用最小「结束战斗」避免软锁 |

---

## 6. Snapshot 影响

- A：freeze reason + saved pause/scale（缺省＝未冻结）  
- B+：participant snapshots  
- E：interrupt queue  
- 旧档无字段 → 视为未冻结  

---

## 7. 测试影响

| 用例 | Phase |
|------|-------|
| 产生 Offer 后 Tick 不变 | A |
| Manual 进行中 Tick 不变；战术可暂停 | A |
| AutoResolve 前后 Tick 相同 | A |
| Modal 下战略出行失败 | A |
| Resolve／结束战斗后恢复 pause/scale | A（薄）／D |
| ReinforcementRange／勾选／恢复 PreBattle | B–D |
| 同 Tick 双 Offer 串行 | E |

---

## 8. 实施分期

| Phase | 内容 | 不做 |
|-------|------|------|
| **A** | ClockFreeze；Offer／Manual／薄结束；Modal 锁图锁令；EditMode 冻结断言 | 增援勾选、Queue、完整 PostBattle |
| **B** | ParticipantSnapshot + ReinforcementRange | UI 勾选 |
| **C** | Offer 增援勾选 UI | 动态战中援军 |
| **D** | PostBattle + WorldPresence 恢复 + 实例销毁 | 搜刮／俘虏内容深度 |
| **E** | BattleInterruptQueue | 并行战场 |
| **F** | EditMode 全套 + Host 手操验收 | — |

---

## 9. Phase A 验收（本轮）

1. BattleOffer 弹出后 `WorldTick` 不因 auto-tick／Space 解除战略冻结而推进  
2. 选手动战后进入遭遇图，交战过程中 Tick 不变  
3. 大地图不可对参战者下宏观移动／不可进其他 LocalMap  
4. 清场后点「结束战斗」→ 清遭遇、恢复冻结前 pause/scale、Tick 可再推进  
5. AutoResolve 不增加 Tick  

---

## 10. 明确不扩

攻城、战场动态援军、飞行支援、ArmyGroup 实体化、复杂 NPC 支援 AI、伏击、阵型、战后追击。
