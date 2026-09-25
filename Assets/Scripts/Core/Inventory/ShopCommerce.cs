using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Entities;

namespace XianXia.Core.Inventory
{
    public enum SpiritStoneGrade { Low, Mid, High }
    public enum TradeCategory { Material, Manual }

    public sealed class SpiritStoneWallet
    {
        readonly long[] balances = new long[3];
        public long Low => balances[0];
        public long Mid => balances[1];
        public long High => balances[2];
        public SpiritStoneWallet(long low = 0, long mid = 0, long high = 0)
        {
            if (low < 0 || mid < 0 || high < 0) throw new ArgumentOutOfRangeException(nameof(low));
            balances[0] = low; balances[1] = mid; balances[2] = high;
        }
        public static bool ValidGrade(SpiritStoneGrade grade) => grade >= SpiritStoneGrade.Low && grade <= SpiritStoneGrade.High;
        public long Balance(SpiritStoneGrade grade) => ValidGrade(grade) ? balances[(int)grade] : throw new ArgumentOutOfRangeException(nameof(grade));
        public bool CanAfford(SpiritStoneGrade grade, long amount) => ValidGrade(grade) && amount >= 0 && Balance(grade) >= amount;
        public bool CanAdd(SpiritStoneGrade grade, long amount) => ValidGrade(grade) && amount >= 0 && Balance(grade) <= long.MaxValue - amount;
        public bool TrySpend(SpiritStoneGrade grade, long amount)
        { if (!CanAfford(grade, amount)) return false; balances[(int)grade] -= amount; return true; }
        public bool Add(SpiritStoneGrade grade, long amount)
        { if (!CanAdd(grade, amount)) return false; balances[(int)grade] += amount; return true; }
        public static bool TryTransfer(SpiritStoneWallet from, SpiritStoneWallet to, SpiritStoneGrade grade, long amount)
        {
            if (from == null || to == null || ReferenceEquals(from, to) || !from.CanAfford(grade, amount) || !to.CanAdd(grade, amount)) return false;
            from.TrySpend(grade, amount); to.Add(grade, amount); return true;
        }
        public SpiritStoneWallet Copy() => new SpiritStoneWallet(Low, Mid, High);
    }
    public sealed class TradePrice
    {
        public SpiritStoneGrade Grade { get; }
        public long Amount { get; }
        public TradePrice(SpiritStoneGrade grade, long amount)
        { if (!SpiritStoneWallet.ValidGrade(grade) || amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); Grade = grade; Amount = amount; }
    }
    public sealed class ShopStockSpec
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public TradePrice SalePriceOverride { get; set; }
    }
    public class ShopDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public SpiritStoneWallet InitialWallet { get; set; } = new SpiritStoneWallet();
        public decimal BuybackMultiplier { get; set; } = 0.5m;
        public HashSet<TradeCategory> AcceptedTradeCategories { get; } = new HashSet<TradeCategory>();
        public List<ShopStockSpec> InitialStock { get; } = new List<ShopStockSpec>();
    }
    public sealed class ShopRuntime
    {
        public SpiritStoneWallet Wallet { get; set; } = new SpiritStoneWallet();
        public Dictionary<string, int> Stock { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public ShopRuntime Copy()
        { var copy = new ShopRuntime { Wallet = Wallet.Copy() }; foreach (var p in Stock) copy.Stock.Add(p.Key, p.Value); return copy; }
    }
    public sealed class CommerceState
    {
        public SpiritStoneWallet PlayerWallet { get; set; } = new SpiritStoneWallet();
        public Dictionary<EntityId, SpiritStoneWallet> CharacterWallets { get; } = new Dictionary<EntityId, SpiritStoneWallet>();
        public Dictionary<string, ShopRuntime> Shops { get; } = new Dictionary<string, ShopRuntime>(StringComparer.Ordinal);
        // Static content, deliberately excluded from snapshots.
        public Dictionary<string, ShopDefinition> Definitions { get; } = new Dictionary<string, ShopDefinition>(StringComparer.Ordinal);
        public Dictionary<string, string> Providers { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public SpiritStoneWallet GetCharacterWallet(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                throw new ArgumentException("Character wallet requires a real character EntityId.");
            if (!CharacterWallets.TryGetValue(id, out var wallet)) CharacterWallets.Add(id, wallet = new SpiritStoneWallet());
            return wallet;
        }
        public bool TryGetProvider(SimulationWorld world, EntityId id, out string shopId)
        {
            shopId = null;
            return world.Entities.TryGet(id, out var entity) && entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive && Providers.TryGetValue(entity.DefinitionId.ToString(), out shopId)
                && Definitions.ContainsKey(shopId) && Shops.ContainsKey(shopId);
        }
        public void ResetShopForDebug(string id)
        {
            var def = Definitions[id]; var shop = new ShopRuntime { Wallet = def.InitialWallet.Copy() };
            foreach (var entry in def.InitialStock) shop.Stock.Add(entry.ItemId, entry.Quantity);
            Shops[id] = shop;
        }
        public CommerceState Capture()
        {
            var copy = new CommerceState { PlayerWallet = PlayerWallet.Copy() };
            foreach (var p in CharacterWallets) copy.CharacterWallets.Add(p.Key, p.Value.Copy());
            foreach (var p in Shops) copy.Shops.Add(p.Key, p.Value.Copy());
            return copy;
        }
    }
    public static class ShopTradeService
    {
        public static string GradeName(SpiritStoneGrade grade) => grade == SpiritStoneGrade.Low ? "下品" : grade == SpiritStoneGrade.Mid ? "中品" : "上品";
        public static bool Tradable(InventoryItemInfo item) => item != null && item.BaseTradePrice != null && !item.NotTradable &&
            !item.Tags.Exists(t => string.Equals(t, "questBound", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "notTradable", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "nonTransferable", StringComparison.OrdinalIgnoreCase));
        public static TradePrice SalePrice(ShopDefinition def, InventoryItemInfo item)
        { foreach (var entry in def.InitialStock) if (entry.ItemId == item.Id) return entry.SalePriceOverride ?? item.BaseTradePrice; return null; }
        public static TradePrice BuybackPrice(ShopDefinition def, InventoryItemInfo item)
        {
            if (!Tradable(item)) return null;
            try { return new TradePrice(item.BaseTradePrice.Grade, checked((long)Math.Max(1m, decimal.Floor(checked(item.BaseTradePrice.Amount * def.BuybackMultiplier))))); }
            catch (OverflowException) { return null; }
        }
        // Single-threaded world command: all rejectable operations precede mutation. Bag rollback protects its all-or-nothing boundary.
        public static bool TryTrade(SimulationWorld world, EntityId provider, string shopId, string itemId, int quantity, bool buy, out string message)
        {
            message = "交易对象或数量无效。";
            var board = world.Commerce;
            if (quantity <= 0 || !board.TryGetProvider(world, provider, out var bound) || bound != shopId ||
                !board.Definitions.TryGetValue(shopId, out var def) || !board.Shops.TryGetValue(shopId, out var shop) ||
                !world.InventoryCatalog.TryGet(itemId, out var item)) return false;
            message = "物品不可交易。";
            if (!Tradable(item)) return false;
            if (!buy && !def.AcceptedTradeCategories.Contains(item.TradeCategory)) { message = "商店不收购此类别。"; return false; }
            var price = buy ? SalePrice(def, item) : BuybackPrice(def, item);
            message = "价格无效或金额溢出。";
            if (price == null) return false;
            long total;
            try { total = checked(price.Amount * quantity); } catch (OverflowException) { return false; }
            if (buy && (!shop.Stock.TryGetValue(itemId, out var stock) || stock < quantity)) { message = "库存不足／已售罄。"; return false; }
            if (!buy && world.Inventory.GetCount(itemId) < quantity) { message = "随身背包数量不足。"; return false; }
            if (buy && !world.Inventory.CanAddAll(itemId, quantity)) { message = "随身背包空间不足。"; return false; }
            var payer = buy ? board.PlayerWallet : shop.Wallet;
            var receiver = buy ? shop.Wallet : board.PlayerWallet;
            if (!payer.CanAfford(price.Grade, total)) { message = (buy ? "玩家的" : "商店的") + GradeName(price.Grade) + "灵石不足。"; return false; }
            if (!receiver.CanAdd(price.Grade, total)) { message = "接收方灵石余额溢出。"; return false; }
            var bag = world.Inventory.CaptureState();
            var changed = buy ? world.Inventory.TryAddAll(itemId, quantity) : world.Inventory.TryRemoveAll(itemId, quantity);
            if (!changed || !SpiritStoneWallet.TryTransfer(payer, receiver, price.Grade, total))
            { world.Inventory.RestoreState(bag); message = "交易未提交。"; return false; }
            if (buy) shop.Stock[itemId] -= quantity;
            message = buy ? "购买成功。" : "出售成功（物品由市场吸收）。";
            return true;
        }
    }
}
