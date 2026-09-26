using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Inventory;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    public static class AuctionContent
    {
        public static void Load(JsonValue node, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            try
            {
                ShopContent.Fields(node, "id", "type", "displayName", "durationDays", "feePercent", "marketReviewIntervalDays", "minimumIncrements", "initialListings");
                var definition = new AuctionHouseDefinition
                {
                    Id = id.ToString(), DisplayName = node.GetString("displayName"),
                    DurationDays = PositiveInt(ShopContent.Required(node,"durationDays"), "durationDays"),
                    FeePercent = NonnegativeInt(ShopContent.Required(node,"feePercent"), "feePercent"),
                    MarketReviewIntervalDays = PositiveInt(ShopContent.Required(node,"marketReviewIntervalDays"), "marketReviewIntervalDays")
                };
                if (string.IsNullOrWhiteSpace(definition.DisplayName) || definition.FeePercent > 100) throw new FormatException("Invalid auction displayName or feePercent.");
                var increments = ShopContent.Required(node,"minimumIncrements");
                ShopContent.Fields(increments,"low","mid","high");
                definition.LowMinimumIncrement = PositiveAmount(ShopContent.Required(increments,"low"));
                definition.MidMinimumIncrement = PositiveAmount(ShopContent.Required(increments,"mid"));
                definition.HighMinimumIncrement = PositiveAmount(ShopContent.Required(increments,"high"));
                var listings = ShopContent.Required(node,"initialListings");
                if (listings.Kind != JsonValueKind.Array) throw new FormatException("initialListings must be array.");
                foreach (var entry in listings.Array)
                {
                    ShopContent.Fields(entry,"itemId","quantity","startingPrice");
                    var itemId = entry.GetString("itemId");
                    if (!DefinitionId.TryParse(itemId,out _)) throw new FormatException("Invalid auction itemId.");
                    definition.InitialListings.Add(new AuctionInitialListingSpec { ItemId=itemId, Quantity=PositiveInt(ShopContent.Required(entry,"quantity"),"quantity"), StartingPrice=ShopContent.Price(ShopContent.Required(entry,"startingPrice")) });
                }
                var registered = registry.RegisterAuctionHouse(definition);
                if (registered.IsFailure) report.Add(registered.Error);
            }
            catch (Exception ex) { report.Add(ErrorCode.ContentLoadFailed, ex.Message, id.ToString()); }
        }

        public static void Validate(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var character in registry.Characters.Values)
                if (!string.IsNullOrEmpty(character.AuctionProviderHouseId) && (!DefinitionId.TryParse(character.AuctionProviderHouseId,out var id) || !registry.AuctionHouses.ContainsKey(id)))
                    report.Add(ErrorCode.ContentLoadFailed,"AuctionProvider AuctionHouseId missing.",character.Id.ToString());
            foreach (var house in registry.AuctionHouses.Values)
                foreach (var entry in house.InitialListings)
                {
                    if (!DefinitionId.TryParse(entry.ItemId,out var id) || !registry.Items.TryGetValue(id,out var item) || item.BaseTradePrice == null || item.NotTradable)
                    { report.Add(ErrorCode.ContentLoadFailed,"Auction listing requires a tradable priced item.",entry.ItemId); continue; }
                    var info = new InventoryItemInfo { BaseTradePrice=item.BaseTradePrice, NotTradable=item.NotTradable }; info.Tags.AddRange(item.Tags);
                    if (!ShopTradeService.Tradable(info) || entry.StartingPrice.Grade != item.BaseTradePrice.Grade)
                        report.Add(ErrorCode.ContentLoadFailed,"Auction listing grade/restriction mismatch.",entry.ItemId);
                    try { checked { var ceiling = item.BaseTradePrice.Amount * entry.Quantity; } }
                    catch (OverflowException) { report.Add(ErrorCode.ContentLoadFailed,"Auction market ceiling overflow.",entry.ItemId); }
                }
        }

        public static Result Bind(SimulationWorld world, DefinitionRegistry registry, bool newGame)
        {
            var commerce = world.Commerce;
            commerce.AuctionDefinitions.Clear(); commerce.AuctionProviders.Clear();
            foreach (var definition in registry.AuctionHouses.Values) commerce.AuctionDefinitions.Add(definition.Id,definition);
            foreach (var character in registry.Characters.Values)
                if (!string.IsNullOrEmpty(character.AuctionProviderHouseId)) commerce.AuctionProviders.Add(character.Id.ToString(),character.AuctionProviderHouseId);
            if (newGame) foreach (var id in commerce.AuctionDefinitions.Keys) AuctionHouseService.ResetForDebug(commerce,id,world.Tick.Value);
            if (commerce.AuctionHouses.Count != commerce.AuctionDefinitions.Count) return Result.Failure(ErrorCode.SnapshotInvalid,"Auction runtime/definition mismatch.");
            foreach (var pair in commerce.AuctionHouses)
            {
                if (!commerce.AuctionDefinitions.ContainsKey(pair.Key) || pair.Value == null || pair.Value.HouseWallet == null)
                    return Result.Failure(ErrorCode.SnapshotInvalid,"Invalid restored AuctionHouse.",pair.Key);
                foreach (var listing in pair.Value.Listings.Values)
                    if (listing == null || listing.AuctionHouseId != pair.Key || !world.InventoryCatalog.TryGet(listing.ItemId,out var item) ||
                        item.BaseTradePrice == null || item.BaseTradePrice.Grade != listing.Grade || !ShopTradeService.Tradable(item))
                        return Result.Failure(ErrorCode.SnapshotInvalid,"Auction listing content mismatch.",listing?.ListingId);
                foreach (var claim in pair.Value.Claims.Values)
                    if (claim == null || claim.AuctionHouseId != pair.Key || !world.InventoryCatalog.TryGet(claim.ItemId,out _))
                        return Result.Failure(ErrorCode.SnapshotInvalid,"Auction claim content mismatch.",claim?.ClaimId);
            }
            return Result.Success();
        }

        static int PositiveInt(JsonValue node, string name) { var n=ShopContent.Amount(node); if(n==0 || n>int.MaxValue) throw new FormatException(name+" must be positive Int32."); return (int)n; }
        static int NonnegativeInt(JsonValue node, string name) { var n=ShopContent.Amount(node); if(n>int.MaxValue) throw new FormatException(name+" must be Int32."); return (int)n; }
        static long PositiveAmount(JsonValue node) { var n=ShopContent.Amount(node); if(n<=0) throw new FormatException("Auction increment must be positive."); return n; }
    }
}
