using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Npc;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Content;
using XianXia.Data.Cultivation;
using XianXia.Data.Opportunity;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Playable-day assembly shared by Unity Host and EditMode tests.
    /// VS0.7+: spawn／NPC／faction／relations driven by openingScenario content.
    /// </summary>
    public sealed class PlayableDayBootstrap
    {
        public const string DefaultScheduleId = "base:playable_labor_day";

        public static readonly DefinitionId DefaultScenarioId = ContentGameStart.DefaultPlayableScenarioId;

        readonly ContentPackageLoader _loader;
        readonly ContentGameStart _contentGameStart;

        public PlayableDayBootstrap(ContentPackageLoader loader = null, ContentGameStart contentGameStart = null)
        {
            _loader = loader ?? new ContentPackageLoader();
            _contentGameStart = contentGameStart ?? new ContentGameStart(_loader);
        }

        public Result<PlayableDayBootstrapResult> Start(
            string packageDirectory,
            PlayableDayOptions options = null,
            IRandomSource random = null)
        {
            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                return Result.Fail<PlayableDayBootstrapResult>(
                    ErrorCode.ContentLoadFailed,
                    "Content package directory is empty.");
            }

            var loaded = _loader.Load(new[] { packageDirectory });
            if (loaded.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(loaded.Error);

            return Start(loaded.Value, options, random);
        }

        public Result<PlayableDayBootstrapResult> Start(
            LoadedContent loaded,
            PlayableDayOptions options = null,
            IRandomSource random = null)
        {
            options = options ?? new PlayableDayOptions();

            if (loaded == null || loaded.Registry == null)
            {
                return Result.Fail<PlayableDayBootstrapResult>(
                    ErrorCode.InvalidArgument,
                    "LoadedContent is null.");
            }

            var scenarioResolved = ResolveScenarioId(options.OpeningScenarioId);
            if (scenarioResolved.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(scenarioResolved.Error);
            var scenarioId = scenarioResolved.Value;
            if (!loaded.Registry.TryGetOpeningScenario(scenarioId, out var scenario))
            {
                return Result.Fail<PlayableDayBootstrapResult>(
                    ErrorCode.NotFound,
                    "Opening scenario definition missing.",
                    scenarioId.ToString());
            }

            System.Collections.Generic.IList<OpeningSpawnEntry> spawnEntries = scenario.Spawns;
            if (!string.IsNullOrWhiteSpace(options.CharacterRosterId))
            {
                var rosterParsed = DefinitionId.Parse(options.CharacterRosterId.Trim());
                if (rosterParsed.IsFailure)
                    return Result.Fail<PlayableDayBootstrapResult>(rosterParsed.Error);
                if (!loaded.Registry.TryGetCharacterRoster(rosterParsed.Value, out var roster) ||
                    roster.Entries == null ||
                    roster.Entries.Count == 0)
                {
                    return Result.Fail<PlayableDayBootstrapResult>(
                        ErrorCode.NotFound,
                        "Character roster missing or empty (export from CharacterNpcEditor).",
                        options.CharacterRosterId.Trim());
                }

                spawnEntries = roster.Entries;
            }

            var started = _contentGameStart.StartFromScenario(
                loaded,
                scenarioId,
                random,
                options.CharacterRosterId);
            if (started.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(started.Error);

            var world = started.Value.World;
            var registry = loaded.Registry;

            var manuals = RegisterManuals(world, registry);
            if (manuals.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(manuals.Error);
            var arts = RegisterCombatArts(world, registry);
            if (arts.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(arts.Error);

            var ladder = RegisterRealmLadder(world, registry);
            if (ladder.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(ladder.Error);

            var sites = RegisterSites(world, registry);
            if (sites.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(sites.Error);

            var scheduleId = string.IsNullOrWhiteSpace(scenario.ScheduleId)
                ? DefaultScheduleId
                : scenario.ScheduleId;
            var schedules = ScheduleRuntimeBootstrap.Register(world, registry);
            if (schedules.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(schedules.Error);
            EnsureBuiltinSchedules(world, scheduleId);

            var jobs = JobRuntimeBootstrap.Register(world, registry);
            if (jobs.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(jobs.Error);

            var lookup = new GameStartLookup(started.Value.SpawnedByDefinitionId);
            var applied = OpeningScenarioApplier.Apply(
                world,
                scenario,
                lookup,
                options.DailyRequiredAmount,
                spawnEntries);
            if (applied.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(applied.Error);

            var settlement = SettlementBootstrap.ApplyOpening(world, registry, scenario, lookup);
            if (settlement.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(settlement.Error);

            var region = WorldRegionBootstrap.ApplyOpening(
                world, registry, scenario, lookup, spawnEntries);
            if (region.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(region.Error);

            var graph = WorldGraphBootstrap.ApplyOpening(
                world, registry, scenario, lookup, spawnEntries);
            if (graph.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(graph.Error);

            var spawnZones = SpawnZoneApplier.ApplyAll(world, registry, world.Random);
            if (spawnZones.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(spawnZones.Error);

            var content = ContentRuntimeBootstrap.Apply(world, registry);
            if (content.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(content.Error);

            var chapter = ChapterRuntimeBootstrap.ApplyOpening(world, registry, scenario, lookup);
            if (chapter.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(chapter.Error);

            HousingAssignmentService.SeedFromHomeBindings(world);

            var recruitableId = OpeningScenarioApplier.FindFirstRecruitable(scenario, lookup, spawnEntries);
            if (recruitableId.IsNone)
            {
                return Result.Fail<PlayableDayBootstrapResult>(
                    ErrorCode.NotFound,
                    "Opening scenario has no recruitable spawn.",
                    DefaultScenarioId.ToString());
            }

            if (options.ObservationDiscoverChancePercent.HasValue)
            {
                var chance = options.ObservationDiscoverChancePercent.Value;
                if (chance < 0 || chance > 100)
                {
                    return Result.Fail<PlayableDayBootstrapResult>(
                        ErrorCode.InvalidArgument,
                        "ObservationDiscoverChancePercent must be 0–100.",
                        chance.ToString());
                }

                world.ObservationDiscoverChancePercent = chance;
            }

            var loop = new SimulationLoop(world, enableSocialTick: true);
            loop.AddDayBoundaryHandler(new ChapterDayHandler());
            loop.AddDayBoundaryHandler(new QuestDeadlineDayHandler());
            loop.AddDayBoundaryHandler(new SupervisorPressureHandler());
            IPlayerInputPort port = new PlayerInputPort(loop);

            return Result.Ok(new PlayableDayBootstrapResult(
                world,
                loop,
                port,
                registry,
                loaded,
                started.Value.CharacterIds,
                scheduleId,
                recruitableId));
        }

        static Result RegisterManuals(SimulationWorld world, DefinitionRegistry registry)
        {
            foreach (var kv in registry.Cultivations)
            {
                var mapped = CultivationManualMapper.ToManualSpec(kv.Value);
                if (mapped.IsFailure)
                    return Result.Failure(mapped.Error);
                world.RegisterManual(mapped.Value);
            }

            return Result.Success();
        }

        static Result RegisterCombatArts(SimulationWorld world, DefinitionRegistry registry)
        {
            if (registry?.CombatArts == null || registry.CombatArts.Count == 0)
            {
                RegisterBuiltinCombatArts(world);
                return Result.Success();
            }

            foreach (var kv in registry.CombatArts)
            {
                var mapped = XianXia.Data.Combat.CombatArtMapper.ToSpec(kv.Value);
                if (mapped.IsFailure)
                    return Result.Failure(mapped.Error);
                world.RegisterCombatArt(mapped.Value);
            }

            return Result.Success();
        }

        static void RegisterBuiltinCombatArts(SimulationWorld world)
        {
            // 正式内容在 CombatArts/combat_arts.json；此处仅在包内无 combatArt 时保底，避免空表。
            if (world == null)
                return;
            world.RegisterCombatArt(new XianXia.Core.Combat.CombatArtSpec
            {
                Id = new DefinitionId("base", "art_liezhao_claw"),
                Name = "裂爪击",
                Grade = "黄阶中级",
                EffectSummary = "三连击（保底定义；请用 JSON／编辑器维护）",
                DamageAttackMult = 2.0,
                HitCount = 3,
                CooldownSeconds = 4f
            });
            world.RegisterCombatArt(new XianXia.Core.Combat.CombatArtSpec
            {
                Id = new DefinitionId("base", "art_kaishan_fist"),
                Name = "开山拳",
                Grade = "黄阶中级",
                EffectSummary = "一击（保底定义；请用 JSON／编辑器维护）",
                DamageAttackMult = 5.0,
                HitCount = 1,
                CooldownSeconds = 5f
            });
        }

        static Result RegisterRealmLadder(SimulationWorld world, DefinitionRegistry registry)
        {
            if (registry.TryGetPrimaryRealmLadder(out var def))
            {
                var mapped = RealmLadderMapper.ToBoard(def);
                if (mapped.IsFailure)
                    return Result.Failure(mapped.Error);
                world.RealmLadder = mapped.Value;
            }
            else
            {
                world.RealmLadder = RealmLadderBoard.CreateDefault();
            }

            new CultivationService().SyncAllEntities(world);
            return Result.Success();
        }

        static Result RegisterSites(SimulationWorld world, DefinitionRegistry registry)
        {
            foreach (var kv in registry.OpportunitySites)
            {
                var mapped = OpportunitySiteMapper.ToRuntime(kv.Value);
                if (mapped.IsFailure)
                    return Result.Failure(mapped.Error);
                world.RegisterOpportunitySite(mapped.Value);
            }

            if (world.OpportunitySites.Count == 0)
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "No OpportunitySite definitions loaded; playable day requires at least one site.");
            }

            return Result.Success();
        }

        /// <summary>
        /// Content schedules are preferred. Factories remain as safety net if JSON missing an id.
        /// </summary>
        static void EnsureBuiltinSchedules(SimulationWorld world, string primaryScheduleId)
        {
            if (!world.TryGetSchedule(primaryScheduleId, out _))
                world.RegisterSchedule(CreateLaborDaySchedule(primaryScheduleId));
            if (!world.TryGetSchedule("base:schedule_mortal_day", out _))
                world.RegisterSchedule(ScheduleDefinition.CreateMortalDay());
            if (!world.TryGetSchedule("base:schedule_cultivator_day", out _))
                world.RegisterSchedule(ScheduleDefinition.CreateCultivatorDay());
            if (!world.TryGetSchedule("base:schedule_supervisor_day", out _))
                world.RegisterSchedule(ScheduleDefinition.CreateSupervisorDay());
            if (!world.TryGetSchedule("base:schedule_laborer_day", out _))
                world.RegisterSchedule(ScheduleDefinition.CreateDefaultLaborerDay());
        }

        static ScheduleDefinition CreateLaborDaySchedule(string scheduleId)
        {
            return new ScheduleDefinition(scheduleId)
                .AddBlock(0, 24, ScheduleActivity.Rest, 6)
                .AddBlock(24, 144, ScheduleActivity.Labor, 12)
                .AddBlock(144, 168, ScheduleActivity.Rest, 6)
                .AddBlock(168, 240, ScheduleActivity.Labor, 12)
                .AddBlock(240, 288, ScheduleActivity.Rest, 6);
        }

        static Result<DefinitionId> ResolveScenarioId(string openingScenarioId)
        {
            if (string.IsNullOrWhiteSpace(openingScenarioId))
                return Result.Ok(DefaultScenarioId);
            var parsed = DefinitionId.Parse(openingScenarioId);
            if (parsed.IsFailure)
                return Result.Fail<DefinitionId>(parsed.Error);
            return Result.Ok(parsed.Value);
        }
    }
}
