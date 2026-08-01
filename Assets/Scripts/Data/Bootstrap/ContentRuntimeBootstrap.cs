using XianXia.Core.Content;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Maps quest／content-event definitions onto session boards.</summary>
    public static class ContentRuntimeBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ContentRuntime bootstrap args null.");

            foreach (var kv in registry.Quests)
            {
                var def = kv.Value;
                var spec = new QuestSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Description = def.Description ?? string.Empty,
                    AutoOffer = def.AutoOffer
                };
                spec.OfferConditions.AddRange(def.OfferConditions);
                spec.CompleteConditions.AddRange(def.CompleteConditions);
                spec.FailConditions.AddRange(def.FailConditions);
                spec.Rewards.AddRange(def.Rewards);
                spec.FailResults.AddRange(def.FailResults);
                world.Quests.Register(spec);
            }

            foreach (var kv in registry.ContentEvents)
            {
                var def = kv.Value;
                var spec = new ContentEventSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Body = def.Body ?? string.Empty,
                    Trigger = def.Trigger ?? string.Empty,
                    LocationId = def.LocationId ?? string.Empty,
                    QuestId = def.QuestId ?? string.Empty,
                    Once = def.Once
                };
                spec.Conditions.AddRange(def.Conditions);
                for (var i = 0; i < def.Choices.Count; i++)
                {
                    var c = def.Choices[i];
                    var choice = new ContentEventChoiceSpec
                    {
                        Id = c.Id ?? string.Empty,
                        Text = c.Text ?? string.Empty
                    };
                    choice.Conditions.AddRange(c.Conditions);
                    choice.Outcomes.AddRange(c.Outcomes);
                    spec.Choices.Add(choice);
                }

                world.ContentEvents.Register(spec);
            }

            return Result.Success();
        }
    }
}
