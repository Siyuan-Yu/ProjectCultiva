using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    /// <summary>己方 Character 默认不跟课表；NPC 仍自动日程。</summary>
    public sealed class HostCharacterManualControlTests
    {
        [Test]
        public void ScheduleDriver_DoesNotInjectOrdersForCharacter()
        {
            var world = new SimulationWorld();
            var schedule = new ScheduleDefinition("test:manual_day")
                .AddBlock(0, WorldTick.TicksPerDay, ScheduleActivity.Labor, 4);
            world.RegisterSchedule(schedule);

            var hero = world.Entities.CreateCharacter(new DefinitionId("base", "hero"), "主角").Value;
            Assert.IsTrue(hero.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            hero.Get<DailyTaskComponent>().RequiredAmount = 20;

            var loop = new SimulationLoop(world);
            loop.TickOnce();

            Assert.IsFalse(hero.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsFalse(world.GetOrCreateOrderQueue(hero.Id).HasSource(XianXia.Core.Orders.OrderSource.Schedule));
        }

        [Test]
        public void ScheduleDriver_StillInjectsOrdersForNpc()
        {
            var world = new SimulationWorld();
            var schedule = new ScheduleDefinition("test:npc_day")
                .AddBlock(0, WorldTick.TicksPerDay, ScheduleActivity.Labor, 4);
            world.RegisterSchedule(schedule);

            var npc = world.Entities.CreateNpc(new DefinitionId("base", "guard"), "守卫").Value;
            Assert.IsTrue(npc.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            npc.Get<DailyTaskComponent>().RequiredAmount = 20;

            var loop = new SimulationLoop(world);
            loop.TickOnce();

            Assert.IsTrue(npc.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsInstanceOf<LaborAction>(
                world.ActiveActions[npc.Get<ActionStateComponent>().ActiveActionId]);
        }
    }
}
