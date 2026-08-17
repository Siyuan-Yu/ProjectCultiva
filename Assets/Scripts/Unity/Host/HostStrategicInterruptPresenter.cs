using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Route 遭遇与 BattleOffer 战略打断弹窗（138 §3.1／§4）。</summary>
    public sealed class HostStrategicInterruptPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _holding;
        bool _pausedBefore;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);

        public bool HasBlockingInterrupt
        {
            get
            {
                var session = bootstrap != null ? bootstrap.Session : null;
                if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                    return false;
                return session.World.Strategic.HasBlockingInterrupt;
            }
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _holding = false;
            _pausedBefore = false;
        }

        void Update() => SyncPause();

        void SyncPause()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                ClearSessionState();
                return;
            }

            if (HasBlockingInterrupt)
            {
                if (!_holding)
                {
                    _pausedBefore = session.IsPaused;
                    _holding = true;
                }

                session.IsPaused = true;
            }
            else if (_holding)
            {
                session.IsPaused = _pausedBefore;
                _holding = false;
            }
        }

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                return;
            if (!HasBlockingInterrupt)
                return;

            EnsureStyles();
            GUI.depth = -90;

            var strategic = session.World.Strategic;
            if (!strategic.BattleOffer.Resolved && !string.IsNullOrEmpty(strategic.BattleOffer.OfferId))
                DrawBattleOffer(session, strategic.BattleOffer);
            else if (!strategic.RouteEncounter.Resolved &&
                     !string.IsNullOrEmpty(strategic.RouteEncounter.EncounterId))
                DrawRouteEncounter(session, strategic.RouteEncounter);
        }

        void DrawRouteEncounter(PlayableHostSession session, RouteEncounterPending pending)
        {
            DrawDim();
            var box = ModalBox();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var title = string.IsNullOrEmpty(pending.Title) ? "路遇险情" : pending.Title;
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), title, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 42f, box.width - 32f, 24f),
                "已暂停 — 选择迎战或避开",
                _body);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 72f, box.width - 32f, 80f),
                "遭遇：" + pending.EncounterId,
                _body);

            var y = box.y + box.height - 44f;
            var half = (box.width - 40f) * 0.5f;
            if (GUI.Button(new Rect(box.x + 16f, y, half, 32f), "迎战（进 LocalMap）"))
            {
                EnterEncounterLocalMap(session, pending.LocalMapId);
                RouteEncounterService.ResolveSuccess(session.World);
            }

            if (GUI.Button(new Rect(box.x + 24f + half, y, half, 32f), "避开"))
                RouteEncounterService.ResolveSuccess(session.World);
        }

        void DrawBattleOffer(PlayableHostSession session, BattleOfferPending offer)
        {
            DrawDim();
            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 170f, 480f, 340f);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var title = string.IsNullOrEmpty(offer.Title) ? "遭遇接战" : offer.Title;
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), title, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 40f, box.width - 32f, 22f),
                offer.PlayerLabel + "  战力 " + offer.PlayerPower,
                _body);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 62f, box.width - 32f, 22f),
                offer.EnemyLabel + "  战力 " + offer.EnemyPower,
                _body);

            var barY = box.y + 92f;
            var barW = box.width - 32f;
            var total = Mathf.Max(1, offer.PlayerPower + offer.EnemyPower);
            var pw = barW * (offer.PlayerPower / (float)total);
            GUI.color = new Color(0.35f, 0.72f, 0.42f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f, barY, pw, 14f), _px);
            GUI.color = new Color(0.78f, 0.32f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f + pw, barY, barW - pw, 14f), _px);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(box.x + 16f, barY + 18f, box.width - 32f, 22f),
                "自动战胜率约 " + offer.AutoWinPercent + "%（选定后不可反悔）",
                _body);

            var y = box.y + box.height - 44f;
            var third = (box.width - 40f) / 3f;
            if (GUI.Button(new Rect(box.x + 16f, y, third, 32f), "自动战斗"))
                BattleOfferService.ResolveAuto(session.World);

            if (GUI.Button(new Rect(box.x + 20f + third, y, third, 32f), "手动战斗"))
            {
                EnterEncounterLocalMap(session, offer.EncounterLocalMapId);
                session.World.Strategic.ClearBattleOffer();
            }

            if (GUI.Button(new Rect(box.x + 24f + third * 2f, y, third, 32f), "撤退"))
                session.World.Strategic.ClearBattleOffer();
        }

        void EnterEncounterLocalMap(PlayableHostSession session, string localMapId)
        {
            if (session?.World == null || bootstrap == null)
                return;
            if (string.IsNullOrWhiteSpace(localMapId))
                localMapId = RouteEncounterService.DefaultEncounterLocalMapId;
            session.World.PartyWorld.LocalMapId = localMapId.Trim();
            var closeMap = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: closeMap);
        }

        void DrawDim()
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
        }

        static Rect ModalBox() =>
            new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 130f, 440f, 260f);

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), _px);
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _stylesReady = true;
        }
    }
}
