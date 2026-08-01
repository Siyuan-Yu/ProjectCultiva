namespace XianXia.Core.Social
{
    /// <summary>
    /// Demo [49] supervisor anger 0–100, display-only (no punishment events).
    /// </summary>
    public sealed class SupervisorAngerBoard
    {
        int _value;

        public int Value
        {
            get => _value;
            set
            {
                if (value < 0) _value = 0;
                else if (value > 100) _value = 100;
                else _value = value;
            }
        }

        public void Add(int delta) => Value = _value + delta;
    }
}
