# 术语表

> **现行术语：行动小队（Squad）** 是正常活动人物唯一成员组织，单人也是小队；成员各有真实位置。PlayerParty 是玩家 Squad／Active 控制投影。历史 FormalArmy／Hex 输入不是 runtime definition，必须先离线转换为独立的当前格式副本。CharacterEncounter 固定范围、初始双方与未参战候选分离，定义见 [ADR-0035](../40-process/43-decisions/ADR-0035-unified-squads-and-encounter-scope.md)／[ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。

> 状态：持续维护 | 最后更新：2026-09-22
>
> 规则：**代码标识符、配置表字段、文档用词必须与本表一致。**
> 新增概念时先来这里登记，再去写代码。这一条是长期可维护性的关键，也是交接时对方最需要的文件。
> 架构冻结相关术语以 **`33` v0.2**／`34`／`35`／`36`／`2C`／`2E` 为准；RPG-First／连续世界以 **`2K`／ADR-0026** 为准。

## 使用约定

- 代码：英文 PascalCase / camelCase，取本表 Code 列
- 配置表 ID：`小写下划线`，取本表 Code 列的 snake_case
- UI 与文档：中文，取本表 中文 列
- 禁止同义词混用（例如不要 Cultivation / Practice / Training 混着指同一件事）

## 连续世界术语（Current vs Future）

> 真源：[ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)（地图与去 Hex 产品方向）／[ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)（Editor 工具链与旧地图 Content 迁移方向）／[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)。
>
> - **Current（已存在）**：Continuous Surface 是正常 Outdoor 物理真源；Runtime Chunk 为 50×50 Surface Cells；Actual Administrative Control 为 world-space；WorldComposer／FineEditor、Authoring Source 与 Runtime Content 分离、WorldMap Surface LOD 及 MAP-01～04 已实现并封板。
> - **Retired Runtime Dependency**：`hexWorld`／FormalArmy／AtHex／Hex travel 不再由正常产品编译或加载；旧输入只允许在独立离线转换器、稳定 wire 检测、历史资料或明确测试夹具中出现。Separate Space 的 `mapLayout`／`localPlaceSet` 是当前合法格式，不属于旧 Outdoor runtime。
> - **Future（未实现）**：自动水文、道路自动寻路、detail scatter、minor POI、terrain compatibility matrix、Runtime Chunk profiling 等后续制作能力。不得从 MAP 封板推断这些功能已实现。
> - **Authoring Source ≠ Runtime Content**：地图 authoring 源（Composer／FineEditor 编辑）与 bake 后的 runtime 产物不是同一类 JSON；见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) §10～§13。
> - **150×150 Surface Cells** 只表示 Level 1 SiteCore 的**理论行政控制范围**，**不是** Runtime Chunk、World Editor Cell、WorldSite Blueprint 或地图 authoring 最小尺寸。

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 连续世界格 | SurfaceCell | 最小真实连续世界地形格：terrain、walkability、footprint、水、道路与精修单位 | 1×1；早期 local tile 的正式后继 |
| 运行块 | RuntimeChunk | 当前 50×50 Surface Cells 的 Streaming／materialization 技术分区 | 当前 50×50 Surface Cells；**不是制作／authoring 单位**，也不是 Site／行政单位 |
| 大地图编辑格 | WorldEditorCell | 10×10 Surface Cells 的宏观地理制作格；只属于 Authoring | WorldComposer 已使用；runtime 不读取 |
| 据点蓝图 | WorldSiteBlueprint | 任意尺寸 Surface Cell 布局的 WorldSite 精细 authoring 源 | MAP-01 V1 已支持；可跨多个 World Editor Cell／Runtime Chunk |
| 精修块 | DetailPatch | 任意尺寸（最小 1×1 Surface Cell）的局部地形／环境精修覆盖 | MAP-01 V1 已支持；不是 runtime map piece |
| 世界合成器 | WorldComposer | 整张大陆宏观地形、道路／河流、Blueprint／Patch 与 Composition 编辑器 | MAP-01 Production V1 已实现、验收并封板；自动水文等仍属 Future |
| 精细编辑器 | FineEditor | 1×1 Surface Cell 精度的 Blueprint／Detail Patch 编辑器 | MAP-01 Production V1 已实现、验收并封板 |
| 最终连续世界面 | FinalContinuousSurface | Bake 后唯一 Outdoor Runtime 空间真源 | 一大陆一张；当前主 Surface 已接入，source 与 runtime output 分离 |
| 地形／细节确定性展开 | TerrainDetailDeterministicExpansion | 将制作人宏观意图稳定展开成局部地形与环境细节 | 不是 runtime Procedural World Generation |

## 建造系统 V1

- **Construction（建造）**：以静态 `ConstructionCatalog` 列出已解锁建筑，并把 PartyInventory 材料事务性转换为真实世界对象的 RPG 系统；建筑本身不是 Inventory Item。
- **BuildingDefinition（建筑定义）**：Content 中 `type = building` 的静态模板，描述显示信息、PlacementKind、材料成本与主动拆除返还率；不进入 Snapshot。
- **Dismantle（主动拆除）**：玩家主动移除己方建筑并按定义返料的 Construction 操作；与战斗摧毁严格分离。

- **Runtime Constructed Outdoor Asset（运行时建造户外资产）**：玩家建造后进入 `OutdoorConstructedAssetBoard` 的持久物理资产；当前仅 FarmField，记录稳定身份和 Surface 矩形，不保存行政管理者或产权。
- **FarmField（可建造农田）**：普通建筑放置类型，以 Surface cell grid 定尺寸；全 footprint 必须处于发起势力的 Actual Administrative Control。
- **StartingInventory（开局背包内容）**：OpeningScenario 静态初始物资，仅 NewGame 发放；Snapshot restore 以已保存背包槽位为准。

## 核心概念

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 境界 | Realm | 角色修行的纵向阶段 | 全局进度主轴 |
| 小阶段 | RealmStage | 境界内的细分（初期/中期/后期/圆满） | |
| 突破 | Breakthrough | 修为达标后，由玩家主动开始的阶段／境界跨越过程 | 达标只获得资格；结果可为普通、完美、瑕疵、失败 |
| 突破资格 | BreakthroughEligibility | 修为与前置条件达标后允许尝试突破的状态 | 不会自动升级 |
| 突破异象 | BreakthroughPhenomenon | 突破引发的灵气波动、环境或天象反馈 | 随境界增强，可能暴露角色 |
| 护法 | BreakthroughGuard | 突破期间负责警戒、防御、引敌与处理事件的同伴／势力成员 | 突破者无法正常行动 |
| 天劫／渡劫 | Tribulation | 高境界突破逐渐出现的天地考验 | 不是所有突破都有；开始境界待确定 |
| 灵根 | SpiritRoot | 角色对某种天地能量的亲和程度，以数值表示（如火灵根 15/30） | **已与「属性亲和」合并为同一概念**，不再并行两套命名；不是职业或流派 |
| 属性 | Element | 火金土木雷风冰毒等能力倾向 | **不做传统五行相克**；「水」是否保留**待确定** |
| 神识 | SpiritSense | 感知、洞察与控制精神力量的能力 | 所有角色都有；影响社交洞察、探索感知、可控制的法宝／法术数量与精神抗性；不替代关系系统 |
| 悟性 | Comprehension | 理解与学习能力 | 影响学习速度与领悟，不直接提升技能威力 |
| 体魄 | Physique | 肉身属性（`AttributeId.Physique`） | 负重、劳作、部分近战／门槛；**不是**血条 |
| 生命 | MaxHp／CurrentHp | 血条池 | `MaxHp`＝上限；当前值在 `CombatVitalsComponent` |
| 掌握程度 | Mastery | 单个功法／技能的熟练档位 | 初学→入门→小成→大成→圆满→化境（命名**待确定**）；受使用频率、时间、悟性、灵根适配影响 |
| 功法 | Manual | 决定角色如何修炼的成长核心法门 | 不是职业锁；灵根不禁学，只影响效率；品阶黄／玄／地／天；感应境可持有但不能正式修习 |
| 斗技 | CombatArt | 战斗中可释放的核心招式 | 品阶黄／玄／地／天；战斗中最多装备 6 个；与"神通"是否同词**待确定** |
| 技能栏 | SkillBar | 战斗中可即时释放的装备技能位 | 固定 6 格，对应快捷键 1–6 |
| 自动释放 | AutoCast | 技能的手动／半自动／自动释放模式 | AI 细则待设计 |
| 重伤 | Incapacitated | 生命归零后的非死亡状态；亦为 LifecycleState 之一 | 默认不继续攻击；可求饶／威胁／交易；**不是** Dead |
| 生命周期状态 | LifecycleState | Alive／Incapacitated／Missing／Captured／Dead／Removed | Dead≠Removed；ADR-0019 |
| 恢复 | Recovered | 从 Incapacitated 回到 Alive 的结果 | 非长期并行枚举 |
| 失踪 | Missing | 下落不明 | |
| 被俘 | Captured | 被俘 | 可与 FactionRole=俘虏并存 |
| 永久死亡 | Dead | 永久世界状态 | 禁止普通复活撤销 |
| 移出模拟 | Removed | 不再参与当前模拟 | **不等于**死亡 |
| 死亡保护模式 | DeathProtectionMode | None／TemporaryProtection | 默认 None |
| 临时剧情保护 | TemporaryProtection | 显式阶段性免死 | ≠永久无敌 |
| 剧情重要 | IsStoryImportant | 内容标记 | **≠** CannotDie |
| 焦点不可用 | FocusCharacterUnavailable | Focus 失能时的 Agency 标记 | 不立即改玩家身份 |
| 灵力护盾 | QiShield | 额外生命层，非装备盾 | 承伤顺序：灵力护盾 → 肉身生命 |
| 踏空 | SkyWalking | 改变移动规则的高阶空中机动 | 区别于普通飞行；可空中停留、自由转向；归属境界**待确定** |
| 灵气汇聚 | QiConvergence | 将多据点灵气导向主洞府等修炼点 | 占领地盘的核心修炼动机；损耗与上限待确定 |
| 词条 | Affix | 附加在功法/装备上的可组合效果（内容数据） | 构筑深度来源；运行时必须落地为 AttributeModifier |
| 属性修正 | AttributeModifier | 属性管道中的一条可溯源加成／减成 | 见 `33` §1；禁止直接改 Final |
| 灵气 | Qi | 修行消耗/环境资源 | 环境与角色两种含义需区分 |
| 修为 | CultivationPoint | 修行积累的进度值 | |
| 心境 | MindState | 影响修行效率与事件的心理状态 | |
| 心魔 | InnerDemon | 修行失败引发的负面状态/事件 | |
| 弟子 | Disciple | 受玩家管理的角色 | 仅宗门玩法适用 |
| 宗门 | Sect | 玩家经营的组织 | 仅宗门玩法适用 |
| 事件 | Event | 配置化的叙事/抉择单元 | |
| 抉择 | Choice | 事件中的玩家选项 | |
| 世界账本 | WorldLedger | 分册长期世界记忆 | 非万能字典 |
| 关系账本 | RelationshipLedger | **关系唯一真源**；事件历史累积算最终值 | Component 仅缓存；ADR-0017 |
| 关系事件 | RelationshipEvent | Ledger 中一条关系变化记录 | 含 Tick／来源／对象／数值／原因 |
| 社会关系事实 | Social Bond | 角色间客观、可查询的亲子／手足／配偶／师徒／结义事实 | 与主观态度分离；`SocialBondBoard` 为 Runtime authority；见 2M |
| 角色态度 | Social Attitude | Character A→B 的五维单向主观态度 | Affection／Trust／Respect／Fear／Grudge；Ledger 为真源 |
| 依恋强度 | Attachment | 击杀反应使用的临时派生值 | 最强 Bond 基值 + 好感/2 - 仇恨/2；不落盘 |
| 社会反应 | SocialReaction | 社会后果结算后供表现消费的汇总事件 | 每名 reactor 一条，记录实际好感变化与上下文人物 |
| 人格档案 | PersonalityProfile | 角色性格／特质标签集合（Component） | VS0.5-A；Content tags 写入；尚未进 Snapshot |
| 领域事件 | DomainEvent | 刚刚发生的事实 | 见 `2E` |
| 计划事件 | ScheduledEvent | 未来某 Tick 要执行的事 | 禁止系统私有逻辑倒计时 |
| 知识账本 | KnowledgeLedger | 区分世界事实与各主体知道程度 | Known／Suspected／Unknown |
| 传承 | Legacy | 角色死亡后传给下一代的内容 | |
| 秘境 | Realm**Zone** | 可探索的副本区域 | 注意与"境界"英文冲突，故用 Zone |
| 机制能力 | RealmAbility | 由大境界解锁、会改变实际操作规则的超凡能力 | 如飞行、灵气外放、护体 |
| 感应境 | SensingStage | 核心角色的开局境界：能感受天地灵气、进行基础吸收、进入正式修炼准备阶段 | 不是触发解锁；**不是特殊视觉能力**（隐藏信息感知归 `SpiritSense`）；不能学正式功法、不能真正运转灵力 |
| 炼气 | QiRefining | 第一个正式修士境界 | **统一写作「炼气」，不要写成「练气」** |
| 灵力／灵气池 | QiPool | 炼气后可储存、消耗与恢复的灵气资源 | 战斗优先消耗灵气而非生命；与环境灵气 `Qi` 区分 |
| 灵力质量 | QiQuality | 同等数量灵力的凝练程度与实际效能 | 受功法影响；具体表现及是否独立显示**待确定** |
| 品阶 | GradeRank | 功法与斗技的黄／玄／地／天分级 | 影响效率、容量、技能体系与战斗方式，不是纯数值 |
| 引气入体 | QiIntroduction | 感应境角色首次依照功法将天地灵气正式引入并形成运转 | 成功后进入炼气 |
| 反噬 | Backlash | 修炼或突破失败造成的负面结果 | 可为修为下降、伤势、经脉损伤、心境影响或突破障碍 |
| 法宝 | MagicTreasure | 修士以灵气驱动的器物 | 筑基起稳定驭使；主要提供特殊效果与战斗方式变化，非主战力 |
| 驭器 | ArtifactControl | 以灵气操控法宝进行远程攻击与交互 | 筑基解锁；可支持短距离飞行 |
| 灵物 | SpiritItem | 可提升区域灵气或属性倾向的物品 | 同类不无限叠加；不做疯狂摆放 |
| 阵法 | Formation | 由阵法师掌握的高级设施系统 | 如聚灵阵；主要用于宗门阶段 |
| 内丹 | InnerCore | 金丹阶段形成的能量核心 | 属性不同则战斗风格不同 |
| 灵气外放 | QiProjection | 灵气离体形成的攻击或护体形态 | 类似斗气铠甲；金丹阶段 |
| 飞行 | Flight | 不依赖法宝的肉身飞行 | 解锁于金丹还是元婴**待确定** |
| 空间锚点 | SpatialAnchor | 绑定空间虫洞端点的战略设施 | 悟道阶段可建设 |
| 空间虫洞 | SpatialGate | 连接两个空间锚点的快速通道 | 有容量、维护成本，可被破坏 |
| 核心修士 | CoreCultivator | 可逐个养成、装备和战术控制的修士角色 | 长期目标上限约 30–50 人 |
| 凡人 | Mortal | 世界中的普通人口 | 普通凡人群体统计；关键凡人实体化 |
| 重要凡人 | NamedMortal | 拥有姓名、关系、性格与故事的凡人 | 叙事锚点 |
| 身世标签 | OriginTag | 角色的出身与经历标签 | 半固定背景要素之一 |
| 性格标签 | TraitTag | 影响对话、NPC 反应与自动行为倾向 | 半固定背景要素之一 |
| 天赋倾向 | Aptitude | 影响修炼与劳役方向 | 三人分工的依据 |
| 家乡 | Hometown | 角色出身地，地图上真实存在 | 可回访，牵出旧识与家族 |
| 隐藏经历 | HiddenBackground | NPC 身上未主动展示的过去、秘密或机缘 | 需玩家通过聊天、观察或感应挖掘 |
| 据点 | Settlement | 城市区域内可探索、占领、建设和管理的区块 | 荒村、矿山、灵地等；落在格子地图上 |
| 世界地图 | World | 修仙世界顶层 | Freeze v0.2 三层之一 |
| 区域 | Region | 较大连续区域（城市区域） | 可行走／战斗／飞行 |
| 局部地图 | LocalMap | **Current（SPACE-01）**：独立可玩空间 MapLayout（Cave／Interior／Dungeon）；由 SeparateSpaceSession 掌管；不再表示 Continuous Outdoor 近景 | 见 [246](../40-process/246-space-01-separate-space-interior-transition-v1-2026-09-18.md) |
| 独立空间 | Separate Space | 与 Continuous Outdoor Surface 并列的 playable space；进入离开 Outdoor presentation，离开后 exact Surface return | Cave 为第一份样板 |
| 独立空间会话 | SeparateSpaceSession | `LocalMapSession` 收窄后的正式语义：ActiveMapLayout／SpaceKind／Outdoor return／occupants | DTO：`StrategicSnapshotDto.SeparateSpace`；当前 JSON serializer wire 尚未接线，见 247 |
| 世界地图（旧称） | WorldMap | 同 World | 兼容旧文档 |
| 区域地图（旧称） | RegionMap | 同 Region | 兼容旧文档 |
| 实例地图（旧称） | InstanceMap | 同 Separate Space／LocalMap | 兼容旧文档 |
| 路线 | Route | 跨 Region 旅行路径 | 非瞬移 |
| 同源独立遭遇 | Encounter | 玩家实际参与的新战斗所用临时独立空间；取接战地点当前关键地形／建筑 | 首击前统一确认；主世界停表；见 23／ADR-0033 |
| 战术临时坐标 | EncounterLocalPosition | 只在本场独立遭遇中使用的战术位置 | 不提交为主世界旅行；结束后释放 |
| 战前世界锚点 | PreEncounterWorldAnchor | 参与者为本场行动前的 `WorldSpaceId +` 精确世界位置 | 结束时各自回归；只恢复坐标，不回滚战果 |
| 城市区域 | CityRegion | Region 的玩法称呼 | 对齐 Region |
| 格子 | Tile | 最小逻辑空间单位 | |
| 区域出口 | RegionExit | Region 边缘／Route 端点 | |
| 领地 | Territory | 玩家势力控制的一组据点及其人口、资源 | |
| 群体模拟 | PopulationSim | 普通凡人以人口统计／岗位组模拟，不逐人存档 | 地图用代表性群体单位表现 |
| 关键 NPC | KeyNpc | 实体化的重要凡人／功能角色 | 商人、村长、剧情人物等 |
| 小队 | Party | 由少量核心修士组成的行动或战斗编组 | **正式 RPG 编组见 PlayerParty（2K）** |
| 玩家冒险队 | PlayerParty | 当前玩家本人所在少人数 RPG 队：1 Active + Followers；上限 6 | **≠ FormalArmy**；见 [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) |
| 当前主控角色 | ActiveControlledCharacter | 任意时刻玩家唯一可直接即时控制的 Character | 对齐 DirectControl；切换仅限 Party 成员（Succession 例外） |
| 跟随者 | Follower | PlayerParty 内非 Active 成员；AI 控制 | Follow ≡ 加入 PlayerParty |
| 后台角色 | Background Character | 非 PlayerParty、由个人或 NPC Squad authority 管理的真实角色 | 可后台旅行／战斗；WorldMap 不常驻可手操头像；组织类型本身不决定政治接管资格 |
| 角色方针 | Character Policy | 非 Active 的长期权限／行为倾向（非即时命令） | 如 AllowLeaveFactionTerritory；见 2K |
| 派生位置格 | DerivedPresenceHex | `CanonicalWorldSurfacePosition → WorldToHex` 的**派生战略查询结果**，不落盘为位置真源 | 普通户外不经 Site LocalMap mapping，也不 clamp 到 Site Footprint；见 2K／ADR-0031 |
| 旧 Hex 几何 | Legacy Hex Geometry | 历史 Pointy-top Odd-R Q/R 与中心换算规则 | 正常产品不再编译对应 Core 类型；仅离线转换器和历史／测试边界可实现该算法 |
| 旧 Hex wire 字段 | Legacy Hex Wire Fields | Snapshot／JSON 中为检测和离线转换保留的旧 Q/R key | 不代表 runtime Hex 状态；当前输出不得恢复旧位置 authority |
| 连续 Hex 世界 | Continuous Hex World | HexWorld 一度被定义为唯一世界拓扑；LocalMap=近景；逻辑连续旅行 | **Retired runtime model**：产品 authority 已由 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) supersede；历史输入必须离线转换 |
| 连续世界坐标 | CanonicalWorldSurfacePosition | PlayerParty 在连续世界表面的**唯一物理位置真源**（Wilderness 与 WorldSite 内统一） | `DerivedPresenceHex` 为派生查询；`LegacyCurrentHex` 仅兼容摘要／旧路线提交或缓存；LocalPosition 非持久真源；见 2K §5.8／ADR-0027 |
| 世界表面（讨论概念） | World Surface | 长期可能承载 Ground／Flight 连续室外移动的统一二维 Outdoor World Space | **DISCUSSION / NOT IMPLEMENTED**（仅指这个泛化概念本身）；它与已实现的 `Continuous Outdoor World Surface` 不是同一个东西；见 203 |
| 连续室外世界表面 | Continuous Outdoor World Surface | 一个大陆内普通 Outdoor Geography 的真实连续物理世界 | **Current：runtime 已存在并承担正常 Outdoor 物理空间**（ADR-0031）；Indoor / Cave 等独立 Space 不在其中。**Future：新的 World Authoring／Composition／完整 de-Hex 与 Final Surface bake 尚未实现**（ADR-0036／ADR-0037）；不得再把「未实现」读成整个 Continuous Surface 不存在 |
| 世界空间标识 | WorldSpaceId | 标识一个独立 Physical World Space（例如一块大陆或特殊独立世界） | 长期 Outdoor Physical Position = `WorldSpaceId + WorldPosition`；Future 架构 |
| 表面区块 | Surface Chunk（现称 RuntimeChunk） | 连续世界中 Runtime Streaming／materialization 的技术单位 | **不是 Gameplay Boundary，也不是地图制作／authoring 基本单位**；不等于 Strategic Hex；当前 50×50 Surface Cells，未来是否迁 100×100 待 profiling（Open）；见 ADR-0031 |
| 地表出口触发深度 | ExitTriggerDepth | Surface LocalMap 自边界向内的 Exit Trigger 深度（Gameplay） | MapLayout 可配；见 2K §5.8.7／164 |
| 地表出口触发区 | Surface Exit Trigger Zone | 可触发 Hex／Site 边缘过渡的固定几何 ∩ 运行时合法性 | Geometry 固定；Availability 可变；见 2K §5.8.7 |
| 世界定位 | WorldLocation | `AtWorldSite{SiteId}` \| `AtWorldPosition{ContinuousPosition}` | 与 MovementState 分离；Party 共用一个 |
| 移动状态 | MovementState | `Idle` \| `AutoTravel` | 与 WorldLocation 正交；见 2K §5.8 |
| 地点定位 | WorldSite Location Context | 全体 WorldSite（1-Hex／Multi）站内 = `AtSite(SiteId)`；WorldMap 投影 = **CanonicalWorldSurfacePosition**（SiteSpatialMapping 派生，不跳 Anchor） | ADR-0027 取代旧 Aggregated 固定 PresenceHex 投影 |
| 连续世界目标 | PreciseWorldDestination / Continuous Destination | 有效地面点击经统一 WorldMap→Surface 投影得到的 `WorldPosition` 目标 | 复用既有位置真源；同 Hex 不同点击可为不同目标；不是第二套坐标 |
| 世界存在 | World Presence | Character／Party／Army 在 HexWorld 上的存在状态 | Party／Background／Army 分层 |
| 自动旅行 | Auto Travel | **Current**：WorldMap 选 **Hex／WorldSite** 后进入 `MovementState.AutoTravel`，以 Continuous WorldPosition 真实移动（非传送）；WorldMap 仍有 Hex compatibility wrapper。**Future**：WorldMap 以 exact Surface WorldPosition／WorldSite destination 为目标，退出 Hex 选择 authority（ADR-0036） | Phase 2C 契约（Party）；见 2K §5.8 |
| 手动介入 | Manual Intervention | 玩家实际参与现场遭遇或按有限关系／守备规则介入 | 不由 `HexDistance ≤1` 或 FormalArmy 类型授予；见 23 |
| 队内自动接替 | Active Replacement | 当前 Active 失能时按 Party 固定顺序切换到下一名可控成员 | 不按战力排序，不等同势力继承 |
| 势力继承控制 | Faction Succession | 仅 Party 全员真正死亡后，自动选择玩家势力存活可控且战力最高者 | 在继承者原位置继续；空势力终局延期；见 2K §4 |
| 自动结算 | AutoResolve | 战力悬殊或玩家选择跳过时进行的战斗结果计算 | 战略层瞬时；**不**额外推进 WorldTick；ADR-0023 |
| 暂停即时 | RealTimeWithPause | 战术层时间可暂停下令 | 简称 RTwP；战略冻结时战术暂停仍可用 |
| 遭遇准备窗口 | Encounter Preparation / BattleOffer | 新玩家实战在首击／首发弹道前的强制暂停确认；可合并适用建筑战争后果 | 不等于普通可关闭 AutoPause；敌方袭击成立后不能靠关闭免战 |
| 模态遭遇 | Modal Encounter | 同源独立遭遇从准备、战斗、有限收尾到唯一结算的生命周期 | 主世界冻结；结束各回战前锚点并保留战果 |
| 战略时钟冻结 | StrategicClockFreeze | Offer／Manual／PostBattle 期间不推进 WorldTick | ADR-0023；≠ 第二套世界时间 |
| 胜利可结束 | Victory Available | 正式接管完成或本次有效敌人被打倒后取得的结束资格 | 不是最终结算；可继续场内实际接管 |
| 一次收尾 | One Tail Phase | 最终结算前最多一次、先暂停告知的有限续战 | 不满血重开、不递归拉人；状态须可保存 |
| 最终结算 | Final Encounter Settlement | 唯一提交奖励和战果并准备回归的阶段 | 暂停局部时间；不得重复奖励或长期经营 |
| 战后阶段 | PostBattle（Legacy） | ADR-0023 旧称；当前应区分胜利可结束、一次收尾、最终结算 | 不能再用一个状态混合三种职责 |
| 参战状态快照 | Encounter Participation State | 初始同行／正式守备／有限关系候选的判定、在途、到场与各自战前锚点 | 保存事实，不恢复整份战前状态；读档不得重抽 |
| 援军候选范围 | Reinforcement Candidate Range | 开战时有限真实周边候选的内容／调参范围 | 不用固定 0.25 当永久规则；需状态、职责、风险和可达性判断 |
| 接战队列 | BattleInterruptQueue | 同 Tick 多接战确定性串行 | |
| 业力／业障 | Karma | 不当行为积累的长期因果负担 | 按情境判定，**不是单纯杀人罪恶值**；影响道心、气运、突破与渡劫 |
| 功德 | Merit | 护民、正当护持等行为积累的正面因果 | 与业障如何对冲**待确定**；本阶段只记方向 |
| 道心 | DaoHeart | 角色修行信念与稳定程度 | 滥用力量、执念与心魔会损伤道心 |
| 心魔 | HeartDevil | 道心受损或执念过重时的内在反噬 | 影响突破与渡劫 |
| 气运 | DestinyLuck | 角色／势力的长远机缘与顺逆气数 | 高境界滥用力量可能折损气运；细则待定 |
| 天道 | HeavenlyDao | 约束强者滥用力量的世界规则框架 | 不是简单道德裁判，而是力量越大限制越多 |

## 时间与指令

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 时间刻 | Tick／WorldTick | **世界唯一时间轴** | 1 Tick=15 分；96 Tick／日；ADR-0018；接战冻结见 ADR-0023 |
| 行动钟 | ActionClock | 单个 Action 的 Duration 消耗 | **不得**改变世界时间 |
| 世界钟 | WorldClock | 推进 WorldTick 的逻辑时钟 | |
| 行动持续时间 | ActionDuration | ActionClock 计量的剩余／已耗时间 | 例：采集 8 游戏小时 |
| 时段 | TimeOfDay | 清晨、上午、黄昏、深夜等表现层分段 | 仅用于 UI，逻辑层只认 Tick |
| 时间表 | Schedule | 按身份规定的一日义务与自由时段安排 | 社会规则，非死脚本；具体时段走配置表 |
| 时间表权限 | ScheduleAuthority | 能否查看／修改时间表 | 前期只可查看；夺取第一据点后可制定居民时间表；Demo 可临时开放修改 |
| 指令 | Order | 角色想做什么（玩家／AI／日程／事件生成） | 公开概念；**无**额外 Intent 层 |
| 指令队列 | OrderQueue | 单个角色待执行的指令序列 | 每个完整 Character 独立持有 |
| 行动 | Action | 指令分解后的可执行单元 | 必须可序列化；见 `35` |
| 当前行动 | ActiveAction | 角色正在执行的唯一行动 | 第一版无多并行列 |
| 中断上下文 | InterruptContext | 中断原因与损失／检查点信息 | |
| 指令优先级 | OrderPriority | 紧急玩家＞生存战斗＞玩家队列＞义务＞时间表＞需求＞待机 | 优先级≠可执行性 |
| 行为优先级 | ActionPriority | 同 OrderPriority（旧称兼容） | 以 OrderPriority 为准 |
| 自动模式 | AutoMode | 跟随主角或按时间表行动 | 第一阶段无复杂自主 AI；无命令时默认待机 |
| 自动暂停 | AutoPause | 触发条件时把控制权交还玩家 | 重大接敌等；见 `21`／`33` |
| 定义 ID | DefinitionId | 人工维护、可读、稳定的配置 ID | 格式 `namespace:local_id`；与 EntityId 分离 |
| 实体 ID | EntityId | 程序生成、全局唯一的实例 ID | 显示名不能当 ID |
| 实体引用 | EntityRef | 逻辑层对实体的稳定引用 | 禁止 GameObject／Transform |
| 来源引用 | SourceRef | Modifier／效果来源 | |
| 随机源 | IRandomSource | 可注入、可保存状态的随机接口 | 世界保存 WorldSeed；分系统可有独立流 |
| 军队编组 | ArmyGroup | **仅**凡人／大规模非修士军队的聚合数据对象 | ADR-0008 收窄；**不是**修士战略 Army；修士 Army 见 ADR-0024 |
| 修士群体（Legacy） | CultivatorPopulation | ~~第三层普通修士聚合~~ | **ADR-0024 superseded**；修士 = 真实 Character + LOD |
| 旧战略军队输入 | Army / FormalArmy | 历史 Content／Snapshot wire 形状 | Runtime Loader 拒绝；须由 `LegacyRuntimeConverter` 离线转为 `npcSquad`／当前 Snapshot 独立副本 |
| 军队成员归属 | ArmyMembership | Character 当前所属的 Army（若有） | 同时最多 1 支 |
| 势力 ID | FactionId | **全系统统一**的势力身份 ID | Character／Army／Site Owner／Alliance／Vassalage／War 共用；禁止多套平行 ID |
| 节点归属势力 | OwnerFactionId | WorldSite（历史称 WorldNode）占有点归属 Faction | Pure Hex 下 Site Owner；见 2J |
| 军队领袖 | ArmyLeader | Army 的 `LeaderCharacterID` | 代表角色、大地图头像；第一版无统帅 Buff |
| 军队成员 | ArmyMember | Army 中的 Character | 同 Faction；禁止跨势力混编；编组仅能在己方 WorldSite |
| 势力军队上限 | ArmyCapacity | Faction 同时可维持的 Army 数量上限 | ≠ 单支 Army 人数上限；公式未定 |
| 驻留角色 | ResidentCharacter | 位于 WorldSite、未编入 Army／非当前 Party 主旅行态的 Character | **可**后台世界旅行（2K）；旧「不能跨点」已 supersede |
| 驻扎军队 | GarrisonedArmy | 到达己方 Node 后**保持 Army 身份**驻扎的战略单位 | **不**自动解散；仅 Disband 解除 |
| 撤退军团 | RetreatingArmy | 战后逃脱 Character 组成的撤退／流亡 Army | 可奔向仍控领土；Landless 仍保留 |
| 被俘角色 | CapturedCharacter | 战后被俘、失去原控制权的 Character | Lifecycle Captured |
| 联盟成员身份 | AllianceMembership | 独立 Faction 在 Alliance 中的成员资格 | **同时最多 1 个**正式 Alliance |
| 势力好感 | FactionOpinion | Faction A→B 单向喜欢／讨厌 | -100～+100；与 Trust、Threat 独立 |
| 势力信任 | FactionTrust | 是否相信对方 | 喜欢 ≠ 信任 |
| 势力威胁 | FactionThreat | 对对方实力的畏惧 | 可「恨但怕」 |
| 联盟 | Alliance | 平等多势力政治实体 | 独立 Faction 同时最多 1 个；第一版成员战争绑定 |
| 附庸关系 | Vassalage | Overlord ↔ Vassal + Obligations | 附庸内部自治、外交不自治 |
| 宗主 | Overlord | 附庸的上级势力 | 战争状态与附庸绑定 |
| 附庸 | Vassal | 臣服于宗主的独立 Faction | 不能套附庸；不能独立结盟 |
| 臣属义务 | VassalObligation | 附庸对宗主的周期义务 | 含 Tribute 等；周期数值未定 |
| 贡赋 | Tribute | VassalObligation 中的资源贡品 | 使用 Faction Resource Wallet |
| 独立倾向 | IndependenceDesire | 附庸的独立意愿 | 公式未定 |
| 战争（实体） | War | 独立战争对象，多参与方 | 军事占点前提；非仅 stance |
| 占领目标 | CaptureObjective | **Legacy API 名称**；当前指围绕唯一 SiteCore 的攻破与正式接管交互 | V1 不表示同城多个核心，也不要求“全部目标完成”才获得结束资格；只有实际接管改变 Owner |
| 占领区 | CaptureZone | 核心 HP=0 后需持续站立占领的区域 | 可被打断 |
| 无地势力 | LandlessFaction | 失去全部 Node 但未灭亡的 Faction | 仍可活动、战斗、夺地 |
| 势力定义 | FactionDefinition | 势力静态定义（ID、名、类型、视觉） | 不含运行时状态 |
| 剧本势力 setup | ScenarioFactionSetup | 某 Scenario 开局势力状态种子 | 领地、资源、Army、外交 |
| 势力运行时 | FactionState | 当前局内变化的势力状态 | 领土、资源、成员、Army、外交 |
| 凡人群体 | MortalPopulation | 第四层凡人统计模拟 | 关注后才实体化 |
| 势力归属 | FactionMembership | 角色与 Faction 的**成员关系**（`FactionId`） | 可变更；离开保留历史；**不是**另一套 Faction 实体 |
| 势力职位 | FactionRole | 宗主／长老／执事／成员／客卿／俘虏／临时盟友等 | 预定义；≠控制权 |
| 控制权 | ControlAuthority | 玩家可否直接控制／高层命令／纯 AI 等 | 动态权限 |
| 玩家代理 | PlayerAgency | 焦点人物与控制模式容器 | 含 FocusCharacterUnavailable |
| 焦点人物 | FocusCharacter | 玩家当前依附的核心人物 | ≠ DirectControl 目标必然同一 |
| 直接控制 | DirectControl | 可直接下令的控制权 | ≠ FocusCharacter；≠ FactionLeader |
| 控制模式 | ActiveControlMode | Character／FactionLeadership | |
| 杂役弟子／劳役弟子 | LaborDisciple | 开局三人在压迫宗门中的职位 | Freeze v0.2 开局 Role |
| 内容包 | ContentPackage | 官方与 Mod 统一内容单元 | 见 `36` |
| 模组 ID | ModId | ContentPackage 唯一 ID | |
| 命名空间 | Namespace | DefinitionId 前缀 | 官方为 base |
| 资源 ID | AssetId | 逻辑资源引用 | 禁止绝对路径／GUID 当公开 ID |
| 本地化键 | LocalizationKey | 文本键 | |
| 数据迁移 | DataMigration | DefinitionId 改名等数据迁移 | |
| 补丁定义 | PatchDefinition | 显式修改既有定义的补丁契约 | 禁止静默覆盖 |

## Pure Hex 战略空间（2026-08-24 · 真源 [2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)）

> 2026-09-12：本节 Hex 术语只表示战略叠加／摘要；普通户外物理空间与 SiteCore 行政范围分别以 ADR-0031／0032 为准。

| 中文 | English / Code | 定义 | 边界 |
|---|---|---|---|
| 连续户外世界表面 | Continuous Outdoor World Surface | 一块大陆普通户外共享的连续物理空间 | Site／Hex／Chunk 边界不切探索场景 |
| 户外表面空间元数据 | Outdoor Surface Spatial Metric/Coverage | Core 持有的完整 SurfaceId、origin、cell/chunk metric 与 authored chunk coverage | 只回答 Surface 存在、尺度与 membership；不包含河桥、地形或 walkability |
| 世界地点 | WorldSite | 连续表面上的稳定行政地点身份 | V1 一个 SiteCore；不是人物位置真源或一张户外 LocalMap |
| 地点核心 | SiteCore | WorldSite 唯一行政核心；预设议政厅或玩家建立的势力旗 | 议政厅不可拆但可接管；另立旗产生新 Site |
| 行政／建设范围 | Site Administrative and Build Range | SiteCore 等级以 Surface cells 配置、按目标 Surface 的实际 cellSize 解析出的 world-space 理论管辖与建设许可区域 | 可重叠；建筑再按自身地形／占地规则判断；不得把 cell 数直接当 world 单位。Level 1 = 150×150 Surface Cells，只表示理论行政控制范围，**不是** Runtime Chunk／World Editor Cell／WorldSite Blueprint 或地图 authoring 最小尺寸 |
| 实际行政控制 | WorldSiteAdministrativeControl | 由 Surface 精确位置、当前理论范围与领土取得历史唯一解析出的管理 Site；政治 Owner 从该 Site 读取 | 扩张不得追溯夺取他方或同势力其他 Site 的既有控制；Hex/Region 只是投影 |
| 领土取得记录 | TerritoryClaim | 某 Site 在某 Surface 上一次不可改写的矩形范围取得历史，按 AcquiredOrder 决定重叠位置优先级 | 不保存 Faction Owner；核心失效时保留但不参与解析，恢复后沿用原优先级 |
| 飞舟 | Airship | 运输真实人物的空中载具 | 不沿地面过桥；无宣战、占领或地图移动特权 |

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 控制资产 | Control Asset | 对 Hex 产生政治控制的因果真源 | 有 Owner 的 Fixed WorldSite 或存活 FactionFlag；见 2J |
| 阵营旗 | FactionFlag | 可攻击、非 Character 的控制核心资产；正式 Continuous 旗以 authored Surface 世界位置创建唯一 WorldSite/SiteCore，Legacy/debug-only 旗仅保留兼容数据且不进入正常产品 WorldMap | 有 HP；需 War；旗 Core marker 使用精确 CoreWorldPosition；实际行政控制只来自 SiteCore→TerritoryClaim |
| 建立顺序 | EstablishedOrder | 2026-09-06 Control Asset V1 的历史全局顺序字段 | 可用于旧档迁移；不得让旧核心升级按创建顺序追溯抢占既得控制 |
| 理论核心范围 | Nominal SiteCore Range | SiteCore 当前等级产生、尚未解析重叠的行政／建设候选区域 | Footprint+一环只是旧 V1 实现；最终等级范围为后续内容参数 |
| 有效控制范围 | Effective Control Range | 应用既得控制和稳定交接后，实际归某 Site／Faction 管理的区域 | 每个位置／建筑唯一；同势力 Union 不重复计数 |
| Hex 领土 | Hex Territory | 单个 Hex 当前由哪个 **正式 Territorial Faction** 政治控制 | `ControlFactionId` 是 Control Asset Resolver 的派生投影，不是因果真源；见 2J |
| 辖区（旧） | TerritoryRegion | 旧 Content／Snapshot 的历史地图组织单元 | Runtime board 已退休；当前转换器不接收该 authority，不能进入正常 runtime |
| 地点足迹 | WorldSite Strategic Footprint | WorldSite 在战略地图上占用的 Hex 集合 | `FootprintHexes[]`；与 Territory 严格分离，且不等于 Exact Physical Boundary；见 ADR-0031 |
| 锚点 Hex | AnchorHex | Multi-Hex Site 的图标／名称／编辑器参考点／默认镜头焦点 | 禁止作为 PlayerParty AtSite 的实际位置（ADR-0027）；进入用 Footprint.Contains |
| 固定地点 | Fixed WorldSite | 来自 World Content JSON、开局位置固定的 WorldSite | Capture 改 Owner；不因战斗删除 |
| 动态地点 | Dynamic WorldSite | Runtime 生成、可永久摧毁的 WorldSite Instance | 第一版主要用于山贼寨 |
| 山贼寨 | Bandit Camp | Footprint **永远 1 Hex** 的动态 WorldSite | 每寨独立 Bandit Faction；见 2J §4 |
| 领土势力 | Territorial Faction | 可拥有 Hex Territory、外交、MapColor 的正式政治 Faction | 与 Bandit Faction 相对 |
| 非外交敌对势力 | Non-Diplomatic Hostile Faction | 永远敌对、无外交、无正式 Territory 的 Faction 类型 | 动态山贼寨使用；仍用统一 FactionId |

> Legacy：**OwnerFactionId**（WorldNode `ownerId`）在 Pure Hex 下 supersede 为 Hex **ControlFactionId** + Site **OwnerFactionId**（Fixed Site）；详见 2J Supersede 表。

## 义务与隐匿

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 每日配额／每日任务 | DailyQuota | 主管／监工下达的当日任务指标 | 由若干条目组成，有验收时限 |
| 管事弟子 | StewardDisciple | 宗门派来管理外门劳役产出的弟子 | 约筑基初期，前期第一卡点 |
| 监工 | Overseer | 管事弟子手下的凡人管理者 | 负责点名、验收、巡逻安排 |
| 主管 | Supervisor | 前期对玩家下达任务并执行惩戒的管理角色 | 可能是管事弟子本人或其代理人；具体人设待设计 |
| 主管愤怒 | SupervisorAnger | 管理者对玩家小队的不满与警惕 | 属于 NPC 怀疑／态度反馈的一种表现；**不与**个人隐匿风险合并 |
| 口粮 | Ration | 完成任务换取的官方食物 | 维持生存，对修炼无益 |
| 个人隐匿风险 | PersonalConcealmentRisk | 玩家暗面行为（偷修等）被察觉的自身风险 | 受地点、时间、敛息等影响；Demo 的 ExposureRisk 映射本层 |
| 怀疑度 | Suspicion | **具体 NPC** 对玩家异常的怀疑程度 | 按 NPC 实例记账；影响巡查／对话／调查 |
| 势力敌意 | FactionHostility | 整个势力对玩家的敌对／敌意态度 | 影响追杀、招募、势力级事件；与个人／NPC 层分立 |
| 私藏物 | Contraband | 未上报的私有物资 | 被搜出则没收 |
| 藏匿点 | Stash | 存放私藏物的地点 | 属性为容量与隐蔽度 |
| 敛息／敛息草 | BreathConcealment | 短时间隐藏修为气息的资源或手段 | 非永久；需持续采集；炼气后隐藏身份的核心工具 |
| 地点核心／控制核心 | SiteCore / ControlCore | 一个 WorldSite 唯一的行政核心；预设议政厅或创建新 Site 的旗 | 议政厅不可拆、可升级／接管；防御击破不等于删除 |
| 接管目标 | CaptureObjective（Legacy API name） | 围绕唯一 SiteCore 的攻破与正式接管交互 | V1 不表示同城多个核心全做完；只有实际接管改变 Owner |
| 斩首夺权 | DecapitationCapture | 直接攻击控制核心的占领方式 | 快，危险 |
| 学校／学塾 | Academy | 定期刷新人才候选的领地建筑 | 约每 2～3 游戏月；可收弟子或任命管事 |
| 管事 | Steward | 负责凡人治理的任命角色 | 不要求修炼天赋；与开局“管事弟子”不同，此处指玩家任命的治理职 |
| 掩护 | Cover | 指派角色引开靠近同伴的 NPC | 降低被保护者的怀疑度积累 |
| 秘密灵地 | HiddenQiSite | 营地外灵气浓度显著更高的地点 | 前期偷偷修炼的核心目标 |

## 待定名词

| 中文 | 状态 |
|---|---|
| 飞行的解锁境界 | 待确定：本次方向倾向金丹，仍待最终确认 |
| 踏空的解锁境界 | 待确定 |
| 战略空间网络归属 | 待确定：悟道／羽化／分层 |
| 法器 / 法宝是否分两级 | 待确定 |
| 斗技 / 神通 / 技能 统一用词 | 待确定，避免同义词混用 |
| 灵根 与 属性亲和 是否合并 | **已合并**：统一称灵根 |
| 「水」属性是否保留还是并入冰 | 待确定 |
| 掌握程度六档的最终命名 | 待确定 |
| 神识 / 灵魂力量 用哪个词 | 待确定，暂用神识 |
| 凡人分层模拟的层级命名 | **2026-08-22 修订**：修士 = 真实 Character + LOD（ADR-0024）；凡人 = MortalPopulation 聚合 |
| 传统五行生克 | **明确不做**为核心规则 |
| `[新增概念先登记在这里]` | 待定 |

> 早前版本曾登记过「周天调息」「灵气锻体」「灵力灌注」「丹相」以及炼气四候选能力等提案词。以最新境界／修炼文档为准；未确认者勿当既定设计使用。


## 2026-09-15 SiteCore Warfare

| 术语 | 正式含义 |
|---|---|
| WorldSiteCoreTarget | 只读的 Site/核心资产身份、类型、当前 Owner、Surface 与精确核心位置描述；类型由 CoreIsRemovable 判定 |
| WorldSiteCoreWarfareService | 玩家 SiteCore 战争统一领域入口；真实守军查询、目标约束、占领争夺与原政治服务交接 |
| SiteCoreEncounterObjective | CharacterEncounter 内最多一个未完成的战略核心目标；FixedSiteCoreCapture 或 RemovableFactionFlagDestruction，保存目标身份与结果，不复制物理或政治权威 |
| ObjectiveDefenderSquads | 当前遭遇因战略目标追加的真实防守小队事实；独立于被冻结的关系候选，不重置原 roster |
