# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：RPG-First 方向（文档已拍板／实现未授权）

- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`（**禁止改 Freeze 正文**；ADR-0024／0026 为补丁引用）
- **控制／世界存在真源：** [2K](docs/20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0026](docs/40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)
- **战略势力（外交／Capture／Army 军事）：** [2A](docs/20-systems/2A-factions-armies-diplomacy-and-capture.md)（「跨点必须 Army」已 supersede）
- **迁移计划：** [163](docs/40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)
- **纪律：** 未获制作人「可以开工 Phase N」前 — **禁止** PlayerParty／连续世界／Policy／Auto Travel 编码
- 硬停：改 Freeze 正文／Snapshot 契约／Core·Data 边界／未批准阶段超前实现
- Host：只适配输入／表现；Demo Runtime **只读参考**；禁迁玩法；禁改 ProjectSettings／Packages／Freeze

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. **[2K](docs/20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)** + [ADR-0026](docs/40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)
7. `33` **v0.2** + [2A](docs/20-systems/2A-factions-armies-diplomacy-and-capture.md) + [ADR-0024](docs/40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)
8. `42-devlog.md` 最新 2～3 条

## 硬性规则（摘要）

1. 文档先于代码；未批准阶段不得超前实现。  
2. 总览只放大纲。  
3. 术语走 `03-glossary.md`。  
4. Core／Data 禁止 UnityEngine；随机 `IRandomSource`；**WorldTick 唯一世界时间轴**，ActionClock 只扣 Duration。  
5. AttributeModifier 管道；`PersonalConcealmentRisk` 正式名。  
6. **RelationshipLedger 唯一真源**；Component 只缓存。  
7. Dead ≠ Removed；Focus 失能用 FocusCharacterUnavailable，不立即改玩家身份。  
8. DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity；**ActiveControlledCharacter** 见 2K。  
9. 地图：**HexWorld 唯一拓扑** + LocalMap 近景 + WorldMap 总览（见 2K／ADR-0025）；战略 Army／外交／占点见 `2A`（Army≠移动资格）。  
10. 修士 = 持久真实 Character + LOD（ADR-0024）；**禁止**匿名 `CultivatorPopulation` 代表修士战争。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR。

## 回答语言

中文。
