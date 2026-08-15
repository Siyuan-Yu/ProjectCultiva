using XianXia.Core.Attributes;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 属性中文名。体魄＝肉身属性；生命＝血条上限（MaxHp），二者不可混用。
    /// </summary>
    public static class HostAttributeLabels
    {
        public static string Name(AttributeId id)
        {
            switch (id)
            {
                case AttributeId.MaxHp: return "生命";
                case AttributeId.Physique: return "体魄";
                case AttributeId.Attack: return "攻击";
                case AttributeId.Defense: return "防御";
                case AttributeId.Speed: return "身法";
                case AttributeId.Stamina: return "耐力";
                case AttributeId.SpiritSense: return "神识";
                case AttributeId.Comprehension: return "悟性";
                case AttributeId.SpiritPower: return "灵力";
                case AttributeId.Cultivation: return "修为";
                case AttributeId.MindState: return "心境";
                default: return id.ToString();
            }
        }
    }
}
