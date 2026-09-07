using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>统一人物卡、名称、Bond 称谓和五维详情的 Host Presentation helper。</summary>
    public static class HostCharacterRelationUi
    {
        static readonly Color Card = new Color(0.13f, 0.15f, 0.17f, 0.98f);
        static readonly Color Selected = new Color(0.72f, 0.55f, 0.23f, 1f);
        static readonly Color Border = new Color(0.40f, 0.35f, 0.27f, 1f);

        public static string ResolveDisplayName(SimulationWorld world, EntityId id) =>
            world != null && world.Entities.TryGet(id, out var entity) &&
            !string.IsNullOrEmpty(entity.DisplayName)
                ? entity.DisplayName
                : id.ToString();

        public static string ResolveBondRole(SocialBond bond, EntityId subject) =>
            SocialBondQuery.GetRoleLabel(bond, subject);

        /// <summary>未来 portrait authority 接入点；V1 没有可靠 portrait 时明确返回 false。</summary>
        public static bool TryResolveCharacterPortrait(
            PlayableHostSession session, EntityId id, out Texture portrait)
        {
            portrait = null;
            return false;
        }

        public static bool DrawPersonCard(
            Rect rect,
            PlayableHostSession session,
            EntityId id,
            string badge,
            bool selected,
            GUIStyle nameStyle,
            GUIStyle badgeStyle,
            Texture2D pixel)
        {
            Fill(rect, selected ? Selected : Border, pixel);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Fill(inner, Card, pixel);
            var portraitRect = new Rect(inner.x + 8f, inner.y + 7f, inner.width - 16f, inner.height - 48f);
            if (TryResolveCharacterPortrait(session, id, out var portrait) && portrait != null)
                GUI.DrawTexture(portraitRect, portrait, ScaleMode.ScaleToFit);
            else
            {
                Fill(portraitRect, new Color(0.22f, 0.25f, 0.27f, 1f), pixel);
                var name = ResolveDisplayName(session?.World, id);
                GUI.Label(portraitRect, Initial(name), new GUIStyle(nameStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 28
                });
            }
            GUI.Label(new Rect(inner.x + 4f, inner.yMax - 39f, inner.width - 8f, 20f),
                ResolveDisplayName(session?.World, id), nameStyle);
            GUI.Label(new Rect(inner.x + 4f, inner.yMax - 20f, inner.width - 8f, 18f),
                badge ?? string.Empty, badgeStyle);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        public static void DrawAttitudeDetail(
            Rect rect, string title, SocialAttitude attitude,
            GUIStyle heading, GUIStyle body, Texture2D pixel)
        {
            Fill(rect, new Color(0.10f, 0.12f, 0.14f, 0.96f), pixel);
            Stroke(rect, Border, pixel);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), title, heading);
            attitude = attitude ?? SocialAttitude.Zero;
            var text = "好感　" + attitude.Affection + "\n" +
                       "信任　" + attitude.Trust + "\n" +
                       "敬重　" + attitude.Respect + "\n" +
                       "畏惧　" + attitude.Fear + "\n" +
                       "仇恨　" + attitude.Grudge;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 28f, rect.height - 46f), text, body);
        }

        public static void Fill(Rect rect, Color color, Texture2D pixel)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }

        public static void Stroke(Rect rect, Color color, Texture2D pixel)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1f), color, pixel);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color, pixel);
            Fill(new Rect(rect.x, rect.y, 1f, rect.height), color, pixel);
            Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color, pixel);
        }

        static string Initial(string name) =>
            string.IsNullOrEmpty(name) ? "人" : name.Substring(0, 1);
    }
}
