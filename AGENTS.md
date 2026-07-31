# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：Core Milestone 1 编码（按阶段确认）

- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`
- 实施计划：**`docs/40-process/51-core-milestone-1-implementation-plan-v0.2.md`**（已批准）
- 编码纪律详见 Plan v0.2 §0.2，摘要如下：
  - Demo Runtime／Demo 场景：**冻结**，禁止迁移／重构／删除／修改
  - 每阶段：编译通过 + EditMode 通过 + 文件列表 + **等确认**；建议独立 commit
  - **未经批准禁止改：** 冻结 ADR／Freeze 正文、`ProjectSettings/`、`Packages/`、Demo
  - 设计不够用／要动冻结架构／要加核心概念 → **停码**，提设计问题，不得自行决定
  - 禁止：战斗、修炼、NPC AI、势力、跨 Region 离屏、Mods/、扩 Demo
- Development 不得自行改游戏规则；架构问题提 ACR。

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. `33` **v0.2**
7. Plan **v0.2**／ADR-0022
8. 按当前阶段任务读 `34`／`35`／`36`／`2C`／`2E`
9. `42-devlog.md` 最新 2～3 条

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
10. Core M1 范围见 ADR-0022；禁止偷做跨区离屏／真战斗／完整势力领导／Mods 文件夹。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR（见协作规范）。

## 回答语言

中文。
