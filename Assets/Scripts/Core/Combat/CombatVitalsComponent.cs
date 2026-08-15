using XianXia.Core.Attributes;
using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// Current HP／灵力护盾 pools. Max from Attributes; session presentation (+ Snapshot optional later).
    /// </summary>
    public sealed class CombatVitalsComponent : IComponent
    {
        public int CurrentHp { get; set; }
        public int CurrentSpiritPower { get; set; }
        /// <summary>首次从属性灌满后为 true；避免战斗归零后又被 EnsureVitals 回满。</summary>
        public bool PoolsInitialized { get; set; }

        public void SyncMaxFromAttributes(AttributesComponent attrs, bool fillSpirit)
        {
            if (attrs == null)
                return;
            var maxHp = attrs.GetFinal(AttributeId.MaxHp);
            if (maxHp < 1)
                maxHp = 1;

            var maxSp = attrs.GetFinal(AttributeId.SpiritPower);
            if (maxSp < 0)
                maxSp = 0;

            if (!PoolsInitialized)
            {
                CurrentHp = maxHp;
                if (fillSpirit)
                    CurrentSpiritPower = maxSp;
                PoolsInitialized = true;
            }
            else
            {
                if (CurrentHp > maxHp)
                    CurrentHp = maxHp;
                if (CurrentHp < 0)
                    CurrentHp = 0;
                if (CurrentSpiritPower > maxSp)
                    CurrentSpiritPower = maxSp;
                if (CurrentSpiritPower < 0)
                    CurrentSpiritPower = 0;
            }
        }
    }
}
