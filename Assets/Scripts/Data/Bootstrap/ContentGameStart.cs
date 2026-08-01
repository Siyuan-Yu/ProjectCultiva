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
    /// Content → Core GameStart wiring for Vertical Slice 0.1 technical prep.
    /// </summary>
    public sealed class ContentGameStart
    {
        static readonly DefinitionId ProtagonistId = new DefinitionId("base", "character_protagonist");
        static readonly DefinitionId CompanionAId = new DefinitionId("base", "character_companion_a");
        static readonly DefinitionId CompanionBId = new DefinitionId("base", "character_companion_b");

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
            if (loaded == null || loaded.Registry == null)
                return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "LoadedContent is null.");
            if (loaded.Manifests == null || loaded.Manifests.Count == 0)
                return Result.Fail<GameStartResult>(ErrorCode.ContentLoadFailed, "LoadedContent has no manifests.");

            var registry = loaded.Registry;
            var spawns = new List<CharacterSpawnRequest>();
            foreach (var id in new[] { ProtagonistId, CompanionAId, CompanionBId })
            {
                if (!registry.TryGetCharacter(id, out var def))
                    return Result.Fail<GameStartResult>(ErrorCode.NotFound, "Required character definition missing.", id.ToString());

                spawns.Add(ToSpawn(def));
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

        static CharacterSpawnRequest ToSpawn(CharacterDefinition def)
        {
            var spawn = new CharacterSpawnRequest
            {
                DefinitionId = def.Id,
                Name = string.IsNullOrEmpty(def.Name) ? def.Id.ToString() : def.Name,
                SpiritRootPlaceholder = def.SpiritRootPlaceholder ?? string.Empty,
                InitialRealmPlaceholder = def.InitialRealmPlaceholder ?? string.Empty
            };

            if (def.BaseAttributes != null)
            {
                foreach (var kv in def.BaseAttributes)
                    spawn.BaseAttributes[kv.Key] = kv.Value;
            }

            if (def.Tags != null)
                spawn.PersonalityTags.AddRange(def.Tags);

            return spawn;
        }
    }
}
