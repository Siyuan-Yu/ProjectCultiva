using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>One active loyalty value for a real Character and its current faction.</summary>
    public sealed class CharacterFactionLoyaltyEntry
    {
        public EntityId CharacterId { get; set; }
        public string FactionId { get; set; } = string.Empty;
        public int Loyalty { get; set; }
    }

    public sealed class CharacterFactionLoyaltyLedger
    {
        readonly Dictionary<EntityId, CharacterFactionLoyaltyEntry> _entries =
            new Dictionary<EntityId, CharacterFactionLoyaltyEntry>();
        public IEnumerable<CharacterFactionLoyaltyEntry> Entries => _entries.Values;
        public bool TryGet(EntityId characterId, out CharacterFactionLoyaltyEntry entry) =>
            _entries.TryGetValue(characterId, out entry);
        public bool Restore(CharacterFactionLoyaltyEntry entry)
        {
            if (entry == null || entry.CharacterId.IsNone || string.IsNullOrWhiteSpace(entry.FactionId) ||
                entry.Loyalty < 0 || entry.Loyalty > 100 || _entries.ContainsKey(entry.CharacterId)) return false;
            _entries.Add(entry.CharacterId, entry);
            return true;
        }
        public void Set(EntityId characterId, string factionId, int loyalty) =>
            _entries[characterId] = new CharacterFactionLoyaltyEntry {
                CharacterId = characterId, FactionId = factionId,
                Loyalty = Math.Max(0, Math.Min(100, loyalty))
            };
        public void Remove(EntityId characterId) => _entries.Remove(characterId);
    }

    public static class CharacterFactionLoyaltyService
    {
        public const int DefaultLoyalty = 50;

        public static bool TryGetLoyalty(SimulationWorld world, EntityId characterId, out int loyalty)
        {
            loyalty = 0;
            if (!TryCurrentFaction(world, characterId, out var membership))
            {
                world?.FactionLoyalties.Remove(characterId);
                return false;
            }
            if (!world.FactionLoyalties.TryGet(characterId, out var entry) ||
                !string.Equals(entry.FactionId, membership.FactionId, StringComparison.Ordinal))
            {
                world.FactionLoyalties.Set(characterId, membership.FactionId, membership.InitialLoyalty);
                world.FactionLoyalties.TryGet(characterId, out entry);
            }
            loyalty = entry.Loyalty;
            return true;
        }

        public static bool SetLoyalty(SimulationWorld world, EntityId characterId, int loyalty)
        {
            if (!TryCurrentFaction(world, characterId, out var membership))
            {
                world?.FactionLoyalties.Remove(characterId);
                return false;
            }
            world.FactionLoyalties.Set(characterId, membership.FactionId, loyalty);
            return true;
        }

        public static bool AdjustLoyalty(SimulationWorld world, EntityId characterId, int delta)
        {
            if (!TryGetLoyalty(world, characterId, out var current)) return false;
            return SetLoyalty(world, characterId, (int)Math.Max(0L, Math.Min(100L, (long)current + delta)));
        }

        public static void EnsureAll(SimulationWorld world)
        {
            if (world == null) return;
            foreach (var entity in world.Entities.All)
                TryGetLoyalty(world, entity.Id, out _);
        }

        static bool TryCurrentFaction(SimulationWorld world, EntityId characterId,
            out FactionMembershipComponent membership)
        {
            membership = null;
            return world != null && world.Entities.TryGet(characterId, out var entity) &&
                entity.TryGet(out membership) && membership.IsAffiliated;
        }
    }
}
