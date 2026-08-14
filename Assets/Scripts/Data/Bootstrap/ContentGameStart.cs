using System;
using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.World;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Content → Core GameStart wiring. VS0.7+: opening spawns driven by openingScenario.
    /// </summary>
    public sealed class ContentGameStart
    {
        public static readonly DefinitionId DefaultPlayableScenarioId =
            new DefinitionId("base", "scenario_playable_day");

        readonly ContentPackageLoader _loader;
        readonly GameStartBootstrap _bootstrap;

        public ContentGameStart(ContentPackageLoader loader = null, GameStartBootstrap bootstrap = null)
        {
            _loader = loader ?? new ContentPackageLoader();
            _bootstrap = bootstrap ?? new GameStartBootstrap();
        }

        public Result<GameStartResult> StartVerticalSlice01(string packageDirectory, IRandomSource random = null)
        {
            var loaded = _loader.Load(new[] { packageDirectory });
            if (loaded.IsFailure)
                return Result.Fail<GameStartResult>(loaded.Error);

            return StartVerticalSlice01(loaded.Value, random);
        }

        /// <summary>
        /// Start from an already-loaded package (shared by Host／tests without double-load).
        /// </summary>
        public Result<GameStartResult> StartVerticalSlice01(LoadedContent loaded, IRandomSource random = null)
        {
            return StartFromScenario(loaded, DefaultPlayableScenarioId, random);
        }

        public Result<GameStartResult> StartFromScenario(
            LoadedContent loaded,
            DefinitionId scenarioId,
            IRandomSource random = null,
            string characterRosterId = null)
        {
            if (loaded == null || loaded.Registry == null)
                return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "LoadedContent is null.");
            if (loaded.Manifests == null || loaded.Manifests.Count == 0)
                return Result.Fail<GameStartResult>(ErrorCode.ContentLoadFailed, "LoadedContent has no manifests.");

            var registry = loaded.Registry;
            if (!registry.TryGetOpeningScenario(scenarioId, out var scenario))
            {
                return Result.Fail<GameStartResult>(
                    ErrorCode.NotFound,
                    "Opening scenario definition missing.",
                    scenarioId.ToString());
            }

            IList<OpeningSpawnEntry> spawnEntries = scenario.Spawns;
            if (!string.IsNullOrWhiteSpace(characterRosterId))
            {
                var rosterParsed = DefinitionId.Parse(characterRosterId.Trim());
                if (rosterParsed.IsFailure)
                    return Result.Fail<GameStartResult>(rosterParsed.Error);
                if (!registry.TryGetCharacterRoster(rosterParsed.Value, out var roster) ||
                    roster.Entries == null ||
                    roster.Entries.Count == 0)
                {
                    return Result.Fail<GameStartResult>(
                        ErrorCode.NotFound,
                        "Character roster missing or empty (export from CharacterNpcEditor).",
                        characterRosterId.Trim());
                }

                spawnEntries = roster.Entries;
            }

            var spawns = new List<CharacterSpawnRequest>();
            foreach (var entry in spawnEntries)
            {
                var built = BuildSpawn(registry, entry);
                if (built.IsFailure)
                    return Result.Fail<GameStartResult>(built.Error);
                spawns.Add(built.Value);
            }

            var manifest = loaded.Manifests[0];
            return _bootstrap.Start(
                CreateDefaultWorldLayout(),
                spawns,
                random ?? new DeterministicRandom(20260801),
                manifest.ModId,
                manifest.Version.Value);
        }

        public static WorldInitData CreateDefaultWorldLayout()
        {
            var regionId = new RegionId(1);
            return new WorldInitData
            {
                Regions =
                {
                    new RegionData { Id = regionId, Name = "青石荒村区域" }
                },
                LocalMaps =
                {
                    new LocalMapData { Id = 1, RegionId = regionId, Name = "村边山洞" }
                },
                Settlements =
                {
                    new SettlementData { Id = 1, RegionId = regionId, Name = "青石荒村" }
                }
            };
        }

        static Result<CharacterSpawnRequest> BuildSpawn(DefinitionRegistry registry, OpeningSpawnEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                return Result.Fail<CharacterSpawnRequest>(ErrorCode.MissingRequiredField, "spawn.definitionId required.");

            var parsed = DefinitionId.Parse(entry.DefinitionId);
            if (parsed.IsFailure)
                return Result.Fail<CharacterSpawnRequest>(parsed.Error);

            if (!registry.TryGetCharacter(parsed.Value, out var def))
            {
                return Result.Fail<CharacterSpawnRequest>(
                    ErrorCode.NotFound,
                    "Character definition missing for scenario spawn.",
                    parsed.Value.ToString());
            }

            var kind = ParseEntityKind(entry.EntityKind);
            var name = !string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DisplayName
                : (string.IsNullOrEmpty(def.Name) ? def.Id.ToString() : def.Name);

            var spawn = new CharacterSpawnRequest
            {
                DefinitionId = def.Id,
                Name = name,
                SpiritRootPlaceholder = def.SpiritRootPlaceholder ?? string.Empty,
                InitialRealmPlaceholder = def.InitialRealmPlaceholder ?? string.Empty,
                EntityKind = kind
            };

            if (def.BaseAttributes != null)
            {
                foreach (var kv in def.BaseAttributes)
                    spawn.BaseAttributes[kv.Key] = kv.Value;
            }

            foreach (var tag in def.EnumerateProfileTags())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    spawn.PersonalityTags.Add(tag);
            }

            if (def.ActivityCapabilities != null)
            {
                foreach (var kv in def.ActivityCapabilities)
                    spawn.ActivityCapabilities[kv.Key] = kv.Value;
            }

            if (def.ActivityPriorities != null)
            {
                foreach (var kv in def.ActivityPriorities)
                    spawn.ActivityPriorities[kv.Key] = kv.Value;
            }

            if (def.PreferredWorkAreaIds != null)
            {
                for (var i = 0; i < def.PreferredWorkAreaIds.Count; i++)
                {
                    var id = def.PreferredWorkAreaIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        spawn.PreferredWorkAreaIds.Add(id);
                }
            }

            spawn.HomeWorkAreaId = def.HomeWorkAreaId ?? string.Empty;

            if (def.SpiritRoots != null)
            {
                foreach (var kv in def.SpiritRoots)
                    spawn.SpiritRoots[kv.Key] = kv.Value;
            }

            spawn.Hometown = def.Hometown ?? string.Empty;
            spawn.Reputation = def.Reputation;
            if (def.Goals != null)
            {
                for (var i = 0; i < def.Goals.Count; i++)
                    if (!string.IsNullOrWhiteSpace(def.Goals[i]))
                        spawn.Goals.Add(def.Goals[i]);
            }

            if (def.Desires != null)
            {
                for (var i = 0; i < def.Desires.Count; i++)
                    if (!string.IsNullOrWhiteSpace(def.Desires[i]))
                        spawn.Desires.Add(def.Desires[i]);
            }

            return Result.Ok(spawn);
        }

        static SpawnEntityKind ParseEntityKind(string text)
        {
            if (!string.IsNullOrEmpty(text) &&
                string.Equals(text.Trim(), "npc", StringComparison.OrdinalIgnoreCase))
                return SpawnEntityKind.Npc;
            return SpawnEntityKind.Character;
        }
    }
}
