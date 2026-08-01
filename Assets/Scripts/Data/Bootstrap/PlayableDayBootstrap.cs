using XianXia.Core.Bootstrap;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
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
    /// VS0.4 playable-day assembly shared by Unity Host and EditMode tests.
    /// No UnityEngine. No gameplay rules beyond wiring existing Core systems.
    /// </summary>
    public sealed class PlayableDayBootstrap
    {
        public const string DefaultScheduleId = "base:playable_labor_day";

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
                return Result.Fail<PlayableDayBootstrapResult>(
                    ErrorCode.ContentLoadFailed,
                    "Content package directory is empty.");

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

            var started = _contentGameStart.StartVerticalSlice01(loaded, random);
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

            var schedule = CreateDefaultLaborDaySchedule();
            world.RegisterSchedule(schedule);

            foreach (var entityId in started.Value.CharacterIds)
            {
                if (!world.Entities.TryGet(entityId, out var entity))
                    return Result.Fail<PlayableDayBootstrapResult>(
                        ErrorCode.EntityNotFound,
                        "Spawned character missing after bootstrap.",
                        entityId.ToString());

                EnsurePlayableComponents(entity, schedule.Id, options.DailyRequiredAmount);
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

            // SimulationLoop defaults include QuotaConsequenceHandler on DayEnded.
            var loop = new SimulationLoop(world);
            IPlayerInputPort port = new PlayerInputPort(loop);

            return Result.Ok(new PlayableDayBootstrapResult(
                world,
                loop,
                port,
                registry,
                loaded,
                started.Value.CharacterIds,
                schedule.Id));
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

        static ScheduleDefinition CreateDefaultLaborDaySchedule()
        {
            // Aligns with VS0.3 plan skeleton (default behavior, not a chapter script).
            return new ScheduleDefinition(DefaultScheduleId)
                .AddBlock(0, 8, ScheduleActivity.Rest, 2)
                .AddBlock(8, 48, ScheduleActivity.Labor, 4)
                .AddBlock(48, 56, ScheduleActivity.Rest, 2)
                .AddBlock(56, 80, ScheduleActivity.Labor, 4)
                .AddBlock(80, 96, ScheduleActivity.Rest, 2);
        }

        static void EnsurePlayableComponents(Entity entity, string scheduleId, int dailyRequired)
        {
            if (!entity.TryGet<ScheduleComponent>(out _))
                entity.AddComponent(new ScheduleComponent(scheduleId));

            if (!entity.TryGet<DailyTaskComponent>(out var daily))
            {
                entity.AddComponent(new DailyTaskComponent { RequiredAmount = dailyRequired });
            }
            else
            {
                daily.RequiredAmount = dailyRequired;
            }

            if (!entity.TryGet<KnownSitesComponent>(out _))
                entity.AddComponent(new KnownSitesComponent());

            if (!entity.TryGet<PersonalConcealmentRiskComponent>(out _))
                entity.AddComponent(new PersonalConcealmentRiskComponent());
        }
    }
}
