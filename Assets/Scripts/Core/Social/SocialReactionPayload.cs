using System;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Social
{
    public sealed class SocialReactionPayload
    {
        public string ReasonTag { get; private set; } = string.Empty;
        public int AffectionDelta { get; private set; }
        public EntityId ContextEntityId { get; private set; } = EntityId.None;

        public static string Encode(string reasonTag, int affectionDelta, EntityId contextEntityId) =>
            "reason=" + (reasonTag ?? string.Empty) +
            ";affectionDelta=" + affectionDelta +
            ";contextEntityId=" + contextEntityId.Value;

        public static bool TryDecode(string text, out SocialReactionPayload payload)
        {
            payload = null;
            if (string.IsNullOrEmpty(text))
                return false;
            var reason = string.Empty;
            var delta = 0;
            ulong context = 0;
            var hasDelta = false;
            var fields = text.Split(';');
            for (var i = 0; i < fields.Length; i++)
            {
                var split = fields[i].IndexOf('=');
                if (split <= 0)
                    continue;
                var key = fields[i].Substring(0, split);
                var value = fields[i].Substring(split + 1);
                if (key == "reason") reason = value;
                else if (key == "affectionDelta") hasDelta = int.TryParse(value, out delta);
                else if (key == "contextEntityId") ulong.TryParse(value, out context);
            }
            if (!hasDelta || string.IsNullOrEmpty(reason))
                return false;
            payload = new SocialReactionPayload
            {
                ReasonTag = reason,
                AffectionDelta = delta,
                ContextEntityId = new EntityId(context)
            };
            return true;
        }
    }
}
