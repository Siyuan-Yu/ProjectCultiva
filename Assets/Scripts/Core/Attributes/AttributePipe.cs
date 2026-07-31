using System;
using System.Collections.Generic;

namespace XianXia.Core.Attributes
{
    public readonly struct AttributeContribution
    {
        public AttributeContribution(string label, double value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public double Value { get; }
    }

    /// <summary>
    /// Frozen formula: Final = Clamp((Base + ΣFixed) × (1 + ΣPercentage), Min, Max).
    /// </summary>
    public static class AttributePipe
    {
        public static int Compute(
            int baseValue,
            IEnumerable<AttributeModifier> modifiers,
            int? min = null,
            int? max = null)
        {
            double fixedSum = 0;
            double percentageSum = 0;
            if (modifiers != null)
            {
                foreach (var m in modifiers)
                {
                    if (m.Operation == ModifierOperation.Fixed)
                        fixedSum += m.Value;
                    else if (m.Operation == ModifierOperation.Percentage)
                        percentageSum += m.Value;
                }
            }

            var raw = (baseValue + fixedSum) * (1.0 + percentageSum);
            var final = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            if (min.HasValue && final < min.Value) final = min.Value;
            if (max.HasValue && final > max.Value) final = max.Value;
            return final;
        }

        public static List<AttributeContribution> Explain(
            int baseValue,
            IEnumerable<AttributeModifier> modifiers)
        {
            var list = new List<AttributeContribution>
            {
                new AttributeContribution("Base", baseValue)
            };
            double fixedSum = 0;
            double percentageSum = 0;
            if (modifiers != null)
            {
                foreach (var m in modifiers)
                {
                    if (m.Operation == ModifierOperation.Fixed)
                    {
                        fixedSum += m.Value;
                        list.Add(new AttributeContribution("Fixed:" + m.Id.Value, m.Value));
                    }
                    else if (m.Operation == ModifierOperation.Percentage)
                    {
                        percentageSum += m.Value;
                        list.Add(new AttributeContribution("Percentage:" + m.Id.Value, m.Value));
                    }
                }
            }

            list.Add(new AttributeContribution("Raw", (baseValue + fixedSum) * (1.0 + percentageSum)));
            return list;
        }
    }
}
