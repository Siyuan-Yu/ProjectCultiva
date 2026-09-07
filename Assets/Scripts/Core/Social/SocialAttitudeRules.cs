using System;

namespace XianXia.Core.Social
{
    public static class SocialAttitudeRules
    {
        public static int Clamp(SocialAttitudeAxis axis, int value)
        {
            switch (axis)
            {
                case SocialAttitudeAxis.Fear:
                case SocialAttitudeAxis.Grudge:
                    return Math.Max(0, Math.Min(100, value));
                default:
                    return Math.Max(-100, Math.Min(100, value));
            }
        }
    }
}
