using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public static class ChapterRuntimeBootstrap
    {
        public static Result ApplyDefinitions(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Chapter bootstrap args null.");

            foreach (var kv in registry.Chapters)
            {
                var def = kv.Value;
                var spec = new ChapterSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Description = def.Description ?? string.Empty,
                    OpeningScenarioId = def.OpeningScenarioId ?? string.Empty,
                    PlannedDays = def.PlannedDays
                };
                spec.QuestChainIds.AddRange(def.QuestChainIds);
                spec.EventChainIds.AddRange(def.EventChainIds);
                for (var i = 0; i < def.DayBeats.Count; i++)
                {
                    var b = def.DayBeats[i];
                    var beat = new ChapterDayBeatSpec { DayIndex = b.DayIndex };
                    beat.Conditions.AddRange(b.Conditions);
                    beat.QuestOfferIds.AddRange(b.QuestOfferIds);
                    beat.ContentEventIds.AddRange(b.ContentEventIds);
                    beat.SetFlags.AddRange(b.SetFlags);
                    spec.DayBeats.Add(beat);
                }

                world.Chapters.Register(spec);
            }

            return Result.Success();
        }

        public static Result ApplyOpening(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup)
        {
            var registered = ApplyDefinitions(world, registry);
            if (registered.IsFailure)
                return registered;

            if (scenario == null || string.IsNullOrWhiteSpace(scenario.OpeningChapterId))
                return Result.Success();

            var subject = EntityId.None;
            if (lookup != null)
            {
                foreach (var spawn in scenario.Spawns)
                {
                    if (string.Equals(spawn.EntityKind, "npc", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (lookup.TryGetEntity(spawn.DefinitionId, out subject))
                        break;
                }
            }

            var day = world.Tick.Value / (ulong)WorldTick.TicksPerDay;
            return new ChapterService().Activate(world, scenario.OpeningChapterId, subject, day);
        }
    }
}
