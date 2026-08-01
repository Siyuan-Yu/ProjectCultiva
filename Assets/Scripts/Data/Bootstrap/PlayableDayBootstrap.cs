using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
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

            var started = _contentGameStart.StartFromScenario(loaded, scenarioId, random);
            if (started.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(started.Error);

            var world = started.Value.World;
            var registry = loaded.Registry;

            var manuals = RegisterManuals(world, registry);
            if (manuals.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(manuals.Error);

            var sites = RegisterSites(world, registry);
            if (sites.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(sites.Error);

            var scheduleId = string.IsNullOrWhiteSpace(scenario.ScheduleId)
                ? DefaultScheduleId
                : scenario.ScheduleId;
            world.RegisterSchedule(CreateLaborDaySchedule(scheduleId));

            var lookup = new GameStartLookup(started.Value.SpawnedByDefinitionId);
            var applied = OpeningScenarioApplier.Apply(
                world,
                scenario,
                lookup,
                options.DailyRequiredAmount);
            if (applied.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(applied.Error);

            var settlement = SettlementBootstrap.ApplyOpening(world, registry, scenario, lookup);
            if (settlement.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(settlement.Error);

            var region = WorldRegionBootstrap.ApplyOpening(world, registry, scenario, lookup);
            if (region.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(region.Error);

            var content = ContentRuntimeBootstrap.Apply(world, registry);
            if (content.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(content.Error);

            var chapter = ChapterRuntimeBootstrap.ApplyOpening(world, registry, scenario, lookup);
            if (chapter.IsFailure)
                return Result.Fail<PlayableDayBootstrapResult>(chapter.Error);

            var recruitableId = OpeningScenarioApplier.FindFirstRecruitable(scenario, lookup);
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

        static ScheduleDefinition CreateLaborDaySchedule(string scheduleId)
        {
            return new ScheduleDefinition(scheduleId)
                .AddBlock(0, 8, ScheduleActivity.Rest, 2)
                .AddBlock(8, 48, ScheduleActivity.Labor, 4)
                .AddBlock(48, 56, ScheduleActivity.Rest, 2)
                .AddBlock(56, 80, ScheduleActivity.Labor, 4)
                .AddBlock(80, 96, ScheduleActivity.Rest, 2);
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
