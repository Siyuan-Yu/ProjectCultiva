namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Major realms. Mortal = 感应境 (minor 0/1/2 = 前/中/后期).
    /// QiRefining minor 1–10 = 炼气一层…十层. Foundation = 筑基.
    /// Existing Snapshot values 0/1/2 remain stable; GoldenCore appends 3.
    /// </summary>
    public enum RealmStage
    {
        Mortal = 0,
        QiRefining = 1,
        Foundation = 2,
        GoldenCore = 3
    }
}
