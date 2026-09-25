# SHOP-TRADE-01 — Designated Shop Trading + Spirit-Stone Wallet V1

**Producer Accepted / Sealed（2026-09-25）。**

制作人已完成人工验收，确认指定 TradeProvider、三档钱包、有限库存／资金、类别收购、交易事务、UI 与 Save／Load 行为均符合本页契约。本次封板未启动 Unity，也未运行自动测试。

## Authority 与边界

SimulationWorld.Commerce 保存唯一 PlayerParty 共用钱包、以真实 EntityId 为键的 Character 钱包及以 ShopId 为键的 ShopRuntime。个人钱包默认零，绝不因同行、招募或切换 Active 并入玩家钱包。掌柜只提供显式 ShopId 入口，店铺库存和资金不属于掌柜。

三档 SpiritStoneGrade（Low/Mid/High）各自使用非负 long；TradePrice 使用单档正 long。价值关系 Mid=1000 Low、High=1000 Mid，仅供规则说明；V1 不兑换、不自动拆币、不显示折算总资产。未来仅允许向下兑换，禁止向上兑换。

Item 使用正式 TradeCategory 与 BaseTradePrice；无价格不可交易。ShopDefinition 定义 InitialWallet、InitialStock、AcceptedTradeCategories、BuybackMultiplier（默认0.5）；运行态有限资金/库存独立保存，无补货。出售仅从 PartyInventory 移除并由市场吸收，不回填销售库存。

买卖在 Core 中统一预检数量、同档资金、接收方溢出、价格乘法溢出、库存、物品约束与背包容量，预检全部成功后同步提交。Host 只提供交互表现。TradeProvider 复用 NPC 既有接近/抵达交互，不向普通 NPC 开放交易。

Snapshot v11→v12，旧版本严格拒绝。NewGame 才读取初始资金库存；Restore 只恢复快照，静态定义重新绑定不得重置经济状态。

## 范围

正式 UGUI 双栏交易、专用固定掌柜、三档验收商品、LevelTester 钱包/商店重置与镜头定位、静态编译与内容校验。Shop/Market 是当前已封板渠道；Sect Contribution Exchange 仍为独立 Future 渠道。下一阶段为 AUCTION-01，但本轮只记录 Planned 规则，不实施 Auction runtime、UI 或 Content。

## AUCTION-01 Planned（未实施）

- 使用抽象市场竞拍者，不要求真实 NPC bidder 或 NPC AI。
- 玩家出价立即冻结对应单一灵石档位资金；被超过后自动退还。
- 玩家寄拍物品进入 Auction escrow；成交后结算收入，流拍后进入可领取状态。
- 每个 Listing 只使用一个灵石档位，不自动换币；玩家设置起拍价。
- 固定最小加价、固定拍卖持续时间；V1 计划使用简单固定手续费，建议 5%。

以上仅为 Planned 产品规则，不构成 AUCTION-01 实施或建模授权。

## 实际实现与验收内容

- 入口：`base:character_qingshi_shopkeeper`（青石坊市·杂货铺掌柜），独立固定 NPC，不可招募；绑定 `base:shop_qingshi_general`。
- 位置：`base:surface_main_wilderness_v1`，`base:site_huangcun`，住房区附近精确坐标 `(5.14,11.10)`。Scenario 与 LevelTester roster 都配置，Surface openingEntityAnchors 定位。
- 玩家标准开局钱包：10000 Low / 5 Mid / 2 High，来自 Scenario。商店初始资金：20000 Low / 10 Mid / 2 High。
- V1 类别只有 Material / Manual；验收店只收 Material。旧 tags 保留，未按 ID 推断类别。

| 商品 | 基础/销售单价 | 初始库存 | 收购 |
|---|---|---|---|
| 粗木（样例） | 50 下品 | 10 | 25 下品 |
| 将老残谱（秘籍） | 1 中品 | 6 | 类别拒绝 |
| 洞府秘诀（秘籍） | 1 上品 | 1 | 类别拒绝 |
| 开山拳（斗技秘本） | 1200 下品 | 10 | 类别拒绝 |

所有商品复用既有合法 Item；无新增验收垃圾物品。价格与分类在 items.json，店铺和掌柜在 shop_trade01.json。JSON 是本轮 authoring 入口，不新增 ShopEditor。

`HostShopTradePanel` 为正式 UGUI，顶部独立显示玩家与商店三档资金，左右可滚动列表、库存/背包数量、单价/收购价、同档余额，底部数量 +/- 和独立购买/出售按钮。拒收物品可选并得到明确拒绝原因。打开获取具名 modal pause 和 input owner；关闭/切换 session 释放。开启 LevelTester 或其他主要面板会关闭交易，防止 UI 叠加。

LevelTester → 内容 → SHOP-TRADE-01：镜头定位、玩家标准、仅中品、仅5000下品、商店标准、商店下品为零、重置店铺初始库存/资金。重置没有正式玩家入口。

## 事务与恢复静态审查

- Buy：验证真实有效 provider→ShopId、物品、正数量、转移限制、库存、背包容量、同档余额和收款方 long 上限；checked 单价×数量。背包操作保留恢复点；统一 TryTransfer 后减少销售库存。
- Sell：同样验证，额外要求 AcceptedTradeCategories，来源严格为 PartyInventory。移除物品和资金转移成功后，不写销售库存；无自动补货或换币。
- Character 钱包在实体创建回调分配，以 EntityId 存储，不以 DefinitionId 合并；Temporary Quest Companion、招募、切换 Active 都不搬钱。缺省为零，可经统一 wallet API 加款。
- Snapshot v11→v12，commerce 保存三类钱包和所有库存（包括零库存）。Core Restore 校验实体钱包键；Data 绑定校验 ShopId、库存键、数量区间。Restore 不调用初始化/Reset；掌柜生命周期不删除店铺。
- 编译：Core 323 / Data 82 / Unity 141 / Unity.Editor 6 sources，ALL_OK。BaseGame 正式 loader parse/reference validation 通过。没有启动 Unity、PlayMode、Runner 或任何自动测试；不将静态结果等同人工验收。

## Producer 人工验收 checklist

1. 新开局（v11 存档应被拒绝）；LevelTester 内容页定位商店，关闭工具后右键专用掌柜，选“交易”，确认角色靠近后打开双栏面板；右键阿石/其他普通 NPC 无“交易”。
2. 标准钱包买粗木 1，再用 +/- 买多个，核对玩家扣款、店铺收款、背包与库存等量变化；选择 1 中品和 1 上品商品核对各档独立。
3. 买完唯一洞府秘诀后显示售罄；关闭面板，保存并读取，重开商店确认仍售罄，玩家钱包/店铺资金/背包一致。
4. 关闭交易，用 LevelTester 设玩家仅中品，尝试买1200下品商品，提示下品不足；改5000下品/0中品，尝试1中品商品，提示中品不足。失败后库存/背包/双方钱包不变。
5. 恢复标准玩家/商店钱包，出售粗木，按25下品收款；店铺粗木销售库存不增加。尝试出售买到的秘籍，提示不收购此类别。
6. 设商店下品为零后出售粗木，应提示商店下品不足，中品/上品不代付，交易状态不变。
7. 关闭交易并切换永久 ActiveCharacter，再打开，玩家余额相同。临时任务同行不贡献个人钱包，原已验收跟随/秘境/战斗逻辑保持。
8. 验证关闭/Esc 返回世界，数量不足、售罄或背包容量不足均不发生部分提交。LevelTester Reset 仅恢复验收店铺初始库存/资金，可重复验收，不改变玩家钱包/背包。

封板轮只选择性提交本页列出的 SHOP-TRADE-01 文件；未修改 Packages、ProjectSettings、Freeze 或 Demo Runtime。

## 修改文件与最终静态结果

正式 loader：1 个商店、4 条销售库存、初始资金 20000/10/2；`.meta` GUID 唯一性检查通过。制作人人工验收通过，封板状态为 **Producer Accepted / Sealed**。

- `Assets/Scripts/Core/Inventory/PartyInventory.cs`
- `Assets/Scripts/Core/Inventory/ShopCommerce.cs`
- `Assets/Scripts/Core/Inventory/ShopCommerce.cs.meta`
- `Assets/Scripts/Core/Persistence/SnapshotService.cs`
- `Assets/Scripts/Core/Persistence/WorldSnapshot.cs`
- `Assets/Scripts/Core/Simulation/SimulationWorld.cs`
- `Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs`
- `Assets/Scripts/Data/Bootstrap/RuntimeContentShellBootstrap.cs`
- `Assets/Scripts/Data/Content/CharacterDefinition.cs`
- `Assets/Scripts/Data/Content/ContentPackageLoader.cs`
- `Assets/Scripts/Data/Content/ContentReferenceValidator.cs`
- `Assets/Scripts/Data/Content/DefinitionRegistry.cs`
- `Assets/Scripts/Data/Content/DefinitionSchema.cs`
- `Assets/Scripts/Data/Content/ItemDefinition.cs`
- `Assets/Scripts/Data/Content/OpeningScenarioDefinition.cs`
- `Assets/Scripts/Data/Content/ShopContent.cs`
- `Assets/Scripts/Data/Content/ShopContent.cs.meta`
- `Assets/Scripts/Data/Serialization/CommerceJson.cs`
- `Assets/Scripts/Data/Serialization/CommerceJson.cs.meta`
- `Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs`
- `Assets/Scripts/Unity/Host/HostFormalHud.cs`
- `Assets/Scripts/Unity/Host/HostMoveController.cs`
- `Assets/Scripts/Unity/Host/HostNpcContextMenu.cs`
- `Assets/Scripts/Unity/Host/HostNpcInteraction.cs`
- `Assets/Scripts/Unity/Host/HostShopTradePanel.cs`
- `Assets/Scripts/Unity/Host/HostShopTradePanel.cs.meta`
- `Assets/Scripts/Unity/Host/LevelTesterCheatContentSection.cs`
- `Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`
- `Content/BaseGame/Data/Items/items.json`
- `Content/BaseGame/Data/Items/shop_trade01.json`
- `Content/BaseGame/Data/Rosters/level_tester_roster.json`
- `Content/BaseGame/Data/Scenarios/scenarios.json`
- `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`
- `docs/00-project/00-overview.md`
- `docs/00-project/03-glossary.md`
- `docs/20-systems/24-world-and-settlements.md`
- `docs/20-systems/2D-manuals-arts-and-equipment.md`
- `docs/30-tech/31-architecture.md`
- `docs/30-tech/36-content-package-and-mod-architecture.md`
- `docs/40-process/247-project-handoff-current-state-2026-09-18.md`
- `docs/40-process/265-shop-trade-01-designated-shop-and-wallet-v1-2026-09-25.md`
- `docs/40-process/41-roadmap.md`
- `docs/40-process/42-devlog.md`
- `docs/40-process/43-decisions/ADR-0042-shop-wallet-authority-and-snapshot-v12.md`
- `docs/40-process/43-decisions/README.md`
