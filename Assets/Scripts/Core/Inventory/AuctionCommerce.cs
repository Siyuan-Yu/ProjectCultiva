using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Inventory
{
    public enum AuctionSellerKind { Player, Market }
    public enum AuctionBidderKind { None, Player, Market }
    public enum AuctionListingStatus { Open, Settled }
    public enum AuctionClaimKind { WonItem, UnsoldItem }

    public sealed class AuctionInitialListingSpec
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public TradePrice StartingPrice { get; set; }
    }

    public sealed class AuctionHouseDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int DurationDays { get; set; } = 3;
        public int FeePercent { get; set; } = 5;
        public int MarketReviewIntervalDays { get; set; } = 1;
        public long LowMinimumIncrement { get; set; } = 10;
        public long MidMinimumIncrement { get; set; } = 1;
        public long HighMinimumIncrement { get; set; } = 1;
        public List<AuctionInitialListingSpec> InitialListings { get; } = new List<AuctionInitialListingSpec>();

        public long MinimumIncrement(SpiritStoneGrade grade) => grade == SpiritStoneGrade.Low
            ? LowMinimumIncrement : grade == SpiritStoneGrade.Mid ? MidMinimumIncrement : HighMinimumIncrement;
    }

    public sealed class AuctionListing
    {
        public string ListingId { get; set; }
        public ulong Sequence { get; set; }
        public string AuctionHouseId { get; set; }
        public AuctionSellerKind SellerKind { get; set; }
        public string SellerMarker { get; set; } = string.Empty;
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public SpiritStoneGrade Grade { get; set; }
        public long StartingPriceAmount { get; set; }
        public long CurrentBidAmount { get; set; }
        public AuctionBidderKind CurrentBidderKind { get; set; }
        public long PlayerEscrowAmount { get; set; }
        public ulong CreatedTick { get; set; }
        public ulong EndTick { get; set; }
        public ulong NextMarketReviewTick { get; set; }
        public AuctionListingStatus Status { get; set; }

        public AuctionListing Copy() => (AuctionListing)MemberwiseClone();
    }

    public sealed class AuctionClaim
    {
        public string ClaimId { get; set; }
        public ulong Sequence { get; set; }
        public string AuctionHouseId { get; set; }
        public AuctionClaimKind ClaimKind { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public string SourceListingId { get; set; }
        public ulong CreatedTick { get; set; }

        public AuctionClaim Copy() => (AuctionClaim)MemberwiseClone();
    }

    public sealed class AuctionHouseRuntime
    {
        public ulong NextListingSequence { get; set; } = 1;
        public ulong NextClaimSequence { get; set; } = 1;
        public SpiritStoneWallet HouseWallet { get; set; } = new SpiritStoneWallet();
        public Dictionary<string, AuctionListing> Listings { get; } = new Dictionary<string, AuctionListing>(StringComparer.Ordinal);
        public Dictionary<string, AuctionClaim> Claims { get; } = new Dictionary<string, AuctionClaim>(StringComparer.Ordinal);

        public AuctionHouseRuntime Copy()
        {
            var copy = new AuctionHouseRuntime { NextListingSequence = NextListingSequence, NextClaimSequence = NextClaimSequence, HouseWallet = HouseWallet.Copy() };
            foreach (var p in Listings) copy.Listings.Add(p.Key, p.Value.Copy());
            foreach (var p in Claims) copy.Claims.Add(p.Key, p.Value.Copy());
            return copy;
        }
    }

    public static class AuctionHouseService
    {
        public static bool TryGetProvider(CommerceState commerce, SimulationWorld world, EntityId provider, out string houseId)
        {
            houseId = null;
            return commerce != null && world != null && world.Entities.TryGet(provider, out var entity) &&
                entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive &&
                commerce.AuctionProviders.TryGetValue(entity.DefinitionId.ToString(), out houseId) &&
                commerce.AuctionDefinitions.ContainsKey(houseId) && commerce.AuctionHouses.ContainsKey(houseId);
        }

        public static bool TryGetMinimumBid(AuctionHouseDefinition definition, AuctionListing listing, out long amount)
        {
            amount = 0;
            if (definition == null || listing == null || listing.Status != AuctionListingStatus.Open) return false;
            if (listing.CurrentBidderKind == AuctionBidderKind.None) { amount = listing.StartingPriceAmount; return amount > 0; }
            try { amount = checked(listing.CurrentBidAmount + definition.MinimumIncrement(listing.Grade)); return amount > 0; }
            catch (OverflowException) { return false; }
        }

        public static bool TryBid(SimulationWorld world, EntityId provider, string houseId, string listingId, long amount, out string message)
        {
            message = "拍卖对象或出价无效。";
            if (world == null || !TryGetProvider(world.Commerce, world, provider, out var bound) || bound != houseId ||
                !world.Commerce.AuctionDefinitions.TryGetValue(houseId, out var definition) ||
                !world.Commerce.AuctionHouses.TryGetValue(houseId, out var house) ||
                !house.Listings.TryGetValue(listingId, out var listing) || listing.Status != AuctionListingStatus.Open || world.Tick.Value >= listing.EndTick) return false;
            if (listing.SellerKind == AuctionSellerKind.Player) { message = "不能竞拍自己的寄拍物。"; return false; }
            if (listing.CurrentBidderKind == AuctionBidderKind.Player) { message = "你已经是最高出价者。"; return false; }
            if (!TryGetMinimumBid(definition, listing, out var minimum) || amount != minimum) { message = "只能按当前最低加价出价。"; return false; }
            if (!world.Commerce.PlayerWallet.TrySpend(listing.Grade, amount)) { message = "玩家同档灵石不足。"; return false; }
            listing.CurrentBidAmount = amount;
            listing.CurrentBidderKind = AuctionBidderKind.Player;
            listing.PlayerEscrowAmount = amount;
            message = "出价成功，灵石已进入竞价托管。";
            return true;
        }

        public static bool TryConsign(SimulationWorld world, EntityId provider, string houseId, string itemId, int quantity, long startingPriceAmount, out string message)
        {
            message = "寄拍对象或参数无效。";
            if (world == null || quantity <= 0 || startingPriceAmount <= 0 ||
                !TryGetProvider(world.Commerce, world, provider, out var bound) || bound != houseId ||
                !world.Commerce.AuctionDefinitions.TryGetValue(houseId, out var definition) ||
                !world.Commerce.AuctionHouses.TryGetValue(houseId, out var house) ||
                !world.InventoryCatalog.TryGet(itemId, out var item) || !ShopTradeService.Tradable(item)) return false;
            if (world.Inventory.GetCount(itemId) < quantity) { message = "随身背包数量不足。"; return false; }
            if (house.NextListingSequence == 0 || house.NextListingSequence == ulong.MaxValue) { message = "拍品序号已耗尽。"; return false; }
            if (!TrySchedule(world.Tick.Value, definition, out var endTick, out var reviewTick)) { message = "拍卖时间溢出。"; return false; }
            var sequence = house.NextListingSequence;
            var listingId = houseId + ":listing:" + sequence;
            if (house.Listings.ContainsKey(listingId)) { message = "拍品标识冲突。"; return false; }
            var bag = world.Inventory.CaptureState();
            if (!world.Inventory.TryRemoveAll(itemId, quantity)) { message = "寄拍物品未移入托管。"; return false; }
            var listing = NewListing(listingId, sequence, houseId, AuctionSellerKind.Player, itemId, quantity, item.BaseTradePrice.Grade, startingPriceAmount, world.Tick.Value, endTick, reviewTick);
            try { house.Listings.Add(listingId, listing); house.NextListingSequence = sequence + 1; }
            catch { world.Inventory.RestoreState(bag); throw; }
            message = "寄拍成功，整批物品已进入拍品托管。";
            return true;
        }

        public static bool TryClaim(SimulationWorld world, EntityId provider, string houseId, string claimId, out string message)
        {
            message = "待领取项无效。";
            if (world == null || !TryGetProvider(world.Commerce, world, provider, out var bound) || bound != houseId ||
                !world.Commerce.AuctionHouses.TryGetValue(houseId, out var house) || !house.Claims.TryGetValue(claimId, out var claim)) return false;
            if (!world.Inventory.CanAddAll(claim.ItemId, claim.Quantity)) { message = "随身背包空间不足，待领取物保留。"; return false; }
            if (!world.Inventory.TryAddAll(claim.ItemId, claim.Quantity)) { message = "领取未提交，待领取物保留。"; return false; }
            house.Claims.Remove(claimId);
            message = "领取成功。";
            return true;
        }

        public static void ProcessDue(SimulationWorld world)
        {
            if (world?.Commerce == null) return;
            var houseIds = new List<string>(world.Commerce.AuctionHouses.Keys); houseIds.Sort(StringComparer.Ordinal);
            foreach (var houseId in houseIds)
            {
                if (!world.Commerce.AuctionDefinitions.TryGetValue(houseId, out var definition)) continue;
                var house = world.Commerce.AuctionHouses[houseId];
                var listings = new List<AuctionListing>(house.Listings.Values);
                listings.Sort((a,b) => { var c=a.EndTick.CompareTo(b.EndTick); return c != 0 ? c : a.Sequence.CompareTo(b.Sequence); });
                foreach (var listing in listings)
                {
                    if (listing.Status != AuctionListingStatus.Open) continue;
                    if (world.Tick.Value >= listing.EndTick) TrySettle(world, definition, house, listing);
                    else if (world.Tick.Value >= listing.NextMarketReviewTick) ReviewMarket(world, definition, listing);
                }
            }
        }

        public static void ResetForDebug(CommerceState commerce, string houseId, ulong tick)
        {
            var definition = commerce.AuctionDefinitions[houseId];
            var house = new AuctionHouseRuntime();
            commerce.AuctionHouses[houseId] = house;
            foreach (var spec in definition.InitialListings)
            {
                if (!TrySchedule(tick, definition, out var end, out var review)) throw new InvalidOperationException("Auction schedule overflow.");
                var sequence = house.NextListingSequence++;
                var id = houseId + ":listing:" + sequence;
                house.Listings.Add(id, NewListing(id, sequence, houseId, AuctionSellerKind.Market, spec.ItemId, spec.Quantity, spec.StartingPrice.Grade, spec.StartingPrice.Amount, tick, end, review));
            }
        }

        static AuctionListing NewListing(string id, ulong sequence, string houseId, AuctionSellerKind seller, string itemId, int quantity, SpiritStoneGrade grade, long start, ulong created, ulong end, ulong review) =>
            new AuctionListing { ListingId=id, Sequence=sequence, AuctionHouseId=houseId, SellerKind=seller, SellerMarker=seller==AuctionSellerKind.Player?"PlayerParty":"Market", ItemId=itemId, Quantity=quantity, Grade=grade, StartingPriceAmount=start, CreatedTick=created, EndTick=end, NextMarketReviewTick=review, Status=AuctionListingStatus.Open };

        static bool TrySchedule(ulong tick, AuctionHouseDefinition definition, out ulong end, out ulong review)
        {
            end = review = 0;
            try { end = checked(tick + checked((ulong)definition.DurationDays * (ulong)WorldTick.TicksPerDay)); review = checked(tick + checked((ulong)definition.MarketReviewIntervalDays * (ulong)WorldTick.TicksPerDay)); return true; }
            catch (OverflowException) { return false; }
        }

        static void ReviewMarket(SimulationWorld world, AuctionHouseDefinition definition, AuctionListing listing)
        {
            if (!world.InventoryCatalog.TryGet(listing.ItemId, out var item) || item.BaseTradePrice == null || item.BaseTradePrice.Grade != listing.Grade) { AdvanceReview(world.Tick.Value, definition, listing); return; }
            long ceiling;
            try { ceiling = checked(item.BaseTradePrice.Amount * listing.Quantity); }
            catch (OverflowException) { AdvanceReview(world.Tick.Value, definition, listing); return; }
            if (listing.CurrentBidderKind == AuctionBidderKind.None && listing.StartingPriceAmount <= ceiling)
            { listing.CurrentBidAmount = listing.StartingPriceAmount; listing.CurrentBidderKind = AuctionBidderKind.Market; }
            else if (listing.CurrentBidderKind == AuctionBidderKind.Player && TryGetMinimumBid(definition, listing, out var next) && next <= ceiling &&
                world.Commerce.PlayerWallet.CanAdd(listing.Grade, listing.PlayerEscrowAmount))
            {
                var refund = listing.PlayerEscrowAmount;
                world.Commerce.PlayerWallet.Add(listing.Grade, refund);
                listing.CurrentBidAmount = next; listing.CurrentBidderKind = AuctionBidderKind.Market; listing.PlayerEscrowAmount = 0;
            }
            AdvanceReview(world.Tick.Value, definition, listing);
        }

        static void AdvanceReview(ulong currentTick, AuctionHouseDefinition definition, AuctionListing listing)
        {
            var interval = checked((ulong)definition.MarketReviewIntervalDays * (ulong)WorldTick.TicksPerDay);
            var due = currentTick >= listing.NextMarketReviewTick ? (currentTick - listing.NextMarketReviewTick) / interval + 1UL : 1UL;
            try { listing.NextMarketReviewTick = checked(listing.NextMarketReviewTick + checked(due * interval)); }
            catch (OverflowException) { listing.NextMarketReviewTick = ulong.MaxValue; }
        }

        static bool TrySettle(SimulationWorld world, AuctionHouseDefinition definition, AuctionHouseRuntime house, AuctionListing listing)
        {
            if (listing.CurrentBidderKind == AuctionBidderKind.None)
            {
                if (listing.SellerKind == AuctionSellerKind.Player && !TryCreateClaim(world, house, listing, AuctionClaimKind.UnsoldItem)) return false;
            }
            else if (listing.CurrentBidderKind == AuctionBidderKind.Market && listing.SellerKind == AuctionSellerKind.Player)
            {
                var fee = Fee(listing.CurrentBidAmount, definition.FeePercent); var net = listing.CurrentBidAmount - fee;
                if (!world.Commerce.PlayerWallet.CanAdd(listing.Grade, net) || !house.HouseWallet.CanAdd(listing.Grade, fee)) return false;
                world.Commerce.PlayerWallet.Add(listing.Grade, net); house.HouseWallet.Add(listing.Grade, fee);
            }
            else if (listing.CurrentBidderKind == AuctionBidderKind.Player)
            {
                if (listing.PlayerEscrowAmount != listing.CurrentBidAmount || !house.HouseWallet.CanAdd(listing.Grade, listing.PlayerEscrowAmount) ||
                    !TryCreateClaim(world, house, listing, AuctionClaimKind.WonItem)) return false;
                house.HouseWallet.Add(listing.Grade, listing.PlayerEscrowAmount); listing.PlayerEscrowAmount = 0;
            }
            listing.Status = AuctionListingStatus.Settled;
            return true;
        }

        static bool TryCreateClaim(SimulationWorld world, AuctionHouseRuntime house, AuctionListing listing, AuctionClaimKind kind)
        {
            if (house.NextClaimSequence == 0 || house.NextClaimSequence == ulong.MaxValue) return false;
            var sequence = house.NextClaimSequence; var id = listing.AuctionHouseId + ":claim:" + sequence;
            if (house.Claims.ContainsKey(id)) return false;
            house.Claims.Add(id, new AuctionClaim { ClaimId=id, Sequence=sequence, AuctionHouseId=listing.AuctionHouseId, ClaimKind=kind, ItemId=listing.ItemId, Quantity=listing.Quantity, SourceListingId=listing.ListingId, CreatedTick=world.Tick.Value });
            house.NextClaimSequence = sequence + 1; return true;
        }

        static long Fee(long gross, int percent) => checked((gross / 100L) * percent + ((gross % 100L) * percent) / 100L);
    }
}
