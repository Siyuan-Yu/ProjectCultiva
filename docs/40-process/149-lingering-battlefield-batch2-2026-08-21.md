# 149 · 残留战场批 2：Core 下沉 + 探望到站（2026-08-21）

> 状态：**已落地**｜日期：2026-08-21  
> 上级：[148 大地图弥留交互](148-worldmap-linger-incap-ux-2026-08-21.md)／[147 接战点／弥留残留](147-battlefield-linger-no-teleport-2026-08-21.md)  
> 飞书：https://my.feishu.cn/docx/G8ffdmXmUob8O2xV2aycr39pnGe  
> 游玩入口：`Assets/Scenes/LevelTester.unity`

---

## 1. 一句话

将 **残留战场再入队伍收集** 下沉 Core；**派人探望弥留** 到站后自动衔接「进入残留战场」菜单。

---

## 2. 改动

| 项 | 说明 |
|----|------|
| `LingeringBattlefieldPartyService` | `CollectViewParty`／`TryResolveBattleAnchor`／`CanEnterLingeringBattlefield` |
| `StrategicBoard.PendingLingeringVisitIncapId` | 探望出发时记录目标弥留者 |
| `HostWorldMapPanel.TryOpenPendingLingeringVisitAfterArrival` | 到站「去查看」后自动开进入菜单 |
| `EnterLingeringBattlefield` | Core 校验 + 清 pending |

---

## 3. 流程

```text
左键选活人 → 右键弥留 → 探望确认 → 上路
  → 到站 ArrivalNotice「去查看」
  → 大地图打开 + 选中到站人 + 自动弹「进入残留战场」
```

支援范围内进图规则与 148 一致。

---

## 4. 批 3（已完成）

见 [150 残留再进 Offer](150-lingering-battlefield-batch3-offer-2026-08-21.md)。

---

## 5. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 初版：Core 下沉 + 探望到站衔接 |
