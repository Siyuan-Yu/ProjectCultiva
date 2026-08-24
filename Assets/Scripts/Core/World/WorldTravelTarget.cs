using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World
{
    /// <summary>????????WorldSite ? Hex ???</summary>
    public readonly struct WorldTravelTarget : IEquatable<WorldTravelTarget>
    {
        public bool IsHex { get; }
        public string SiteId { get; }
        public int HexQ { get; }
        public int HexR { get; }

        public HexCoord HexCoord => new HexCoord(HexQ, HexR);

        WorldTravelTarget(bool isHex, string siteId, int hexQ, int hexR)
        {
            IsHex = isHex;
            SiteId = siteId ?? string.Empty;
            HexQ = hexQ;
            HexR = hexR;
        }

        public static WorldTravelTarget AtSite(string siteId) =>
            new WorldTravelTarget(false, siteId, int.MinValue, int.MinValue);

        public static WorldTravelTarget AtHex(HexCoord coord) =>
            new WorldTravelTarget(true, string.Empty, coord.Q, coord.R);

        public string Describe(Simulation.SimulationWorld world)
        {
            if (IsHex)
            {
                if (world?.Strategic?.Sites != null)
                {
                    foreach (var kv in world.Strategic.Sites.Sites)
                    {
                        var candidateSite = kv.Value;
                        if (candidateSite != null && candidateSite.HexCoord == HexCoord)
                            return string.IsNullOrEmpty(candidateSite.DisplayName) ? candidateSite.SiteId : candidateSite.DisplayName;
                    }
                }

                return HexCoord.ToString();
            }

            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(SiteId, out var site) &&
                site != null)
                return string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            return SiteId;
        }

        public bool Equals(WorldTravelTarget other) =>
            IsHex == other.IsHex &&
            string.Equals(SiteId, other.SiteId, StringComparison.Ordinal) &&
            HexQ == other.HexQ &&
            HexR == other.HexR;

        public override bool Equals(object obj) => obj is WorldTravelTarget other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsHex.GetHashCode();
                hash = (hash * 397) ^ (SiteId != null ? SiteId.GetHashCode() : 0);
                hash = (hash * 397) ^ HexQ;
                hash = (hash * 397) ^ HexR;
                return hash;
            }
        }
    }
}
