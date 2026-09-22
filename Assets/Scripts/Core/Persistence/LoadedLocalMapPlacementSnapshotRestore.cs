using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.Persistence
{
    /// <summary>
    /// Save/Load 当前 Loaded LocalMap Character 表现落点（非 WorldLocation 真源）。
    /// Active Separate Space：捕获该图全部 persistent Character local placement，
    /// 不只 PlayerParty occupants。
    /// </summary>
    public static class LoadedLocalMapPlacementSnapshotRestore
    {
        readonly struct PlacementKey
        {
            public readonly ulong CharacterId;
            public readonly string LocalMapId;

            public PlacementKey(ulong characterId, string localMapId)
            {
                CharacterId = characterId;
                LocalMapId = localMapId ?? string.Empty;
            }
        }

        static readonly Dictionary<PlacementKey, (float X, float Z)> Pending =
            new Dictionary<PlacementKey, (float, float)>();

        public static bool DeferFollowRebind { get; private set; }

        public static int PendingCount => Pending.Count;

        public static void BeginRestoreFromSnapshot(StrategicSnapshotDto dto)
        {
            LoadFromDto(dto);
            DeferFollowRebind = Pending.Count > 0;
        }

        public static void FinishRestorePresentation()
        {
            Pending.Clear();
            DeferFollowRebind = false;
        }

        public static void LoadFromDto(StrategicSnapshotDto dto)
        {
            Pending.Clear();
            if (dto?.LoadedLocalMapCharacterPlacements == null)
                return;

            for (var i = 0; i < dto.LoadedLocalMapCharacterPlacements.Count; i++)
            {
                var p = dto.LoadedLocalMapCharacterPlacements[i];
                if (p == null || p.CharacterId == 0 || string.IsNullOrWhiteSpace(p.LocalMapId))
                    continue;
                Pending[new PlacementKey(p.CharacterId, p.LocalMapId.Trim())] = (p.LocalX, p.LocalZ);
            }
        }

        public static void Capture(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.LocalMap == null || dto == null)
                return;

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return;

            dto.LoadedLocalMapCharacterPlacements.Clear();
            foreach (var entity in world.Entities.All)
            {
                if (!TryResolveCapturePlacement(world, entity, mapId, out var x, out var z))
                    continue;

                dto.LoadedLocalMapCharacterPlacements.Add(new LoadedLocalMapCharacterPlacementSnapshotDto
                {
                    CharacterId = entity.Id.Value,
                    LocalMapId = mapId,
                    LocalX = x,
                    LocalZ = z
                });
            }
        }

        /// <summary>
        /// Active Separate Space 中可持久化的 Character：属于 ActiveMapLayoutId、
        /// 有 Interior EntityLocation（或 session occupant）且有合法 local placement。
        /// </summary>
        public static bool BelongsToActiveSeparateSpaceMap(
            SimulationWorld world,
            Entity entity,
            string mapId)
        {
            if (world?.LocalMap == null || entity == null || string.IsNullOrEmpty(mapId))
                return false;
            if ((entity.Tags & EntityTag.Character) == 0)
                return false;

            if (world.LocalMap.IsActive &&
                world.LocalMap.ContainsOccupant(entity.Id) &&
                string.Equals(
                    world.LocalMap.ActiveMapLayoutId?.Trim(),
                    mapId,
                    System.StringComparison.Ordinal))
                return true;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null ||
                !loc.HasLocation)
                return false;

            return world.LocalPlaces.TryGet(loc.LocationId, out var place) &&
                   !string.IsNullOrEmpty(place.LocalMapId) &&
                   string.Equals(place.LocalMapId, mapId, System.StringComparison.Ordinal);
        }

        static bool TryResolveCapturePlacement(
            SimulationWorld world,
            Entity entity,
            string mapId,
            out float x,
            out float z)
        {
            x = 0f;
            z = 0f;
            if (!BelongsToActiveSeparateSpaceMap(world, entity, mapId))
                return false;
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                return false;
            if (!loc.HasPresentationOverride)
                return false;

            x = loc.PresentationOverrideX;
            z = loc.PresentationOverrideZ;
            return true;
        }

        /// <summary>
        /// Materialize 前：把 Pending Saved 落点写入 Domain PresentationOverride。
        /// 不通过 placement 扩展 Party／session occupancy（与 membership 严格分离）。
        /// </summary>
        public static int ApplySavedPlacementsToDomain(SimulationWorld world, string localMapId)
        {
            if (world?.LocalMap == null || Pending.Count == 0)
                return 0;

            var mapId = localMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return 0;

            var applied = 0;
            foreach (var kv in Pending)
            {
                if (!string.Equals(kv.Key.LocalMapId, mapId, System.StringComparison.Ordinal))
                    continue;

                var id = new EntityId(kv.Key.CharacterId);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;

                if (!ent.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                loc.SetPresentationOverride(kv.Value.X, kv.Value.Z);
                applied++;
            }

            return applied;
        }

        public static bool TryGetPlacement(EntityId id, string localMapId, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            if (id.IsNone || string.IsNullOrWhiteSpace(localMapId))
                return false;

            if (!Pending.TryGetValue(new PlacementKey(id.Value, localMapId.Trim()), out var placement))
                return false;

            x = placement.X;
            z = placement.Z;
            return true;
        }
    }
}
