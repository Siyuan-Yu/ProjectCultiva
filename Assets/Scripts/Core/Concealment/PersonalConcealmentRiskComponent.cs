using XianXia.Core.Entities;

namespace XianXia.Core.Concealment
{
    /// <summary>
    /// Minimal PersonalConcealmentRisk (0–100). No supervisor vision / stealth / witnesses.
    /// </summary>
    public sealed class PersonalConcealmentRiskComponent : IComponent
    {
        int _value;

        public int Value
        {
            get => _value;
            set => _value = Clamp(value);
        }

        public void Add(int delta) => Value = _value + delta;

        static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }
    }
}
