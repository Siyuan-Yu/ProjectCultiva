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
            var characters = new List<JsonValue>(); var shops = new List<JsonValue>(); var auctionHouses = new List<JsonValue>();
            foreach (var p in state.CharacterWallets) characters.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> {
                ["entityId"] = JsonValue.FromString(p.Key.Value.ToString(CultureInfo.InvariantCulture)), ["wallet"] = Wallet(p.Value) }));
            foreach (var p in state.Shops)
            {
                var stock = new List<JsonValue>();
                foreach(var e in p.Value.Stock) stock.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> { ["itemId"] = JsonValue.FromString(e.Key), ["quantity"] = N(e.Value) }));
                shops.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> { ["shopId"] = JsonValue.FromString(p.Key), ["wallet"] = Wallet(p.Value.Wallet), ["stock"] = JsonValue.FromArray(stock) }));
            }
            foreach (var p in state.AuctionHouses)
            {
                var listings = new List<JsonValue>(); var claims = new List<JsonValue>();
                foreach (var listing in p.Value.Listings.Values) listings.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> {
                    ["listingId"]=JsonValue.FromString(listing.ListingId), ["sequence"]=JsonValue.FromString(listing.Sequence.ToString(CultureInfo.InvariantCulture)), ["auctionHouseId"]=JsonValue.FromString(listing.AuctionHouseId),
                    ["sellerKind"]=JsonValue.FromString(listing.SellerKind.ToString()), ["sellerMarker"]=JsonValue.FromString(listing.SellerMarker ?? string.Empty),
                    ["itemId"]=JsonValue.FromString(listing.ItemId), ["quantity"]=N(listing.Quantity), ["grade"]=JsonValue.FromString(listing.Grade.ToString()),
                    ["startingPriceAmount"]=N(listing.StartingPriceAmount), ["currentBidAmount"]=N(listing.CurrentBidAmount), ["currentBidderKind"]=JsonValue.FromString(listing.CurrentBidderKind.ToString()),
                    ["playerEscrowAmount"]=N(listing.PlayerEscrowAmount), ["createdTick"]=JsonValue.FromString(listing.CreatedTick.ToString(CultureInfo.InvariantCulture)),
                    ["endTick"]=JsonValue.FromString(listing.EndTick.ToString(CultureInfo.InvariantCulture)), ["nextMarketReviewTick"]=JsonValue.FromString(listing.NextMarketReviewTick.ToString(CultureInfo.InvariantCulture)),
                    ["status"]=JsonValue.FromString(listing.Status.ToString()) }));
                foreach (var claim in p.Value.Claims.Values) claims.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> {
                    ["claimId"]=JsonValue.FromString(claim.ClaimId), ["sequence"]=JsonValue.FromString(claim.Sequence.ToString(CultureInfo.InvariantCulture)),
                    ["auctionHouseId"]=JsonValue.FromString(claim.AuctionHouseId), ["claimKind"]=JsonValue.FromString(claim.ClaimKind.ToString()),
                    ["itemId"]=JsonValue.FromString(claim.ItemId), ["quantity"]=N(claim.Quantity), ["sourceListingId"]=JsonValue.FromString(claim.SourceListingId),
                    ["createdTick"]=JsonValue.FromString(claim.CreatedTick.ToString(CultureInfo.InvariantCulture)) }));
                auctionHouses.Add(JsonValue.FromObject(new Dictionary<string,JsonValue> {
                    ["auctionHouseId"]=JsonValue.FromString(p.Key), ["nextListingSequence"]=JsonValue.FromString(p.Value.NextListingSequence.ToString(CultureInfo.InvariantCulture)),
                    ["nextClaimSequence"]=JsonValue.FromString(p.Value.NextClaimSequence.ToString(CultureInfo.InvariantCulture)), ["houseWallet"]=Wallet(p.Value.HouseWallet),
                    ["listings"]=JsonValue.FromArray(listings), ["claims"]=JsonValue.FromArray(claims) }));
            }
            return JsonValue.FromObject(new Dictionary<string,JsonValue> { ["playerWallet"] = Wallet(state.PlayerWallet), ["characterWallets"] = JsonValue.FromArray(characters), ["shops"] = JsonValue.FromArray(shops), ["auctionHouses"] = JsonValue.FromArray(auctionHouses) });
        }
        public static CommerceState Read(JsonValue node)
        {
            ShopContent.Fields(node,"playerWallet","characterWallets","shops","auctionHouses");
            var state = new CommerceState { PlayerWallet = ShopContent.Wallet(ShopContent.Required(node,"playerWallet")) };
            var characters = ShopContent.Required(node,"characterWallets"); var shops = ShopContent.Required(node,"shops"); var auctionHouses = ShopContent.Required(node,"auctionHouses");
            if(characters.Kind != JsonValueKind.Array || shops.Kind != JsonValueKind.Array || auctionHouses.Kind != JsonValueKind.Array) throw new FormatException("Commerce arrays required.");
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
            foreach(var a in auctionHouses.Array)
            {
                ShopContent.Fields(a,"auctionHouseId","nextListingSequence","nextClaimSequence","houseWallet","listings","claims");
                var id=a.GetString("auctionHouseId"); if(!DefinitionId.TryParse(id,out _)) throw new FormatException("Invalid AuctionHouseId.");
                var house=new AuctionHouseRuntime { NextListingSequence=U(ShopContent.Required(a,"nextListingSequence")), NextClaimSequence=U(ShopContent.Required(a,"nextClaimSequence")), HouseWallet=ShopContent.Wallet(ShopContent.Required(a,"houseWallet")) };
                var listings=ShopContent.Required(a,"listings"); var claims=ShopContent.Required(a,"claims");
                if(listings.Kind!=JsonValueKind.Array || claims.Kind!=JsonValueKind.Array) throw new FormatException("Auction arrays required.");
                foreach(var l in listings.Array)
                {
                    ShopContent.Fields(l,"listingId","sequence","auctionHouseId","sellerKind","sellerMarker","itemId","quantity","grade","startingPriceAmount","currentBidAmount","currentBidderKind","playerEscrowAmount","createdTick","endTick","nextMarketReviewTick","status");
                    var listing=new AuctionListing { ListingId=l.GetString("listingId"), Sequence=U(ShopContent.Required(l,"sequence")), AuctionHouseId=l.GetString("auctionHouseId"),
                        SellerKind=E<AuctionSellerKind>(l,"sellerKind"), SellerMarker=l.GetString("sellerMarker",string.Empty), ItemId=l.GetString("itemId"), Quantity=I(ShopContent.Required(l,"quantity")), Grade=E<SpiritStoneGrade>(l,"grade"),
                        StartingPriceAmount=ShopContent.Amount(ShopContent.Required(l,"startingPriceAmount")), CurrentBidAmount=ShopContent.Amount(ShopContent.Required(l,"currentBidAmount")), CurrentBidderKind=E<AuctionBidderKind>(l,"currentBidderKind"),
                        PlayerEscrowAmount=ShopContent.Amount(ShopContent.Required(l,"playerEscrowAmount")), CreatedTick=U(ShopContent.Required(l,"createdTick")), EndTick=U(ShopContent.Required(l,"endTick")), NextMarketReviewTick=U(ShopContent.Required(l,"nextMarketReviewTick")), Status=E<AuctionListingStatus>(l,"status") };
                    if(string.IsNullOrWhiteSpace(listing.ListingId) || house.Listings.ContainsKey(listing.ListingId)) throw new FormatException("Invalid/duplicate Auction ListingId.");
                    house.Listings.Add(listing.ListingId,listing);
                }
                foreach(var c in claims.Array)
                {
                    ShopContent.Fields(c,"claimId","sequence","auctionHouseId","claimKind","itemId","quantity","sourceListingId","createdTick");
                    var claim=new AuctionClaim { ClaimId=c.GetString("claimId"), Sequence=U(ShopContent.Required(c,"sequence")), AuctionHouseId=c.GetString("auctionHouseId"), ClaimKind=E<AuctionClaimKind>(c,"claimKind"), ItemId=c.GetString("itemId"), Quantity=I(ShopContent.Required(c,"quantity")), SourceListingId=c.GetString("sourceListingId"), CreatedTick=U(ShopContent.Required(c,"createdTick")) };
                    if(string.IsNullOrWhiteSpace(claim.ClaimId) || house.Claims.ContainsKey(claim.ClaimId)) throw new FormatException("Invalid/duplicate Auction ClaimId.");
                    house.Claims.Add(claim.ClaimId,claim);
                }
                if(state.AuctionHouses.ContainsKey(id)) throw new FormatException("Duplicate AuctionHouse runtime.");
                state.AuctionHouses.Add(id,house);
            }
            return state;
        }
        static ulong U(JsonValue node) { if(node.Kind!=JsonValueKind.String || !ulong.TryParse(node.String,NumberStyles.None,CultureInfo.InvariantCulture,out var value)) throw new FormatException("Invalid UInt64."); return value; }
        static int I(JsonValue node) { var value=ShopContent.Amount(node); if(value>int.MaxValue) throw new FormatException("Invalid Int32."); return (int)value; }
        static T E<T>(JsonValue node,string key) where T:struct => ShopContent.EnumText<T>(ShopContent.Required(node,key));
    }
}
