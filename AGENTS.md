# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：VS0.5 社会 Alpha（V5-A 完成 → V5-B Ledger）

- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`（**禁止改 Freeze 正文**）
- 进度总表：`docs/40-process/62-project-status-2026-08-01.md`
- VS0.4 Host：**已验收** → `61-vertical-slice-0.4-acceptance-report.md`
- VS0.5 计划：`60-vertical-slice-0.5-social-alpha-plan-v0.1.md`；**V5-A 人格已提交**；下一步 V5-B RelationshipLedger
- **纪律：** 每内部 Phase：实现 → 测试 → **单独 commit** → Devlog
- 硬停：改 Freeze／Snapshot 契约／Core·Data 边界／大型未设计系统／需人工定规则
- Host：只适配输入／表现；Demo Runtime **只读参考**；禁迁玩法；禁改 ProjectSettings／Packages／Freeze

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. `33` **v0.2**
7. [VS 0.4 验收](docs/40-process/61-vertical-slice-0.4-acceptance-report.md)
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
