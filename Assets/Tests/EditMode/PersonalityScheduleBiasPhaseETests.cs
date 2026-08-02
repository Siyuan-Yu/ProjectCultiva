using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Tests
{
    public sealed class PersonalityScheduleBiasPhaseETests
    {
        [Test]
        public void Bias_BoldLaborLonger_ThanCautious()
        {
            var block = new ScheduleBlock(0, 96, ScheduleActivity.Labor, 4);
            var bold = new PersonalityProfileComponent();
            bold.SetTags(new[] { PersonalityScheduleBias.TagBold });
            var cautious = new PersonalityProfileComponent();
            cautious.SetTags(new[] { PersonalityScheduleBias.TagCautious });

            var boldChoice = PersonalityScheduleBias.Apply(block, bold);
            var cautiousChoice = PersonalityScheduleBias.Apply(block, cautious);

            Assert.AreEqual(ScheduleActivity.Labor, boldChoice.Activity);
            Assert.AreEqual(ScheduleActivity.Labor, cautiousChoice.Activity);
            Assert.Greater(boldChoice.DurationTicks, cautiousChoice.DurationTicks);
            Assert.AreEqual(5UL, boldChoice.DurationTicks);
            Assert.AreEqual(3UL, cautiousChoice.DurationTicks);
        }

        [Test]
        public void Bias_BoldRestBlock_BecomesLabor()
        {
            var block = new ScheduleBlock(0, 8, ScheduleActivity.Rest, 2);
            var bold = new PersonalityProfileComponent();
            bold.SetTags(new[] { PersonalityScheduleBias.TagBold });

            var choice = PersonalityScheduleBias.Apply(block, bold);
            Assert.AreEqual(ScheduleActivity.Labor, choice.Activity);
            Assert.GreaterOrEqual(choice.DurationTicks, 1UL);
        }

        [Test]
        public void Bias_BoldAndCautious_CancelFlipAndDuration()
        {
            var rest = new ScheduleBlock(0, 8, ScheduleActivity.Rest, 2);
            var labor = new ScheduleBlock(8, 48, ScheduleActivity.Labor, 4);
            var mixed = new PersonalityProfileComponent();
            mixed.SetTags(new[]
            {
                PersonalityScheduleBias.TagBold,
                PersonalityScheduleBias.TagCautious
            });

            Assert.AreEqual(ScheduleActivity.Rest, PersonalityScheduleBias.Apply(rest, mixed).Activity);
            Assert.AreEqual(4UL, PersonalityScheduleBias.Apply(labor, mixed).DurationTicks);
        }

        [Test]
        public void ScheduleDriver_AppliesBias_PlayerOverrideStillWins()
        {
            var world = new SimulationWorld();
            var schedule = new ScheduleDefinition("test:bias_day")
                .AddBlock(0, 96, ScheduleActivity.Labor, 4);
            world.RegisterSchedule(schedule);

            // Npc：日程才会自动注入。Character（己方）默认不跟课表。
            var bold = world.Entities.CreateNpc(new DefinitionId("base", "bold"), "勇").Value;
            bold.Get<PersonalityProfileComponent>().SetTags(new[] { PersonalityScheduleBias.TagBold });
            Assert.IsTrue(bold.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            bold.Get<DailyTaskComponent>().RequiredAmount = 20;

            var cautious = world.Entities.CreateNpc(new DefinitionId("base", "caut"), "慎").Value;
            cautious.Get<PersonalityProfileComponent>().SetTags(new[] { PersonalityScheduleBias.TagCautious });
            Assert.IsTrue(cautious.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            cautious.Get<DailyTaskComponent>().RequiredAmount = 20;

            var loop = new SimulationLoop(world);
            loop.TickOnce();

            Assert.IsInstanceOf<LaborAction>(ActiveOf(world, bold));
            Assert.IsInstanceOf<LaborAction>(ActiveOf(world, cautious));

            var boldWait = ActiveOf(world, bold).Clock.RemainingTicks;
            var cautiousWait = ActiveOf(world, cautious).Clock.RemainingTicks;
            Assert.Greater(boldWait, cautiousWait);

            var port = new PlayerInputPort(loop);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(bold.Id, PlayerCommandKind.Rest, 2)).IsSuccess);
            Assert.IsInstanceOf<RestAction>(ActiveOf(world, bold));
            Assert.AreEqual(OrderSource.Player, bold.Get<ActionStateComponent>().ActiveOrderSource);
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == EventType.ScheduleInterrupted));
        }

        static IAction ActiveOf(SimulationWorld world, Entity entity)
        {
            var id = entity.Get<ActionStateComponent>().ActiveActionId;
            Assert.IsTrue(world.ActiveActions.TryGetValue(id, out var action));
            return action;
        }
    }
}
