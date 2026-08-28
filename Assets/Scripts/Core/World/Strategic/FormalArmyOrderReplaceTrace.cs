using System.Collections.Generic;
using System.Text;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy 玩家 Replace Travel Order 开发 Trace（仅事件触发）。</summary>
    public static class FormalArmyOrderReplaceTrace
    {
        public struct Capture
        {
            public FormalArmyOrderKind OldOrderKind;
            public HexCoord OldDestinationHex;
            public string OldDestinationSiteId;
            public WorldVec2 WorldPositionAtReplace;
            public int OldSegmentIndex;
        }

        public static Capture CaptureBeforeReplace(FormalArmyWorldMotion motion)
        {
            if (motion == null)
                return default;

            return new Capture
            {
                OldOrderKind = motion.CurrentOrderKind,
                OldDestinationHex = motion.DestinationHex,
                OldDestinationSiteId = motion.DestinationSiteId ?? string.Empty,
                WorldPositionAtReplace = motion.IsSiteDeparturePending &&
                                         motion.LocationKind == FormalArmyLocationKind.AtWorldSite
                    ? motion.SiteDepartureVirtualPosition
                    : motion.WorldPosition,
                OldSegmentIndex = motion.SegmentIndex,
            };
        }

        public static void Emit(
            FormalArmy army,
            Capture before,
            HexCoord newDestinationHex,
            string newDestinationSiteId,
            IReadOnlyList<HexCoord> newRoute)
        {
            if (army == null)
                return;

            var motion = army.WorldMotion;
            var sb = new StringBuilder(256);
            sb.Append("[FormalArmyOrderReplace] ArmyId=").Append(army.ArmyId);
            sb.Append(" OldOrder=").Append(before.OldOrderKind);
            sb.Append(" OldDestination=").Append(DescribeDestination(before.OldDestinationHex, before.OldDestinationSiteId));
            sb.Append(" NewDestination=").Append(DescribeDestination(newDestinationHex, newDestinationSiteId));
            sb.Append(" WorldPositionAtReplace=(")
                .Append(before.WorldPositionAtReplace.X.ToString("0.##"))
                .Append(',')
                .Append(before.WorldPositionAtReplace.Y.ToString("0.##"))
                .Append(')');
            sb.Append(" OldSegmentIndex=").Append(before.OldSegmentIndex);
            sb.Append(" NewRouteStart=").Append(newRoute != null && newRoute.Count > 0
                ? newRoute[0].ToString()
                : motion.CurrentHex.ToString());
            sb.Append(" NewRouteDestination=").Append(newRoute != null && newRoute.Count > 0
                ? newRoute[newRoute.Count - 1].ToString()
                : newDestinationHex.ToString());
            sb.Append(" TravelState=Moving=").Append(motion.IsMoving);
            sb.Append(" Location=").Append(motion.LocationKind);
            BackgroundTravelTraceSink.Emit(sb.ToString());
        }

        static string DescribeDestination(HexCoord hex, string siteId) =>
            string.IsNullOrEmpty(siteId) ? hex.ToString() : siteId + "@" + hex;
    }
}
