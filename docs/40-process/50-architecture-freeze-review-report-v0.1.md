# Architecture Freeze Review Report v0.1

> 状态：审计报告（只读结论） | 日期：2026-07-31  
> 范围：架构冻结文档包及其增量（Entity／Modifier／Event／Order／时间／地图／PlayerAgency／死亡／ContentPackage／存档随机）  
> 方法：交叉阅读 `33`／`34`／`35`／`36`／`2C`／`2E`／`32`／`31`／`27`／`28`／`24`／术语表／ADR-0001～0016，对照 12 项必查项  
> **本报告不修改冻结形状；不编写正式代码。**

---

## 0. 总判

架构冻结**主轴一致、可进入人工终审**。Unity→Order→Action→DomainEvent／Ledger／Modifier→Snapshot 的数据流在核心文档间对齐；ECS／Intent／事件溯源回放／任意脚本 Mod 等旁路已被明确禁止。

仍有一批**文档间冲突**与**宣称已冻结但字段未闭合**的项，建议在开写 Core 前先做一次「小修补」（尤其是 `24` 地图正文、隐匿术语、Relationship 权威归属、FocusCharacter 失能规则）。

---

## 1. 已一致

| # | 主题 | 结论 |
|---|---|---|
| 1 | 数据流边界 | `33`／`34`／`35`／`32` 一致：Unity 只产 Order、读 Snapshot／Event；禁止直接改 Core |
| 2 | 无公开 Intent | `33`／`35`／`32`／术语表一致 |
| 3 | 非 ECS 组合模型 | `33`／`34`／ADR-0002 一致 |
| 4 | Modifier 公式 | `33` §2 与 `2C` 公式一致：`(Base+Fixed)×(1+Σ%)` → Special → Clamp |
| 5 | 属性≠状态池 | `2C` 明确；与禁止直接改 Final 一致 |
| 6 | 事件三层 | DomainEvent／ScheduledEvent／WorldLedger 在 `33`／`2E` 一致；禁私有逻辑倒计时 |
| 7 | 快照存档 | `33`／`2E`／ADR-0005：非 Event Sourcing、非完整回放 |
| 8 | 随机 | `IRandomSource` + WorldSeed + 分系统流；存档含随机状态 |
| 9 | Order／Action 职责 | Order=想做什么，Action=怎么做；单 ActiveAction；可序列化 |
| 10 | 时间刻度 | 1 Tick=15 分，96 Tick／日；暂停／倍速全球统一 |
| 11 | 四权分离声明 | Membership／Role／Relationship／ControlAuthority + PlayerAgency 在 `33`／`34`／ADR 一致 |
| 12 | 永久死亡默认 | `33` §19／`34`／ADR-0010：Important≠不死；TemporaryProtection 显式 |
| 13 | Lifecycle 枚举集合 | Alive／Incapacitated／Missing／Captured／Dead／Removed 在 `33`／`34`／`2E` 出现且语义同向 |
| 14 | ContentPackage | 官方=包、命名空间 ID、禁静默覆盖、白名单 Effect：`36`／`33` §21／ADR-0013～0016 |
| 15 | DefinitionId／EntityId | 分离 + `namespace:local_id`：`33`／`34`／`36`／术语表 |
| 16 | 离屏原则 | “持续模拟≠全图常驻渲染”；逻辑真源在 Core：`33` §8／ADR-0007 |
| 17 | Mod 不绕过契约 | Effect 须经 Order／Action／Event／Modifier／Ledger |

---

## 2. 存在冲突

| ID | 冲突 | 涉及 | 建议 |
|---|---|---|---|
| C1 | **地图模型过时**：`24` 仍写「三级结构」「城市区域约 10 屏」；`33` 已冻 WorldMap／RegionMap／Instance／Route，尺寸可变、废止统一 1.5 屏 | `24` vs `33` §7 | **以 `33` 为准**修订 `24` 正文与状态栏；总览已更新，`24` 未跟 |
| C2 | **隐匿状态值命名**：`2C` 写 `ExposureAccumulation`；术语／`2F`／`33` 用 `PersonalConcealmentRisk`；Demo 用 `ExposureRisk` | `2C`／术语／`49` | 正式统一为 `PersonalConcealmentRisk`；Demo 名仅留在 `32`／`49` 映射 |
| C3 | **`33` 章节编号错乱**：§16 后跳 §19～21，再出现 §17～18 | `33` | 重排为连续章节（编辑性，不改内容） |
| C4 | **Action 推进时钟表述含糊**：`35` 写 Advance「由 ActionClock／Tick 驱动」；离屏队伍是否只有 WorldTick、无 ActionClock 未写清 | `35`／`33` §3／§8 | 明确：镜头内 ActionClock 细分推进；离屏用 WorldTick 低频 `Advance` 或等价结算，**同一 Action 状态真源** |
| C5 | **Relationship 双存放**：`RelationshipComponent` 与 `RelationshipLedger` 并存，未规定谁为权威、何时同步 | `34`／`2E` | 冻结：Ledger 为跨实体权威；Component 为角色侧缓存／索引，或反过来——必须二选一 |
| C6 | **DisplayName vs LocalizationKey**：`IEntity.DisplayName` 与内容包禁止裸字符串／要求 LocalizationKey 可能冲突 | `34`／`36` | 规定 DisplayName 仅为调试／运行时解析缓存，配置真源为 LocalizationKey |
| C7 | **FactionControl／CharacterControl**：`33`／`34` 使用，术语表未登记；与 `ControlAuthority`／`ActiveControlMode` 关系未定义 | `33`／`34`／术语表 | 标明为权限结果别名或删除，避免第三套词汇 |
| C8 | **Incapacitated 双重语境**：战斗「倒下」与 Lifecycle `Incapacitated` 共用一词；`Alive` 描述「受重伤规则约束前」易误解为 Alive 不含重伤 | `34`／术语／`23` | 明确：Lifecycle=Incapacitated 包含战斗倒下；或拆 `Downed` 子状态——需人工定 |
| C9 | **`01-vision` 过时**：仍可能残留「纯策划／不写代码」类阶段性过时句（相对 Demo 已做完） | `01-vision` | 非架构形状冲突，但易误导新会话；建议改阶段说明 |

---

## 3. 缺少定义（宣称冻结／形状已定，但关键字段或生命周期未闭合）

| ID | 缺什么 | 风险 | 可否开 Core 骨架 |
|---|---|---|---|
| M1 | **ControlAuthority** 的权威存储：组件？由 PlayerAgency 计算？枚举值未成表 | 实现时易重新合并进 IsPlayer | 骨架可先用查询服务；**写入控制逻辑前必须补** |
| M2 | **FocusCharacter 死亡／Captured／Missing** 时 PlayerAgency 规则 | 游戏无法继续或非法空焦点 | **编码玩法前必须人工定** |
| M3 | **TemporaryProtection** 配置 schema（字段类型、替代后果枚举、与事件定义绑定） | 内容无法配置 | 可后置到内容管线；战斗结算前要有最小 schema |
| M4 | **Removed vs Dead** 边界与存档裁剪规则 | 归档误删历史 | 可后置，但存档格式冻结前要定 |
| M5 | **AttributeId** 完整枚举／表 | Modifier 无法落地测试 | Core 第一阶段用最小子集即可 |
| M6 | **离屏 Action Advance 频率与确定性** | 测不准、不同步 | 第一阶段可只做「同 Region 全仿真」；多区前必须定 |
| M7 | **开局三人 FactionMembership** 初始值（空／隐式小队势力／无势力） | 离开／建村事件难写 | **人工判断** |
| M8 | **Building／Settlement／Faction** 最小组件清单 | 不影响 Character 骨架 | 可后置 |
| M9 | **Party** 存档结构与 ControlledEntityIds 关系 | 多队切换 | 多队里程碑前补 |
| M10 | **EventType／Condition／Effect** 第一批正式清单（现为示例） | Mod Ready 无法验收 | 阶段 B 前补最小集 |
| M11 | **IEntity.LifecycleState** 对非 Character 含义 | Building 的 Dead？ | 可后置；或规定仅 Character 使用完整枚举 |
| M12 | **随机流命名与数量**（哪些系统独立流） | 存档字段不稳定 | 第一阶段：World + Combat + Loot 三流即可 |

---

## 4. 可后置（不阻断 Core 第一阶段）

- ArmyGroup 视觉代理数量、全面战争表现（ADR-0008 已划边界）
- Knowledge 传播细则、History 压缩策略
- Patch 冲突自动合并、Workshop、热重载、事件可视化编辑器（`36` 阶段 D／E）
- 完整 GOAP／每类巨型行为树（已明确不做）
- 多乘区、功法独立计算序（已明确暂不做）
- 正式 UGUI（ADR-0009）
- `24` 地图编辑器、完整 Chunk 策略细节
- 突破术法清单、第一次突破完整事件池（玩法内容，非骨架）
- Steam／外部 SDK

---

## 5. 必须人工判断

1. **FocusCharacter 失能／死亡后玩家如何继续？**  
   候选：强制切换到 ControlledEntityIds 下一可控者／进入旁观选继承人／允许读档外「接管」仪式。  
   **建议：** 若仍有 `ControlAuthority=Direct` 的存活同伴 → 自动切换焦点并强提示；若无 → 进入「势力残局／旁观」或游戏结束分支（需你选）。

2. **Relationship 权威在 Component 还是 Ledger？**  
   **建议：** `RelationshipLedger` 为唯一真源；Component 只作加速索引，禁止两处可写。

3. **开局三人是否已有 FactionMembership？**  
   **建议：** 开局 Membership=空或 `base:faction_unaffiliated_labor`；建村夺权后才出现玩家势力 ID——避免「离开一个还不存在的宗门」。

4. **Incapacitated 是否等于战斗倒下？**  
   **建议：** 是同一 Lifecycle 值；战斗「求饶／处决」是其上的交互，不另造平行枚举。

5. **离屏战斗／行动保真度底线？**  
   **建议：** Core 第一阶段不做跨 Region 低频 Action；先保证单 Region 真源一致，降低 C4 风险。

6. **是否允许 DisplayName 运行时缓存？**  
   **建议：** 允许，但序列化存档应存 LocalizationKey／DefinitionId，不把玩家改名以外的显示串当 ID。

---

## 6. 十二项必查对照

| # | 检查项 | 结果 |
|---|---|---|
| 1 | 旁路改 Core | **通过（文档层）**；实现期靠 asmdef + API 审查 |
| 2 | 状态单一权威拥有者 | **部分通过**；卡在 Relationship 双写（C5）、ControlAuthority 存储（M1） |
| 3 | 术语一致 | **部分通过**；C2／C7／C8／C1 |
| 4 | 基础引用类型统一 | **通过**；`34` §7 列表完整，存档／事件均引用 |
| 5 | Tick／ActionClock／ScheduledEvent 职责 | **大体通过**；C4 需补一句离屏规则 |
| 6 | Action／Event／Modifier 越权 | **通过（形状）**；合法链路已写；白名单 Effect 仍是示例 |
| 7 | 场景与离屏同一逻辑真源 | **原则通过**；缺 Advance 频率（M6） |
| 8 | 五权／Agency 分离 | **声明通过**；存储与初始 Membership 未闭合（M1／M7） |
| 9 | 死亡／保护／失踪／被俘／重伤 | **枚举通过**；Removed、Focus 失能、保护 schema 未闭合（M2～M4） |
| 10 | 官方与 Mod 统一包 | **通过** |
| 11 | 过度抽象 | **轻度存在**：ArmyGroup／全量可选组件／Patch 契约超前但已标阶段；可接受 |
| 12 | 冻结宣称 vs 未定义字段 | **存在**；见 §3（M1～M12） |

---

## 7. 新增／修改文件清单（架构冻结文档包累计）

### 本审计新增

- `docs/40-process/50-architecture-freeze-review-report-v0.1.md`（本文）

### 冻结包核心（既有）

| 文件 | 角色 |
|---|---|
| `docs/30-tech/33-architecture-core-rules-freeze-v0.1.md` | 主契约 |
| `docs/30-tech/34-entity-and-component-model.md` | 实体／生命周期／Agency |
| `docs/30-tech/35-order-and-action-system.md` | Order／Action |
| `docs/30-tech/36-content-package-and-mod-architecture.md` | Mod Ready |
| `docs/30-tech/32-prototype-to-product-bridge.md` | Demo 映射 |
| `docs/20-systems/2C-attributes-and-modifier-pipeline.md` | Modifier |
| `docs/20-systems/2E-events-and-world-state.md` | 事件／账本／存档 |
| `docs/30-tech/37-feishu-sync.md` | 飞书工具（非玩法契约） |
| `docs/00-project/00-overview.md`／`03-glossary.md` | 索引与术语 |
| `docs/40-process/41-roadmap.md`／`42-devlog.md` | 过程 |
| `AGENTS.md` | AI 硬规则 |
| `docs/20-systems/27`／`28` 等 | 系统对齐 |

### 已知未跟上的关联文档

- `docs/20-systems/24-world-and-settlements.md`（C1）
- `docs/00-project/01-vision.md` 阶段表述（C9）

---

## 8. ADR 清单

| ADR | 主题 | 状态 |
|---|---|---|
| 0001 | Unity 2022.3.6f1 Built-in | 已采纳 |
| 0002 | 不采用 Unity ECS | 已采纳 |
| 0003 | WorldTick + ActionClock | 已采纳 |
| 0004 | CSV／JSON 混合真源 | 已采纳 |
| 0005 | 快照存档，非完整回放 | 已采纳 |
| 0006 | 分层地图与 Route | 已采纳 |
| 0007 | 多队伍分级模拟 | 已采纳 |
| 0008 | ArmyGroup 群体+视觉代理 | 已采纳 |
| 0009 | 正式 UI（预留，未写） | 预留 |
| 0010 | 默认永久死亡 | 已采纳 |
| 0011 | PlayerAgency | 已采纳 |
| 0012 | 四权分离 | 已采纳 |
| 0013 | Mod 分阶段／当前 Ready | 已采纳 |
| 0014 | 统一 ContentPackage | 已采纳 |
| 0015 | namespace DefinitionId | 已采纳 |
| 0016 | 禁任意脚本 Mod | 已采纳 |

---

## 9. 仍待人工判断的问题（精简）

1. FocusCharacter 死亡／被俘／失踪后的接管或终局规则  
2. Relationship 唯一写入口（建议 Ledger）  
3. 开局 FactionMembership 初值  
4. Incapacitated 与战斗倒下是否同一状态（建议是）  
5. Core 第一阶段是否包含跨 Region 离屏 Action（建议否）  

---

## 10. 建议的正式 Core 第一阶段实现范围

**目标：** 用最小可测骨架证明契约，而非一次实现完整游戏。

### 10.1 应包含

1. asmdef：`XianXia.Core`／`Data`／`Unity`／`Tests`（Unity 零引用 Core）  
2. 基础类型：`EntityId`、`DefinitionId`（含 namespace）、`Tick`、`SourceRef`、`LocationRef`、`IRandomSource`（WorldSeed + ≥1 子流）  
3. **最小 ContentPackage 加载**：仅 BaseGame 测试包（角色模板／物品／一事件）——验证官方也走包  
4. WorldClock → WorldTick；暂停／倍速；**单 Region 全仿真**（暂不实现跨区低频 Action）  
5. Character 最小组件：Identity／Attributes／ActionState／Location／Inventory／Lifecycle／FactionMembership  
6. AttributeModifier 管道 + 3～5 个 AttributeId  
7. OrderQueue + ActionRunner：Move／Gather／Cultivate 对齐 Demo 语义  
8. DomainEvent 总线 + ScheduledEvent 队列 + 最小 Resource／History 记录  
9. 快照存档：实体＋ScheduledEvent＋随机流＋SaveVersion＋启用包列表  
10. PlayerAgency（Character 模式为主）；ControlAuthority 先做「可直接控／不可控」两档  
11. Lifecycle：至少 Alive／Incapacitated／Dead；TemporaryProtection 可读配置但可不接复杂替代池  
12. 回归：Demo 操作可在新骨架上复述（三人分工劳动＋灵地入定）

### 10.2 明确不包含（第一阶段）

- 跨 Region Route／离屏多队保真  
- FactionLeadership 完整经营 UI  
- 真战斗伤害与技能树  
- 第一次突破完整事件  
- Mods/ 文件夹与 Workshop  
- 完整 Knowledge 传播、ArmyGroup 入镜  
- 正式 UGUI  

### 10.3 进入编码前建议先改的文档（小修补）

1. 修订 `24` 对齐地图四类（消 C1）  
2. `2C` 隐匿字段改名对齐术语（消 C2）  
3. `33` 章节重排（消 C3）  
4. `35` 补离屏 Advance 一句（消 C4）  
5. `34`／`2E` 写明 Relationship 权威（待你确认建议后）  
6. 登记 FactionControl 等别名或删除（消 C7）  

---

## 11. 审计结论一句话

**可以进入「人工终审 → 小修补 → Core 第一阶段」；不应在未处理 C1／C5／M2 的情况下直接铺开多区域与势力领导权实现。**

---

*报告结束。未编写正式游戏代码。*
