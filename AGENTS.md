# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 多会话协作（强制）

本项目存在三类长期 AI 工作流：**Architecture**／**Development**／**Narrative**。  
**所有 AI 会话必须遵守：**

[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)

要点：聊天不是真源；重要决定必须进入 Architecture／System Design／ADR／Devlog／Glossary；跨角色冲突时已冻结架构优先。

## 当前阶段：Hex／Army 正式运行依赖退役已封板

- 最终冻结入口：[ADR-0038](docs/40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) + [当前交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md)
- MAP-01～04、SPACE-01、LEGACY-FINAL-A／B／C，以及 Hex／Army 正式运行依赖退役与 021915 统一收尾，均已 **Producer Accepted / Sealed**。不得重新开启迁移分期，也不得按 Hex／Army／Legacy 关键词再开一轮扫描删除。
- 正常 Outdoor authority：`SurfaceId + exact WorldPosition + Continuous Surface`；NPC group 为 `Squad + SquadWorldMotion`；现代战斗为 `CharacterEncounter`。
- 正式运行依赖已退役：正常产品不编译旧 Hex 几何，也不在 Runtime Loader 内接受或自动迁移 `formalArmy`／`hexWorld`。`LegacyRuntimeConverter` 只无损转换旧 FormalArmy 与“current authority 已完整、仅 FormalArmy 待转”的 Snapshot；`hexWorld`／`openingHexWorldId` 只检测并拒绝，须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径，无样例时不猜。独立旧数据转换工具不重新接入正式运行。
- `FormalArmy`、`ArmyStack`、`TerritoryRegion`、`AtHex`、Outdoor LocalMap／Hex travel 只可存在于稳定 wire 检测、离线转换器、历史文档或有证据的工具／测试边界；协议键、保留枚举值、历史 ID 与必要的旧序列化字段映射不是“尚未清完”的理由。
- 硬停：改 Freeze 正文、稳定 Snapshot 数值／ID、Core·Data 边界，或把合法兼容输入重新提升为 normal runtime authority。
- Host：只适配输入／表现；Demo Runtime **只读参考**；禁迁玩法；禁改 ProjectSettings／Packages／Freeze
- 后续功能方向尚未批准，不自动启动下一项任务。

## 普通实施与验收（强制）

1. 一轮只形成一份明确、可复制的实施指令；尽量一次完成该轮范围及直接接线，不按文件或子系统拆成多轮要求重复验收。
2. 普通实现只要求没有基础编译错误，并配合最少量必要的静态引用与文件完整性检查。没有 Unity 不是停在半接线状态的理由。
3. 执行代理不编写或运行自动测试，包括单元测试、定向纯 C#／headless 回归、行为矩阵、存档回放、工具测试、Unity Test Runner、PlayMode 或 batchmode。
4. 不自行启动 Unity；不新增测试脚本、测试框架、行为矩阵或大规模验证任务。运行行为由制作人人工验收。
5. 有运行改动时，交付简短、可操作且与范围对应的人工验收项；纯文档修改不要求进入 Unity。
6. 不将编译通过或历史测试记录等同于人工验收，也不将“测试通过”作为代理实施完成的必需条件。Design Confirmed、Implemented、Producer Accepted、Committed／Sealed 与 Proposed 必须分开记录。
7. 普通实施完成后不自动提交。制作人明确要求“封板”时，即授权选择性 `git add` + `git commit`，无需再申请一次提交许可。
8. 默认不 push、不打 tag、不 amend、不 reset、不 clean；不得混入或覆盖无关工作区改动。

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
9. 地图：普通 Outdoor 以 **Continuous Surface + exact WorldPosition** 为真源；正常产品不编译旧 Hex 几何，历史输入只可离线转换。WorldMap 是同一 Surface 的战略视图。
10. 修士 = 持久真实 Character + LOD（ADR-0024）；**禁止**匿名 `CultivatorPopulation` 代表修士战争。  
11. 改实质内容更新 devlog；贵决定写 ADR。  
12. Development 不得自行改规则；Narrative 不得自行定数据结构；发现问题用 ACR／SDR。

## 回答语言

中文。
