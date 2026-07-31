using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;

namespace XianXia.Tests
{
    public sealed class DomainEventTests
    {
        [Test]
        public void Publish_PreservesOrder_And_DrainEmpties()
        {
            var q = new DomainEventQueue();
            var t = new WorldTick(3);
            q.Publish(EventType.EntityCreated, t, target: new EntityId(1));
            q.Publish(EventType.ModifierAdded, t, target: new EntityId(1), payload: "Attack");
            q.Publish(EventType.ActionFailed, t, actor: new EntityId(1), payload: "cannot_start");

            Assert.IsTrue(q.TryPeek(out var first));
            Assert.AreEqual(EventType.EntityCreated, first.Type);
            Assert.AreEqual(1UL, first.Id.Value);

            var all = q.Drain();
            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(EventType.ModifierAdded, all[1].Type);
            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(3, q.Cursor);
        }

        [Test]
        public void FailureFacts_AreEvents_NotExceptions()
        {
            var q = new DomainEventQueue();
            var evt = q.Publish(EventType.OrderRejected, WorldTick.Zero, actor: new EntityId(9), payload: "incapacitated");
            Assert.AreEqual(EventType.OrderRejected, evt.Type);
            Assert.AreEqual("incapacitated", evt.Payload);
        }
    }
}
