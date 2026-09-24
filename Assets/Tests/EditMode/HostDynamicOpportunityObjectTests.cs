using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostDynamicOpportunityObjectTests
    {
        [TearDown] public void TearDown() => HostDynamicWorldObjectRegistry.Clear();

        [Test]
        public void TransientRegistryEntryCreatesStableInspectContextWithoutEntityIdentity()
        {
            var entry = new HostDynamicWorldObjectEntry
            {
                WorldObjectInstanceId = "worldObject:opportunity:9",
                OpportunityInstanceId = "opportunity:9",
                OpportunityDefinitionId = "test:stele",
                SurfaceId = "test:surface",
                PresentationBounds = new Bounds(Vector3.zero, new Vector3(2, 2, 2)),
                ApproachPosition = Vector3.right,
                DisplayLabel = "残破石碑"
            };
            HostDynamicWorldObjectRegistry.Register(entry);
            Assert.AreSame(entry, HostDynamicWorldObjectRegistry.All[entry.WorldObjectInstanceId]);
            var target = new WorldObjectInteractionTarget(WorldObjectTargetKind.OpportunityObject,
                displayLabel: entry.DisplayLabel, approachPosition: entry.ApproachPosition,
                opportunityObject: entry);
            Assert.AreEqual("opportunityObject:worldObject:opportunity:9", target.StableTargetKey);
            Assert.IsTrue(target.TryCreateContentContext(new EntityId(3), out var context));
            Assert.AreEqual("test:stele", context.TargetDefinitionId);
            Assert.IsTrue(context.TargetEntityId.IsNone);
        }
    }
}
