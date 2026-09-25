# ADR-0042：指定商店、三档灵石钱包与 Snapshot v12

**Producer Accepted / Sealed（2026-09-25）。** 来源：制作人 SHOP-TRADE-01 完整实施与最终人工验收；不构成 AUCTION-01 或其他后续功能的实施授权。

## 决定

- `SimulationWorld.Commerce` 是经济状态容器。`PlayerWallet` 为唯一 PlayerParty 共享钱包；`CharacterWallets` 以真实 EntityId 分离个人资金；`Shops` 以 ShopId 保存库存/资金。掌柜的生死、更换、同行及 Active 切换都不转移这些余额。
- 钱包三档独立非负 long，价格单档正 long。支付仅同档；1 Mid=1000 Low、1 High=1000 Mid 是规则价值关系，V1 无兑换。未来只能向下兑换，向上兑换禁止。
- `ShopDefinition` 与 `ShopRuntime` 分离；显式 `tradeProviderShopId` 仅绑定世界入口。普通 NPC 有钱包不开放交易。买卖命令统一调用钱包 API，全部校验在提交前；背包异常失败回滚，出售不新增销售库存。
- `TradeCategory` V1 只有 Material / Manual。既有 tags 混合用途、教习属性和来源，不能单独可靠表达商业分类；因此增加最小分类，保留既有 tags。无 `baseTradePrice` 默认不可交易。
- Snapshot 从实查 v11 升至 v12；v1～v11 严格拒绝。新增 commerce 段保存 playerWallet、characterWallets、shops。长整数使用十进制字符串避免 double 精度损失。定义不入存档；恢复只绑定定义并校验库存键和数量，不重新初始化。
- 不修改 Core/Data 依赖方向、Surface/Squad/战斗 authority、Freeze 正文。仅 Shop/Market 当前实施；拍卖、宗门兑换、生产物流与商业 AI 均未实施。

## 验证与限制

离线 C# 编译和 BaseGame 正式 loader 静态校验通过；未启动 Unity、未运行自动测试。制作人已按 [265](../265-shop-trade-01-designated-shop-and-wallet-v1-2026-09-25.md) 完成人工验收，确认 TradeProvider gate、交易事务、有限库存／资金、三档钱包与 Save／Load 正常。
