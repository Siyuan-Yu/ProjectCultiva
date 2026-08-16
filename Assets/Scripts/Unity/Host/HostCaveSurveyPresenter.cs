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
    /// 隐藏洞府类交互：近距气息提示；F7 勘查（半径＝神识×2）；可选 surveySenseRequired 门槛。
    /// </summary>
    public sealed class HostCaveSurveyPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;

        string _hintToast = string.Empty;
        string _flashToast = string.Empty;
        float _flashUntil;
        bool _modalOpen;
        string _modalTitle = string.Empty;
        string _modalBody = string.Empty;
        bool _heldPauseForModal;
        GUIStyle _toastStyle;
        GUIStyle _modalTitleStyle;
        GUIStyle _modalBodyStyle;
        Texture2D _px;
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
            _modalOpen = false;
            _modalTitle = string.Empty;
            _modalBody = string.Empty;
            _heldPauseForModal = false;
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

            DiagnoseNearby(session, probes, out var maxSense, out var inSurveyGateFail, out var inHintOutOfRange);

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
                return true;
            }

            // 半径内但门槛不够／仅气息圈内扫不到 → 弹窗说明神识
            if (inSurveyGateFail > 0)
            {
                ShowSenseModal(
                    "神识不足",
                    "附近似有隐藏洞府，但你的神识尚不足以探明入口。\n（当前神识 " + maxSense + "）");
                return true;
            }

            if (inHintOutOfRange > 0)
            {
                ShowSenseModal(
                    "神识未及",
                    "附近似有洞府气息，但神识覆盖未及。\n可再靠近，或提升神识后再勘查。\n（当前神识 " + maxSense + "，勘查半径约 " +
                    OpportunityEntranceRules.SurveyRadius(maxSense).ToString("0.#") + "）");
                return true;
            }

            Flash("四周未见异样。");
            return true;
        }

        void DiagnoseNearby(
            PlayableHostSession session,
            System.Collections.Generic.List<Probe> probes,
            out int maxSense,
            out int inSurveyGateFail,
            out int inHintOutOfRange)
        {
            maxSense = 0;
            inSurveyGateFail = 0;
            inHintOutOfRange = 0;
            for (var i = 0; i < probes.Count; i++)
            {
                if (probes[i].Sense > maxSense)
                    maxSense = probes[i].Sense;
            }

            foreach (var kv in session.World.WorldRegion.Locations)
            {
                var entrance = kv.Value;
                if (!OpportunityEntranceRules.IsHiddenEntrance(entrance))
                    continue;
                if (OpportunityEntranceRules.IsRevealed(session.World, entrance))
                    continue;

                var inSurvey = false;
                var inHint = false;
                for (var i = 0; i < probes.Count; i++)
                {
                    var p = probes[i];
                    var hintR = Mathf.Max(OpportunityEntranceRules.DefaultHintRadius, p.Radius);
                    if (OpportunityEntranceRules.IsWithinSurveyRange(p.X, p.Z, hintR, entrance))
                        inHint = true;
                    if (OpportunityEntranceRules.IsWithinSurveyRange(p.X, p.Z, p.Radius, entrance))
                        inSurvey = true;
                }

                if (inSurvey)
                {
                    if (!OpportunityEntranceRules.MeetsSenseRequirement(entrance, maxSense))
                        inSurveyGateFail++;
                }
                else if (inHint)
                {
                    inHintOutOfRange++;
                }
            }
        }

        void ShowSenseModal(string title, string body)
        {
            _modalOpen = true;
            _modalTitle = title ?? string.Empty;
            _modalBody = body ?? string.Empty;
            if (bootstrap?.Session != null && !bootstrap.Session.IsPaused)
            {
                bootstrap.Session.IsPaused = true;
                _heldPauseForModal = true;
            }
        }

        void CloseModal()
        {
            _modalOpen = false;
            _modalTitle = string.Empty;
            _modalBody = string.Empty;
            if (_heldPauseForModal && bootstrap?.Session != null)
            {
                bootstrap.Session.IsPaused = false;
                _heldPauseForModal = false;
            }
        }

        struct Probe
        {
            public float X;
            public float Z;
            public float Radius;
            public int Sense;
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

            probe = new Probe { X = px, Z = pz, Radius = radius, Sense = sense };
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
            EnsureStyles();

            if (_modalOpen)
            {
                DrawSenseModal();
                return;
            }

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
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
            GUI.Label(r, msg, _toastStyle);
        }

        void DrawSenseModal()
        {
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, Screen.height));
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;

            var w = Mathf.Min(420f, Screen.width - 48f);
            var h = 168f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(r);
            GUI.color = new Color(0.92f, 0.86f, 0.74f, 0.98f);
            GUI.DrawTexture(r, _px);
            GUI.color = prev;

            GUI.Label(new Rect(r.x + 16f, r.y + 14f, r.width - 32f, 28f), _modalTitle, _modalTitleStyle);
            GUI.Label(new Rect(r.x + 16f, r.y + 48f, r.width - 32f, 72f), _modalBody, _modalBodyStyle);
            if (GUI.Button(new Rect(r.x + r.width * 0.5f - 48f, r.yMax - 44f, 96f, 28f), "知道了"))
                CloseModal();
        }

        void EnsureStyles()
        {
            if (_px == null)
            {
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }

            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _toastStyle.normal.textColor = new Color(0.95f, 0.88f, 0.72f);
            }

            if (_modalTitleStyle == null)
            {
                _modalTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _modalTitleStyle.normal.textColor = new Color(0.2f, 0.14f, 0.08f);
            }

            if (_modalBodyStyle == null)
            {
                _modalBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    wordWrap = true
                };
                _modalBodyStyle.normal.textColor = new Color(0.28f, 0.2f, 0.12f);
            }
        }
    }
}
