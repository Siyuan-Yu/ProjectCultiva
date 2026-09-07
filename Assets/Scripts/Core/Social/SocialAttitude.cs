namespace XianXia.Core.Social
{
    public sealed class SocialAttitude
    {
        public static readonly SocialAttitude Zero = new SocialAttitude(0, 0, 0, 0, 0);

        public SocialAttitude(int affection, int trust, int respect, int fear, int grudge)
        {
            Affection = affection;
            Trust = trust;
            Respect = respect;
            Fear = fear;
            Grudge = grudge;
        }

        public int Affection { get; }
        public int Trust { get; }
        public int Respect { get; }
        public int Fear { get; }
        public int Grudge { get; }

        public bool IsZero => Affection == 0 && Trust == 0 && Respect == 0 && Fear == 0 && Grudge == 0;

        public int GetValue(SocialAttitudeAxis axis)
        {
            switch (axis)
            {
                case SocialAttitudeAxis.Trust: return Trust;
                case SocialAttitudeAxis.Respect: return Respect;
                case SocialAttitudeAxis.Fear: return Fear;
                case SocialAttitudeAxis.Grudge: return Grudge;
                default: return Affection;
            }
        }
    }
}
