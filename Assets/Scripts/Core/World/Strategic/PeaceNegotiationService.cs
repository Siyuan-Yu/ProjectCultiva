using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    public enum PeaceOfferStatus { Draft, CounterOffer, Accepted, Cancelled }

    /// <summary>Persistent offer, owned by the world; UI never grants resources or changes custody.</summary>
    public sealed class PeaceNegotiation
    {
        public string OtherFaction = string.Empty;
        public PeaceOfferStatus Status;
        public int Round;
        public bool Vassalage;
        public string ResourceId = string.Empty;
        public string PlayerSite = string.Empty, OtherSite = string.Empty;
        /// <summary>Positive = compensation to player, negative = player payment.</summary>
        public int Amount;
        public List<ulong> ReleasePrisoners = new List<ulong>();
        public string Message = string.Empty;
        public PeaceNegotiation Copy() => new PeaceNegotiation
        {
            OtherFaction = OtherFaction, Status = Status, Round = Round, Vassalage = Vassalage,
            ResourceId = ResourceId, PlayerSite = PlayerSite, OtherSite = OtherSite,
            Amount = Amount, ReleasePrisoners = new List<ulong>(ReleasePrisoners), Message = Message
        };
    }

    public static class PeaceNegotiationService
    {
        public static bool HasPeace(SimulationWorld world, string a, string b)
        {
            if (world == null || WarGateService.IsAtWar(world, a, b)) return false;
            var player = world.Strategic.PlayerFactionId;
            if (a != player && b != player) return false;
            var other = a == player ? b : a;
            foreach (var offer in world.Strategic.PeaceNegotiations)
                if (offer.OtherFaction == other && offer.Status == PeaceOfferStatus.Accepted) return true;
            return false;
        }

        public static PeaceNegotiation Open(SimulationWorld world, string faction)
        {
            foreach (var offer in world.Strategic.PeaceNegotiations)
                if (offer.OtherFaction == faction && offer.Status != PeaceOfferStatus.Accepted &&
                    offer.Status != PeaceOfferStatus.Cancelled) return offer;
            var created = new PeaceNegotiation { OtherFaction = faction };
            world.Strategic.PeaceNegotiations.Add(created);
            return created;
        }

        public static string CustodianFaction(SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var e) || !MortalCivilianQuery.IsManagedCivilian(world, e) ||
                !e.TryGet<LifecycleComponent>(out var life) ||
                life.State != LifecycleState.Captured || !world.Civilians.TryGet(id, out var state)) return string.Empty;
            if (state.Disposition == CivilianDisposition.CapturedEscorted &&
                world.Entities.TryGet(state.CarrierId, out var carrier) &&
                carrier.TryGet<FactionMembershipComponent>(out var membership)) return membership.FactionId;
            if (state.Disposition == CivilianDisposition.Detained &&
                world.OutdoorConstructedAssets.Assets.TryGetValue(state.DetainedResidenceWorkAreaId, out var room) &&
                world.Strategic.Sites.TryGet(room.BoundWorldSiteId, out var site)) return site.OwnerFactionId;
            return string.Empty;
        }

        public static Result Validate(SimulationWorld world, PeaceNegotiation offer)
        {
            if (world == null || offer == null || !world.Strategic.PeaceNegotiations.Contains(offer) ||
                offer.OtherFaction == world.Strategic.PlayerFactionId || string.IsNullOrEmpty(offer.OtherFaction) ||
                offer.Status == PeaceOfferStatus.Accepted || offer.Status == PeaceOfferStatus.Cancelled)
                return Invalid("谈判已结束或对象无效。");
            if (world.Strategic.CharacterEncounter != null || world.Strategic.ContinuousManualCombat.IsActive)
                return Invalid("请先结束当前交战，再进行势力谈判。");
            if (!WarGateService.IsAtWar(world, world.Strategic.PlayerFactionId, offer.OtherFaction))
                return Invalid("双方当前没有需要结束的战争。");
            if (offer.Amount == int.MinValue) return Invalid("赔偿数量无效。");
            if (offer.Vassalage && !CanVassalize(world, offer.OtherFaction))
                return Invalid("现有附庸关系不允许建立这条附庸；不允许套娃或转卖其他宗主的附庸。");
            if (offer.Amount != 0)
            {
                var player = world.Strategic.PlayerFactionId;
                if (string.IsNullOrEmpty(offer.ResourceId) ||
                    !world.Strategic.Sites.TryGet(offer.PlayerSite, out var ps) || ps.OwnerFactionId != player || !ps.IsCoreActive ||
                    !world.Strategic.Sites.TryGet(offer.OtherSite, out var os) || os.OwnerFactionId != offer.OtherFaction || !os.IsCoreActive)
                    return Invalid("资源交付必须选择双方各自实际控制的有效地点。");
                var from = offer.Amount > 0 ? os.SiteId : ps.SiteId;
                var to = offer.Amount > 0 ? ps.SiteId : os.SiteId;
                var count = Math.Abs(offer.Amount);
                if (WorldSitePublicStockService.GetCount(world, from, offer.ResourceId) < count ||
                    WorldSitePublicStockService.GetCount(world, to, offer.ResourceId) > int.MaxValue - count)
                    return Invalid("实际库存不足，或接收公库已满；请修改条件。");
            }
            var seen = new HashSet<ulong>();
            foreach (var id in offer.ReleasePrisoners)
            {
                var faction = CustodianFaction(world, new EntityId(id));
                if (!seen.Add(id) || (faction != world.Strategic.PlayerFactionId && faction != offer.OtherFaction))
                    return Invalid("只能释放双方实际关押的俘虏，不能交换自由人物。");
                if (world.Strategic.Squads.TryGetForCharacter(new EntityId(id), out var squad) &&
                    SquadMembershipService.IsBattleLocked(world, squad))
                    return Invalid("请先完成俘虏所在战斗的结算。");
            }
            return Result.Success();
        }

        public static bool CanVassalize(SimulationWorld world, string faction)
        {
            var board = world.Strategic.Vassalages;
            if (board.IsVassal(faction) || board.IsVassal(world.Strategic.PlayerFactionId)) return false;
            // Existing board's minimum API does not guard this reverse nesting case.
            foreach (var pair in board.All) if (pair.Value == faction) return false;
            return true;
        }

        static double Strength(SimulationWorld world, string faction)
        {
            double score = 1;
            foreach (var e in world.Entities.All)
                if (CombatLifeStateService.CanFight(e) && e.TryGet<FactionMembershipComponent>(out var m) &&
                    m.FactionId == faction && e.TryGet<AttributesComponent>(out var stats))
                    score += Math.Max(1, stats.GetFinal(AttributeId.Attack)) +
                        (e.TryGet<CombatVitalsComponent>(out var hp) ? Math.Max(0, hp.CurrentHp) :
                         Math.Max(1, stats.GetFinal(AttributeId.MaxHp))) * .1;
            return score;
        }

        /// <summary>No RNG reroll: the counter follows current strength, actual stocks and reciprocal releases.</summary>
        public static Result Propose(SimulationWorld world, PeaceNegotiation offer)
        {
            var valid = Validate(world, offer); if (valid.IsFailure) return valid;
            var ratio = Strength(world, world.Strategic.PlayerFactionId) / Strength(world, offer.OtherFaction);
            var cap = 0;
            if (!string.IsNullOrEmpty(offer.OtherSite) && !string.IsNullOrEmpty(offer.ResourceId))
                cap = (int)(WorldSitePublicStockService.GetCount(world, offer.OtherSite, offer.ResourceId) *
                    Math.Max(0, Math.Min(.75, (ratio - 1) * .25)));
            var ourHeld = new List<ulong>();
            var theirHeld = new List<ulong>();
            foreach (var e in world.Entities.All)
            {
                var owner = CustodianFaction(world, e.Id);
                if (owner == world.Strategic.PlayerFactionId) ourHeld.Add(e.Id.Value);
                else if (owner == offer.OtherFaction) theirHeld.Add(e.Id.Value);
            }
            ourHeld.Sort(); theirHeld.Sort();
            int requestedRelease = 0, offeredRelease = 0;
            foreach (var id in offer.ReleasePrisoners)
                if (theirHeld.Contains(id)) requestedRelease++; else offeredRelease++;
            var generousEnough = requestedRelease <= offeredRelease || ratio >= 1.5;
            var acceptable = offer.Amount <= cap && (!offer.Vassalage || ratio >= 2) && generousEnough;
            offer.Round++;
            if (acceptable) return Commit(world, offer);
            offer.Amount = Math.Min(offer.Amount, cap);
            if (ratio < 2) offer.Vassalage = false;
            // Counter-release is explicit and visible; nothing is released until player accepts.
            if (!generousEnough)
                foreach (var id in ourHeld)
                {
                    if (offeredRelease >= requestedRelease) break;
                    if (offer.ReleasePrisoners.Contains(id)) continue;
                    offer.ReleasePrisoners.Add(id); offeredRelease++;
                }
            while (requestedRelease > offeredRelease && ratio < 1.5)
            {
                var index = offer.ReleasePrisoners.FindLastIndex(id => theirHeld.Contains(id));
                if (index < 0) break;
                offer.ReleasePrisoners.RemoveAt(index); requestedRelease--;
            }
            offer.Status = PeaceOfferStatus.CounterOffer;
            offer.Message = "对方提出还价：请核对赔偿、释放名单和附庸条款，可接受或修改后再谈。";
            return Result.Success();
        }

        public static Result AcceptCounter(SimulationWorld world, PeaceNegotiation offer) =>
            offer != null && offer.Status == PeaceOfferStatus.CounterOffer
                ? Commit(world, offer) : Invalid("没有可接受的对方还价。");

        static Result Commit(SimulationWorld world, PeaceNegotiation offer)
        {
            var valid = Validate(world, offer); if (valid.IsFailure) return valid;
            // All preconditions are checked before any mutation; this is one synchronous Core transaction.
            if (offer.Vassalage && !world.Strategic.Vassalages.TryBindVassalage(offer.OtherFaction, world.Strategic.PlayerFactionId))
                return Invalid("附庸条件已变化，请重新谈判。");
            if (offer.Vassalage) world.Strategic.Alliances.TryLeaveAlliance(offer.OtherFaction, out _, out _);
            if (offer.Amount != 0)
            {
                var from = offer.Amount > 0 ? offer.OtherSite : offer.PlayerSite;
                var to = offer.Amount > 0 ? offer.PlayerSite : offer.OtherSite;
                var count = Math.Abs(offer.Amount);
                WorldSitePublicStockService.TryRemove(world, from, offer.ResourceId, count);
                WorldSitePublicStockService.TryAdd(world, to, offer.ResourceId, count);
            }
            foreach (var id in offer.ReleasePrisoners) ReleaseDirectly(world, new EntityId(id));
            WarGateService.MakePeace(world, world.Strategic.PlayerFactionId, offer.OtherFaction);
            offer.Status = PeaceOfferStatus.Accepted;
            offer.Message = "谈判成交：双方势力停战，资源和俘虏释放条款已一次性结算。";
            return Result.Success();
        }

        static void ReleaseDirectly(SimulationWorld world, EntityId id)
        {
            // Release existing custody first; retain identity, faction, wounds and HP.
            var released = MortalCivilianService.Release(world, id);
            if (released.IsFailure || !world.Entities.TryGet(id, out var entity) ||
                !world.Civilians.TryGet(id, out var civilian)) return;
            BackgroundCharacterTravelService.CancelTravelIfAny(world, id);
            if (world.Strategic.PlayerPartyContext?.IsMember(id) != true &&
                world.Strategic.Squads.TryGetForCharacter(id, out _))
            {
                SquadMembershipService.LeaveToSingleton(world, id);
                world.Strategic.SquadWorldMotions.Remove(SquadMembershipService.SingletonSquadId(id));
            }
            civilian.Disposition = CivilianDisposition.Normal;
            civilian.HasFleeTarget = false;
            civilian.FleeStartedTick = 0;
            if (entity.TryGet<MovementIntentComponent>(out var movement)) movement.Clear();
            if (!entity.TryGet<FactionMembershipComponent>(out var membership)) return;
            var sites = new List<string>();
            foreach (var site in world.Strategic.Sites.Sites.Values)
                if (site.IsCoreActive && site.OwnerFactionId == membership.FactionId) sites.Add(site.SiteId);
            sites.Sort(StringComparer.Ordinal);
            foreach (var siteId in sites)
                if (world.SurfaceGround.TryResolveSiteArrival(siteId, out var surfaceId, out var arrival) &&
                    world.SurfaceGround.TryGet(surfaceId, out var navigation) && navigation != null &&
                    navigation.IsWalkable(arrival.X, arrival.Y))
                {
                    world.WorldPresence.SetAtSiteWithAnchor(id, siteId, arrival, surfaceId);
                    civilian.CurrentSiteId = siteId;
                    return;
                }
            // No legal home Site: free the person at their current real position, never invent a destination.
        }

        static Result Invalid(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
