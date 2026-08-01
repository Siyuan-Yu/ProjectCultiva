# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：Vertical Slice 0.3 编码（Phase A 完成，等确认进 B）

- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`（**禁止改 Freeze 正文**）
- VS 0.1／VS 0.2：**已验收**
- 计划：`docs/40-process/57-vertical-slice-0.3-plan-v0.1.md`（A–D；已批准）
- 冻结：RTS；Schedule＝默认行为；非固定剧情；禁战斗／地图／寻路／NPC AI／主管 Boss／完整关系
- 纪律：每 Phase 测试 + commit + **等确认** 再进下一 Phase
- Demo／ProjectSettings／Packages 禁擅改

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. `33` **v0.2**
7. [VS 0.2 验收](docs/40-process/56-vertical-slice-0.2-acceptance-report.md) → [VS 0.3 计划](docs/40-process/57-vertical-slice-0.3-plan-v0.1.md)
8. `42-devlog.md` 最新 2～3 条

## 硬性规则（摘要）

1. 文档先于代码；未批准阶段不得超前实现。  
2. 总览只放大纲。  
3. 术语走 `03-glossary.md`。  
4. Core／Data 禁止 UnityEngine；随机 `IRandomSource`；**WorldTick 唯一世界时间轴**，ActionClock 只扣 Duration。  
5. AttributeModifier 管道；`PersonalConcealmentRisk` 正式名。  
6. **RelationshipLedger 唯一真源**；Component 只缓存。  
7. Dead ≠ Removed；Focus 失能用 FocusCharacterUnavailable，不立即改玩家身份。  
8. DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity。  
9. 地图：World → Region → LocalMap。  
10. 禁止偷做跨区离屏／真战斗／完整势力领导／Mods 文件夹／扩 Demo。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR。

## 回答语言

中文。
