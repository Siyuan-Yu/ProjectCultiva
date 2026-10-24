using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Immutable opening perimeter, rebound from authored Surface walls after NewGame/Load.</summary>
    public sealed class HuangcunOpeningArea
    {
        public string SurfaceId { get; private set; } = string.Empty;
        public float MinX { get; private set; }
        public float MinY { get; private set; }
        public float MaxX { get; private set; }
        public float MaxY { get; private set; }
        public float CellSize { get; private set; }
        public bool IsConfigured => !string.IsNullOrEmpty(SurfaceId) && MaxX > MinX && MaxY > MinY;

        // Session-only development override; deliberately absent from Snapshot and story flags.
        public bool DebugBypassMovementRestriction { get; set; }

        public void Clear() { SurfaceId = string.Empty; DebugBypassMovementRestriction = false; }

        public void Configure(string surfaceId, float minX, float minY, float maxX, float maxY, float cellSize)
        {
            SurfaceId = surfaceId ?? string.Empty;
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; CellSize = cellSize;
        }

        public bool Contains(string surfaceId, float x, float y)
        {
            // Half a cell keeps the entire one-cell footprint inside, not just its centre.
            var inset = CellSize * .5f;
            return IsConfigured && string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal) &&
                x >= MinX + inset && y >= MinY + inset && x <= MaxX - inset && y <= MaxY - inset;
        }
    }

    /// <summary>ADR-0052. Flags persist progress; existing Site ownership alone grants liberation.</summary>
    public static class HuangcunOpeningAccess
    {
        public const string ConfinedFlag = "story:huangcun_confined_opening";
        public const string UnlockedFlag = "story:huangcun_world_unlocked";
        public const string BlockedMessage = "村界仍被封锁；取得荒村后才能离开。";
        public const string ManagementLockedMessage = "取得荒村后，才能经营领地与建造。";

        public static bool IsLocked(SimulationWorld world) =>
            world != null && world.Flags.Has(ConfinedFlag) && !world.Flags.Has(UnlockedFlag) &&
            !OwnsVillage(world);

        static bool OwnsVillage(SimulationWorld world) =>
            world.Strategic.Sites.TryGet(Ch01ScenarioProgressionHooks.HuangcunSiteId, out var site) &&
            string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);

        public static void RefreshUnlock(SimulationWorld world)
        {
            if (world == null || !world.Flags.Has(ConfinedFlag) || world.Flags.Has(UnlockedFlag) ||
                !OwnsVillage(world)) return;
            world.Flags.Set(UnlockedFlag);
            world.Flags.Set(Ch01ScenarioProgressionHooks.FlagPlayerFactionPoliticallyActive);
        }

        public static bool IsMovementLocked(SimulationWorld world) =>
            IsLocked(world) && !world.OpeningArea.DebugBypassMovementRestriction;

        public static bool AllowsPosition(SimulationWorld world, string surfaceId, float x, float y) =>
            !IsMovementLocked(world) || world.OpeningArea.Contains(surfaceId, x, y);

        public static bool IsSimulated(SimulationWorld world, Entity entity)
        {
            if (entity == null) return false;
            if (!IsLocked(world)) return true;
            if (world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, entity.Id)) return true;
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at)) return false;
            return at.HasWorldPosition
                ? world.OpeningArea.Contains(at.SurfaceId, at.WorldPosition.X, at.WorldPosition.Y)
                : string.Equals(at.SiteId, Ch01ScenarioProgressionHooks.HuangcunSiteId, StringComparison.Ordinal);
        }

        public static bool IsSimulated(SimulationWorld world, EntityId id) =>
            world != null && world.Entities.TryGet(id, out var entity) && IsSimulated(world, entity);

        /// <summary>Exclude suspended time from existing elapsed-time consumers, without advancing gameplay.</summary>
        public static void KeepDormantTimeCurrent(SimulationWorld world, Entity entity)
        {
            var now = world.Tick.Value;
            if (entity.TryGet<PersonalLifeComponent>(out var personal)) personal.LastProcessedTick = now;
            if (world.Civilians.TryGet(entity.Id, out var civilian))
            {
                civilian.LastUpdateTick = now;
                civilian.BackgroundNeedsLastProcessedTick = now;
                civilian.CareDayIndex = now / Domain.Time.WorldTick.TicksPerDay;
            }
            if (world.BackgroundCharacterTravel.TryGet(entity.Id, out var motion) && motion != null)
                motion.LastProcessedWorldTick = now;
            // Existing decay timestamps are absolute WorldTicks; shift them while suspended.
            if (entity.TryGet<LifecycleComponent>(out var life) && life.BleedOutAfterTick > 0)
                life.BleedOutAfterTick++;
            if (entity.TryGet<Combat.CorpseComponent>(out var corpse) && corpse.RemoveAfterTick > 0)
                corpse.RemoveAfterTick++;
        }

        public static void HoldExternalMarkets(SimulationWorld world)
        {
            foreach (var house in world.Commerce.AuctionHouses.Values)
                foreach (var listing in house.Listings.Values)
                    if (listing.Status == Inventory.AuctionListingStatus.Open)
                    {
                        listing.CreatedTick++;
                        listing.EndTick++;
                        listing.NextMarketReviewTick++;
                    }
        }
    }
}
