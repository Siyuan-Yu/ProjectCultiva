namespace XianXia.Core.Cultivation
{
    public static class RealmDisplay
    {
        public static string Format(RealmStage realm, int minor)
        {
            switch (realm)
            {
                case RealmStage.Mortal:
                    switch (minor)
                    {
                        case 0: return "感应境·前期";
                        case 1: return "感应境·中期";
                        default: return "感应境·后期";
                    }
                case RealmStage.QiRefining:
                    if (minor < 1) minor = 1;
                    if (minor > 10) minor = 10;
                    return "炼气" + ToLayerName(minor) + "层";
                case RealmStage.Foundation:
                    return "筑基期";
                default:
                    return realm.ToString();
            }
        }

        public static string FormatStep(RealmLadderStep step)
        {
            if (step == null)
                return "—";
            return Format(step.FromRealm, step.FromMinor) + " → " + Format(step.ToRealm, step.ToMinor);
        }

        static string ToLayerName(int layer)
        {
            switch (layer)
            {
                case 1: return "一";
                case 2: return "二";
                case 3: return "三";
                case 4: return "四";
                case 5: return "五";
                case 6: return "六";
                case 7: return "七";
                case 8: return "八";
                case 9: return "九";
                case 10: return "十";
                default: return layer.ToString();
            }
        }
    }
}
