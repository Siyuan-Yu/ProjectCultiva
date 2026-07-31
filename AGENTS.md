# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。

## 当前阶段：Architecture Freeze v0.2（待人工审核）

- **只写／改设计文档**；不要实现 Core，不要扩展 Demo，不要写正式游戏代码。
- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`
- 必读：`34`、`35`、`36`、`2C`、`2E`、`32`、`50` 审计报告
- ADR-0002～0008、0010～0022（UI=0009 预留）
- 未写入冻结文档的内容标「待确定」，不得自行拍板。
- 变更冻结规则：升版本／写 ADR／记 devlog。

## 开工前必读

1. `00-overview.md`
2. `33` **v0.2**
3. `34`／`35`／`36`／`2C`／`2E`
4. `42-devlog.md` 最新 2～3 条
5. `14-borrow-and-differentiate.md`

## 硬性规则（摘要）

1. 文档先于代码；冻结阶段默认不写代码。  
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

## 回答语言

中文。
