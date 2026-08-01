# 项目现状总览 — 2026-08-01

> 状态：**现行进度真源（过程文档）**｜最后更新：2026-08-01  
> 用途：一次看清 VS0.1～0.6 做到哪、本轮改了什么、下一步是什么。  
> 架构规则仍以 [33 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) 为准；本页不改 Freeze。

---

## 1. 一句话现状

**Architecture Freeze v0.2 有效。** Core／Data／Host **VS0.1～0.6 自动化已验收**。  
**VS0.5** 社会 Alpha（Core）＋**VS0.6** Playable Social Host（Unity 接线）已完成；**当前进入制作人人工试玩验收**（见 [66](66-vs0.6-producer-playtest-checklist.md)）。  
Demo Runtime **继续冻结**。关系入 Snapshot 前须硬停确认 schema。

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
| VS0.6 | **自动化已验收／人工试玩中** | [64](64-vertical-slice-0.6-playable-social-host-plan-v0.1.md)／[65](65-vertical-slice-0.6-acceptance-report.md)／[66 试玩](66-vs0.6-producer-playtest-checklist.md) | Social 接入 Unity Host |

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

- EditMode：**157/157 全绿**（含 VS0.4～0.6 Host／Social；`tools/run-editmode-tests.ps1`）。  
- PlayMode：Host 选择／命令烟测保持绿。  
- Snapshot：`WorldSnapshot.CurrentSchemaVersion = 1` **未升版**（关系／人格／隶属尚未入档）。

---

## 7. 下一步

1. **制作人按 [66](66-vs0.6-producer-playtest-checklist.md) 人工试玩签收 VS0.6**（开发已停）。  
2. 若要关系／人格／隶属进 Snapshot：**先停**，确认 schema 后再做。  
3. Content Authoring Tool：可招 NPC／关系种子不宜继续软编码膨胀。  
4. 下一切片方向待人工验收结论后定。

---

## 8. 明确不做（当前）

- 扩 Demo Runtime 玩法  
- 无计划战斗／地图／寻路／正式 UI 框架  
- 修改 Freeze 正文  
- 把 Host 做成「第二个 Demo」

---

## 9. 飞书阅读入口（同步后）

| 文档 | 飞书 |
|---|---|
| 项目现状（本页） | https://my.feishu.cn/docx/F1FJdQ1usoWzsIxfiTFcKbOQnM8 |
| 策划总览 | https://my.feishu.cn/docx/Oowtd4tyRoQBuxxMiBIcEkSbnBc |
| VS0.4 验收 | https://my.feishu.cn/docx/MK2gdVR5korKBrx8cLBctqmQnfc |
| VS0.5 计划 | https://my.feishu.cn/docx/BzFidWf30oHWmXxphZzcJxk0nmf |
| VS0.5 验收 | https://my.feishu.cn/docx/KK54d38O9oI81LxdpRqcmUWJnFc |
| VS0.6 计划 | https://my.feishu.cn/docx/BN4Zdflc5oNJdcx6xgTcYa3knNg |
| VS0.6 验收 | https://my.feishu.cn/docx/HTOndyRhWonYbWx2sz8cFlbjndc |
| VS0.6 制作人试玩 | https://my.feishu.cn/docx/DRHBdkcx4o3O88xjX6gcDUNinrh |
| 路线图 | https://my.feishu.cn/docx/Kj1odxkhBoa4YmxBCrYcYgn3n4e |
| 开发日志 | https://my.feishu.cn/docx/JOrrdevURodYaoxhTZGcamFAnQd |

完整映射见 `tools/feishu-map.json`。新建文档若不可见，需在飞书把应用文档分享到个人账号（或提供 open_id 后跑 `--share`）。
