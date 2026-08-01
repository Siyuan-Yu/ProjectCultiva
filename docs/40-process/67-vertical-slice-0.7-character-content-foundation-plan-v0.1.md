# Vertical Slice 0.7 Plan v0.1 — Character & Content Foundation

> 状态：**已验收**｜最后更新：2026-08-01｜验收：[68](68-vertical-slice-0.7-acceptance-report.md)  
> 前置：VS0.6 自动化已验收；制作人试玩清单 [66](66-vs0.6-producer-playtest-checklist.md)  
> **不改 Freeze；不升 Snapshot schema；不新增战斗／地图。**

## 0. 目标

将测试向角色／社会接线升级为**可持续用数据生产内容**的修仙角色基础：

- 新人物／新 NPC／新功法 → **只改 Content，不改 Core 规则代码**
- Personality／Background／Talent 可配置
- 开局生成、可招 NPC、开局关系、薄势力隶属由 Scenario 表驱动

## 1. 现状缺口（依据）

| 项 | 现状 |
|---|---|
| Character／Cultivation JSON | 已有；人物 tags 混用 |
| 开局三人 ID | ContentGameStart 硬编码 |
| 可招 NPC | PlayableDayBootstrap 软编码 CreateNpc |
| 开局关系 | OpeningRelationsSeeder 全员互惠常量 |
| 势力隶属 | Bootstrap 硬编码 OpeningFactionId |
| 功法 | JSON＋注册已通；需验收「只加数据」 |

## 2. 内部 Phase

| Phase | 交付 | Commit |
|---|---|---|
| V7-0 | 本计划 | docs: vs0.7 character content foundation plan |
| V7-A | Character 字段：personality／background／talent | eat(data): vs0.7 phase a character content tags |
| V7-B | openingScenario 内容类型＋加载 | eat(data): vs0.7 phase b opening scenario loader |
| V7-C | Bootstrap／GameStart 改读 Scenario（去硬编码 spawn／NPC） | eat(data): vs0.7 phase c scenario-driven bootstrap |
| V7-D | 开局关系／势力由 Scenario 配置 | eat(data): vs0.7 phase d content opening relations |
| V7-E | 数据-only 增样例＋回归测 | 	est(data): vs0.7 phase e content-only additions |
| V7-F | 验收报告 | docs: vs0.7 acceptance report |

## 3. Scenario 形状（试玩，非 Freeze）

Content/BaseGame/Data/scenarios.json，	ype=openingScenario：

- spawns[]：definitionId、entityKind(character\|
pc)、displayName?、assignOpeningFaction、factionRole、bindSchedule、bindDailyTask、recruitable?
- openingFactionId、scheduleId
- openingRelations[]：fromDefinitionId、toDefinitionId、delta、reasonTag、mutual?

## 4. 硬停

Freeze／Snapshot schema／Core·Data 边界／发明大型未文档玩法（据点经营留 VS0.8）。

## 5. 验收标准

1. 仅改 JSON（＋必要时 CSV）可增加：人物、可招 NPC、功法  
2. PlayableDay／Host 仍能启动；EditMode 全绿  
3. 无软编码「村内可招者」创建路径  
4. 文档 SCHEMA／Devlog 更新  
