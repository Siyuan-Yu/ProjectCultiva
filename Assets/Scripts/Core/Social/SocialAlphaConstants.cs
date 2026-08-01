namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 playtest constants — not Freeze. Change only with Devlog note.
    /// </summary>
    public static class SocialAlphaConstants
    {
        public const int OpeningCompanionFavor = 20;
        public const int HelpDelta = 10;
        public const int SlightDelta = -8;
        public const int RecruitMinScore = 20;

        // V5-E: Schedule duration micro-bias (ticks). Not Freeze.
        public const int BoldLaborDurationBonus = 1;
        public const int BoldRestDurationPenalty = 1;
        public const int CautiousLaborDurationPenalty = 1;
        public const int CautiousRestDurationBonus = 1;
        public const int CuriousLaborDurationBonus = 1;

        // V5-F: abstract social drift. Not Freeze.
        public const int SocialTickIntervalTicks = 16;
        public const int SocialTickInteractChancePercent = 50;
        public const int SocialTickBoldHelpChancePercent = 30;
        public const int SocialTickCautiousHelpChancePercent = 70;

        public const string OpeningFactionId = "base:sect_huangcun_labor";
        public const string ReasonOpeningCompanion = "opening_companion";
        public const string ReasonHelp = "help";
        public const string ReasonSlight = "slight";
        public const string ReasonRecruited = "recruited";
    }
}

