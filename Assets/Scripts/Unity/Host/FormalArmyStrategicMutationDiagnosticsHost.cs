#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Development-only：将 FormalArmy mutation 诊断接到 Unity Console。</summary>
    public static class FormalArmyStrategicMutationDiagnosticsHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            FormalArmyStrategicMutationDiagnostics.Enabled = true;
            FormalArmyStrategicMutationDiagnostics.LogInfo = message => Debug.Log(message);
            FormalArmyStrategicMutationDiagnostics.LogError = message => Debug.LogError(message);
            LingeringExitPositionTrace.LogInfo = message => Debug.Log(message);
            LingeringExitPositionTrace.LogWarning = message => Debug.LogWarning(message);
            SecondBattleAnchorTrace.LogInfo = message => Debug.Log(message);
            ArmyWorldMapRenderDiagnostics.Enabled = true;
            ArmyWorldMapRenderDiagnostics.LogInfo = message => Debug.Log(message);
            Debug.Log("[FormalArmyPosDiag] Runtime tracing ENABLED. Search Console for ATTACK-BEGIN / FORMAL-ARMY-POS-MUTATION / ILLEGAL-ARMY-TELEPORT / ARMY-RENDER-POS-JUMP / LINGERING-EXIT-POSITION-TRACE / SECOND-BATTLE-ANCHOR-TRACE.");
        }

        public static void BindSession(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session?.World == null)
                return;

            var world = bootstrap.Session.World;
            FormalArmyStrategicMutationDiagnostics.WorldTickProvider = () => world.Tick.Value;
                FormalArmyStrategicMutationDiagnostics.BindPresentationWorld(world);
        }

        public static void TickFrame() =>
            FormalArmyStrategicMutationDiagnostics.UnityFrame = Time.frameCount;
    }
}
#endif
