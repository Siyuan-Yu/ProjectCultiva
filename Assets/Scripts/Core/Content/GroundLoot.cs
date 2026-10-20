using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Content
{
    public sealed class GroundLootState
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public GroundLootState Copy() => (GroundLootState)MemberwiseClone();
        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(ItemId) &&
            !string.IsNullOrWhiteSpace(SurfaceId) && Count > 0 &&
            !float.IsNaN(WorldX) && !float.IsInfinity(WorldX) && !float.IsNaN(WorldY) && !float.IsInfinity(WorldY);
    }

    public static class GroundLootService
    {
        public static bool TryDropFromCharacter(SimulationWorld world, Entity entity, string itemId, int count)
        {
            if (world == null || entity == null || count <= 0 || string.IsNullOrEmpty(itemId)) return false;
            // Tactical/separate-space coordinates are not outdoor coordinates. Keep the held
            // unit until the existing return flow restores a trustworthy surface position.
            if (CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, entity.Id)) return false;
            string surface; float x, y;
            if (CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) && presence.HasWorldPosition)
            { surface = presence.SurfaceId; x = presence.WorldPosition.X; y = presence.WorldPosition.Y; }
            else if (world.WorldPresence.TryGet(entity.Id, out var saved) && saved.HasContinuousWorldPosition)
            { surface = saved.PersonalSurfaceId; x = saved.WorldPosX; y = saved.WorldPosY; }
            else return false;
            if (string.IsNullOrEmpty(surface) || !world.SurfaceSpatial.TryGet(surface, out var metric) ||
                !metric.ContainsWorldPosition(x, y)) return false;
            var prefix = "ground-loot:" + entity.Id.Value + ":" + world.Tick.Value + ":";
            var index = 0;
            while (world.GroundLoot.ContainsKey(prefix + index) || WorldLootPickupService.IsTaken(world, prefix + index)) index++;
            var drop = new GroundLootState { Id = prefix + index, ItemId = itemId, Count = count,
                SurfaceId = surface, WorldX = x, WorldY = y };
            if (!drop.IsValid) return false;
            world.GroundLoot.Add(drop.Id, drop);
            return true;
        }
    }
}
