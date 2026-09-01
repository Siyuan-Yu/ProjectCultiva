# 150 · 残留战场批 3：接战 Offer + Encounter 地图（2026-08-21）

> **⚠️ 2026-09-01 · 已被 [186 Phase 5S Final Architecture Closure](186-phase-5s-final-architecture-closure-2026-09-01.md) §2 SUPERSEDED：**
> 敌方残留栈再进（`LingeringLocalMapId` 产品入口）不再是 production 路径；`LingeringBattlefieldRegistry` 仅 legacy / compatibility 职责（invariant 1386 / 1387）。历史实现记录保留不改。

> 状态：**已落地（EditMode 通过；手操待验）**｜日期：2026-08-21  
> 上级：[149 批 2](149-lingering-battlefield-batch2-2026-08-21.md)／[138 战略接战](138-world-strategic-battle-offer-plan-2026-08-17.md)  
> 飞书：https://my.feishu.cn/docx/XE8EdWZCDoSDPjxRSXTcat5bn7R  
> 游玩入口：`Assets/Scenes/LevelTester.unity`

---

## 1. 一句话

**我方弥留再进** 与 **敌方残留栈再攻** 均先弹 **接战 Offer**（战力／自动／手动／撤退），手动进图使用 **`LingeringLocalMapId`**，不再 bypass 弹窗或误进 stub。

---

## 2. 改动

| 项 | 说明 |
|----|------|
| `BattleOfferService.TryBuildOfferForLingeringBattlefield` | 弥留菜单／再入统一开 Offer |
| `ActivateOffer` 残留分支 | `HasIncapacitatedRemnant`／`BattlefieldLingering` 时用 `LingeringLocalMapId`、标题「残留战场」 |
| `StrategicPursuitService.AfterTravelTick` | 残留栈追击到站标题改为「残留战场」 |
| `HostWorldMapPanel` | 「进入残留战场」→ `TryBuildOfferForLingeringBattlefield`（不再直连 `EnterLingeringBattlefield`） |
| `HostStrategicInterruptPresenter.EnterManualEncounter` | 手动进图补 `EncounterLinkId`（`linger` 回落） |
| `LingeringBattlefieldPartyService.cs.meta` | 补缺失 meta，Unity 才能编进 Core |
| `StrategicPhaseTests` | `LingeringReentry_*`／`RemnantStackAttack_*` |

Content 另见 [151 Encounter stub 150×80](151-encounter-stub-map-150x80-2026-08-21.md)。

---

## 3. 流程

```text
残留战场存在
  ├─ 右键我方弥留 →「进入残留战场」→ Offer（残留战场）
  └─ 选活人 → 攻击敌方残留栈 → 到站 → Offer（残留战场）
        ├─ 自动战斗 → 结算弹窗
        └─ 手动战斗 → LingeringLocalMapId 遭遇图
```

`EnterLingeringBattlefield` 仍保留作 Host 内部进图实现；UI 层不再 bypass Offer。

---

## 4. 验收（手操）

1. 自动战不处决 → 大地图残留  
2. 我方弥留右键 → 弹 Offer，非直进图  
3. 敌方残留栈再攻 → 到站弹 Offer  
4. 选手动 → 进正确 Encounter 图（非错节点 stub）

EditMode：`LingeringReentry_OpensBattleOffer_WithLingeringLocalMap`、`RemnantStackAttack_OpensBattleOffer_WithLingeringLocalMap`。

---

## 5. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 初版：Offer 统一 + LocalMap + meta 修复 |
