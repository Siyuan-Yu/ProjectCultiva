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

        sealed class Entry
        {
            public string Text;
            public float BornAt;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        readonly List<Entry> _visible = new List<Entry>(MaxVisible);
        readonly Queue<string> _pending = new Queue<string>();
        GUIStyle _style;

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
                _pending.Enqueue(text);
            }
            PromotePending();
        }

        string Format(EntityId reactor, EntityId target, SocialReactionPayload payload)
        {
            var reactorName = Name(reactor);
            var targetName = IsActive(target) ? "你" : Name(target);
            var targetPossessive = IsActive(target) ? "你的" : targetName + "的";
            var contextName = Name(payload.ContextEntityId);
            var arrow = Arrow(payload.AffectionDelta);
            switch (payload.ReasonTag)
            {
                case "character_killed":
                    return reactorName + "因" + contextName + "之死，对" + targetName + "的好感 " + arrow;
                case "character_attacked":
                    return reactorName + "因遭到" + targetPossessive + "攻击，对" + targetName + "的好感 " + arrow;
                case "character_helped":
                    return reactorName + "因受到" + targetPossessive + "帮助，对" + targetName + "的好感 " + arrow;
                case "character_rescued":
                    return reactorName + "因受到" + targetPossessive + "救助，对" + targetName + "的好感 " + arrow;
                default:
                    return reactorName + "对" + targetName + "的好感 " + arrow;
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
                _visible.Add(new Entry { Text = _pending.Dequeue(), BornAt = Time.unscaledTime });
        }

        void OnGUI()
        {
            if (_visible.Count == 0)
                return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 14,
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 6, 6)
                };
                _style.normal.textColor = new Color(1f, 0.94f, 0.78f);
            }

            var now = Time.unscaledTime;
            for (var i = _visible.Count - 1; i >= 0; i--)
                if (now - _visible[i].BornAt >= Lifetime)
                    _visible.RemoveAt(i);
            PromotePending();
            const float width = 390f;
            const float height = 48f;
            for (var i = 0; i < _visible.Count; i++)
            {
                var age = now - _visible[i].BornAt;
                var alpha = Mathf.Min(1f, age / 0.25f, (Lifetime - age) / 0.4f);
                var rect = new Rect(Screen.width - width - 22f, 92f + i * (height + 6f), width, height);
                var old = GUI.color;
                GUI.color = new Color(0.12f, 0.09f, 0.06f, 0.82f * alpha);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(rect, _visible[i].Text, _style);
                GUI.color = old;
            }
        }
    }
}
