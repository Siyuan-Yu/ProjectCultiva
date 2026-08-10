# 项目现状总览 — 2026-08-01

> 状态：**现行进度真源（过程文档）**｜最后更新：2026-08-10（收束导航／NPC／Demo0.1；编辑器计划确认）  
> 用途：一次看清 VS0.1～1.0／Content Ready 做到哪、本轮改了什么、下一步是什么。  
> 架构规则仍以 [33 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) 为准；本页不改 Freeze。  
> 近期增量一次看清：[107](107-recent-milestones-rollup-2026-08-10.md)

---

## 1. 一句话现状

**Architecture Freeze v0.2 有效。** Core／Data／Host **VS0.1～1.0 Demo 自动化已验收**；样例关可玩弧／导航／NPC 活动底座／Demo 0.1 Production **自动化已验收（手操待签）**。  
**下一步：** [106 编辑器工具](106-content-authoring-editors-plan-v0.1.md)（`ExternalTools/content-authoring`，第一期 A～D）。  
制作人手操：[105](105-demo-0.1-producer-playbook-30min.md)。Demo Runtime **继续冻结**。

---

## 2. 切片进度与验收

| 切片 | 状态 | 计划／验收 | 要点 |
|---|---|---|---|
| Core M1 | 完成 | [51](51-core-milestone-1-implementation-plan-v0.2.md)／ADR-0022 | Entity／Order／Action／Snapshot 骨架 |
| Data M1 | 完成 | [53](53-data-pipeline-milestone-1-plan-v0.2.md) | Definitions＋CSV 校验 |
| VS0.1 | 完成 | [54](54-vertical-slice-0.1-acceptance-report.md) | Bootstrap＋Cultivation 子集 |
| VS0.2 | 完成 | [55](55-vertical-slice-0.2-plan-v0.1.md)／[56 验收](56-vertical-slice-0.2-acceptance-report.md) | PlayerOrder／Schedule／Override／Quota |
| VS0.3 | 完成 | [57](57-vertical-slice-0.3-plan-v0.1.md)／[58](58-vertical-slice-0.3-acceptance-report.md) | DayClock／Observe／Site／Gate／日终后果 |
| VS0.4 | **完成** | [59](59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md)／[61](61-vertical-slice-0.4-acceptance-report.md) | Unity Host 可玩日 |
| VS0.5 | **已验收** | [60](60-vertical-slice-0.5-social-alpha-plan-v0.1.md)／[63](63-vertical-slice-0.5-alpha-acceptance.md) | 人格／关系／招募／日程偏置／社会 Tick |
| VS0.6 | **自动化已验收** | [64](64-vertical-slice-0.6-playable-social-host-plan-v0.1.md)／[65](65-vertical-slice-0.6-acceptance-report.md)／[66 试玩](66-vs0.6-producer-playtest-checklist.md) | Social 接入 Unity Host |
| VS0.7 | **已验收** | [67](67-vertical-slice-0.7-character-content-foundation-plan-v0.1.md)／[68](68-vertical-slice-0.7-acceptance-report.md) | Scenario／人物标签／数据-only 增内容 |
| VS0.8 | **已验收** | [69](69-vertical-slice-0.8-cultivation-settlement-plan-v0.1.md)／[70](70-vertical-slice-0.8-acceptance-report.md) | 据点／资源／设施／分工／日产 |
| VS0.9 | **已验收** | [71](71-vertical-slice-0.9-world-interaction-plan-v0.1.md)／[72](72-vertical-slice-0.9-acceptance-report.md) | 地点图／Travel／Explore／俯视布局 |
| VS1.0 | **已验收** | [73](73-vertical-slice-1.0-demo-plan-v0.1.md)／[74](74-vertical-slice-1.0-acceptance-report.md) | Demo 0.1 成长闭环 |
| Content Ready | **已验收** | [76](76-content-ready-milestone-plan-v0.1.md)／[77](77-content-ready-milestone-acceptance-report.md) | Quest／ContentEvent／地点进入／天赋成长 |
| Chapter Production | **已验收** | [79](79-chapter-production-framework-plan-v0.1.md)／[81](81-chapter-production-framework-acceptance-report.md)／[80 流程](80-chapter-content-production-guide.md) | 章节／日 beat／Story Flag／Content Debug |
| Chapter Toolkit | **已验收** | [83](83-chapter-production-toolkit-plan-v0.1.md)／[85](85-chapter-production-toolkit-acceptance-report.md)／[84 规范](84-chapter-content-naming-standards.md) | 模板／引用校验／Ch1 Harness；可正式生产第一章 |
| Ch01 Reference Level | **已验收** | [87](87-chapter-01-reference-level-plan-v0.1.md)／[89](89-chapter-01-reference-level-acceptance-report.md)／[88 流程](88-chapter-01-reference-level-production-guide.md) | 模板关：地图／RTS／UI／三类 AI／Data 样板 |
| Demo 手感对齐 | **已验收** | [91](91-demo-v0.1-to-formal-gap-audit.md)／[93](93-demo-parity-level-acceptance-report.md)／[92](92-demo-parity-progress-2026-08-02.md) | `DemoParityHost`＝2D Sprite＋Demo 操作语义（不复活 Runtime） |
| 内容打断 CIF | **自动化已验收** | [95](95-content-interrupt-system-plan-v0.1.md)／[96](96-content-interrupt-system-acceptance-report.md) | 事件／任务弹层强制暂停 |
| Ch01 可玩弧＋UX | **自动化已验收；手操签收中** | [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)／[94](94-chapter-full-production-and-sample-guide.md) | 2G 至炼气→隐藏→伏笔；首次入区勘察 |
| RTS 手动控制＋HUD | **已记录** | [98](98-rts-manual-control-and-hud-pass-2026-08-03.md) | 己方不自动；点选指令；多交互点；时钟分钟 |
| Navigation Foundation | **自动化已验收；手操待签** | [99](99-navigation-foundation-milestone-plan-v0.1.md)／[100](100-navigation-foundation-acceptance-report.md) | 网格 A*；玩家／NPC 沿路径；基础避让 |
| NPC Simulation Foundation | **自动化已验收；手操待签** | [101](101-npc-simulation-foundation-milestone-plan-v0.1.md)／[102](102-npc-simulation-foundation-acceptance-report.md) | Job／WorkArea；Move→Work；样例四职 |
| Demo 0.1 Production | **自动化已验收；手操待签** | [103](103-demo-0.1-production-milestone-plan-v0.1.md)／[104](104-demo-0.1-production-acceptance-report.md)／[105](105-demo-0.1-producer-playbook-30min.md) | 三人分派＋矿工／老人拆角＋30 分钟手操 |
| 内容编辑器工具 | **计划已确认；待开工** | [106](106-content-authoring-editors-plan-v0.1.md) | ExternalTools；第一期校验台＋地点＋任务＋事件 |

---

## 3. VS0.4 Unity Host — 本轮交付清单

场景：`Assets/Scenes/PlayableHost.unity`（可用菜单 `XianXia/VS0.4/Create Or Update Playable Host Scene` 重建）。

| Phase | Commit | 交付 |
|---|---|---|
| V4-A | `4769cd1` | `PlayableHostSession`／Bootstrap；加载 `Content/BaseGame`；可 Tick World |
| V4-B | `eafa290` | `EntityView`／Spawner／Registry／CameraRig |
| V4-C | `a61afcc` | RTS 点选／框选／`HostSelectionState`／高亮；Shift=Toggle；框选覆盖 |
| V4-D | `1ac9e28` | `HostCommandBridge` → `PlayerCommandRequest` → Port（Labor／Rest／Observe／Cultivate） |
| V4-E | `9fb2b63` | `HostHudSnapshot`／`HostDebugHud`；Space 暂停；`.`／N 单步；`[`／`]` 倍速 |
| V4-F | `2595859` | `HostEventFeed` DomainEvent 环形缓冲（F2） |
| V4-G | `d15d351` | `HostSnapshotPanel` F5／F9；Load 后重建 View（**未改** Snapshot schema） |
| V4-H | `8b0b118` | 一日可玩整合测＋[61 验收报告](61-vertical-slice-0.4-acceptance-report.md) |

**Host 纪律：** 只适配输入／表现；禁止直改 Core 组件；禁止迁 Demo Runtime 玩法。

### 3.1 手操速查

| 输入 | 作用 |
|---|---|
| 左键／拖拽／Shift | 点选／框选／Toggle |
| 1–4 | Labor／Rest／Observe／Cultivate |
| Space | 暂停／继续 |
| `.`／N | 单步 Tick |
| `[`／`]` | 倍速 1→2→5 |
| F5／F9 | 存／读 Snapshot |
| F1／F2 | HUD／事件面板 |

---

## 4. VS0.5 社会 Alpha — 已做／待做

计划：[60](60-vertical-slice-0.5-social-alpha-plan-v0.1.md)

| Phase | 状态 | Commit／说明 |
|---|---|---|
| V5-0 计划 | 完成 | `5207037` |
| V5-A 人格 | 完成 | `e443eee` |
| V5-B RelationshipLedger | 完成 | `4205430` |
| V5-C 开局关系 | 完成 | `34f6e4c` |
| V5-D 招募 | 完成 | `2663ffd`（含 `EntityTag.Npc`） |
| V5-E NPC 日程偏置 | 完成 | `4e24d39` |
| V5-F 社会 Tick | 完成 | `c4799d9` |
| V5-G Alpha 验收 | **完成** | 见 [63](63-vertical-slice-0.5-alpha-acceptance.md)；关系／人格／隶属仍未进 Snapshot |

**硬停：** 改 Freeze／改 Snapshot 契约（含关系入档前须确认）／破 Core·Data 边界／无计划战斗·地图·正式 UI。

---

## 5. 文档与工程入口

| 入口 | 路径 |
|---|---|
| AI 协作 | 根目录 `AGENTS.md` |
| 策划总览 | [00-overview](../00-project/00-overview.md) |
| 路线图 | [41-roadmap](41-roadmap.md) |
| 开发日志 | [42-devlog](42-devlog.md) |
| 术语 | [03-glossary](../00-project/03-glossary.md)（含 PersonalityProfile） |
| 实体模型 | [34](../30-tech/34-entity-and-component-model.md)（含 PersonalityProfileComponent） |
| 飞书同步 | [37](../30-tech/37-feishu-sync.md)／`tools/feishu-sync.mjs` |
| 叙事草稿 | [2I 荒村杂役阶段叙事](../20-systems/2I-huangcun-labor-phase-narrative-v0.1.md) |

---

## 6. 测试门禁（截至本页）

- EditMode：**194/194 全绿**（含打断、全弧、RTS 首次勘察；`tools/run-editmode-tests.ps1`）。  
- PlayMode：Host 选择／命令烟测保持绿。  
- Snapshot：`WorldSnapshot.CurrentSchemaVersion = 1` **未升版**（关系／据点／地点尚未入档）。

---

## 7. 下一步

1. 制作人手操签收：`DemoParityHost`（路径见 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md) §3；必要时菜单重建场景）。  
2. 按 [94]／[2G](../20-systems/2G-first-chapter-flow.md) 换真实第一章文案／ID。  
3. 若要关系／据点／地点进 Snapshot：**先停**，确认 schema 后再做。  
4. 战斗／夺据点／多段对话树／产品 UGUI 另开切片。

---

## 8. 明确不做（当前）

- 扩 Demo Runtime 玩法  
- 真战斗／网格寻路／产品级 UGUI 皮肤（Host FormalHud 仅为模板面板）  
- 修改 Freeze 正文  
- 未确认前升 Snapshot schema（关系／据点／地点入档）  

交付总览另见 [75](75-vs0.7-to-1.0-delivery-summary-2026-08-01.md)。

---

## 9. 飞书阅读入口（同步后）

| 文档 | 飞书 |
|---|---|
| 项目现状（本页） | https://my.feishu.cn/docx/F1FJdQ1usoWzsIxfiTFcKbOQnM8 |
| 策划总览 | https://my.feishu.cn/docx/Oowtd4tyRoQBuxxMiBIcEkSbnBc |
| **VS0.7→1.0 交付总结** | https://my.feishu.cn/docx/DkNld4wZAowzGHx5yebcRb5onCd |
| VS1.0 计划／验收 | https://my.feishu.cn/docx/Txr0dU8lWokJHMxMOsUcu4XNnCg ／ https://my.feishu.cn/docx/IjshdGym4oFdufxuiqfcOdM6nZd |
| VS0.9 计划／验收 | https://my.feishu.cn/docx/EzTDddmHKonu81x34hucUKx7nrg ／ https://my.feishu.cn/docx/UjPIdfz59orUwTxHZfmcMV6Nnyf |
| VS0.8 计划／验收 | https://my.feishu.cn/docx/VrgpdyOZhoTaXAxMa1bctK4bnoe ／ https://my.feishu.cn/docx/RWNFdJKsQoRzMxx6M4IcMtyAnjc |
| VS0.7 计划／验收 | https://my.feishu.cn/docx/Rnggd9MEEopQ1fx2a3Dc6ZZTnud ／ https://my.feishu.cn/docx/W2GpdEq4boeQPHx1uwJcDNlsnZg |
| BaseGame SCHEMA | https://my.feishu.cn/docx/ItIMdNCxkoMbVXxZMNmcIObjnTc |
| VS0.6 验收／试玩 | https://my.feishu.cn/docx/HTOndyRhWonYbWx2sz8cFlbjndc ／ https://my.feishu.cn/docx/DRHBdkcx4o3O88xjX6gcDUNinrh |
| 路线图 | https://my.feishu.cn/docx/Kj1odxkhBoa4YmxBCrYcYgn3n4e |
| 开发日志 | https://my.feishu.cn/docx/JOrrdevURodYaoxhTZGcamFAnQd |
| **Ch01 可玩弧交付 [97]** | https://my.feishu.cn/docx/R3nHdBINFomeL7xsMrLcjMWmnub |
| 章节制作指南 [94] | https://my.feishu.cn/docx/RupXd581doYa2LxvCP4c8vHcnGe |
| 内容打断计划 [95] | https://my.feishu.cn/docx/TXAud6e1coTmifxWPklcRUngn5e |
| 内容打断验收 [96] | https://my.feishu.cn/docx/DOeidICK3oLP67xEbsUcKxk1n4e |
| Demo 手感对齐验收 [93] | https://my.feishu.cn/docx/W9ZgdAoY0odhINxIiGLcnwYonCg |
| Demo 缺口审计 [91] | https://my.feishu.cn/docx/YtOsdxza5oDF79xAdNLcqOXinAc |

完整映射见 `tools/feishu-map.json`。应用新建文档若个人账号不可见，需在飞书把文档分享给你，或提供 `open_id` 后跑 `node tools/feishu-sync.mjs --share --openid ou_xxx`。
