using UnityEngine;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// LevelTester 统一 Development Cheat Tools UI。
    /// 仅服务于 LevelTester 人工验收；不占用 F-Key；不嵌入正式 Gameplay UI。
    /// </summary>
    public sealed class HostLevelTesterCheatPanel : MonoBehaviour
    {
        const int WindowId = 0x1E7E573;
        const float PanelWidth = 620f;
        const float PanelMinHeight = 480f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        readonly LevelTesterCheatTimeSection _time = new LevelTesterCheatTimeSection();
        readonly LevelTesterCheatBackgroundSection _background = new LevelTesterCheatBackgroundSection();
        readonly LevelTesterCheatFormalArmySection _formalArmy = new LevelTesterCheatFormalArmySection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();

        bool _foldTime = true;
        bool _foldBackground = true;
        bool _foldFormalArmy = true;
        bool _foldContent;
        bool _foldDiplomacy;
        bool _foldSnapshot = true;
        bool _foldBattle;
        bool _foldDiagnostics;
        bool _resetConfirmPending;
        string _sessionStatus = string.Empty;
        string _snapshotStatus = string.Empty;
        Vector2 _scroll;
        Rect _panelRect;
        bool _panelRectInitialized;
        GUIStyle _title;
        GUIStyle _body;

        public bool IsVisible => visible;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ToggleVisible() => visible = !visible;

        public void Show() => visible = true;

        public void Hide() => visible = false;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            EnsurePanelRect();
            _panelRect = GUI.Window(WindowId, _panelRect, DrawWindow, "LevelTester Cheat Tools");
            HostUiHitTest.Block(_panelRect);
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;
            _panelRect = new Rect(24f, 120f, PanelWidth, PanelMinHeight);
            _panelRectInitialized = true;
        }

        void EnsureStyles()
        {
            if (_body != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { wordWrap = true };
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));
            const float pad = 8f;
            var innerW = PanelWidth - pad * 2f - 16f;
            var viewH = _panelRect.height - 36f;
            var contentH = 3200f;
            _scroll = GUI.BeginScrollView(
                new Rect(pad, 26f, PanelWidth - pad * 2f, viewH),
                _scroll,
                new Rect(0f, 0f, innerW, contentH));

            var y = 0f;
            y = DrawFoldoutSection(ref _foldTime, "Time / Simulation", y, innerW,
                w => _time.Draw(bootstrap, 0f, w, innerW, _body));
            y = DrawFoldoutSection(ref _foldBackground, "Background Character", y, innerW,
                w => _background.Draw(bootstrap, 0f, w, innerW, _body));
            y = DrawFoldoutSection(ref _foldFormalArmy, "FormalArmy", y, innerW,
                w => _formalArmy.Draw(bootstrap, 0f, w, innerW, _body));
            y = DrawFoldoutSection(ref _foldContent, "Content", y, innerW,
                w => _content.Draw(bootstrap, selectionController, 0f, w, innerW, _body));
            y = DrawFoldoutSection(ref _foldDiplomacy, "Diplomacy", y, innerW,
                w => _diplomacy.Draw(bootstrap, 0f, w, innerW, _body));
            y = DrawSnapshotSection(y, innerW);
            y = DrawBattleSection(y, innerW);
            y = DrawDiagnosticsSection(y, innerW);

            GUI.EndScrollView();

            if (GUI.Button(new Rect(PanelWidth - 72f, 4f, 64f, 20f), "Close"))
                visible = false;
        }

        float DrawFoldoutSection(
            ref bool expanded,
            string title,
            float y,
            float width,
            System.Func<float, float> drawContent)
        {
            expanded = GUI.Toggle(new Rect(0f, y, width, 22f), expanded, title, _title);
            y += 24f;
            if (!expanded)
                return y + 4f;
            y = drawContent(y) + 8f;
            return y;
        }

        float DrawSnapshotSection(float y, float width)
        {
            _foldSnapshot = GUI.Toggle(new Rect(0f, y, width, 22f), _foldSnapshot, "Snapshot / Session", _title);
            y += 24f;
            if (!_foldSnapshot)
                return y + 4f;

            var lineH = 18f;
            GUI.Label(new Rect(0f, y, width, lineH),
                "Snapshot v" + HostLevelTesterSnapshotOps.SchemaVersion + "  " +
                HostLevelTesterSnapshotOps.SlotPath, _body);
            y += lineH + 4f;

            if (GUI.Button(new Rect(0f, y, width * 0.48f, 24f), "Save Snapshot"))
            {
                var r = HostLevelTesterSnapshotOps.TrySave(bootstrap);
                _snapshotStatus = (r.Success ? "OK: " : "FAIL: ") + r.Message;
            }

            if (GUI.Button(new Rect(width * 0.52f, y, width * 0.48f, 24f), "Load Snapshot"))
            {
                var r = HostLevelTesterSnapshotOps.TryLoad(bootstrap);
                _snapshotStatus = (r.Success ? "OK: " : "FAIL: ") + r.Message;
            }

            y += 28f;
            if (!_resetConfirmPending)
            {
                if (GUI.Button(new Rect(0f, y, width, 24f), "Reset LevelTester Session..."))
                    _resetConfirmPending = true;
                y += 28f;
            }
            else
            {
                GUI.Label(new Rect(0f, y, width, lineH * 2f),
                    "This rebuilds the entire session from current Inspector config.", _body);
                y += lineH * 2f;
                if (GUI.Button(new Rect(0f, y, width * 0.48f, 24f), "Confirm Reset"))
                {
                    _resetConfirmPending = false;
                    if (bootstrap != null)
                    {
                        var ok = bootstrap.TryInitialize();
                        _sessionStatus = ok ? "OK: Session reset." : "FAIL: Session reset failed.";
                    }
                    else
                    {
                        _sessionStatus = "FAIL: No bootstrap.";
                    }
                }

                if (GUI.Button(new Rect(width * 0.52f, y, width * 0.48f, 24f), "Cancel"))
                    _resetConfirmPending = false;
                y += 28f;
            }

            if (!string.IsNullOrEmpty(_snapshotStatus))
            {
                GUI.Label(new Rect(0f, y, width, lineH * 2f), _snapshotStatus, _body);
                y += lineH * 2f;
            }

            if (!string.IsNullOrEmpty(_sessionStatus))
            {
                GUI.Label(new Rect(0f, y, width, lineH * 2f), _sessionStatus, _body);
                y += lineH * 2f;
            }

            return y + 4f;
        }

        float DrawBattleSection(float y, float width)
        {
            _foldBattle = GUI.Toggle(new Rect(0f, y, width, 22f), _foldBattle, "Battle / Acceptance", _title);
            y += 24f;
            if (!_foldBattle)
                return y + 4f;

            var forceSolo = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            var next = GUI.Toggle(
                new Rect(0f, y, width, 22f),
                forceSolo,
                "DEBUG: Next Solo Auto-Battle Guaranteed Incapacitation");
            if (next != forceSolo)
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = next;
            y += 26f;
            return y + 4f;
        }

        float DrawDiagnosticsSection(float y, float width)
        {
            _foldDiagnostics = GUI.Toggle(new Rect(0f, y, width, 22f), _foldDiagnostics, "Diagnostics (Visualization)", _title);
            y += 24f;
            if (!_foldDiagnostics)
                return y + 4f;

            var strongSep = HostHexWorldRenderer.DebugStrongHexSeparation;
            var nextSep = GUI.Toggle(new Rect(0f, y, width, 22f), strongSep, "Debug: Strong Hex Separation (render only)");
            if (nextSep != strongSep)
                HostHexWorldRenderer.DebugStrongHexSeparation = nextSep;
            y += 26f;
            return y + 4f;
        }
    }
}
