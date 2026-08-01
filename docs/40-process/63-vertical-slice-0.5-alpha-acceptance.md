# Vertical Slice 0.5 验收报告 — Social／Personality Alpha

> 状态：**已通过（自动化门禁）**｜日期：2026-08-01  
> 计划：[60-vertical-slice-0.5-social-alpha-plan-v0.1.md](60-vertical-slice-0.5-social-alpha-plan-v0.1.md)

## 1. 范围回顾

在正式 Core 落地最小社会闭环（非 Demo）：

人格标签 → RelationshipLedger → 开局关系 → 薄招募 → NPC 日程偏置 → 社会 Tick。

**非目标（保持）：** 战斗、地图／寻路、正式 UI、复杂 NPC AI、大世界／势力战争、改 Freeze、关系入 Snapshot。

## 2. Phase 与 Commit

| Phase | Commit | 说明 |
|---|---|---|
| V5-0 | `5207037` | 计划落盘 |
| V5-A | `e443eee` | PersonalityProfileComponent |
| V5-B | `4205430` | RelationshipLedger／Service／缓存 |
| V5-C | `34f6e4c` | 开局关系 + Help／Slight |
| V5-D | `2663ffd` | FactionMembership + RecruitService；`EntityTag.Npc` |
| V5-E | `4e24d39` | PersonalityScheduleBias |
| V5-F | `c4799d9` | SocialTickDriver（opt-in） |
| V5-G | （本提交） | Alpha 整合验收 |

## 3. 验收清单

- [x] Content／Spawn 写入差异人格标签并可查询  
- [x] RelationshipLedger 为唯一写路径；Component 仅缓存  
- [x] 开局三人互惠关系；Help／Slight 可改分  
- [x] 关系门槛招募；离开清隶属、保留 Ledger；可招者为 Npc  
- [x] bold／cautious 日程偏置可观察；Player Override 仍优先  
- [x] 社会 Tick 低频漂移；固定 seed 可复现  
- [x] PlayableDay 闭环整合测（`SocialAlphaAcceptancePhaseGTests`）  
- [x] 未改 Freeze；Snapshot schema 仍为 **v1**；关系／隶属／人格 **未**入档  
- [x] 无战斗／地图／寻路／正式 UI／Demo 玩法污染  

## 4. 测试门禁

- EditMode：含 Phase G 整合测在内全绿（`tools/run-editmode-tests.ps1`）  
- 架构：Core 规则／Data 配置／Unity 表现边界保持  

## 5. Content Authoring Tool 需求（记录）

- 可招 NPC／势力角色／关系种子若继续手写 spawn，应改为 Content 表＋编辑器  
- 当前 Alpha 仅 1 个软编码「村内可招者」实例  

## 6. 后续（硬停提醒）

- 关系／人格／隶属若要进存档 → **先停**，确认 Snapshot schema 后再做（V5-H 可选）  
- 下一产品方向另开切片；本 Alpha **不**自动扩战斗／地图  
