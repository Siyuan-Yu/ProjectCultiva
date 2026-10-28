namespace XianXia.Core.Social
{
    /// <summary>
    /// Supervisor anger 0–15; Huangcun discipline persists and consumes this value.
    /// </summary>
    public sealed class SupervisorAngerBoard
    {
        public const int MaxValue = 15;
        int _value;

        public int Value
        {
            get => _value;
            set
            {
                if (value < 0) _value = 0;
                else if (value > MaxValue) _value = MaxValue;
                else _value = value;
            }
        }

        public void Add(int delta) => Value = _value + delta;
    }
}
