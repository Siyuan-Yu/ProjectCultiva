using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;

namespace XianXia.Tests
{
    public sealed class IdTypesTests
    {
        [Test]
        public void OpaqueIds_SameValue_AreEqual_DifferentAreNot()
        {
            Assert.AreEqual(new EntityId(7), new EntityId(7));
            Assert.AreNotEqual(new EntityId(7), new EntityId(8));

            Assert.AreEqual(new ActionId(3), new ActionId(3));
            Assert.AreNotEqual(new ActionId(3), new ActionId(4));

            Assert.AreEqual(new EventId(9), new EventId(9));
            Assert.AreNotEqual(new EventId(9), new EventId(1));

            Assert.AreEqual(new SnapshotId(2), new SnapshotId(2));
            Assert.AreNotEqual(new SnapshotId(2), new SnapshotId(5));

            Assert.AreEqual(new ModifierId(11), new ModifierId(11));
            Assert.AreNotEqual(new ModifierId(11), new ModifierId(12));

            Assert.AreEqual(new RegionId(1), new RegionId(1));
            Assert.AreNotEqual(new RegionId(1), new RegionId(2));
        }

        [Test]
        public void OpaqueIds_WorkAsDictionaryKeys()
        {
            var entities = new Dictionary<EntityId, string> { [new EntityId(1)] = "a" };
            Assert.AreEqual("a", entities[new EntityId(1)]);

            var actions = new Dictionary<ActionId, string> { [new ActionId(2)] = "b" };
            Assert.AreEqual("b", actions[new ActionId(2)]);

            var events = new Dictionary<EventId, string> { [new EventId(3)] = "c" };
            Assert.AreEqual("c", events[new EventId(3)]);

            var snapshots = new Dictionary<SnapshotId, string> { [new SnapshotId(4)] = "d" };
            Assert.AreEqual("d", snapshots[new SnapshotId(4)]);
        }

        [Test]
        public void EntityId_And_DefinitionId_HaveNoImplicitConversions()
        {
            AssertNoImplicitConversion(typeof(EntityId), typeof(DefinitionId));
            AssertNoImplicitConversion(typeof(DefinitionId), typeof(EntityId));
            AssertNoImplicitConversion(typeof(EntityId), typeof(ulong));
            AssertNoImplicitConversion(typeof(DefinitionId), typeof(string));
        }

        [Test]
        public void SourceRef_Equality_UsesKindAndOptionalHandles()
        {
            var def = new DefinitionId("base", "talent_fire");
            var a = new SourceRef(SourceKind.Talent, def, new EntityId(1), new ModifierId(2));
            var b = new SourceRef(SourceKind.Talent, def, new EntityId(1), new ModifierId(2));
            var c = new SourceRef(SourceKind.Equipment, def, new EntityId(1), new ModifierId(2));

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
        }

        static void AssertNoImplicitConversion(System.Type from, System.Type to)
        {
            var ops = from.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "op_Implicit")
                .Where(m => m.ReturnType == to)
                .ToArray();
            Assert.IsEmpty(ops, $"{from.Name} must not implicitly convert to {to.Name}");
        }
    }
}
