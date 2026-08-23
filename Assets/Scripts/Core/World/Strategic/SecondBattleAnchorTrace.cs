#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.Text;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development：第二场 Encounter Anchor 生命周期 Trace。</summary>
    public static class SecondBattleAnchorTrace
    {
        public static Action<string> LogInfo { get; set; }
        public static string LastTrace { get; private set; } = string.Empty;

        public static void Emit(
            string stage,
            SimulationWorld world,
            string extra = null)
        {
            var sb = new StringBuilder(384);
            sb.AppendLine("[SECOND-BATTLE-ANCHOR-TRACE]");
            sb.Append("Stage=").AppendLine(stage ?? string.Empty);
            sb.Append("Tick=").Append(world?.Tick.Value ?? 0UL).AppendLine();

            var rt = world?.Strategic?.Encounter;
            if (rt != null)
            {
                sb.Append("BattlefieldLingering=").Append(rt.BattlefieldLingering).AppendLine();
                sb.Append("Runtime.ArmyStackId=").Append(rt.ArmyStackId ?? string.Empty).AppendLine();
                sb.Append("Runtime.LingeringAnchor=");
                if (rt.TryGetLingeringBattleAnchorHex(out var lingerHex))
                    sb.Append(lingerHex);
                else
                    sb.Append("NONE");
                sb.AppendLine();
                sb.Append("Runtime.LingeringHexCount=").Append(rt.LingeringBattlefieldHexCount).AppendLine();
            }

            var snap = world?.Strategic?.Participants;
            if (snap != null)
            {
                sb.Append("Snap.OfferId=").Append(snap.OfferId ?? string.Empty).AppendLine();
                sb.Append("Snap.AttackerArmyId=").Append(snap.AttackerArmyId ?? string.Empty).AppendLine();
                sb.Append("Snap.PrimaryEnemyStackId=").Append(snap.PrimaryEnemyStackId ?? string.Empty).AppendLine();
                sb.Append("Snap.BattleAnchorHex=");
                if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var snapHex))
                    sb.Append(snapHex);
                else
                    sb.Append("NONE");
                sb.AppendLine();
                sb.Append("Snap.BattleAnchorNodeId=").Append(snap.BattleAnchorNodeId ?? string.Empty).AppendLine();
            }

            var offer = world?.Strategic?.BattleOffer;
            if (offer != null)
            {
                sb.Append("Offer.OfferId=").Append(offer.OfferId ?? string.Empty).AppendLine();
                sb.Append("Offer.AttackerArmyId=").Append(offer.AttackerArmyId ?? string.Empty).AppendLine();
                sb.Append("Offer.ArmyStackId=").Append(offer.ArmyStackId ?? string.Empty).AppendLine();
            }

            if (!string.IsNullOrEmpty(extra))
                sb.AppendLine(extra);

            LastTrace = sb.ToString();
            if (LogInfo != null)
                LogInfo(LastTrace);
            else
                Debug.WriteLine(LastTrace);
        }

        public static void EmitArmyHex(
            string stage,
            SimulationWorld world,
            FormalArmy army,
            HexCoord? contactHex = null)
        {
            var extra = new StringBuilder(128);
            if (army != null)
            {
                extra.Append("ArmyId=").Append(army.ArmyId).AppendLine();
                extra.Append("Army.CurrentHex=")
                    .Append(army.UsesHexStrategicPosition ? army.CurrentHex.ToString() : "NONE")
                    .AppendLine();
            }

            if (contactHex.HasValue)
                extra.Append("ContactHex=").Append(contactHex.Value).AppendLine();

            Emit(stage, world, extra.ToString());
        }
    }
}
#endif
