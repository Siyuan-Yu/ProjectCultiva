using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class ContentEventOnTalkTests
    {
        [Test]
        public void OnTalk_TriggersMatchingNpcEvent()
        {
            var world = new SimulationWorld();
            var spec = new ContentEventSpec
            {
                Id = "base:event_talk_test",
                Name = "测试对话",
                Body = "你好。",
                Trigger = "onTalk",
                NpcDefinitionId = "base:character_test_npc",
                Once = false
            };
            spec.Choices.Add(new ContentEventChoiceSpec { Id = "ok", Text = "好" });
            world.ContentEvents.Register(spec);

            var svc = new ContentEventService();
            Assert.IsTrue(svc.TryTalkToNpc(world, EntityId.None, "base:character_test_npc").IsSuccess);
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_talk_test", world.ContentEvents.ActiveEventId);
        }

        [Test]
        public void OnTalk_IgnoresDifferentNpc()
        {
            var world = new SimulationWorld();
            var spec = new ContentEventSpec
            {
                Id = "base:event_talk_test",
                Trigger = "onTalk",
                NpcDefinitionId = "base:character_a"
            };
            world.ContentEvents.Register(spec);

            var svc = new ContentEventService();
            svc.TryTalkToNpc(world, EntityId.None, "base:character_b");
            Assert.IsFalse(world.ContentEvents.HasActive);
        }
    }
}
