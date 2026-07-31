using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Tests
{
    public sealed class AttributeModifierTests
    {
        [Test]
        public void GoldenFormula_Base100_Fixed10_Pct20_Pct30_Equals165()
        {
            var attrs = new AttributesComponent();
            attrs.SetBase(AttributeId.Attack, 100);
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, 10, new SourceRef(SourceKind.Equipment));
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, 0.20, new SourceRef(SourceKind.SpiritRoot));
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, 0.30, new SourceRef(SourceKind.Manual));

            Assert.AreEqual(165, attrs.GetFinal(AttributeId.Attack));
        }

        [Test]
        public void RemoveBySource_RecalculatesFinal()
        {
            var attrs = new AttributesComponent();
            attrs.SetBase(AttributeId.Attack, 100);
            var gear = new SourceRef(SourceKind.Equipment, new DefinitionId("base", "sword"));
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, 10, gear);
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, 0.50, new SourceRef(SourceKind.Talent));

            Assert.AreEqual(165, attrs.GetFinal(AttributeId.Attack));
            Assert.AreEqual(1, attrs.RemoveBySource(gear));
            Assert.AreEqual(150, attrs.GetFinal(AttributeId.Attack));
        }

        [Test]
        public void Explain_IncludesContributions()
        {
            var attrs = new AttributesComponent();
            attrs.SetBase(AttributeId.MaxHp, 100);
            attrs.AddModifier(AttributeId.MaxHp, ModifierOperation.Fixed, 5, new SourceRef(SourceKind.Event));
            var parts = attrs.Explain(AttributeId.MaxHp);
            Assert.GreaterOrEqual(parts.Count, 2);
            Assert.AreEqual(105, attrs.GetFinal(AttributeId.MaxHp));
        }
    }
}
