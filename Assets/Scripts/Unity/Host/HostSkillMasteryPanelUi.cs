using System.Text;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 功法／斗技 UI 排布：列表行＋熟练度详情页（参考仙侠模拟器式两级页，用词与玩法用本项目）。
    /// </summary>
    public static class HostSkillMasteryPanelUi
    {
        public static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        public static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        public static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        public static readonly Color RowIdle = new Color(0.88f, 0.80f, 0.66f, 0.55f);
        public static readonly Color RowCurrent = new Color(0.82f, 0.68f, 0.42f, 0.75f);
        public static readonly Color RowDone = new Color(0.78f, 0.84f, 0.72f, 0.55f);
        public static readonly Color IconFill = new Color(0.55f, 0.42f, 0.28f, 0.9f);
        public static readonly Color Accent = new Color(0.55f, 0.18f, 0.14f, 1f);

        static readonly SkillMasteryTier[] DisplayOrder =
        {
            SkillMasteryTier.Novice,
            SkillMasteryTier.Entry,
            SkillMasteryTier.Minor,
            SkillMasteryTier.Major,
            SkillMasteryTier.Perfect,
            SkillMasteryTier.Transcendent
        };

        public static void DrawIconCircle(Rect r, string glyph, GUIStyle labelStyle)
        {
            Fill(r, IconFill);
            DrawFrame(r, ParchmentDark);
            if (labelStyle == null)
                return;
            var g = string.IsNullOrEmpty(glyph) ? "技" : glyph.Substring(0, 1);
            var prev = labelStyle.alignment;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, g, labelStyle);
            labelStyle.alignment = prev;
        }

        /// <summary>列表行：左圆标＋名称／摘要。返回是否被点击。</summary>
        public static bool DrawListRow(
            Rect row,
            string title,
            string subtitle,
            string badge,
            bool selected,
            GUIStyle titleStyle,
            GUIStyle smallStyle)
        {
            Fill(row, selected ? RowCurrent : RowIdle);
            DrawFrame(row, ParchmentDark);
            var icon = new Rect(row.x + 8f, row.y + (row.height - 40f) * 0.5f, 40f, 40f);
            DrawIconCircle(icon, title, titleStyle);
            var tx = icon.xMax + 10f;
            GUI.Label(new Rect(tx, row.y + 6f, row.width - 90f, 20f), title ?? "", titleStyle);
            GUI.Label(new Rect(tx, row.y + 28f, row.width - 90f, 34f), subtitle ?? "", smallStyle);
            if (!string.IsNullOrEmpty(badge))
            {
                var br = new Rect(row.xMax - 72f, row.y + 8f, 64f, 22f);
                Fill(br, new Color(0.35f, 0.28f, 0.18f, 0.85f));
                var prev = GUI.skin.label.normal.textColor;
                var s = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
                s.normal.textColor = Color.white;
                GUI.Label(br, badge, s);
            }

            return GUI.Button(row, GUIContent.none, GUIStyle.none);
        }

        /// <returns>内容总高度（相对 y0）。</returns>
        public static float DrawMasteryDetailBody(
            Rect area,
            string skillName,
            string gradeLine,
            string summaryLine,
            SkillMasteryState state,
            SkillMasteryProfile profile,
            System.Func<SkillMasteryTier, string> effectForTier,
            SimulationWorld world,
            GUIStyle titleStyle,
            GUIStyle bodyStyle,
            GUIStyle smallStyle,
            ref Vector2 scroll)
        {
            if (state == null)
                state = SkillMasteryState.CreateEntry(profile);
            if (profile == null)
                profile = new SkillMasteryProfile();

            var headerH = 96f;
            var header = new Rect(area.x, area.y, area.width, headerH);
            Fill(header, new Color(0.86f, 0.78f, 0.62f, 0.45f));
            DrawFrame(header, ParchmentDark);
            DrawIconCircle(new Rect(header.x + 14f, header.y + 18f, 56f, 56f), skillName, titleStyle);
            var hx = header.x + 84f;
            GUI.Label(new Rect(hx, header.y + 12f, header.width - 100f, 24f), skillName ?? "", titleStyle);
            GUI.Label(new Rect(hx, header.y + 38f, header.width - 100f, 18f), gradeLine ?? "", smallStyle);
            GUI.Label(new Rect(hx, header.y + 58f, header.width - 100f, 28f), summaryLine ?? "", smallStyle);

            var listTop = header.yMax + 8f;
            var listH = area.yMax - listTop;
            var view = new Rect(area.x, listTop, area.width, listH);
            var rowH = 52f;
            var contentH = Mathf.Max(listH, DisplayOrder.Length * (rowH + 4f) + 8f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, view.width - 18f, contentH));
            var y = 4f;
            for (var i = 0; i < DisplayOrder.Length; i++)
            {
                var tier = DisplayOrder[i];
                var row = new Rect(4f, y, view.width - 26f, rowH);
                DrawTierRow(
                    row,
                    tier,
                    state,
                    profile,
                    effectForTier != null ? effectForTier(tier) : "",
                    world,
                    bodyStyle,
                    smallStyle);
                y += rowH + 4f;
            }

            GUI.EndScrollView();
            return headerH + 8f + listH;
        }

        static void DrawTierRow(
            Rect row,
            SkillMasteryTier tier,
            SkillMasteryState state,
            SkillMasteryProfile profile,
            string effect,
            SimulationWorld world,
            GUIStyle bodyStyle,
            GUIStyle smallStyle)
        {
            var cur = state.Tier;
            var reached = (int)tier < (int)cur;
            var isCurrent = tier == cur;
            Fill(row, isCurrent ? RowCurrent : reached ? RowDone : RowIdle);
            DrawFrame(row, ParchmentDark);

            var status = isCurrent ? "当前" : reached ? "已达" : "未达";
            var statusCol = isCurrent ? Accent : Ink;
            var statusStyle = new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold };
            statusStyle.normal.textColor = statusCol;
            GUI.Label(new Rect(row.x + 8f, row.y + 6f, 40f, 18f), status, statusStyle);

            var tierName = SkillMasteryTierNames.Display(tier);
            GUI.Label(new Rect(row.x + 48f, row.y + 4f, 64f, 22f), tierName, bodyStyle);

            var mid = new Rect(row.x + 112f, row.y + 4f, row.width - 220f, row.height - 8f);
            var effectLine = string.IsNullOrEmpty(effect) ? "—" : effect;
            if (isCurrent && state.ProgressRequired > 0)
                effectLine += "\n进度 " + state.Progress + "/" + state.ProgressRequired +
                              (state.IsAtBottleneck ? " 【可冲击】" : "");
            GUI.Label(mid, effectLine, smallStyle);

            var costR = new Rect(row.xMax - 108f, row.y + 4f, 100f, row.height - 8f);
            var costText = FormatBreakthroughCosts(profile, tier, world);
            GUI.Label(costR, costText, smallStyle);
        }

        static string FormatBreakthroughCosts(
            SkillMasteryProfile profile,
            SkillMasteryTier from,
            SimulationWorld world)
        {
            if (!SkillMasteryLookup.CanBreakthrough(profile, from))
                return "（满档）";
            var costs = SkillMasteryLookup.BreakthroughCosts(profile, from);
            if (costs == null || costs.Count == 0)
                return "冲击材料\n—";
            var sb = new StringBuilder(48);
            sb.Append("冲击需");
            for (var i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c == null || string.IsNullOrEmpty(c.ItemId) || c.Count <= 0)
                    continue;
                sb.Append('\n');
                var name = ShortItemName(world, c.ItemId);
                var have = world?.Inventory != null ? world.Inventory.GetCount(c.ItemId) : 0;
                sb.Append(name).Append(' ').Append(have).Append('/').Append(c.Count);
            }

            return sb.ToString();
        }

        public static string ShortItemName(SimulationWorld world, string itemId)
        {
            if (world?.InventoryCatalog != null)
            {
                var n = world.InventoryCatalog.GetName(itemId);
                if (!string.IsNullOrEmpty(n))
                    return n.Length > 4 ? n.Substring(0, 4) : n;
            }

            if (string.IsNullOrEmpty(itemId))
                return "?";
            var colon = itemId.LastIndexOf(':');
            var bare = colon >= 0 && colon + 1 < itemId.Length ? itemId.Substring(colon + 1) : itemId;
            return bare.Length > 6 ? bare.Substring(0, 6) : bare;
        }

        public static string ArtEffectLine(CombatArtSpec art, SkillMasteryTier tier)
        {
            if (art == null)
                return "—";
            if (art.IsActiveSkill)
            {
                var mult = SkillMasteryLookup.ResolveDamageAttackMult(art, tier);
                return "单段伤害 " + Pct(mult);
            }

            var bonus = SkillMasteryLookup.ResolveAttackBonusPercent(art, tier);
            var flat = SkillMasteryLookup.ResolveDamageFlat(art, tier);
            var s = "普攻 +" + Pct(bonus);
            if (flat > 0)
                s += "　固伤 +" + flat;
            return s;
        }

        public static string ManualEffectLine(CultivationManualSpec manual, SkillMasteryTier tier)
        {
            if (manual == null)
                return "—";
            var speed = SkillMasteryLookup.ResolveCultivationSpeed(manual, tier);
            return "打坐每 5 游戏分 +" + speed + " 修为";
        }

        static string Pct(double v) => ((int)System.Math.Round(v * 100)).ToString() + "%";

        public enum ConfirmChoice
        {
            None = 0,
            Yes = 1,
            No = 2
        }

        /// <summary>熟练冲击确认窗：问是否突破，并显示成功率（结果弹窗不再重复）。</summary>
        public static ConfirmChoice DrawBreakthroughConfirm(
            string title,
            string body,
            GUIStyle titleStyle,
            GUIStyle bodyStyle)
        {
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(dim, new Color(0f, 0f, 0f, 0.5f));
            HostUiHitTest.Block(dim);

            var w = 420f;
            var h = 220f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(r);
            Fill(r, Parchment);
            DrawFrame(r, ParchmentDark);
            GUI.Label(new Rect(r.x + 16f, r.y + 14f, w - 32f, 28f), title ?? "确认冲击", titleStyle);
            GUI.Label(new Rect(r.x + 16f, r.y + 48f, w - 32f, 110f), body ?? "", bodyStyle);

            if (HostImguiStyles.ParchmentBtn(new Rect(r.x + w * 0.5f - 120f, r.yMax - 44f, 100f, 30f), "确认冲击"))
                return ConfirmChoice.Yes;
            if (HostImguiStyles.ParchmentBtn(new Rect(r.x + w * 0.5f + 20f, r.yMax - 44f, 100f, 30f), "取消"))
                return ConfirmChoice.No;
            return ConfirmChoice.None;
        }

        public static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawFrame(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
