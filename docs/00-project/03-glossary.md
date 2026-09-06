# 术语表

> 状态：持续维护 | 最后更新：2026-08-26
>
> 规则：**代码标识符、配置表字段、文档用词必须与本表一致。**
> 新增概念时先来这里登记，再去写代码。这一条是长期可维护性的关键，也是交接时对方最需要的文件。
> 架构冻结相关术语以 **`33` v0.2**／`34`／`35`／`36`／`2C`／`2E` 为准；RPG-First／连续世界以 **`2K`／ADR-0026** 为准。

## 使用约定

- 代码：英文 PascalCase / camelCase，取本表 Code 列
- 配置表 ID：`小写下划线`，取本表 Code 列的 snake_case
- UI 与文档：中文，取本表 中文 列
- 禁止同义词混用（例如不要 Cultivation / Practice / Training 混着指同一件事）

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
| 局部地图 | LocalMap | 独立加载地图 | 洞／秘境／洞府等 |
| 世界地图（旧称） | WorldMap | 同 World | 兼容旧文档 |
| 区域地图（旧称） | RegionMap | 同 Region | 兼容旧文档 |
| 实例地图（旧称） | InstanceMap | 同 LocalMap | 兼容旧文档 |
| 路线 | Route | 跨 Region 旅行路径 | 非瞬移 |
| 遭遇地图 | EncounterMap | 途中临时地图 | 可视为临时 LocalMap |
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
| 后台角色 | Background Character | 非 Party、非 FormalArmy 的我方角色 | 可后台旅行／战斗；WorldMap 不常驻头像；无 Capture 权 |
| 角色方针 | Character Policy | 非 Active 的长期权限／行为倾向（非即时命令） | 如 AllowLeaveFactionTerritory；见 2K |
| 派生位置格 | DerivedPresenceHex | `CanonicalWorldSurfacePosition → WorldToHex` 的**派生查询结果**（不落盘为真源；Site 内经 WorldSiteSpatialMapping） | Site Context 内 ∈ Footprint；ADR-0027 |
| 连续 Hex 世界 | Continuous Hex World | HexWorld=唯一世界拓扑；LocalMap=近景；逻辑连续旅行 | 非必须 Unity 无缝开放世界 |
| 连续世界坐标 | CanonicalWorldSurfacePosition | PlayerParty 在连续世界表面的**唯一物理位置真源**（Wilderness 与 WorldSite 内统一） | `DerivedPresenceHex` 为**派生**；`CurrentHex` 为混合语义（PhysicalDerivedHex／RouteCommittedHex／CurrentWildernessHex，5R-C 分类）；LocalPosition 非持久真源；见 2K §5.8／ADR-0027 |
| 地表出口触发深度 | ExitTriggerDepth | Surface LocalMap 自边界向内的 Exit Trigger 深度（Gameplay） | MapLayout 可配；见 2K §5.8.7／164 |
| 地表出口触发区 | Surface Exit Trigger Zone | 可触发 Hex／Site 边缘过渡的固定几何 ∩ 运行时合法性 | Geometry 固定；Availability 可变；见 2K §5.8.7 |
| 世界定位 | WorldLocation | `AtWorldSite{SiteId}` \| `AtWorldPosition{ContinuousPosition}` | 与 MovementState 分离；Party 共用一个 |
| 移动状态 | MovementState | `Idle` \| `AutoTravel` | 与 WorldLocation 正交；见 2K §5.8 |
| 地点定位 | WorldSite Location Context | 全体 WorldSite（1-Hex／Multi）站内 = `AtSite(SiteId)`；WorldMap 投影 = **CanonicalWorldSurfacePosition**（SiteSpatialMapping 派生，不跳 Anchor） | ADR-0027 取代旧 Aggregated 固定 PresenceHex 投影 |
| 精确世界目的地 | PreciseWorldDestination | ~~WorldMap 点击像素／精确连续坐标作命令目标~~ | **FORBIDDEN（永久）**；WorldMap 命令精度仅 Hex／WorldSite |
| 世界存在 | World Presence | Character／Party／Army 在 HexWorld 上的存在状态 | Party／Background／Army 分层 |
| 自动旅行 | Auto Travel | WorldMap 选 **Hex／WorldSite** 后进入 `MovementState.AutoTravel`；以 Continuous WorldPosition 真实移动（非传送） | Phase 2C 契约（Party）；见 2K §5.8 |
| 手动介入 | Manual Intervention | Party 距 BattleHex ≤1 时亲自参战；不接管 Army | 仅控 Active；见 2K |
| 继承控制 | Succession | Party 全灭后从己方 Site 合格角色重建 Party／Active | 非默认 Game Over；细则见 2K §4 |
| 自动结算 | AutoResolve | 战力悬殊或玩家选择跳过时进行的战斗结果计算 | 战略层瞬时；**不**额外推进 WorldTick；ADR-0023 |
| 暂停即时 | RealTimeWithPause | 战术层时间可暂停下令 | 简称 RTwP；战略冻结时战术暂停仍可用 |
| 接战弹窗 | BattleOffer | 战略相遇后的自动／手动选择 | 产生即冻结 WorldTick |
| 模态遭遇 | ModalEncounter | 手动战略战：锁 Encounter LocalMap | 禁切图／禁战略派参战者 |
| 战略时钟冻结 | StrategicClockFreeze | Offer／Manual／PostBattle 期间不推进 WorldTick | ADR-0023；≠ 第二套世界时间 |
| 战后阶段 | PostBattle | 清场后至点「结束战斗」前；可继续场景操作，WorldTick 仍冻结 | ADR-0023 |
| 参战快照 | BattleParticipantSnapshot | Offer 时强制／可选／敌军与 PreBattle 位置 | ADR-0023 |
| 支援距离 | ReinforcementRange | 大地图世界坐标半径（默认 **0.25**；可调滑块） | ADR-0023／[147](../40-process/147-battlefield-linger-no-teleport-2026-08-21.md) |
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
| 战略军队 | Army / FormalArmy | **正式军事远征组织**；`MemberCharacterIDs[]`；`Army.FactionId` | **不再是**世界移动资格（ADR-0026／2K）；Prototype 见 ArmyStack |
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
| 占领目标 | CaptureObjective | 可占领 Node 的核心建筑／要点 | 全部完成才 Capture；generalize 自 ControlCore |
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

| 中文 | Code | 含义 | 备注 |
|---|---|---|---|
| 控制资产 | Control Asset | 对 Hex 产生政治控制的因果真源 | 有 Owner 的 Fixed WorldSite 或存活 FactionFlag；见 2J |
| 阵营旗 | FactionFlag | Anchor+完整一环的可攻击、非 Character Control Asset | 有 HP；需 War；不进入参战者快照 |
| 建立顺序 | EstablishedOrder | Control Asset 的全局稳定先后序 | 数值越小越早；Capture 不变；first claim tie-break |
| 名义控制范围 | Nominal Control Range | 某 Control Asset 未考虑早到资产时的全部候选 Hex | WorldSite=Footprint+一环；Flag=Anchor+一环 |
| 有效控制范围 | Effective Control Range | 按 EstablishedOrder first claim 后实际获得的 Hex | 可从全部 Control Asset 确定性重建 |
| Hex 领土 | Hex Territory | 单个 Hex 当前由哪个 **正式 Territorial Faction** 政治控制 | `ControlFactionId` 是 Control Asset Resolver 的派生投影，不是因果真源；见 2J |
| 辖区 | TerritoryRegion | 绑定 Primary WorldSite 的兼容地图组织单元 | Runtime `Hexes[]` 由 Control Asset Resolver 重建；不是政治真源 |
| 地点足迹 | WorldSite Footprint | WorldSite 在战略地图上占用的 Hex 集合 | `FootprintHexes[]`；与 Territory 严格分离 |
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
| 控制核心 | ControlCore | LocalMap 层据点控制权所系的核心建筑（Prototype） | 如主管府；**正式占点** generalize 为 CaptureObjective |
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
