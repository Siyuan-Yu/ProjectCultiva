# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：Continuous World Legacy Final Seal

- 最终冻结入口：[ADR-0038](docs/40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) + [当前交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md)
- LEGACY-FINAL-A／B／C、MAP-01～04 与 SPACE-01 均已 Producer Accepted / Sealed；不得重新开启迁移分期。
- 正常 Outdoor authority：`SurfaceId + exact WorldPosition + Continuous Surface`；NPC group 为 `Squad + SquadWorldMotion`；现代战斗为 `CharacterEncounter`。
- `FormalArmy`、`ArmyStack`、`TerritoryRegion`、`AtHex`、Outdoor LocalMap／Hex travel 只能存在于明确的旧 Content／Snapshot 单向迁移或有证据的 compatibility／工具／测试边界。现行代码入口必须使用已落地的 `Legacy*` 名称；`HexCoord`／`HexMath`／Odd-R Q/R 与 `HexWorld` 几何类型合法保留，不得据此恢复正常 Gameplay Hex authority。
- 硬停：改 Freeze 正文、稳定 Snapshot 数值／ID、Core·Data 边界，或把合法兼容输入重新提升为 normal runtime authority。
- Host：只适配输入／表现；Demo Runtime **只读参考**；禁迁玩法；禁改 ProjectSettings／Packages／Freeze

## 开工前必读

1. 本文件 `AGENTS.md`
2. `README.md`
3. `docs/40-process/52-ai-collaboration-protocol.md`
4. `docs/00-project/00-overview.md`
5. `docs/00-project/03-glossary.md`
6. `33` **v0.2** + [ADR-0038](docs/40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)
7. **[2K](docs/20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)** + [2N](docs/20-systems/2N-continuous-surface-world-authoring-and-composition.md) + [2A](docs/20-systems/2A-factions-armies-diplomacy-and-capture.md)
8. [247 当前交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md) + `42-devlog.md` 最新 2～3 条

## 硬性规则（摘要）

1. 文档先于代码；未批准阶段不得超前实现。  
2. 总览只放大纲。  
3. 术语走 `03-glossary.md`。  
4. Core／Data 禁止 UnityEngine；随机 `IRandomSource`；**WorldTick 唯一世界时间轴**，ActionClock 只扣 Duration。  
5. AttributeModifier 管道；`PersonalConcealmentRisk` 正式名。  
6. **RelationshipLedger 唯一真源**；Component 只缓存。  
7. Dead ≠ Removed；Focus 失能用 FocusCharacterUnavailable，不立即改玩家身份。  
8. DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity；**ActiveControlledCharacter** 见 2K。  
9. 地图：普通 Outdoor 以 **Continuous Surface + exact WorldPosition** 为真源；Hex／Outdoor LocalMap 只限明确兼容边界。WorldMap 是同一 Surface 的战略视图。
10. 修士 = 持久真实 Character + LOD（ADR-0024）；**禁止**匿名 `CultivatorPopulation` 代表修士战争。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR。

## 回答语言

中文。
