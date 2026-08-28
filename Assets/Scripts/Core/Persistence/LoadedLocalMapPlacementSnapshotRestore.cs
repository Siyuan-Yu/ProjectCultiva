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
    /// </summary>
    public static class LoadedLocalMapPlacementSnapshotRestore
    {
        public enum SpawnPlacementSource
        {
            DefaultStart = 0,
            SnapshotLocalPlacement = 1
        }

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

        /// <summary>Snapshot Load 流程中：禁止 Start Snap / Follow 首帧覆盖 Saved 落点。</summary>
        public static bool IsRestoringFromSnapshot { get; private set; }

        public static bool DeferFollowRebind { get; private set; }

        public static int PendingCount => Pending.Count;

        public static void Clear()
        {
            Pending.Clear();
            IsRestoringFromSnapshot = false;
            DeferFollowRebind = false;
        }

        public static void BeginRestoreFromSnapshot(StrategicSnapshotDto dto)
        {
            LoadFromDto(dto);
            IsRestoringFromSnapshot = Pending.Count > 0;
            DeferFollowRebind = IsRestoringFromSnapshot;
        }

        public static void FinishRestorePresentation()
        {
            Pending.Clear();
            IsRestoringFromSnapshot = false;
            DeferFollowRebind = false;
        }

        public static bool HasRestoredPlacementsForMap(string localMapId)
        {
            var mapId = localMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return false;

            foreach (var kv in Pending)
            {
                if (string.Equals(kv.Key.LocalMapId, mapId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
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
                if (entity == null || (entity.Tags & EntityTag.Character) == 0)
                    continue;
                if (!world.LocalMap.ContainsOccupant(entity.Id))
                    continue;
                if (!entity.TryGet<EntityLocationComponent>(out var loc) ||
                    loc == null ||
                    !loc.HasPresentationOverride)
                    continue;

                dto.LoadedLocalMapCharacterPlacements.Add(new LoadedLocalMapCharacterPlacementSnapshotDto
                {
                    CharacterId = entity.Id.Value,
                    LocalMapId = mapId,
                    LocalX = loc.PresentationOverrideX,
                    LocalZ = loc.PresentationOverrideZ
                });
            }
        }

        /// <summary>
        /// Materialize 前：把 Pending Saved 落点写入 Domain（AddOccupant + PresentationOverride）。
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

                world.LocalMap.AddOccupant(id);
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

        public static bool TryGetPendingPlacement(ulong characterId, string localMapId, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            if (characterId == 0 || string.IsNullOrWhiteSpace(localMapId))
                return false;

            if (!Pending.TryGetValue(new PlacementKey(characterId, localMapId.Trim()), out var placement))
                return false;

            x = placement.X;
            z = placement.Z;
            return true;
        }

        /// <summary>WorldSite Materialize：Saved LocalPlacement 优先于 Default Start。</summary>
        public static bool TryResolveWorldSiteSpawnPosition(
            EntityId id,
            string localMapId,
            float defaultX,
            float defaultZ,
            out float x,
            out float z,
            out SpawnPlacementSource source)
        {
            if (TryGetPlacement(id, localMapId, out x, out z))
            {
                source = SpawnPlacementSource.SnapshotLocalPlacement;
                return true;
            }

            x = defaultX;
            z = defaultZ;
            source = SpawnPlacementSource.DefaultStart;
            return false;
        }

        public static string DescribeWorldLocation(SimulationWorld world, EntityId id)
        {
            if (world?.WorldPresence == null || id.IsNone)
                return "Unknown";

            if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                return "NoPresence";

            if (wp.Mode == PartyWorldPresenceMode.AtSite)
                return "AtWorldSite(" + (wp.SiteId ?? string.Empty) + ")";

            if (wp.Mode == PartyWorldPresenceMode.AtHex || wp.UsesHexPresence)
                return "AtHex(" + wp.ResidualHex + ")";

            return wp.Mode.ToString();
        }
    }
}
