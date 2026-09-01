# 147 · 接战点无瞬移＋弥留残留战场＋支援半径（2026-08-21）

> **⚠️ 2026-09-01 · 已被 [186 Phase 5S Final Architecture Closure](186-phase-5s-final-architecture-closure-2026-09-01.md) §2 SUPERSEDED（残留再入 gateway 部分）：**
> `EnterLingeringBattlefield`／攻击残留再入不再是 production gateway；残余角色（弥留／可见尸体）通过普通世界移动 + `LoadedStrategicPopulationMaterializer` 自然成为 Loaded LocalMap 人口（invariant 1384 / 1386）。
> 历史 diagnosis 保留不改。

> 状态：**已落地（代码已推 `eece220`）**｜日期：2026-08-21  
> 相对提交：`3b563de` → **`eece220`**（`main`）  
> **后续 Host 交互修补：** [148 大地图弥留交互与点击](148-worldmap-linger-incap-ux-2026-08-21.md)（待手操验）  
> 上级：[146 Host 打磨](146-adr0023-host-ux-polish-2026-08-21.md)／[145 验收](145-adr0023-phases-af-acceptance-2026-08-21.md)／[ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)  
> 关联：[142 自动战／弥留](142-auto-battle-incap-corpse-2026-08-20.md)（未处决语义已由本篇修订）  
> 游玩入口：`Assets/Scenes/LevelTester.unity`  
> 飞书：https://my.feishu.cn/docx/Ik0NdqMYAovL23xewRgcr7plnfd

---

## 1. 一句话

战后（手动「结束战斗」／自动「确认结算」）参战者**钉在大地图接战点（BattleAnchor）**，禁止瞬移回村；场上仍有弥留则**保留遭遇战场**可再进；未勾选处决＝敌军**全员弥留**；大地图底部可调**支援半径**（默认 **0.25**）。

---

## 2. 需求对照

| 主题 | 规则 | 实现要点 |
|------|------|----------|
| **无瞬移回家** | 出 LocalMap／确认自动结算后，仍在路上／接战锚点 | `PlaceAtBattleAnchor`；禁止对上场者 `Apply PreBattle` |
| **结束≠销毁** | 「结束战斗」解冻 Modal；有弥留则残留 | `ParkLingeringBattlefield`／`DestroyBattlefieldCompletely` |
| **残留再进** | 敌弥留→攻击再入；我方弥留→查看再入 | `BattlefieldLingering`＋`EnterLingeringBattlefield`／残留栈 `PlanManualEncounter` |
| **未处决＝全员弥留** | 不勾选「战胜时直接击杀」→ 0 阵亡、全员弥留 | `ApplyPlayerVictory(executeOnWin:false)` → `IsBattlefieldRemnant`＋`IncapacitatedMemberCount` |
| **再进仍是弥留** | 禁止刷满血新怪 | `ApplyPending` 对残留栈 `TryEnterIncapacitated` |
| **再进不进荒村** | 遭遇图，不跟焦点 Node 的 LocalMap | `HasActiveManualEncounter` 认弥留刷怪；`ResolveActiveEncounterLocalMapId` |
| **换路不瞬移** | 路中改点他路：先走到端点再续走 | `WorldTravelPathService.BeginRouteProgressTarget` |
| **支援半径 UI** | 大地图底栏滑块 0.25～4.0，默认 0.25 | `HostWorldMapPanel`＋`ReinforcementRangeService.DefaultWorldRadius` |

### 明确不做

- 临时 WorldGraph 节点  
- 丹药复活／背回／搜刮内容  
- 占点／外交（另开）

---

## 3. 产品流程

### 3.1 手动遭遇

```text
Offer → 手动战斗 → LocalMap
  → 敌清空／我方全倒 → PostBattle（可补刀）
  → 「结束战斗」
       ├─ 场上仍有弥留 → Park：大地图见人／「弥」头像／残留栈；WorldTick 解冻
       └─ 无弥留 → Destroy 遭遇
  → 右键残留栈「攻击（再入战场）」或弥留头像「查看」→ 同一 Encounter 图
```

### 3.2 自动遭遇

```text
Offer →（可选）勾选处决 → 自动战斗 → 结算弹窗「确认结算」
  ├─ 勾选处决：敌军栈移除；无我方弥留则 Destroy
  └─ 未勾选：敌军全员弥留残留（IsBattlefieldRemnant）；确认后 Park
→ 再攻／查看：刷出已是弥留的敌，可补刀
```

### 3.3 ADR-0023 补丁（相对初版）

| 旧默认 | 现默认 |
|--------|--------|
| Resolve 后销毁道路遭遇 | **有弥留则保留**，无弥留才销毁 |
| 可选支援战后回 PreBattle | **上场／Engaged 钉 BattleAnchor**；仅勾选未上场留原处 |
| 「结束战斗」≈结算销毁 | **结束＝退出 Modal**；销毁另判 |

---

## 4. 核心文件

| 层 | 文件 | 职责 |
|----|------|------|
| Core | `StrategicEncounterResolveService.cs` | 落点、Park／Destroy、全倒进 PostBattle、`HasLingeringIncapacitated` |
| Core | `StrategicEncounterRuntime.cs` | `BattlefieldLingering`／`LingeringLocalMapId` |
| Core | `ArmyStack.cs` | `IsBattlefieldRemnant`／`IncapacitatedMemberCount`／`HasIncapacitatedRemnant` |
| Core | `AutoBattleCasualtyService.cs` | 未处决＝全员弥留、0 阵亡 |
| Core | `StrategicEncounterSpawner.cs` | 残留再进、刷弥留怪、`SyncArmyStackMemberCount` 保留弥留栈 |
| Core | `BattleOfferService.cs` | `HasActiveManualEncounter`（含仅弥留刷怪）、`HasLingeringBattlefield` |
| Core | `BattleParticipantSnapshot.cs` | `BattleAnchorDestNodeId`；默认支援半径 **0.25** |
| Core | `WorldTravelPathService.cs` | 同路对齐／换路先出端点，禁挂路瞬移 |
| Host | `HostWorldMapPanel.cs` | 支援半径滑块＋圈；弥留头像／菜单 |
| Host | `PlayableHostBootstrap.cs` | `EnterLingeringBattlefield` |
| Host | `HostStrategicInterruptPresenter.cs` | 结束战斗残留 toast；处决文案 |
| Host | `LocalMapVisibility.cs` | 再进遭遇图可装图 |
| Test | `Adr0023BattlePhasesTests`／`StrategicPhaseTests` | 落点／未处决全员弥留 |

---

## 5. 手操清单（LevelTester）

1. 路上接匪 → 手动清场 →「结束战斗」→ 人仍在路上接战点（不回荒村）  
2. 未勾选处决自动战 → 摘要「全部弥留」→ 确认 → 残留栈仍在 → 再攻进图敌为弥留  
3. 勾选处决 → 敌军栈消失  
4. 留敌弥留结束 → 右键「攻击（再入战场）」补刀；清光且无弥留 → 战场消失  
5. 我方弥留 → 存活角色右键「查看」再入（详见 [148](148-worldmap-linger-incap-ux-2026-08-21.md)：未选活人＝查看菜单；已选活人＝派人探望）  
6. 战后右键路上另一点 → **慢移**，不瞬移  
7. 大地图底栏拖「支援半径」→ 绿圈变化；「默认」＝0.25  

---

## 6. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 初版手操清单 |
| 2026-08-21 | 收束全文：未处决全员弥留、再进、换路、支援半径；对齐提交 `eece220` |
| 2026-08-21 | 增补 [148](148-worldmap-linger-incap-ux-2026-08-21.md) 交叉引用（弥留左／右键与点击修补） |
