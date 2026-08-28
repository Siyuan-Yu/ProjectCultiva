using System.Text;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Snapshot Load 后 WorldMap Army Marker 审计 trace（仅 DEBUG/Editor）。</summary>
    public static class WorldMapArmyMarkerDiagnostics
    {
        public static void LogFormalArmyDomainAfterLoad(PlayableHostSession session, string phase)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (session == null || !session.IsInitialized)
                return;

            var world = session.World;
            var playerFactionId = HostStrategicRosterQueries.ResolvePlayerFactionId(world, session.CharacterIds);
            Debug.Log("[FormalArmy.DomainAfterLoad] phase=" + phase +
                      " PlayerFactionId=" + playerFactionId +
                      " StrategicPlayerFactionId=" + (world?.Strategic?.PlayerFactionId ?? string.Empty));

            if (world?.Strategic?.FormalArmies?.Armies == null)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;

                var sb = new StringBuilder();
                sb.Append("[FormalArmy.DomainAfterLoad] ArmyId=").Append(army.ArmyId);
                sb.Append(" FactionId=").Append(army.FactionId ?? string.Empty);
                sb.Append(" LeaderCharacterId=").Append(army.LeaderCharacterId.Value);
                sb.Append(" MemberCount=").Append(army.MemberCharacterIds?.Count ?? 0);
                sb.Append(" CurrentHex=").Append(army.CurrentHex);
                sb.Append(" State=").Append(army.State);
                sb.Append(" OrderKind=").Append(army.WorldMotion.CurrentOrderKind);
                sb.Append(" OrderTargetArmyId=").Append(army.WorldMotion.OrderTargetArmyId ?? string.Empty);
                sb.Append(" DestinationHex=").Append(army.DestinationHex);
                sb.Append(" UsesHex=").Append(army.UsesHexStrategicPosition);
                sb.Append(" ShouldDrawPortrait=").Append(
                    ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army));
                sb.Append(" DomainFactionMatchesPlayer=").Append(
                    string.Equals(army.FactionId, playerFactionId, System.StringComparison.Ordinal));
                Debug.Log(sb.ToString());
            }
#endif
        }

        public static void LogWorldMapArmyMarkers(PlayableHostSession session)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (session == null || !session.IsInitialized)
                return;

            var world = session.World;
            var playerFactionId = HostStrategicRosterQueries.ResolvePlayerFactionId(world, session.CharacterIds);

            if (world?.Strategic?.FormalArmies?.Armies != null)
            {
                foreach (var kv in world.Strategic.FormalArmies.Armies)
                {
                    var army = kv.Value;
                    if (army == null)
                        continue;

                    var shouldPortrait = ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army);
                    var isPlayer = string.Equals(army.FactionId, playerFactionId, System.StringComparison.Ordinal);
                    StrategicFactionCatalog.MapTint(army.FactionId, out var pr, out var pg, out var pb);

                    var sb = new StringBuilder();
                    sb.Append("[WorldMapArmyMarker] ArmyId=").Append(army.ArmyId);
                    sb.Append(" ArmyFactionId=").Append(army.FactionId ?? string.Empty);
                    sb.Append(" MarkerExists=").Append(shouldPortrait ? "FormalPortrait" : "NoneOrStack");
                    sb.Append(" MarkerKind=").Append(shouldPortrait && isPlayer ? "FormalArmy" : "ArmyStackOrHidden");
                    sb.Append(" MarkerArmyId=").Append(army.ArmyId);
                    sb.Append(" DisplayName=").Append(ResolveLeaderShortName(world, army));
                    sb.Append(" ResolvedSide=").Append(isPlayer ? "Player" : "Hostile");
                    sb.Append(" ResolvedColor=(").Append(pr.ToString("F2")).Append(',')
                        .Append(pg.ToString("F2")).Append(',').Append(pb.ToString("F2")).Append(')');
                    sb.Append(" Selectable=").Append(shouldPortrait && isPlayer);
                    sb.Append(" SelectionPayloadKind=").Append(shouldPortrait && isPlayer ? "FormalArmy" : "None");
                    sb.Append(" SelectionPayloadId=").Append(shouldPortrait && isPlayer ? army.ArmyId : string.Empty);
                    Debug.Log(sb.ToString());
                }
            }

            if (world?.Strategic?.Armies?.Stacks != null)
            {
                foreach (var kv in world.Strategic.Armies.Stacks)
                {
                    var stack = kv.Value;
                    if (stack == null)
                        continue;
                    if (!ArmyWorldMapPresentation.ShouldDrawArmyStackMarker(world, stack, playerFactionId))
                        continue;

                    StrategicFactionCatalog.MapTint(stack.FactionId, out var sr, out var sg, out var sb);
                    ArmyStackAdapter.TryGetFormalArmy(world, stack, out var linkedArmy);
                    var tag = stack.MemberCount + "人 · " +
                              (string.IsNullOrEmpty(stack.DisplayName) ? "敌军" : stack.DisplayName);

                    var line = new StringBuilder();
                    line.Append("[WorldMapArmyMarker] StackId=").Append(stack.Id);
                    line.Append(" FormalArmyId=").Append(stack.FormalArmyId ?? string.Empty);
                    line.Append(" MarkerKind=EnemyArmyStack");
                    line.Append(" DisplayName=").Append(tag);
                    line.Append(" ResolvedColor=(").Append(sr.ToString("F2")).Append(',')
                        .Append(sg.ToString("F2")).Append(',').Append(sb.ToString("F2")).Append(')');
                    line.Append(" Selectable=true SelectionPayloadKind=ArmyStack SelectionPayloadId=").Append(stack.Id);
                    Debug.Log(line.ToString());
                }
            }
#endif
        }

        public static void LogWorldMapSelectionClick(
            string clickedMarkerArmyId,
            string beforeSelectionKind,
            string beforeSelectionId,
            string afterMarkerClickKind,
            string afterMarkerClickId,
            string afterFullPointerDispatchKind,
            string afterFullPointerDispatchId,
            int eventId)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[WorldMapSelectionClick] ClickedMarkerArmyId=" + (clickedMarkerArmyId ?? string.Empty) +
                      " BeforeSelectionKind=" + (beforeSelectionKind ?? string.Empty) +
                      " BeforeSelectionId=" + (beforeSelectionId ?? string.Empty) +
                      " AfterMarkerClickKind=" + (afterMarkerClickKind ?? string.Empty) +
                      " AfterMarkerClickId=" + (afterMarkerClickId ?? string.Empty) +
                      " AfterFullPointerDispatchKind=" + (afterFullPointerDispatchKind ?? string.Empty) +
                      " AfterFullPointerDispatchId=" + (afterFullPointerDispatchId ?? string.Empty) +
                      " EventId=" + eventId);
#endif
        }

        public static void LogMarkerSelectionVisual(
            string armyId,
            HostWorldMapSelectionKind authoritativeSelectionKind,
            string authoritativeSelectionId,
            bool visualSelected)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ArmyMarkerSelectionVisual] ArmyId=" + (armyId ?? string.Empty) +
                      " AuthoritativeSelectionKind=" + authoritativeSelectionKind +
                      " AuthoritativeSelectionId=" + (authoritativeSelectionId ?? string.Empty) +
                      " VisualSelected=" + visualSelected);
#endif
        }

        public static void LogWorldMapSelection(
            string clickedMarkerKind,
            string clickedArmyId,
            string beforeKind,
            string beforeId,
            string afterKind,
            string afterId)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[WorldMapSelection] ClickedMarkerKind=" + (clickedMarkerKind ?? string.Empty) +
                      " ClickedArmyId=" + (clickedArmyId ?? string.Empty) +
                      " BeforeSelectionKind=" + (beforeKind ?? string.Empty) +
                      " BeforeSelectionId=" + (beforeId ?? string.Empty) +
                      " AfterSelectionKind=" + (afterKind ?? string.Empty) +
                      " AfterSelectionId=" + (afterId ?? string.Empty));
#endif
        }

        public static void LogWorldMapPointerDispatch(
            Vector2 mousePosition,
            bool overStrategicUi,
            bool overArmyMarker,
            bool overPlayerMarker,
            string handledBy,
            bool mapInputExecuted)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[WorldMapPointerDispatch] MousePosition=" + mousePosition +
                      " OverStrategicUI=" + overStrategicUi +
                      " OverArmyMarker=" + overArmyMarker +
                      " OverPlayerMarker=" + overPlayerMarker +
                      " HandledBy=" + (handledBy ?? string.Empty) +
                      " MapInputExecuted=" + mapInputExecuted);
#endif
        }

        static string ResolveLeaderShortName(SimulationWorld world, FormalArmy army)
        {
            var leaderId = ArmyWorldMapPresentation.ResolvePortraitLeader(army);
            if (leaderId.IsNone || world?.Entities == null ||
                !world.Entities.TryGet(leaderId, out var leader) || leader == null)
                return "?";
            var name = leader.DisplayName ?? string.Empty;
            return name.Length > 2 ? name.Substring(0, 2) : name;
        }
    }
}
