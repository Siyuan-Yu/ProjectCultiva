namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Major realms. Mortal = 感应境 (minor 0/1/2 = 前/中/后期).
    /// QiRefining minor 1–10 = 炼气一层…十层. Foundation = 筑基.
    /// Enum ints 0/1 frozen for Snapshot; Foundation = 2.
    /// </summary>
    public enum RealmStage
    {
        Mortal = 0,
        QiRefining = 1,
        Foundation = 2
    }
}
