using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Inventory;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;
namespace XianXia.Data.Serialization
{
    public static class CommerceJson
    {
        static JsonValue N(long n) => JsonValue.FromString(n.ToString(CultureInfo.InvariantCulture));
        static JsonValue Wallet(SpiritStoneWallet w) => JsonValue.FromObject(new Dictionary<string,JsonValue> { ["low"] = N(w.Low), ["mid"] = N(w.Mid), ["high"] = N(w.High) });
        public static JsonValue Write(CommerceState state)
        {
            var characters = new List<JsonValue>(); var shops = new List<JsonValue>();
            foreach (var p in state.CharacterWallets) characters.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> {
                ["entityId"] = JsonValue.FromString(p.Key.Value.ToString(CultureInfo.InvariantCulture)), ["wallet"] = Wallet(p.Value) }));
            foreach (var p in state.Shops)
            {
                var stock = new List<JsonValue>();
                foreach(var e in p.Value.Stock) stock.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> { ["itemId"] = JsonValue.FromString(e.Key), ["quantity"] = N(e.Value) }));
                shops.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> { ["shopId"] = JsonValue.FromString(p.Key), ["wallet"] = Wallet(p.Value.Wallet), ["stock"] = JsonValue.FromArray(stock) }));
            }
            return JsonValue.FromObject(new Dictionary<string,JsonValue> { ["playerWallet"] = Wallet(state.PlayerWallet), ["characterWallets"] = JsonValue.FromArray(characters), ["shops"] = JsonValue.FromArray(shops) });
        }
        public static CommerceState Read(JsonValue node)
        {
            ShopContent.Fields(node,"playerWallet","characterWallets","shops");
            var state = new CommerceState { PlayerWallet = ShopContent.Wallet(ShopContent.Required(node,"playerWallet")) };
            var characters = ShopContent.Required(node,"characterWallets"); var shops = ShopContent.Required(node,"shops");
            if(characters.Kind != JsonValueKind.Array || shops.Kind != JsonValueKind.Array) throw new FormatException("Commerce arrays required.");
            foreach(var c in characters.Array)
            {
                ShopContent.Fields(c,"entityId","wallet");
                if (!ulong.TryParse(c.GetString("entityId"),NumberStyles.None,CultureInfo.InvariantCulture,out var id) || id == 0) throw new FormatException("Invalid character wallet EntityId.");
                state.CharacterWallets.Add(new EntityId(id), ShopContent.Wallet(ShopContent.Required(c,"wallet")));
            }
            foreach(var s in shops.Array)
            {
                ShopContent.Fields(s,"shopId","wallet","stock");
                var id = s.GetString("shopId");
                if(!DefinitionId.TryParse(id,out _)) throw new FormatException("Invalid ShopId.");
                var shop = new ShopRuntime { Wallet = ShopContent.Wallet(ShopContent.Required(s,"wallet")) };
                var stock = ShopContent.Required(s,"stock");
                if(stock.Kind != JsonValueKind.Array) throw new FormatException("Shop stock array required.");
                foreach(var e in stock.Array)
                {
                    ShopContent.Fields(e,"itemId","quantity");
                    var itemId = e.GetString("itemId"); var quantity = ShopContent.Amount(ShopContent.Required(e,"quantity"));
                    if(!DefinitionId.TryParse(itemId,out _) || quantity > int.MaxValue) throw new FormatException("Invalid Shop stock.");
                    shop.Stock.Add(itemId,(int)quantity);
                }
                state.Shops.Add(id,shop);
            }
            return state;
        }
    }
}
