namespace XianXia.Core.World.Strategic
{
    public enum FormalArmyLocationKind
    {
        Unknown = 0,
        AtWorldSite = 1,
        AtWorldPosition = 2,
    }

    public enum FormalArmyMovementKind
    {
        Idle = 0,
        AutoTravel = 1,
    }

    public enum FormalArmyOrderKind
    {
        None = 0,
        TravelToHex = 1,
        TravelToWorldSite = 2,
    }
}
