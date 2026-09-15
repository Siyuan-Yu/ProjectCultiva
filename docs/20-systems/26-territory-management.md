# 领地经营

> 状态：SiteCore 行政管理最终设计已确认；CW-04／CW-04.5 Producer Accepted / Sealed；CW-05A/B/Closing Producer Accepted / Sealed | 优先级：P0 | 最后更新：2026-09-14
> 上级：`docs/00-project/00-overview.md`
> 关联：`25-cultivation-and-breakthrough.md`、`24-world-and-settlements.md`、`27-characters-and-population.md`、`22-realms-and-abilities.md`、**[2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)**
> **Hex Territory / TerritoryRegion / Capture 后整块易主：** 正式规则见 **[2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)**（2026-08-24）。本文 §2「Strategic Node」术语在 Pure Hex 下对应 **Fixed WorldSite + TerritoryRegion**。

## 1. 这个系统解决什么问题

领地是修炼的供养来源，也是"从个人成长为势力"的载体。它必须在规模扩大时不增加等比例的微操负担。

**核心动机不是为了扩大地图面积，而是为了获得修炼优势。**

成长闭环：

```
势力改变资源 → 资源推动修炼 → 修炼推动更高境界 → 境界改变能力与玩法
```

**取得第一个据点后才解锁**，之前不显示任何经营界面。

<a id="sitecore-administration"></a>
## 2. SiteCore 覆盖、重叠与管理接续（2026-09-12）

- 核心理论范围允许重叠，不禁止邻近建旗、升级或相邻发展。同势力范围以并集显示和统计；每个位置／建筑的实际行政管理必须唯一。
- 不同势力保留先取得的有效控制。核心升级新增范围不能抢走他方既有土地；同势力多个 Site 也使用稳定的有效先占／交接规则。实现前核对控制历史数据和平局算法。
- 接管议政厅整体移交该 Site 现有实际行政控制，保留 Site 身份、名称、等级、核心位置和实体资产；不吞并其他 Site 的土地，也不自动改变居民／守卫的关系、势力成员资格或忠诚。
- 拆旗只移除控制声明并重算覆盖，不删除建筑、不清库存、不恢复生命／损伤。有其他 Site 接续时转交管理；无接续时暂停依赖行政管理的生产／功能，建筑独立交互继续按自身规则。
- 行政管辖和允许建设使用同一范围；水面／岸边等由建筑本身判定。所有权／范围变化不移动人物或重置日程、出生点和移动命令。

完整决策见 [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)。CW-04 已以持久化 `TerritoryClaim` 取得历史取代 Site `EstablishedOrder` 的实际控制职责：理论范围允许重叠，实际位置按最早有效 Claim 唯一解析；核心升级只新增较晚 Claim，不能抢走已有管理。CW-04 已于 2026-09-14 经制作人人工验收并封板。

WorldMap 正常产品势力范围直接绘制 Actual Administrative Control 的 world-space 矩形片段及真实边界，不再从 `HexCell.ControlFactionId`／`TerritoryRegion.Hexes` 反推轮廓。CW-04.5 已实现并封板按 Faction 的只读视觉 union：同势力相邻 Site 不画内部政治边界，断开的领土仍是独立 pieces；Site identity、Claim 与唯一 Actual Manager 保持独立，内部 Site 边界继续由 per-Site builder 和 diagnostics 查询。Strategic Hex Projection 仅保留作旧系统兼容、摘要统计和显式 debug。

CW-05A 明确 **Stateful World Object != Administrative Asset**。tree／wall／farm 都可有持久 identity 与 physical-natural state，但当前只有 Farm Plot 显式进入 Administrative Asset semantics。Farm 管理锚点从 checked-in Continuous Surface placement 的 StableId、SurfaceId 和 cell center 派生；当前 managing `WorldSite` 每次经 `WorldSiteAdministrativeControlResolver` 查询，不保存 Site/Faction owner。核心失效只改变 manager 查询结果，CropStage、Growth 与 StableId 不变；无 manager 是合法状态。树墙仍保存 HP/Destroyed，但附近 Site 只能提供 Territorial Context，不是 Tree/Wall Administrative Manager。Physical Asset Identity、Property Ownership、Administrative Manager 相互独立，本轮未实现产权。无人管理不等于时间停止：Growing 作物由 Core WorldTick 推进，即使 chunk 未加载仍生长。CW-05B 已让玩家下达的整片农作按真实 farm cell `StableCellId` 消费该动态行政管理结果，并在逐格选工、开工与完成前复核；NPC schedule、Settlement production 与 generic construction 延后 Economy / Automated Settlement Production migration。CW-05A/B 与 CW-05 Closing 已通过统一正常玩法验收，2026-09-15 Producer Accepted / Sealed。

## 20. 历史战略 Node 占领（2026-08-22）

战略层占点规则以 [2A 势力、军队、外交与战略占领](2A-factions-armies-diplomacy-and-capture.md) 为准。本节保留 LocalMap 层「夺取控制权」体验方向，并 generalize 术语。

### 20.1 CaptureObjective（占领目标）

`CaptureObjective` 是既有 API／内容名称；当前 V1 每个 WorldSite 只有一个 SiteCore。预设 Site 的议政厅／主管府等核心固定存在、不可拆除，可被攻破防御并由真人通过正式交互接管；玩家另立势力旗会创建新的 Site，而不是给同一 Site 增加第二核心。

2026-09-14 Content migration：正式预设势力旗与玩家新建旗共用同一语义。Content 明确保存 `SurfaceId + WorldPosition + createsWorldSite + CoreLevel`；加载后用稳定 `SiteIdForCoreFlag(flagId)` 创建唯一 WorldSite，按既有 `EstablishedOrder` 与预设议政厅共同建立 baseline Claim。未明确 authored 精确位置的兼容旗必须显式 `legacyDebugOnly=true`，不从 Hex 猜测行政范围，也不进入正常产品 WorldMap marker。

WorldMap 的 Core marker 按 `Site.CoreAssetId → FactionFlag` 正式身份区分：旗 Core 在 `Site.CoreWorldPosition` 只画一面旗，议政厅及普通 Site 继续使用房屋表现。CW-04.5 正常产品按 Faction union 显示；内部 Site 行政边界只用于 diagnostics，Site、Claim 和实际管理权从未合并。

Continuous 势力旗放置由两层共同完成：Host CompositeWalkGrid 校验当前加载状态下真实4×4建筑占地与动态 blocker；Core `OutdoorSurfaceSpatialAuthority` 只校验完整 Surface identity/coverage/metric，再执行政治规则。Strategic Hex passability 与局部 `SurfaceGroundNavigation` coverage 均不得否决精确 Continuous 建造。同势力 actual territory 内可建立新 Site；不同势力 actual manager 拒绝。理论范围继续允许任意重叠，AcquiredOrder 不变。

| 据点类型 | CaptureObjective 示例 |
|---|---|
| 荒村／资源点 | 主管府（Prototype 已实现为 `ControlCore`） |
| 城市 | 城主府／议政厅（该 Site 的唯一核心） |
| 宗门 | 宗门大殿／阵枢（该 Site 的唯一核心） |

玩家攻击 SiteCore 防御 → **耐久（HP）归零** → 进入可占领状态 → 进攻方角色进入正式接管区域并持续交互 → 接管完成。敌方 Character 可攻击占领者、打断接管。只有接管完成才改变 Owner；“接管完成 OR 打倒本次有效对手”只是战斗结束资格，不表示打倒人物会自动送地。

**无需杀光**地图上所有敌人才能夺取节点。

### 20.2 军事占领前提与 Owner 规则

- **必须处于 War** 才能军事夺取 Node（见 [2A](2A-factions-armies-diplomacy-and-capture.md) §29）
- 成功后 **Owner 直接易主**；**不做** Owner／Controller 双层、不做 Occupied Territory 中间态
- 未来可通过**交易／外交转让**改变 Owner —— **本阶段不做**

### 20.3 Node Defense（节点防御）

防御力量来自真实世界状态：

- Node **Resident Characters**
- **Garrisoned Armies**
- **Formation**／阵法
- 未来其他防御设施

**禁止**按 Node 等级临时凭空生成匿名修士守军。

Resident Character 是否响应或跨区行动取决于职责、动机、发现、可达性、AI／Policy 与真实移动计划；不要求先组成 Army。远方角色仍不开放逐人 RTS 指挥。

### 20.4 手动攻点收尾（2026-08-22 · 2A）

议政厅可在同源战场内按正式交互接管。正式接管完成或打倒本次有效对手任一成立即可获得结束资格；只有实际完成接管才改变 Owner。残存守军不被自动删除或收编；见 [2A](2A-factions-armies-diplomacy-and-capture.md) §37、[23](23-combat.md) §12.2。

| 方式 | 做法 | 状态 |
|---|---|---|
| **军事接管** | 具备有效 War 授权并在战场内按正式条件接管议政厅 | 接管改变 Owner；仅打倒对手不送土地 |
| **逐步瓦解** | 清理威胁、争取支持、再夺核心 | 与军事攻点可组合 |
| ~~**外交接管**~~ | 利用关系、谈判和平获得控制 | **superseded** → 未来「交易／外交转让」方向；**不作为本阶段占点实现** |

## 3. 据点是可发展的区域

据点**不是固定不变的资源点**，而是可以发展的区域。

玩家可以在占领的区块上建设，例如：

| 类别 | 示例 |
|---|---|
| 修炼 | 洞府 |
| 生产 | 生产建筑、仓储 |
| 灵气 | 聚灵设施／灵气建筑 |
| 防御 | 防御建筑 |
| 人才 | 学校／学塾（见 `27`） |

建筑落在城市区域的**格子地图**上，占多个格子（见 `24-world-and-settlements.md`）。具体建设流程、解锁条件与 UI **待设计**。

## 4. 占领后的管理：先拿回时间表

占领据点后，玩家获得管理权限。

**第一项重要权限：修改时间表。**

| 之前 | 之后 |
|---|---|
| 玩家受主管时间表限制，只能服从／偷时间 | 玩家可调整居民的工作、休息、娱乐等安排 |

时间表影响：

- 生产效率
- 幸福度／民心
- 人才成长质量

这与 `21-core-loop-and-time.md` 的时间表权限一致：前期只可查看，夺权后开始制定。时间表权限本身就是从被管理者变成管理者的成长回报。

## 5. 玩家在领地里做什么

- 修改居民时间表与岗位分配。
- 分配凡人人口从事耕种、畜牧、伐木、采矿、练兵、探索、建设与情报。
- 修建洞府、生产建筑、灵气建筑、防御设施、仓储与学校。
- 从学校人才候选中收弟子或任命管事（见 `27`）。
- 委任修士坐镇、修行、探索、处理事务或带队出征。
- 与周围村镇、宗门和势力进行外交、贸易、结盟、威慑或战争。
- 在多个据点之间调配人员、物资、核心修士与**灵气流向**。

## 6. 据点灵气来源

每个据点拥有自己的灵气资源。灵气可来自：

1. **地形天然灵气**（荒村低、灵泉高、特殊地点有属性倾向）
2. **建筑提升**（聚灵设施等）
3. **灵物提升**（同类不无限叠加，见 `25`）
4. **多据点灵气汇聚**（见下一节）

| 据点类型 | 灵气方向 |
|---|---|
| 荒村 | 低灵气 |
| 普通村庄／农田 | 偏低至中等 |
| 灵泉／灵地 | 高灵气 |
| 特殊地点 | 特殊属性灵气 |

灵气浓度与属性环境影响修炼效率、突破成功率与灵力恢复，规则与 `25-cultivation-and-breakthrough.md` 一致。自然环境仍是主表达；**不做修仙模拟器式疯狂叠放灵物玩法**，但允许有限的建筑与灵物提升。

## 7. 灵气汇聚

玩家占领多个据点后，可以**把多个据点的灵气集中到核心洞府**，提高主修炼地点效率。

例如：将多个村庄、森林、资源点的灵气，汇聚到主洞府。

结果：

- 主修炼地点的灵气浓度提升。
- 被抽取灵气的据点，自身修炼条件可能下降（幅度与代价**待确定**）。

占领更多地盘的核心动力之一：把分散的灵气优势集中到真正用于修炼的地点。

约束方向：

- 不是无限叠加；汇聚应有损耗、容量或管理成本，避免“占越多越无敌”而无战略取舍。
- 灵物与阵法仍按 `25` 的既有约束。
- 具体汇聚公式、损耗率、是否可被敌对势力截断，**待确定**。

## 8. 人口的管理粒度

**管理界面**上，凡人以**人口组 + 岗位**的形式呈现，玩家不需要逐个下令。例如：

```
荒村（人口 2300）
├── 耕种农田    500
├── 养殖牲畜    200
├── 外出历练    500
├── 操练兵勇   1000
└── 待分配      100
```

普通凡人采用群体／统计模拟；关键 NPC 与修士才是实体。建筑代表人口容量（如住宅 500 人）。详见 `27-characters-and-population.md`。

地图上可用代表性群体单位表现生产活动，不对应每个真实个体。

## 9. 势力成长循环

凡人治理质量影响未来发展。

```
治理凡人
  → 提高幸福度、人才质量、资源产出
  → 学校发现人才
  → 培养弟子／任命管事
  → 增强势力
  → 占领更多土地
  → 继续发展
```

好的管理提高幸福度、人才质量与资源产出，最终产生更多优秀修士。占领更多地盘是为了修炼优势与更大人才池，不是单纯扩图。

## 10. 修士驻留的价值

角色留在领地可提升治理效率（例如政务助手让各项产出效率提升），外出则能探索、招募与作战。玩家必须在两者之间取舍时间——这是本系统与 `21-core-loop-and-time.md` 的主要耦合点。

## 11. 与其他系统的接口

- 输入：据点类型、CaptureObjective 状态、War 状态、占领关系、地形天然灵气、建筑、阵法、人口与修士驻留、Garrison Army、时间表安排。
- 输出：主洞府与各据点的修炼环境、资源产出、幸福度、人才候选、防御与外交筹码、时间表制定权。
- 大型势力战争不走数千人实时微操，而走算法／战报等表现，见 `23-combat.md`。

## 12. 未决问题

- [x] 占领地盘的核心动机是获得修炼优势，不是单纯扩图。
- [x] 战场结束资格为正式接管 OR 打倒本次有效对手；只有实际接管才改变土地 Owner，建筑军事攻击须先处理 War 后果
- [x] 占领后第一项重要权限是修改时间表。
- [x] 据点是可发展区域，可建设洞府、生产、灵气、防御等建筑。
- [x] 灵气来源：地形天然 + 建筑 + 灵物 + 多据点汇聚。
- [x] 可把多据点灵气汇聚到主洞府。
- [x] 不做疯狂叠放灵物；凡人管理用群体岗位。
- [x] 治理质量 → 人才 → 弟子 → 扩张的势力成长循环方向已定。
- [ ] CaptureObjective 耐久、Capture Zone 时长、夺回窗口与驻防规则
- [ ] 建设交互：直接在格子上选址建造，还是先用地块／槽位再落到格子？第一阶段建议简化。
- [ ] 灵气汇聚的损耗、容量上限、是否可被截断或争夺。
- [ ] 被抽走灵气的据点，对凡人生活与本地修士有何可见后果？
- [ ] 幸福度／民心的具体表现，反抗与逃亡如何触发。
- [ ] 岗位与时间表调整是否有过渡期，还是即时生效？
- [ ] 多据点时是否需要"总览面板"，避免逐个据点点进去管理？


## 2026-09-14 CW-05 Closing：可建造农田

制作人已授权并实现 `farmField`：正常建筑入口创建5×4 Surface cells的农田，成本粗木5；reference scenario 新游戏通过 `startingInventory` 发粗木20。Core 逐格查询 Actual Administrative Control，只要各格当前 managing Site 同属玩家势力即可，允许跨同势力 Site 边界。Host 校验加载范围、几何、距离并提供预览；Core 在扣料前重验并事务注册。

`OutdoorConstructedAssetBoard` 保存稳定 root ID、建筑/kind、SurfaceId、精确左下角矩形、格数和独立 BoundLocationId；每格复用 `OutdoorStatefulObjectId.ForCell`。Snapshot v6 optional additive `outdoorConstructedAssets`＋`nextOutdoorConstructedAssetSequence` 保存物理资产和序列，crop state 继续走原 FarmPlots；旧档缺字段为空，新格式损坏报错。当前 manager 不进资产或 Snapshot，由 authored＋runtime anchors 动态派生；拆旗不删除田和作物，新 Site 接管立即恢复组织权限。读档内容壳绝不执行 OpeningInventoryBootstrap。

Streaming 复用现有农田 stamping；建造立即局部补齐，卸载只销毁表现，重载恢复相同 cell IDs/crop。CW-05A Probe UI 已移除。CW-05A/B 与 Closing 已于 2026-09-15 Producer Accepted / Sealed；NPC schedule economy、SettlementProduction、generic house/workshop、产权和农田拆除继续延期；玩家 SiteCore 战争转入 CW-08 / CW-09。


## 2026-09-15 制作人封板

CW-05A、CW-05B 与 CW-05 Closing Slice 正式 **Producer Accepted / Sealed**。制作人已通过正常可建农田、管理接续、拆旗保留资产、重新取得管理及 Save/Load 验收。Subsequently Producer Accepted after normal gameplay validation. 历史记录中当时未运行 Unity 验证的事实保持不变。

## 2026-09-15 CW-08 / CW-09：玩家 SiteCore 战争

制作人授权规则见 [235](../40-process/235-sitecore-warfare-worldsite-takeover-2026-09-15.md)／[236](../40-process/236-world-object-interaction-fixed-core-capture-closure-2026-09-15.md)。固定核心由 `CoreIsRemovable=false` 判定；`ControlCoreState` 保存建筑耐久、占领进度和 Content 派生的 Site identity binding。占领经 `WorldSiteCoreWarfareService` → `WorldSiteTerritoryTransferService` 只改同一 Site 的 Owner 并恢复核心满耐久；ClaimId、AcquiredOrder、农田 identity/crop、人物 faction/home/squad 均不改。TerritoryRegion／Hex 是派生 compatibility projection，不是 Capture transaction 前置 authority。

`CoreIsRemovable=true` 的势力旗被击毁即移除物理旗、令原 Site inactive；保留原 Owner 与 Claim 历史，不占领、不自动变成己方旗。攻方通过正常建造建立新旗、新 Site 和新 Claim。资产继续存在，行政管理者由原 CW-05 查询动态接续。

## 2026-09-15：恢复处 Utility Building

`base:building_recovery_spot` 是正式 2×2 Continuous Outdoor 普通建筑，成本为粗木 5，`createsWorldSite=false`。玩家建造时整个 footprint 必须落在玩家势力当前 Actual Control 内；可跨两个同势力 Site 的管理边界，但无主或敌方管理格会拒绝。恢复处没有行政锚点，也不进入农田状态与 Site Economy。

恢复处建成后通过通用 `OutdoorConstructedAssetBoard` 保留稳定物理身份，立即参与当前已加载 chunk 的 materialization，并沿用同一 snapshot DTO 保存。使用恢复处不检查所属势力：玩家右键选择“休息恢复”，抵达中心后执行 6 WorldTicks（游戏内 30 分钟）的 `RecoveryAction`，完成时把当前生命与当前灵力写到角色现有上限；弥留、死亡、Removed 或战斗冻结期间不可使用。本轮不提供恢复处拆除、床位、收费、NPC 自动使用或医疗系统。

正常玩家入口统一为 SiteCore Warfare → real Character/Squad → CharacterEncounter，退出两条旧 Siege/BattleOffer 编排。按精确 Surface/WorldPosition、目标 Site 等级范围和 War side 选守军，最近者优先、EntityId 升序打破平局。战中目标仍限定同一 frozen range，最多一个未完成 Site 目标；新守军追加原 roster，不回血、不重置冷却或候选。

目标捕获或摧毁完成可 ReadyToEnd；击倒敌人也可 ReadyToEnd，但不自动占地。ReadyToEnd 仍可攻击与占领当前目标。仅玩家发起 SiteCore 战争属于本轮；NPC 自动攻城、普通建筑战争、产权及居民政治后果延期。
