using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Inventory;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    public static class ShopContent
    {
        public static void Fields(JsonValue node, params string[] names)
        {
            if (node == null || node.Kind != JsonValueKind.Object) throw new FormatException("Expected commerce object.");
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var key in node.Object.Keys) if (!allowed.Contains(key)) throw new FormatException("Unknown commerce field: " + key);
        }
        public static JsonValue Required(JsonValue node, string key)
        { if (!node.TryGetProperty(key, out var value)) throw new FormatException("Missing commerce field: " + key); return value; }
        // JSON numbers are IEEE doubles in the existing parser. Above its exact integer range require decimal strings.
        public static long Amount(JsonValue node)
        {
            if (node.Kind == JsonValueKind.String && long.TryParse(node.String, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0) return n;
            if (node.Kind == JsonValueKind.Number && node.Number >= 0 && node.Number <= 9007199254740991d && Math.Floor(node.Number) == node.Number) return (long)node.Number;
            throw new FormatException("Expected nonnegative Int64 (decimal string above 2^53-1).");
        }
        public static SpiritStoneWallet Wallet(JsonValue node)
        { Fields(node, "low", "mid", "high"); return new SpiritStoneWallet(Amount(Required(node,"low")),Amount(Required(node,"mid")),Amount(Required(node,"high"))); }
        public static T EnumText<T>(JsonValue node) where T : struct
        {
            if (node.Kind != JsonValueKind.String || !Enum.TryParse<T>(node.String, false, out var value) || !Enum.IsDefined(typeof(T), value) || value.ToString() != node.String)
                throw new FormatException("Invalid " + typeof(T).Name);
            return value;
        }
        public static TradePrice Price(JsonValue node)
        { Fields(node,"grade","amount"); return new TradePrice(EnumText<SpiritStoneGrade>(Required(node,"grade")), Amount(Required(node,"amount"))); }
        public static void Load(JsonValue node, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            try
            {
                Fields(node,"id","type","displayName","initialWallet","buybackMultiplier","acceptedTradeCategories","initialStock");
                var def = new ShopDefinition { Id = id.ToString(), DisplayName = node.GetString("displayName"), InitialWallet = Wallet(Required(node,"initialWallet")) };
                if (string.IsNullOrWhiteSpace(def.DisplayName)) throw new FormatException("Shop displayName required.");
                if (node.TryGetProperty("buybackMultiplier", out var mult))
                {
                    if (mult.Kind != JsonValueKind.Number || !decimal.TryParse(mult.Number.ToString("R",CultureInfo.InvariantCulture), NumberStyles.Float,CultureInfo.InvariantCulture,out var value) || value < 0)
                        throw new FormatException("Invalid buybackMultiplier.");
                    def.BuybackMultiplier = value;
                }
                var categories = Required(node,"acceptedTradeCategories");
                if (categories.Kind != JsonValueKind.Array) throw new FormatException("Categories must be array.");
                foreach (var c in categories.Array) if (!def.AcceptedTradeCategories.Add(EnumText<TradeCategory>(c))) throw new FormatException("Duplicate category.");
                var stock = Required(node,"initialStock");
                if (stock.Kind != JsonValueKind.Array) throw new FormatException("Stock must be array.");
                var ids = new HashSet<string>();
                foreach (var e in stock.Array)
                {
                    Fields(e,"itemId","quantity","salePriceOverride");
                    var itemId = e.GetString("itemId"); var q = Amount(Required(e,"quantity"));
                    if (!DefinitionId.TryParse(itemId,out _) || !ids.Add(itemId) || q <= 0 || q > int.MaxValue) throw new FormatException("Invalid/duplicate shop stock.");
                    def.InitialStock.Add(new ShopStockSpec { ItemId = itemId, Quantity = (int)q, SalePriceOverride = e.TryGetProperty("salePriceOverride",out var price) ? Price(price) : null });
                }
                var result = registry.RegisterShop(def);
                if (result.IsFailure) report.Add(result.Error);
            }
            catch (Exception ex) { report.Add(ErrorCode.ContentLoadFailed, ex.Message, id.ToString()); }
        }
        public static void ReadItem(JsonValue node, ItemDefinition item, ValidationReport report)
        {
            try
            {
                if (node.TryGetProperty("tradeCategory", out var category)) item.TradeCategory = EnumText<TradeCategory>(category);
                if (node.TryGetProperty("baseTradePrice", out var price))
                {
                    if (!node.TryGetProperty("tradeCategory", out _)) throw new FormatException("Priced item requires tradeCategory.");
                    item.BaseTradePrice = Price(price);
                }
                if (node.TryGetProperty("notTradable",out var flag))
                { if (flag.Kind != JsonValueKind.Boolean) throw new FormatException("notTradable must be boolean."); item.NotTradable = flag.Bool; }
            }
            catch(Exception ex) { report.Add(ErrorCode.ContentLoadFailed,ex.Message,item.Id.ToString()); }
        }
        public static SpiritStoneWallet ReadStartingWallet(JsonValue node, ValidationReport report, string id)
        {
            try { return node.TryGetProperty("startingWallet",out var wallet) ? Wallet(wallet) : new SpiritStoneWallet(); }
            catch(Exception ex) { report.Add(ErrorCode.ContentLoadFailed,ex.Message,id); return new SpiritStoneWallet(); }
        }
        public static void Validate(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var c in registry.Characters.Values)
                if (!string.IsNullOrEmpty(c.TradeProviderShopId) && (!DefinitionId.TryParse(c.TradeProviderShopId,out var id) || !registry.Shops.ContainsKey(id)))
                    report.Add(ErrorCode.ContentLoadFailed,"TradeProvider ShopId missing.",c.Id.ToString());
            foreach (var shop in registry.Shops.Values)
            {
                foreach (var e in shop.InitialStock)
                {
                    if (!DefinitionId.TryParse(e.ItemId,out var id) || !registry.Items.TryGetValue(id,out var item) || item.BaseTradePrice == null || item.NotTradable)
                    { report.Add(ErrorCode.ContentLoadFailed,"Shop stock requires a tradable priced item.", e.ItemId); continue; }
                    var info = new InventoryItemInfo { BaseTradePrice = item.BaseTradePrice, NotTradable = item.NotTradable };
                    info.Tags.AddRange(item.Tags);
                    if (!ShopTradeService.Tradable(info)) report.Add(ErrorCode.ContentLoadFailed,"Shop stock item has a transfer restriction.",e.ItemId);
                    try { checked { var amount = (long)Math.Max(1m, decimal.Floor(item.BaseTradePrice.Amount * shop.BuybackMultiplier)); } }
                    catch(OverflowException) { report.Add(ErrorCode.ContentLoadFailed,"Buyback price overflow.",shop.Id); }
                }
            }
        }
        public static Result Bind(SimulationWorld world, DefinitionRegistry registry, bool newGame, OpeningScenarioDefinition scenario = null)
        {
            var board = world.Commerce;
            board.Definitions.Clear(); board.Providers.Clear();
            foreach (var def in registry.Shops.Values) board.Definitions.Add(def.Id, def);
            foreach (var def in registry.Characters.Values) if (!string.IsNullOrEmpty(def.TradeProviderShopId)) board.Providers.Add(def.Id.ToString(), def.TradeProviderShopId);
            if (newGame)
            {
                board.PlayerWallet = scenario?.StartingWallet.Copy() ?? new SpiritStoneWallet();
                foreach (var id in board.Definitions.Keys) board.ResetShopForDebug(id);
            }
            // Strict v12 content/runtime coherence: never create missing stores while restoring.
            if (board.Shops.Count != board.Definitions.Count) return Result.Failure(ErrorCode.SnapshotInvalid,"Shop runtime/definition mismatch.");
            foreach (var p in board.Shops)
            {
                if (!board.Definitions.TryGetValue(p.Key,out var def) || p.Value.Stock.Count != def.InitialStock.Count)
                    return Result.Failure(ErrorCode.SnapshotInvalid,"Shop stock definition mismatch.",p.Key);
                foreach (var e in def.InitialStock) if (!p.Value.Stock.TryGetValue(e.ItemId,out var q) || q < 0 || q > e.Quantity)
                    return Result.Failure(ErrorCode.SnapshotInvalid,"Invalid restored Shop stock.",p.Key);
            }
            return Result.Success();
        }
    }
}
