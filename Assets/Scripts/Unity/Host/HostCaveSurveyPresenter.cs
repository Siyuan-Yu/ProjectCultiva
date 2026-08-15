using System.Globalization;
using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 洞府勘查：近距 toast；指令条「勘查」以选中己方各自位置为圆心、神识×2 为半径扫描。
    /// </summary>
    public sealed class HostCaveSurveyPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;

        string _hintToast = string.Empty;
        string _flashToast = string.Empty;
        float _flashUntil;
        GUIStyle _toastStyle;
        int _lastFoundCount = -1;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
        }

        public void ClearSessionState()
        {
            _hintToast = string.Empty;
            _flashToast = string.Empty;
            _lastFoundCount = -1;
        }

        void Update()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || session.World.LocalMap.IsInInterior)
            {
                _hintToast = string.Empty;
                return;
            }

            if (!TryBuildPartyProbes(session, out var probes) || probes.Count == 0)
            {
                _hintToast = string.Empty;
                return;
            }

            string nearestHint = null;
            var bestHint = float.MaxValue;

            foreach (var kv in session.World.WorldRegion.Locations)
            {
                var loc = kv.Value;
                if (!OpportunityEntranceRules.IsHiddenEntrance(loc))
                    continue;
                if (OpportunityEntranceRules.IsRevealed(session.World, loc))
                    continue;

                for (var i = 0; i < probes.Count; i++)
                {
                    var p = probes[i];
                    // toast 半径 ≥ 该角色勘查半径，避免「有提示却扫不到」
                    var hintR = Mathf.Max(OpportunityEntranceRules.DefaultHintRadius, p.Radius);
                    var dx = loc.PresentationX - p.X;
                    var dz = loc.PresentationZ - p.Z;
                    var d = Mathf.Sqrt(dx * dx + dz * dz) - OpportunityEntranceRules.EntranceHitPadding;
                    if (d < 0f)
                        d = 0f;
                    if (d <= hintR && d < bestHint)
                    {
                        bestHint = d;
                        nearestHint = loc.Id;
                    }
                }
            }

            _hintToast = !string.IsNullOrEmpty(nearestHint) ? "这附近似有洞府气息…" : string.Empty;
        }

        /// <summary>框选多人时每人一圈；半径＝神识×2。</summary>
        public bool TrySurvey()
        {
            if (commandBridge == null)
                return false;

            var session = bootstrap?.Session;
            if (session == null || !TryBuildPartyProbes(session, out var probes) || probes.Count == 0)
            {
                Flash("勘查失败。");
                return false;
            }

            var hint = FormatProbes(probes);
            var before = CountRevealedEntrances(session);
            var ok = commandBridge.IssueSurveyAround(hint) > 0;
            if (!ok)
            {
                Flash("勘查失败。");
                return false;
            }

            var after = CountRevealedEntrances(session);
            _lastFoundCount = after - before;
            if (_lastFoundCount > 0)
            {
                Flash("神识扫过，洞府入口显露！");
                bootstrap?.RefreshMapStampsOnly();
            }
            else
            {
                Flash("四周未见异样。");
            }

            return true;
        }

        struct Probe
        {
            public float X;
            public float Z;
            public float Radius;
        }

        bool TryBuildPartyProbes(PlayableHostSession session, out System.Collections.Generic.List<Probe> probes)
        {
            probes = new System.Collections.Generic.List<Probe>(8);
            var spawner = bootstrap != null ? bootstrap.ViewSpawner : null;

            if (selectionController != null && selectionController.State.Count > 0)
            {
                for (var i = 0; i < selectionController.State.Count; i++)
                {
                    var id = selectionController.State.SelectedIds[i];
                    if (id.IsNone)
                        continue;
                    if (selectionController != null && !selectionController.IsPartyUnit(id))
                        continue;
                    if (!TryResolveUnitProbe(session, spawner, id, out var probe))
                        continue;
                    probes.Add(probe);
                }
            }

            if (probes.Count == 0 && session.CharacterIds.Count > 0)
            {
                if (TryResolveUnitProbe(session, spawner, session.CharacterIds[0], out var fallback))
                    probes.Add(fallback);
            }

            return probes.Count > 0;
        }

        static bool TryResolveUnitProbe(
            PlayableHostSession session,
            EntityViewSpawner spawner,
            EntityId id,
            out Probe probe)
        {
            probe = default;
            if (!session.World.Entities.TryGet(id, out var entity))
                return false;

            var sense = 0;
            if (entity.TryGet<AttributesComponent>(out var attrs))
                sense = attrs.GetFinal(AttributeId.SpiritSense);
            var radius = OpportunityEntranceRules.SurveyRadius(sense);

            float px, pz;
            if (spawner != null && spawner.Registry.TryGet(id, out var view) && view != null)
            {
                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                px = p.x;
                pz = p.y;
            }
            else if (entity.TryGet<EntityLocationComponent>(out var loc) &&
                     loc.HasLocation &&
                     session.World.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                px = place.PresentationX;
                pz = place.PresentationZ;
            }
            else
                return false;

            probe = new Probe { X = px, Z = pz, Radius = radius };
            return true;
        }

        static string FormatProbes(System.Collections.Generic.List<Probe> probes)
        {
            var sb = new StringBuilder(64);
            var inv = CultureInfo.InvariantCulture;
            for (var i = 0; i < probes.Count; i++)
            {
                if (i > 0)
                    sb.Append(';');
                var p = probes[i];
                sb.Append(p.X.ToString("0.###", inv)).Append(',')
                    .Append(p.Z.ToString("0.###", inv)).Append(',')
                    .Append(p.Radius.ToString("0.###", inv));
            }

            return sb.ToString();
        }

        static int CountRevealedEntrances(PlayableHostSession session)
        {
            if (session?.World?.WorldRegion?.Locations == null)
                return 0;
            var n = 0;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                if (OpportunityEntranceRules.IsHiddenEntrance(kv.Value) &&
                    OpportunityEntranceRules.IsRevealed(session.World, kv.Value))
                    n++;
            }

            return n;
        }

        void Flash(string text)
        {
            _flashToast = text ?? string.Empty;
            _flashUntil = Time.unscaledTime + 3.5f;
        }

        void OnGUI()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
                return;
            EnsureStyle();

            var msg = string.Empty;
            if (!string.IsNullOrEmpty(_flashToast) && Time.unscaledTime <= _flashUntil)
                msg = _flashToast;
            else if (!string.IsNullOrEmpty(_hintToast))
                msg = _hintToast;
            else if (Time.unscaledTime > _flashUntil)
                _flashToast = string.Empty;

            if (string.IsNullOrEmpty(msg))
                return;

            var w = Mathf.Min(480f, Screen.width - 40f);
            var r = new Rect((Screen.width - w) * 0.5f, 56f, w, 34f);
            HostUiHitTest.Block(r);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.12f, 0.08f, 0.9f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(r, msg, _toastStyle);
        }

        void EnsureStyle()
        {
            if (_toastStyle != null)
                return;
            _toastStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _toastStyle.normal.textColor = new Color(0.95f, 0.88f, 0.72f);
        }
    }
}
