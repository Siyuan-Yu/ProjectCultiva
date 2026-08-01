# Vertical Slice 0.5 Plan v0.1 — Social／Personality Alpha

> 状态：**执行中（V5-A 已完成）**｜最后更新：2026-08-01  
> 前置：VS0.4 已验收（[61](61-vertical-slice-0.4-acceptance-report.md)）  
> 进度总表：[62](62-project-status-2026-08-01.md)  
> 依据：`33` §7、ADR-0017、`2E` §5A、`34`、`27`、`28`  
> **不改 Freeze 正文；Snapshot 含关系前须停等确认 schema。**

## 0. 目标

在正式 Core 落地最小社会闭环（非 Demo）：

人格标签 → RelationshipLedger → 开局关系 → 薄招募 → NPC 日程偏置 → 社会 Tick。

## 1. 内部 Phase

| Phase | 交付 | Commit |
|---|---|---|
| V5-0 | 本计划落盘 | `5207037` ✅ |
| V5-A | PersonalityProfileComponent + Bootstrap | `e443eee` ✅ |
| V5-B | RelationshipLedger／Service／缓存 | `feat(core): vs0.5 phase b relationship ledger` |
| V5-C | 开局关系种子 + Help／Slight | `feat(core): vs0.5 phase c opening relations` |
| V5-D | FactionMembership + RecruitService | `feat(core): vs0.5 phase d recruitment` |
| V5-E | 人格日程偏置 | `feat(core): vs0.5 phase e npc personality schedule` |
| V5-F | 社会 Tick 漂移 | `feat(core): vs0.5 phase f social tick` |
| V5-G | Alpha 整合验收 | `test(core): vs0.5 phase g social alpha acceptance` |

## 2. 硬停

改 Freeze／Snapshot schema／Core·Data 边界／发明大型未文档规则／无计划战斗·地图·正式 UI。

## 3. Alpha 常量（试玩，非 Freeze）

见实现中 `SocialAlphaConstants`：初始关系分、招募门槛等；变更记 Devlog。
