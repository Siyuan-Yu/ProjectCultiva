# Vertical Slice 0.6 Plan v0.1 — Playable Social Host

> 状态：**执行中**｜最后更新：2026-08-01  
> 前置：VS0.5 已验收（[63](63-vertical-slice-0.5-alpha-acceptance.md)）；Alpha Readiness Audit  
> **不改 Freeze；不升 Snapshot schema；不新增 Core 社会规则。**

## 0. 目标

把 VS0.5 Social Alpha 接到 Unity 玩家路径：看见 NPC → 读人格／关系 → Help／Slight／Recruit → 事件反馈。

## 1. 内部 Phase

| Phase | 交付 | Commit |
|---|---|---|
| V6-0 | 本计划 | （本提交） |
| V6-A | Recruitable Npc EntityView＋可选中 | eat(unity): vs0.6 phase a recruitable npc views |
| V6-B | HUD 社会薄信息 | eat(unity): vs0.6 phase b social hud |
| V6-C | Help／Slight／Recruit 经 Port | eat(core+unity): vs0.6 phase c social commands |
| V6-D | 社会事件 Feed 优先 | eat(unity): vs0.6 phase d social event feed |
| V6-E | 整合验收报告 | 	est(unity): vs0.6 phase e social host acceptance |

## 2. 交互约定

- 1–4：仅 Character（劳动／休息／观察／修炼）
- 5／6／7：Help／Slight／Recruit  
  - Actor＝选中集第一个 Character  
  - Target＝选中集第一个非 Actor  
- Snapshot 社会状态不入档（已知限制）

## 3. 硬停

Freeze／Snapshot schema／Core·Data 边界／新大型核心系统／重定义玩法。
