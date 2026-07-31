using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;

namespace XianXia.Tests
{
    public sealed class DefinitionIdTests
    {
        [Test]
        public void TryParse_BaseItem_Succeeds()
        {
            Assert.IsTrue(DefinitionId.TryParse("base:item_x", out var id));
            Assert.AreEqual("base", id.Namespace);
            Assert.AreEqual("item_x", id.LocalId);
            Assert.AreEqual("base:item_x", id.ToString());
        }

        [Test]
        public void TryParse_MissingColon_Fails()
        {
            Assert.IsFalse(DefinitionId.TryParse("baseitem_x", out _));
        }

        [Test]
        public void TryParse_EmptyNamespace_Fails()
        {
            Assert.IsFalse(DefinitionId.TryParse(":item_x", out _));
        }

        [Test]
        public void TryParse_EmptyLocalId_Fails()
        {
            Assert.IsFalse(DefinitionId.TryParse("base:", out _));
        }

        [Test]
        public void ToString_RoundTrip_IsStable()
        {
            Assert.IsTrue(DefinitionId.TryParse("base:item_x", out var id));
            var text = id.ToString();
            Assert.IsTrue(DefinitionId.TryParse(text, out var again));
            Assert.AreEqual(id, again);
            Assert.AreEqual(text, again.ToString());
        }

        [Test]
        public void Equality_And_Hash_AreStable()
        {
            Assert.IsTrue(DefinitionId.TryParse("base:item_x", out var a));
            Assert.IsTrue(DefinitionId.TryParse("base:item_x", out var b));
            Assert.IsTrue(DefinitionId.TryParse("base:other", out var c));

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);

            var map = new Dictionary<DefinitionId, int> { [a] = 1 };
            Assert.AreEqual(1, map[b]);
            Assert.IsFalse(map.ContainsKey(c));
        }
    }
}
