using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Events;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase F: drain DomainEvents into a Host-side ring buffer for debug display.
    /// Does not invent gameplay rules.
    /// </summary>
    public sealed class HostEventFeed : MonoBehaviour
    {
        [SerializeField] int capacity = 40;
        [SerializeField] bool visible = false;
        [SerializeField] KeyCode toggleKey = KeyCode.F2;

        readonly List<string> _lines = new List<string>();
        int _totalPulled;

        public int Count => _lines.Count;

        public int TotalPulled => _totalPulled;

        public IReadOnlyList<string> Lines => _lines;

        public string LastStatusLine
        {
            get
            {
                if (_lines.Count == 0)
                    return "(no events)";
                var start = _lines.Count > 6 ? _lines.Count - 6 : 0;
                var sb = new StringBuilder();
                for (var i = start; i < _lines.Count; i++)
                {
                    if (i > start)
                        sb.Append('\n');
                    sb.Append(_lines[i]);
                }

                return sb.ToString();
            }
        }

        public void Clear()
        {
            _lines.Clear();
            _totalPulled = 0;
        }

        /// <summary>Drain world event queue into the feed (Host ownership after pull).</summary>
        public int PullFrom(DomainEventQueue events)
        {
            if (events == null || events.Count == 0)
                return 0;
            return Ingest(events.Drain());
        }

        /// <summary>Append already-drained events（与 InterruptPresenter 共享同一批）。</summary>
        public int Ingest(IReadOnlyList<DomainEvent> drained)
        {
            if (drained == null || drained.Count == 0)
                return 0;
            for (var i = 0; i < drained.Count; i++)
            {
                var line = Format(drained[i]);
                _lines.Add(line);
                _totalPulled++;
            }

            Trim();
            return drained.Count;
        }

        public bool ContainsTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            for (var i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].IndexOf(typeName, System.StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        public string ToDebugText()
        {
            if (_lines.Count == 0)
                return "(no events)";
            var sb = new StringBuilder(256);
            var start = _lines.Count > 12 ? _lines.Count - 12 : 0;
            for (var i = start; i < _lines.Count; i++)
                sb.AppendLine(_lines[i]);
            return sb.ToString();
        }

        // F2 留给 FormalHud「斗气纱衣」；事件流水改 Ctrl+F2。
        void Update()
        {
            if (Input.GetKeyDown(toggleKey) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            const float pad = 8f;
            var width = 460f;
            var height = 180f;
            var rect = new Rect(Screen.width - width - pad, pad, width, height);
            GUI.Box(rect, "Events (Ctrl+F2) pulled=" + _totalPulled);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 28f), ToDebugText());
        }

        void Trim()
        {
            var max = capacity < 8 ? 8 : capacity;
            while (_lines.Count > max)
                _lines.RemoveAt(0);
        }

        static string Format(DomainEvent evt)
        {
            if (evt == null)
                return "?";

            var priority = IsPriority(evt.Type) ? "*" : " ";
            return priority + "t" + evt.Tick.Value + " " + evt.Type +
                   " a=" + (evt.Actor.HasValue ? evt.Actor.Value.ToString() : "-") +
                   " " + evt.Payload;
        }

        static bool IsPriority(XianXia.Core.Events.EventType type)
        {
            switch (type)
            {
                case XianXia.Core.Events.EventType.DayStarted:
                case XianXia.Core.Events.EventType.DayEnded:
                case XianXia.Core.Events.EventType.ScheduleInterrupted:
                case XianXia.Core.Events.EventType.QuotaDeviationCreated:
                case XianXia.Core.Events.EventType.ObservationResolved:
                case XianXia.Core.Events.EventType.OpportunitySiteDiscovered:
                case XianXia.Core.Events.EventType.OrderRejected:
                case XianXia.Core.Events.EventType.ActionFailed:
                case XianXia.Core.Events.EventType.QuotaConsequenceApplied:
                case XianXia.Core.Events.EventType.ActionCompleted:
                case XianXia.Core.Events.EventType.Breakthrough:
                case XianXia.Core.Events.EventType.RelationshipChanged:
                case XianXia.Core.Events.EventType.FactionMembershipChanged:
                case XianXia.Core.Events.EventType.StoryFlagChanged:
                case XianXia.Core.Events.EventType.ChapterActivated:
                case XianXia.Core.Events.EventType.ChapterDayBeatApplied:
                case XianXia.Core.Events.EventType.ContentEventPresented:
                case XianXia.Core.Events.EventType.QuestCompleted:
                case XianXia.Core.Events.EventType.LocalMapChanged:
                    return true;
                default:
                    return false;
            }
        }
    }
}
