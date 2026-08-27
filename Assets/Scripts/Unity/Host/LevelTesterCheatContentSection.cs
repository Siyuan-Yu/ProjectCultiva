using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Content flags / events。</summary>
    public sealed class LevelTesterCheatContentSection
    {
        readonly ContentDebugService _debug = new ContentDebugService();
        string _flagInput = "story:debug_flag";
        string _eventInput = "base:event_herb_whisper";
        string _dump = string.Empty;
        string _sectionStatus = string.Empty;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            HostSelectionController selection,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            GUI.Label(new Rect(x, y, width, lineH), "Content", body);
            y += lineH + 4f;

            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "Session not ready.");
                return y + lineH;
            }

            RefreshDump(session, selection);

            GUI.Label(new Rect(x, y, 40f, lineH), "Flag");
            _flagInput = GUI.TextField(new Rect(x + 44f, y, width - 200f, 22f), _flagInput);
            if (GUI.Button(new Rect(x + width - 148f, y, 68f, 22f), "Set"))
                RunFlag(session, selection, true);
            if (GUI.Button(new Rect(x + width - 74f, y, 68f, 22f), "Clear"))
                RunFlag(session, selection, false);
            y += 26f;

            GUI.Label(new Rect(x, y, 40f, lineH), "Event");
            _eventInput = GUI.TextField(new Rect(x + 44f, y, width - 100f, 22f), _eventInput);
            if (GUI.Button(new Rect(x + width - 90f, y, 88f, 22f), "Force Present"))
                ForceEvent(session, selection);
            y += 26f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "Refresh Dump"))
                RefreshDump(session, selection);
            y += 28f;

            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f), _sectionStatus, body);
                y += lineH * 2f;
            }

            var dumpH = Mathf.Min(120f, body.CalcHeight(new GUIContent(_dump), width));
            GUI.TextArea(new Rect(x, y, width, dumpH), _dump);
            y += dumpH + 4f;
            return y;
        }

        void RunFlag(PlayableHostSession session, HostSelectionController selection, bool set)
        {
            var subject = FocusId(session, selection);
            var r = set
                ? _debug.SetFlag(session.World, _flagInput, subject)
                : _debug.ClearFlag(session.World, _flagInput, subject);
            _sectionStatus = r.IsSuccess
                ? (set ? "OK: Flag set." : "OK: Flag cleared.")
                : "FAIL: " + r.Error;
            RefreshDump(session, selection);
        }

        void ForceEvent(PlayableHostSession session, HostSelectionController selection)
        {
            var r = _debug.ForcePresentEvent(session.World, FocusId(session, selection), _eventInput);
            _sectionStatus = r.IsSuccess ? "OK: Event presented." : "FAIL: " + r.Error;
            RefreshDump(session, selection);
        }

        void RefreshDump(PlayableHostSession session, HostSelectionController selection)
        {
            _dump = _debug.Dump(session.World, FocusId(session, selection));
        }

        static EntityId FocusId(PlayableHostSession session, HostSelectionController selection)
        {
            if (selection != null && selection.State.Count > 0)
                return selection.State.SelectedIds[0];
            if (session?.CharacterIds != null && session.CharacterIds.Count > 0)
                return session.CharacterIds[0];
            return EntityId.None;
        }
    }
}
