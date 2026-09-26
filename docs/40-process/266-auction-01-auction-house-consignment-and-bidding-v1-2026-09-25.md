# AUCTION-01 — Auction House Consignment and Bidding V1

**Producer Accepted / Sealed（2026-09-27）。**

制作人人工验收已确认指定入口、双 escrow、Market 超价退款、成交／流拍 Claim、领取、Save／Load exactly-once 与 SHOP-TRADE-01 回归。V1 至此封板；更自然竞价节奏、更多拍品来源、真实 NPC bidder、更复杂市场行为与 Auction UX 统一留给 Future AUCTION-02／economy expansion。

## Product Goal 与 Commerce boundary

在已封板的 SHOP-TRADE-01 旁增加独立拍卖渠道。只有 CharacterDefinition 显式配置 `auctionProviderHouseId` 才提供“拍卖”；TradeProvider、普通 NPC 钱包和 ShopRuntime 都不隐含拍卖权。AuctionHouse 的拍品、资金、托管与 Claim 属于 AuctionHouseId，不属于执事 NPC。Sect Contribution Exchange 保持 Future，未获自动实施授权。

## AuctionHouse authority 与 Listing identity

`SimulationWorld.Commerce.AuctionHouses` 是 Auction Runtime authority；静态 `AuctionHouseDefinition` 定义 3 天持续时间、每日一次市场 review、5% 手续费，以及 Low/Mid/High 最小加价 10/1/1。每个 Listing 使用 `houseId + sequence` 稳定身份，同物品的不同拍品不合并；保存 seller、item/quantity、单档 grade、整批起拍价、当前出价/竞拍者、玩家资金托管、创建/结束/review tick 与状态。结算顺序固定为 EndTick 后 sequence，状态只从 Open 进入 Settled。

## Item escrow 与 Bid escrow

玩家寄拍只读取 PartyInventory。Core 先验证显式 provider、数量、正式价格、转移限制、背包持有量、WorldTick 算术和序号，再原子移除整批物品并创建 Player listing。无主动撤销。

玩家只能对非自己拍品按当前最低价出价；首次为 StartingPrice，之后为 CurrentBid+同档 increment。后端拒绝自己的拍品、自己已为最高价、任意非最低价、过期拍品与跨档支付。成功时对应档位立即从 PlayerWallet 扣除到 Listing.PlayerEscrowAmount。

## Deterministic Market bidder

V1 的 Market 是抽象、确定性竞拍者，不对应真实 NPC、NPC wallet 或 NPC AI。上限为 `Item.BaseTradePrice.Amount * Quantity`，保持物品价格档位。每个 review：无出价且起拍价不高于上限时 Market 出起拍价；Player 最高时，若下一档最低价不高于上限，Market 同事务退款完整玩家 escrow 并成为最高价；Market 已最高时不自抬价格。review tick 始终推进到未来。

## Settlement、5% fee 与 Claims

- 无出价：Player seller 生成 UnsoldItem Claim；Market seller 直接结束。
- Market 最高且 Player seller：`fee=floor(gross*5/100)`，同档 `net=gross-fee` 进入 PlayerWallet，fee 进入 HouseWallet；先检查双方 overflow。10 Low 与 1 High 的手续费均为 0，不拆币。
- Player 最高：托管资金被消费；Market seller 的成交款进入 HouseWallet，物品生成 WonItem Claim。玩家不能竞拍自己的拍品。

WonItem／UnsoldItem Claim 持久保存 item、quantity、source listing 与 created tick。领取先检查 PartyInventory 完整容量；失败保留 Claim，成功才加入背包并删除。玩家被 Market 超价后的退款立即回原档钱包，不进入 Claim。

## Snapshot、UI 与 Acceptance Content

Snapshot v12→v13；v1～v12 严格拒绝。commerce 段保存每个 AuctionHouse 的 sequence、HouseWallet、全部 Listing 与 Claim。Capture 前按当前 WorldTick reconcile due listing；Restore 不创建初始拍品、不再次扣 escrow、不再次移除物品、不重复退款/结算/Claim。New Game 与 LevelTester 显式 Reset 才应用 InitialListings。

正式 `HostAuctionPanel` 使用既有 commerce modal pause，包含拍品／我的寄拍／待领取三页，显示三档钱包、当前价、最高竞拍者、剩余天时、整批起拍价、出价、寄拍和领取。打开面板时 WorldTick 按既有模态规则暂停。

验收拍卖行为 `base:auction_house_qingshi`（青石坊市·拍卖行），专用入口 `base:character_qingshi_auction_attendant`（拍卖行执事），位于 `base:surface_main_wilderness_v1` 黄村 `(5.30,11.10)`。从出生点 `(5.25291,10.23925)` 向北约 0.86 世界单位；LevelTester 按钮为“定位 AUCTION-01 验收拍卖行”。初始 Low/Mid/High 三拍品；粗木整批起拍 100 Low 低于 bundle ceiling 200，可自然覆盖 Market 参与和超价退款。

## Non-Goals

不实现真实 NPC bidder/到场/钱包/AI、动态供需与估价、关系定价、讨价还价、换币、撤单、保证金/VIP/等级、多地区价差、无限生成、Sect Contribution Exchange、Equipment/Crafting/Production/Logistics/Knowledge/Rumor。

## Producer Acceptance Result

1. 专用执事可打开拍卖；普通 NPC、普通商店与 TradeProvider 不获得拍卖入口，SHOP-TRADE-01 无明显回归。
2. 玩家出价后同档灵石立即进入 escrow；Market 超价后完整退回，不自动换币、不重复扣款或退款。
3. 寄拍确认后物品真实离开 PartyInventory；玩家不能竞拍自己的 Listing，后端与 UI 均拒绝。
4. 到期只结算一次；成交产生 WonItem Claim，流拍产生 UnsoldItem Claim，领取成功后才进入 PartyInventory。
5. Save／Load 不重复扣款、退款、结算、初始拍品或 Claim。

## 静态结果

Core/Data/Unity/Unity.Editor 离线编译 `ALL_OK`，BaseGame 正式 loader/reference validation 通过；Snapshot v13 序列化/恢复路径、provider gate、同档 overflow guard、NewGame/Restore 分流、Shop 独立性、`.meta`/GUID 和 diff whitespace 均作最终静态核对。未启动 Unity、PlayMode、Runner，也未新增或运行测试。最终状态：**Producer Accepted / Sealed**。
