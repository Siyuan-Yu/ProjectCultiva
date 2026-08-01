using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Chapter Production content debug (F3). Not formal UI — authoring／QA only.
    /// </summary>
    public sealed class HostContentDebugPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.F3;
        [SerializeField] KeyCode advanceDayKey = KeyCode.F4;

        readonly ContentDebugService _debug = new ContentDebugService();
        string _dump = "";
        string _flagInput = "story:debug_flag";
        string _eventInput = "base:event_herb_whisper";
        string _status = "F3 content debug";

        public void Bind(PlayableHostBootstrap hostBootstrap, HostSelectionController selection)
        {
            bootstrap = hostBootstrap;
            selectionController = selection;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;

            if (!visible)
                return;

            if (Input.GetKeyDown(advanceDayKey))
                AdvanceOneDay();
        }

        void OnGUI()
        {
            if (!visible)
                return;

            RefreshDump();
            const float pad = 8f;
            var width = 520f;
            var height = Mathf.Min(420f, Screen.height - 20f);
            var rect = new Rect(Screen.width - width - pad, pad, width, height);
            GUI.Box(rect, "Content Debug (F3)  F4=+1Day");

            var y = rect.y + 24f;
            var x = rect.x + 8f;
            var w = rect.width - 16f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "+1 Day (F4)"))
                AdvanceOneDay();
            if (GUI.Button(new Rect(x + 108f, y, 100f, 24f), "Refresh"))
                RefreshDump();
            y += 30f;

            GUI.Label(new Rect(x, y, 60f, 22f), "Flag");
            _flagInput = GUI.TextField(new Rect(x + 60f, y, 220f, 22f), _flagInput);
            if (GUI.Button(new Rect(x + 288f, y, 70f, 22f), "Set"))
                RunFlag(true);
            if (GUI.Button(new Rect(x + 364f, y, 70f, 22f), "Clear"))
                RunFlag(false);
            y += 28f;

            GUI.Label(new Rect(x, y, 60f, 22f), "Event");
            _eventInput = GUI.TextField(new Rect(x + 60f, y, 220f, 22f), _eventInput);
            if (GUI.Button(new Rect(x + 288f, y, 140f, 22f), "Force Present"))
                ForceEvent();
            y += 28f;

            GUI.Label(new Rect(x, y, w, 20f), _status);
            y += 22f;
            GUI.TextArea(new Rect(x, y, w, rect.yMax - y - 8f), _dump);
        }

        void AdvanceOneDay()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized || session.Loop == null)
            {
                _status = "Session not ready";
                return;
            }

            var r = _debug.AdvanceDays(session.Loop, 1);
            _status = r.IsSuccess ? "Advanced +1 day" : r.Error.ToString();
            RefreshDump();
        }

        void RunFlag(bool set)
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                _status = "Session not ready";
                return;
            }

            var subject = FocusId();
            var r = set
                ? _debug.SetFlag(session.World, _flagInput, subject)
                : _debug.ClearFlag(session.World, _flagInput, subject);
            _status = r.IsSuccess ? (set ? "Flag set" : "Flag cleared") : r.Error.ToString();
            RefreshDump();
        }

        void ForceEvent()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                _status = "Session not ready";
                return;
            }

            var r = _debug.ForcePresentEvent(session.World, FocusId(), _eventInput);
            _status = r.IsSuccess ? "Event presented" : r.Error.ToString();
            RefreshDump();
        }

        void RefreshDump()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                _dump = "session not ready";
                return;
            }

            _dump = _debug.Dump(session.World, FocusId());
        }

        EntityId FocusId()
        {
            if (selectionController != null && selectionController.State.Count > 0)
                return selectionController.State.SelectedIds[0];
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session != null && session.CharacterIds != null && session.CharacterIds.Count > 0)
                return session.CharacterIds[0];
            return EntityId.None;
        }
    }
}
