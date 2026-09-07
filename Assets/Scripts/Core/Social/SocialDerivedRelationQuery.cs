namespace XianXia.Core.Social
{
    public static class SocialDerivedRelationQuery
    {
        public static string GetLabel(SocialAttitude attitude)
        {
            if (attitude == null)
                return string.Empty;
            if (attitude.Grudge >= 60)
                return "仇视";
            if (attitude.Affection >= 60)
                return "亲近";
            return string.Empty;
        }
    }
}
