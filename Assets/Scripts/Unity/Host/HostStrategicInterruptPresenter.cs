using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>BattleOffer 战略接战弹窗（138 §3.1／§4）。</summary>
    public sealed class HostStrategicInterruptPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _holding;
        bool _pausedBefore;
        string _toast = string.Empty;
        double _toastUntil;
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
            _toast = string.Empty;
            _toastUntil = 0;
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

                var offer = session.World.Strategic.BattleOffer;
                if (offer != null &&
                    offer.IsJoinOngoingBattle &&
                    bootstrap.WorldMapPanel != null &&
                    !bootstrap.WorldMapPanel.IsOpen)
                {
                    bootstrap.WorldMapPanel.Open();
                }
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

            EnsureStyles();
            DrawToast();

            if (!HasBlockingInterrupt)
                return;

            GUI.depth = -90;

            var offer = session.World.Strategic.BattleOffer;
            if (!offer.Resolved && !string.IsNullOrEmpty(offer.OfferId))
            {
                if (offer.IsJoinOngoingBattle)
                    DrawJoinOngoingBattleOffer(session, offer);
                else
                    DrawBattleOffer(session, offer);
            }
        }

        void DrawJoinOngoingBattleOffer(PlayableHostSession session, BattleOfferPending offer)
        {
            DrawDim();
            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 150f, 480f, 300f);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var title = string.IsNullOrEmpty(offer.Title) ? "加入进行中的战斗" : offer.Title;
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), title, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 44f, box.width - 32f, 48f),
                "同一场战斗已在进行，无法自动接战。是否让 " + offer.PlayerLabel + " 加入当前 LocalMap？",
                _body);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 92f, box.width - 32f, 22f),
                offer.EnemyLabel + "  战力 " + offer.EnemyPower,
                _body);

            var y = box.y + box.height - 44f;
            var half = (box.width - 40f) * 0.5f;
            if (GUI.Button(new Rect(box.x + 16f, y, half, 32f), "加入战斗"))
            {
                var newcomers = StrategicPursuitService.CollectEngagedPartyFromOffer(offer);
                var encounterMapId = offer.EncounterLocalMapId;
                var joined = StrategicEncounterSpawner.JoinEngagedMembers(session.World, newcomers);
                if (joined.IsSuccess)
                {
                    session.World.Strategic.ClearBattleOffer();
                    bootstrap.CompleteEncounterJoinPresentation(newcomers, encounterMapId);
                    ShowToast("增援已加入当前战斗。");
                }
                else
                {
                    ShowToast(joined.Error.Message);
                }
            }

            if (GUI.Button(new Rect(box.x + 24f + half, y, half, 32f), "暂不加入"))
            {
                session.World.Strategic.ClearBattleOffer();
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
                return;
            EnsureStyles();
            var rect = new Rect(Screen.width * 0.5f - 220f, 72f, 440f, 32f);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            GUI.DrawTexture(rect, _px);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f), _toast, _body);
            GUI.color = prev;
        }

        void ShowToast(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 4f;
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
            {
                var resolved = BattleOfferService.ResolveAuto(session.World, out var won);
                if (resolved.IsSuccess)
                    ShowToast(won ? "自动战斗胜利。" : "自动战斗失利，敌军仍在。");
            }

            if (GUI.Button(new Rect(box.x + 20f + third, y, third, 32f), "手动战斗"))
            {
                EnterManualEncounter(session, offer.EncounterLocalMapId, offer.ArmyStackId);
                session.World.Strategic.ClearBattleOffer();
            }

            if (GUI.Button(new Rect(box.x + 24f + third * 2f, y, third, 32f), "撤退"))
            {
                StrategicPursuitService.ClearPursuit(session.World);
                session.World.Strategic.ClearBattleOffer();
            }
        }

        void EnterManualEncounter(
            PlayableHostSession session,
            string localMapId,
            string armyStackId)
        {
            if (session?.World == null || bootstrap == null)
                return;
            if (string.IsNullOrWhiteSpace(localMapId))
                localMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            var engaged = ResolveEngagedPartyForManualEncounter(session.World);
            StrategicEncounterSpawner.PlanManualEncounter(
                session.World,
                armyStackId,
                session.World.PartyWorld.EncounterId,
                engaged,
                StrategicEncounterCatalog.DefaultFallbackMemberCount,
                StrategicEncounterCatalog.DefaultFallbackCombatPower);
            StrategicPursuitService.ClearPursuit(session.World);
            session.World.PartyWorld.LocalMapId = localMapId.Trim();

            var closeMap = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: closeMap);
        }

        static List<EntityId> ResolveEngagedPartyForManualEncounter(SimulationWorld world)
        {
            var list = new List<EntityId>(4);
            if (world?.Strategic == null)
                return list;

            var offer = world.Strategic.BattleOffer;
            if (offer.PlayerPartyIds.Count > 0)
                list.AddRange(StrategicPursuitService.CollectEngagedPartyFromOffer(offer));

            if (list.Count == 0 && world.Strategic.Encounter.HasEngagedParty)
                list.AddRange(StrategicPursuitService.CollectEngagedParty(world, world.Strategic.Encounter));

            return list;
        }

        void DrawDim()
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
        }

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
