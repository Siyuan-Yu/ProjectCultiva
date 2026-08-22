# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：战略势力层文档审核（尚未批准实现）

- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`（**禁止改 Freeze 正文**；ADR-0024 为 v0.2 补丁引用）
- **战略势力设计真源：** [2A 势力、军队、外交与战略占领](docs/20-systems/2A-factions-armies-diplomacy-and-capture.md) + [ADR-0024](docs/40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)
- 状态：Faction / Army / Diplomacy / Vassalage / Alliance / War / Capture **基础设计已拍板并写入文档**；当前处于**文档审核 / 实现前设计冻结**，**尚未批准进入代码实现**
- 进度总表：`docs/40-process/62-project-status-2026-08-01.md`
- Core／Data／Host／**VS 0.1～1.0 自动化已验收**；大地图接战 Prototype（`139`～`150`）已落地
- **纪律：** 未获制作人「可以开工」前 — **禁止**战略势力层编码、Army 迁移、外交 UI、占点实现
- 硬停：改 Freeze 正文／Snapshot 契约／Core·Data 边界／未批准阶段超前实现
- Host：只适配输入／表现；Demo Runtime **只读参考**；禁迁玩法；禁改 ProjectSettings／Packages／Freeze

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. `33` **v0.2** + [2A](docs/20-systems/2A-factions-armies-diplomacy-and-capture.md) + [ADR-0024](docs/40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)
7. `42-devlog.md` 最新 2～3 条

## 硬性规则（摘要）

1. 文档先于代码；未批准阶段不得超前实现。  
2. 总览只放大纲。  
3. 术语走 `03-glossary.md`。  
4. Core／Data 禁止 UnityEngine；随机 `IRandomSource`；**WorldTick 唯一世界时间轴**，ActionClock 只扣 Duration。  
5. AttributeModifier 管道；`PersonalConcealmentRisk` 正式名。  
6. **RelationshipLedger 唯一真源**；Component 只缓存。  
7. Dead ≠ Removed；Focus 失能用 FocusCharacterUnavailable，不立即改玩家身份。  
8. DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity。  
9. 地图：WorldGraph + LocalMap（见 `113`）；战略 Army／外交／占点见 `2A`。  
10. 修士 = 持久真实 Character + LOD（ADR-0024）；**禁止**匿名 `CultivatorPopulation` 代表修士战争。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR。

## 回答语言

中文。
