using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>Coarse injuries, not a wound/bleeding simulation. Limb bits: LA=1, RA=2, LL=4, RL=8.</summary>
    public sealed class CombatInjuryComponent : IComponent
    {
        public int CutRecovery { get; set; }
        public int SevereRecovery { get; set; }
        public int MissingLimbs { get; set; }
        public bool MeridiansBroken { get; set; }
        internal int AppliedSignature { get; set; } = -1;
        public bool HasRecoverableInjury => CutRecovery > 0 || SevereRecovery > 0;
        public bool HasAny => HasRecoverableInjury || MissingLimbs != 0 || MeridiansBroken;
        public int MissingArms => ((MissingLimbs & 1) != 0 ? 1 : 0) + ((MissingLimbs & 2) != 0 ? 1 : 0);
        public int MissingLegs => ((MissingLimbs & 4) != 0 ? 1 : 0) + ((MissingLimbs & 8) != 0 ? 1 : 0);
    }
}
