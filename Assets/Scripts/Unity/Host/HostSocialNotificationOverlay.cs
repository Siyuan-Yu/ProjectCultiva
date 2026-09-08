using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>只呈现与 PlayerParty 有关的社会反应；不参与暂停与输入门。</summary>
    public sealed class HostSocialNotificationOverlay : MonoBehaviour
    {
        const int MaxVisible = 3;
        const float Lifetime = 2.8f;
        const float FadeInDuration = 0.2f;
        const float FadeOutDuration = 0.5f;
        const float FloatDistance = 14f;
        const int OverlayDepth = -1000;

        sealed class Entry
        {
            public string Text;
            public string Arrow;
            public bool Positive;
            public float BornAt;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        readonly List<Entry> _visible = new List<Entry>(MaxVisible);
        readonly Queue<Entry> _pending = new Queue<Entry>();
        GUIStyle _bodyStyle;
        GUIStyle _arrowStyle;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;
        public void Clear()
        {
            _visible.Clear();
            _pending.Clear();
        }

        public void Ingest(IReadOnlyList<DomainEvent> events)
        {
            var world = bootstrap?.Session?.World;
            var party = bootstrap?.Session?.PlayerParty;
            if (world == null || party == null || events == null)
                return;
            for (var i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null || evt.Type != XianXia.Core.Events.EventType.SocialReaction ||
                    !evt.Actor.HasValue || !evt.Target.HasValue ||
                    !party.IsMember(evt.Target.Value) ||
                    !SocialReactionPayload.TryDecode(evt.Payload, out var payload))
                    continue;
                var text = Format(evt.Actor.Value, evt.Target.Value, payload);
                if (string.IsNullOrEmpty(text))
                    continue;
                _pending.Enqueue(new Entry
                {
                    Text = text,
                    Arrow = Arrow(payload.AffectionDelta),
                    Positive = payload.AffectionDelta >= 0
                });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    "[SocialReactionUI] reactor=" + evt.Actor.Value +
                    " target=" + evt.Target.Value +
                    " delta=" + payload.AffectionDelta +
                    " queued=true",
                    this);
#endif
            }
            PromotePending();
        }

        string Format(EntityId reactor, EntityId target, SocialReactionPayload payload)
        {
            var reactorName = Name(reactor);
            var targetName = IsActive(target) ? "你" : Name(target);
            var targetPossessive = IsActive(target) ? "你的" : targetName + "的";
            var contextName = Name(payload.ContextEntityId);
            switch (payload.ReasonTag)
            {
                case "character_killed":
                    return reactorName + "因" + contextName + "之死，对" + targetName + "的好感";
                case "character_attacked":
                    return reactorName + "因遭到" + targetPossessive + "攻击，对" + targetName + "的好感";
                case "character_helped":
                    return reactorName + "因受到" + targetPossessive + "帮助，对" + targetName + "的好感";
                case "character_rescued":
                    return reactorName + "因受到" + targetPossessive + "救助，对" + targetName + "的好感";
                default:
                    return reactorName + "对" + targetName + "的好感";
            }
        }

        bool IsActive(EntityId id) =>
            bootstrap?.Session?.PlayerParty != null &&
            bootstrap.Session.PlayerParty.ActiveCharacterId == id;

        string Name(EntityId id)
        {
            var world = bootstrap?.Session?.World;
            return world != null && world.Entities.TryGet(id, out var entity) &&
                   !string.IsNullOrEmpty(entity.DisplayName)
                ? entity.DisplayName
                : id.ToString();
        }

        static string Arrow(int delta)
        {
            var count = Mathf.Abs(delta) >= 35 ? 3 : (Mathf.Abs(delta) >= 15 ? 2 : 1);
            return new string(delta >= 0 ? '↑' : '↓', count);
        }

        void PromotePending()
        {
            while (_visible.Count < MaxVisible && _pending.Count > 0)
            {
                var entry = _pending.Dequeue();
                entry.BornAt = Time.unscaledTime;
                _visible.Add(entry);
            }
        }

        void OnGUI()
        {
            if (_visible.Count == 0)
                return;
            EnsureStyles();

            var now = Time.unscaledTime;
            for (var i = _visible.Count - 1; i >= 0; i--)
                if (now - _visible[i].BornAt >= Lifetime)
                    _visible.RemoveAt(i);
            PromotePending();
            if (_visible.Count == 0)
                return;

            var oldDepth = GUI.depth;
            GUI.depth = OverlayDepth;
            try
            {
                const float width = 580f;
                const float height = 34f;
                const float gap = 5f;
                var x = (Screen.width - width) * 0.5f;
                var baseY = HostFormalHud.HeaderReservedHeight + 22f;
                for (var i = 0; i < _visible.Count; i++)
                {
                    var entry = _visible[i];
                    var age = now - entry.BornAt;
                    var alpha = Mathf.Min(
                        1f,
                        age / FadeInDuration,
                        (Lifetime - age) / FadeOutDuration);
                    alpha = Mathf.Clamp01(alpha);
                    var lift = Mathf.Clamp01(age / Lifetime) * FloatDistance;
                    var row = new Rect(x, baseY + i * (height + gap) - lift, width, height);

                    var bodyWidth = _bodyStyle.CalcSize(new GUIContent(entry.Text)).x;
                    var arrowWidth = _arrowStyle.CalcSize(new GUIContent(entry.Arrow)).x;
                    var totalWidth = Mathf.Min(width, bodyWidth + 8f + arrowWidth);
                    var contentX = row.center.x - totalWidth * 0.5f;
                    var bodyRect = new Rect(contentX, row.y, Mathf.Min(bodyWidth, width - arrowWidth - 8f), height);
                    var arrowRect = new Rect(bodyRect.xMax + 8f, row.y, arrowWidth, height);

                    DrawTextWithShadow(bodyRect, entry.Text, _bodyStyle, new Color(1f, 0.95f, 0.82f), alpha);
                    DrawTextWithShadow(
                        arrowRect,
                        entry.Arrow,
                        _arrowStyle,
                        entry.Positive ? new Color(0.4f, 1f, 0.52f) : new Color(1f, 0.38f, 0.32f),
                        alpha);
                }
            }
            finally
            {
                GUI.depth = oldDepth;
            }
        }

        void EnsureStyles()
        {
            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 19,
                    clipping = TextClipping.Clip
                };
                _bodyStyle.normal.textColor = Color.white;
            }
            if (_arrowStyle != null)
                return;
            _arrowStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }

        static void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color color, float alpha)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, style);
            GUI.color = oldColor;
        }
    }
}
