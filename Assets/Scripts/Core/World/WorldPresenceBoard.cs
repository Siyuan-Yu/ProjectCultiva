using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>
    /// Persistent character world presence. Continuous Outdoor presence uses an exact
    /// WorldPosition plus explicit Surface provenance.
    /// </summary>
    public sealed class WorldAgentPresence
    {
        public EntityId EntityId { get; set; }
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtSite;
        public string SiteId { get; set; } = string.Empty;
        public float WorldPosX { get; set; }
        public float WorldPosY { get; set; }
        public bool HasContinuousWorldPosition { get; set; }
        public string PersonalSurfaceId { get; set; } = string.Empty;

        public void SetAtWorldPosition(WorldVec2 position, string surfaceId)
        {
            PersonalSurfaceId = surfaceId ?? string.Empty;
            Mode = PartyWorldPresenceMode.AtWorldPosition;
            SiteId = string.Empty;
            HasContinuousWorldPosition = true;
            WorldPosX = position.X;
            WorldPosY = position.Y;
        }

        public void SetAtSiteWithAnchor(
            string siteId,
            WorldVec2 anchorWorldPosition,
            string surfaceId)
        {
            PersonalSurfaceId = surfaceId ?? string.Empty;
            Mode = PartyWorldPresenceMode.AtSite;
            SiteId = siteId ?? string.Empty;
            WorldPosX = anchorWorldPosition.X;
            WorldPosY = anchorWorldPosition.Y;
            HasContinuousWorldPosition = true;
        }

        public WorldVec2 ContinuousWorldPosition =>
            HasContinuousWorldPosition ? new WorldVec2(WorldPosX, WorldPosY) : default;
    }

    /// <summary>All persistent character macro-presence records.</summary>
    public sealed class WorldPresenceBoard
    {
        readonly Dictionary<ulong, WorldAgentPresence> _byEntity =
            new Dictionary<ulong, WorldAgentPresence>();

        public IReadOnlyDictionary<ulong, WorldAgentPresence> All => _byEntity;

        public void Clear() => _byEntity.Clear();

        public WorldAgentPresence GetOrCreate(EntityId id)
        {
            if (id.IsNone)
                throw new ArgumentException("EntityId required.");
            if (_byEntity.TryGetValue(id.Value, out var existing))
                return existing;
            var presence = new WorldAgentPresence { EntityId = id };
            _byEntity[id.Value] = presence;
            return presence;
        }

        public bool TryGet(EntityId id, out WorldAgentPresence presence)
        {
            presence = null;
            return !id.IsNone && _byEntity.TryGetValue(id.Value, out presence);
        }

        public bool Remove(EntityId id) =>
            !id.IsNone && _byEntity.Remove(id.Value);

        public void SetAtSite(EntityId id, string siteId)
        {
            var presence = GetOrCreate(id);
            presence.EntityId = id;
            presence.PersonalSurfaceId = string.Empty;
            presence.SiteId = siteId ?? string.Empty;
            presence.Mode = PartyWorldPresenceMode.AtSite;
            presence.HasContinuousWorldPosition = false;
            presence.WorldPosX = 0f;
            presence.WorldPosY = 0f;
        }

        public void SetAtSiteWithAnchor(
            EntityId id,
            string siteId,
            WorldVec2 anchorWorldPosition,
            string surfaceId = "")
        {
            var presence = GetOrCreate(id);
            presence.EntityId = id;
            presence.SetAtSiteWithAnchor(siteId, anchorWorldPosition, surfaceId);
        }

        public void SetAtWorldPosition(
            EntityId id,
            WorldVec2 position,
            string surfaceId)
        {
            var presence = GetOrCreate(id);
            presence.EntityId = id;
            presence.SetAtWorldPosition(position, surfaceId);
        }

        public void CollectAtSite(string siteId, List<EntityId> into)
        {
            if (into == null || string.IsNullOrEmpty(siteId))
                return;
            foreach (var pair in _byEntity)
            {
                var presence = pair.Value;
                if (presence != null &&
                    presence.Mode == PartyWorldPresenceMode.AtSite &&
                    string.Equals(presence.SiteId, siteId, StringComparison.Ordinal))
                    into.Add(presence.EntityId);
            }
        }
    }
}
