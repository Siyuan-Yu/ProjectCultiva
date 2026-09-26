# ADR-0043：AuctionHouse 托管 authority 与 Snapshot v13

**已采纳；Producer Accepted / Sealed（2026-09-27）。** 来源：制作人 AUCTION-01 实施与最终封板指令。

## 决定

- `CommerceState.AuctionHouses[AuctionHouseId]` 独立保存 HouseWallet、稳定 Listing、Item/Bid escrow、Claims 与序号；不借用 ShopRuntime，执事 NPC 只通过显式 AuctionProvider 提供入口。
- 寄拍物从 PartyInventory 移入 Listing；玩家出价从单一 SpiritStoneGrade 钱包移入 Listing escrow。Market 超价与退款同事务提交，不创建真实 NPC bidder 或使用 NPC wallet。
- Market ceiling 固定为同档物品基础价乘数量，按 WorldTick 定期 review。到期按 EndTick/sequence 稳定结算一次；5% fee 同档向下取整。
- 竞得物与流拍物进入持久 Claim，领取成功前不删除。成交卖款与即时退款直接写 PlayerWallet。
- Snapshot 升至 v13，v1～v12 严格拒绝。New Game 初始化 InitialListings；Restore 只绑定定义并验证 authority，不重放扣款、移物、退款、结算或初始化。

## 影响

正式 Host 只调用 Core 命令，不计算价格或改变托管。Auction modal 沿用当前世界暂停策略。SHOP-TRADE-01、Character wallet 与 Sect Contribution Exchange 保持各自边界；后者仍为 Future。

## 验证边界

离线编译、正式 Content loader、序列化与静态引用检查通过；未运行 Unity 或自动测试。制作人已完成人工验收并确认 V1 封板，清单见 [266](../266-auction-01-auction-house-consignment-and-bidding-v1-2026-09-25.md)。更丰富 Market bidder、真实 NPC bidder、动态拍品与 UX 扩展只属于 Future AUCTION-02。
