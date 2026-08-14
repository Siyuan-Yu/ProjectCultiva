using System;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>IMGUI bottom-bar dialogue view (replace with UGUI implementation later).</summary>
    public sealed class HostDialogueImguiView : IHostDialogueView
    {
        const float BarH = 168f;
        const float Pad = 12f;
        const float ChoiceW = 220f;
        const float ChoiceH = 30f;

        readonly HostDialogueGuiToolkit _gui = new HostDialogueGuiToolkit();

        public void Draw(
            HostDialogueModel model,
            Action<int> onChoiceSelected,
            Action onDismissFallback)
        {
            if (model == null || !model.IsActive)
                return;

            _gui.EnsureStyles();

            var bar = new Rect(Pad, Screen.height - BarH - Pad, Screen.width - Pad * 2f, BarH);
            HostUiHitTest.Block(bar);

            _gui.Fill(bar, HostDialogueGuiToolkit.Parchment);
            _gui.DrawFrame(bar, HostDialogueGuiToolkit.ParchmentDark);

            var speakerRect = new Rect(bar.x + 16f, bar.y + 10f, bar.width - 32f, 24f);
            GUI.Label(speakerRect, model.SpeakerName, _gui.Title);

            var choiceCount = model.Choices.Count;
            var choiceColW = choiceCount > 0 ? ChoiceW + 8f : 0f;
            var bodyRect = new Rect(
                bar.x + 16f,
                bar.y + 38f,
                bar.width - 32f - choiceColW,
                bar.height - 50f);
            GUI.Label(bodyRect, model.Body, _gui.Body);

            if (choiceCount > 0)
            {
                var cx = bar.xMax - ChoiceW - 16f;
                var cy = bar.y + 38f;
                for (var i = 0; i < choiceCount; i++)
                {
                    var line = model.Choices[i];
                    var row = new Rect(cx, cy, ChoiceW, ChoiceH);
                    GUI.enabled = line.Enabled;
                    if (GUI.Button(row, line.Label, _gui.Button))
                    {
                        if (model.IsFallback)
                            onDismissFallback?.Invoke();
                        else
                            onChoiceSelected?.Invoke(i);
                    }

                    GUI.enabled = true;
                    cy += ChoiceH + 6f;
                    if (cy + ChoiceH > bar.yMax - 10f)
                        break;
                }
            }
        }
    }

    sealed class HostDialogueGuiToolkit
    {
        public static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.98f);
        public static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        public static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);

        Texture2D _px;
        public GUIStyle Title { get; private set; }
        public GUIStyle Body { get; private set; }
        public GUIStyle Button { get; private set; }
        bool _ready;

        public void EnsureStyles()
        {
            if (_ready)
                return;
            EnsurePx();
            Title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            Body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = Ink }
            };
            _ready = true;
        }

        void EnsurePx()
        {
            if (_px != null)
                return;
            _px = Texture2D.whiteTexture;
        }

        public void Fill(Rect r, Color c)
        {
            EnsurePx();
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
        }

        public void DrawFrame(Rect r, Color c)
        {
            const float t = 1f;
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
