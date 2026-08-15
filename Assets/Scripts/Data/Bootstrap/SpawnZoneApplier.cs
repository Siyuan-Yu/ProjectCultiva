using System;
using XianXia.Core.Bootstrap;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// 按 mapLayout 的 spawnZone＋spawnTable 刷 NPC（引用角色定义，不另建敌人类型）。
    /// </summary>
    public static class SpawnZoneApplier
    {
        public const string FlagPrefix = "spawn_zone:";

        public static Result ApplyAll(
            SimulationWorld world,
            DefinitionRegistry registry,
            IRandomSource random = null)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SpawnZone args null.");

            random = random ?? world.Random ?? new DeterministicRandom(1);
            foreach (var kv in registry.MapLayouts)
            {
                var applied = ApplyMap(world, registry, kv.Value, random);
                if (applied.IsFailure)
                    return applied;
            }

            return Result.Success();
        }

        public static Result ApplyMap(
            SimulationWorld world,
            DefinitionRegistry registry,
            MapLayoutDefinition layout,
            IRandomSource random)
        {
            if (world == null || registry == null || layout?.Placements == null)
                return Result.Success();

            random = random ?? world.Random ?? new DeterministicRandom(1);
            var mapKey = layout.Id.ToString();
            for (var i = 0; i < layout.Placements.Count; i++)
            {
                var p = layout.Placements[i];
                if (p == null ||
                    !string.Equals(p.Kind, "spawnZone", StringComparison.OrdinalIgnoreCase))
                    continue;

                var flag = FlagPrefix + mapKey + ":" + p.Id;
                if (world.Flags.Has(flag))
                    continue;

                var zone = ApplyZone(world, registry, layout, p, random);
                if (zone.IsFailure)
                    return zone;
                world.Flags.Set(flag);
            }

            return Result.Success();
        }

        static Result ApplyZone(
            SimulationWorld world,
            DefinitionRegistry registry,
            MapLayoutDefinition layout,
            MapPlacement zone,
            IRandomSource random)
        {
            if (string.IsNullOrWhiteSpace(zone.SpawnTableId))
                return Result.Failure(ErrorCode.MissingRequiredField, "spawnZone.spawnTableId required.", zone.Id);

            var tableParsed = DefinitionId.Parse(zone.SpawnTableId.Trim());
            if (tableParsed.IsFailure)
                return Result.Failure(tableParsed.Error);
            if (!registry.TryGetSpawnTable(tableParsed.Value, out var table) || table.Entries == null ||
                table.Entries.Count == 0)
            {
                return Result.Failure(ErrorCode.NotFound, "Spawn table missing.", zone.SpawnTableId);
            }

            var locationId = zone.BoundLocationId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return Result.Failure(
                    ErrorCode.MissingRequiredField,
                    "spawnZone.boundLocationId required (NPC 逻辑地点).",
                    zone.Id);
            }

            var rolls = BuildRolls(table, zone.SpawnCount, random);
            for (var r = 0; r < rolls.Count; r++)
            {
                var defId = rolls[r];
                var built = ContentGameStart.BuildSpawnFromDefinition(registry, defId, entityKindNpc: true);
                if (built.IsFailure)
                    return Result.Failure(built.Error);

                var spawned = GameStartBootstrap.SpawnIntoWorld(world, built.Value);
                if (spawned.IsFailure)
                    return Result.Failure(spawned.Error);

                var entity = spawned.Value;
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc = new EntityLocationComponent();
                    var added = entity.AddComponent(loc);
                    if (added.IsFailure)
                        return added;
                }

                loc.LocationId = locationId;
                PlaceInZone(layout, zone, loc, random);
            }

            return Result.Success();
        }

        static System.Collections.Generic.List<string> BuildRolls(
            SpawnTableDefinition table,
            int spawnCountOverride,
            IRandomSource random)
        {
            var list = new System.Collections.Generic.List<string>(8);
            if (spawnCountOverride > 0)
            {
                for (var i = 0; i < spawnCountOverride; i++)
                {
                    var pick = PickWeighted(table, random);
                    if (!string.IsNullOrEmpty(pick))
                        list.Add(pick);
                }

                return list;
            }

            for (var e = 0; e < table.Entries.Count; e++)
            {
                var entry = table.Entries[e];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                    continue;
                var n = entry.CountMin;
                if (entry.CountMax > entry.CountMin)
                    n = random.NextInt(entry.CountMin, entry.CountMax + 1);
                for (var i = 0; i < n; i++)
                    list.Add(entry.DefinitionId.Trim());
            }

            return list;
        }

        static string PickWeighted(SpawnTableDefinition table, IRandomSource random)
        {
            var total = 0;
            for (var i = 0; i < table.Entries.Count; i++)
            {
                var w = table.Entries[i]?.Weight ?? 0;
                if (w > 0)
                    total += w;
            }

            if (total <= 0)
                return table.Entries[0].DefinitionId;

            var roll = random.NextInt(0, total);
            var acc = 0;
            for (var i = 0; i < table.Entries.Count; i++)
            {
                var entry = table.Entries[i];
                if (entry == null || entry.Weight < 1)
                    continue;
                acc += entry.Weight;
                if (roll < acc)
                    return entry.DefinitionId?.Trim();
            }

            return table.Entries[table.Entries.Count - 1].DefinitionId?.Trim();
        }

        static void PlaceInZone(
            MapLayoutDefinition layout,
            MapPlacement zone,
            EntityLocationComponent loc,
            IRandomSource random)
        {
            var cs = layout.CellSize > 0.01f ? layout.CellSize : 1f;
            var w = Math.Max(1, zone.W);
            var h = Math.Max(1, zone.H);
            var cellX = zone.X + (w <= 1 ? 0 : random.NextInt(0, w));
            var cellY = zone.Y + (h <= 1 ? 0 : random.NextInt(0, h));
            var px = layout.OriginX + (cellX + 0.5f) * cs;
            var pz = layout.OriginY + (cellY + 0.5f) * cs;
            loc.SetPresentationOverride(px, pz);
        }
    }
}
