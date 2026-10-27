using System;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>IMGUI bottom-bar dialogue view (replace with UGUI implementation later).</summary>
    public sealed class HostDialogueImguiView : IHostDialogueView
    {
        const float Pad = 12f;

        Vector2 _choicesScroll;
        string _pageKey = string.Empty;
        readonly HostDialogueGuiToolkit _gui = new HostDialogueGuiToolkit();

        public void Draw(
            HostDialogueModel model,
            Action<int> onChoiceSelected,
            Action onDismissFallback)
        {
            if (model == null || !model.IsActive)
                return;

            _gui.EnsureStyles();

            var choiceCount = model.Choices.Count;
            var width = Mathf.Min(1040f, Screen.width - Pad * 2f);
            var columns = choiceCount > 6 && width >= 920f ? 2 : 1;
            var choiceWidth = choiceCount == 0 ? 0f : width * (columns == 2 ? 0.55f : 0.40f);
            var cellWidth = (choiceWidth - 18f - (columns - 1) * 8f) / columns;
            var rows = Mathf.CeilToInt((float)choiceCount / columns);
            var choicesHeight = Mathf.Max(0f, rows * 56f - 8f);
            var bodyWidth = Mathf.Max(40f, width - 48f - choiceWidth);
            var bodyHeight = _gui.Body.CalcHeight(new GUIContent(model.Body ?? string.Empty), bodyWidth);
            var height = Mathf.Min(Mathf.Max(220f, Mathf.Max(choicesHeight + 52f, bodyHeight + 60f)), Screen.height - Pad * 2f);
            var bar = new Rect((Screen.width - width) * 0.5f, Screen.height - height - Pad, width, height);
            var pageKey = model.PageKey + "|" + width + "|" + height;
            for (var i = 0; i < choiceCount; i++)
                pageKey += "|" + model.Choices[i].ChoiceId + ":" + model.Choices[i].Label;
            if (_pageKey != pageKey)
            {
                _pageKey = pageKey;
                _choicesScroll = Vector2.zero;
            }
            HostUiHitTest.Block(bar);

            _gui.Fill(bar, HostDialogueGuiToolkit.Parchment);
            _gui.DrawFrame(bar, HostDialogueGuiToolkit.ParchmentDark);

            var speakerRect = new Rect(bar.x + 16f, bar.y + 10f, bar.width - 32f, 24f);
            GUI.Label(speakerRect, model.SpeakerName, _gui.Title);

            var bodyRect = new Rect(
                bar.x + 16f,
                bar.y + 38f,
                bodyWidth,
                bar.height - 50f);
            GUI.Label(bodyRect, model.Body, _gui.Body);

            if (choiceCount > 0)
            {
                var cx = bar.xMax - choiceWidth - 16f;
                _choicesScroll = GUI.BeginScrollView(new Rect(cx, bar.y + 38f, choiceWidth, bar.height - 48f),
                    _choicesScroll, new Rect(0f, 0f, choiceWidth - 18f, choicesHeight));
                var previousEnabled = GUI.enabled;
                for (var i = 0; i < choiceCount; i++)
                {
                    var line = model.Choices[i];
                    var row = new Rect((i % columns) * (cellWidth + 8f), (i / columns) * 56f, cellWidth, 48f);
                    GUI.enabled = previousEnabled && line.Enabled;
                    if (GUI.Button(row, line.Label, _gui.Button))
                    {
                        if (model.IsFallback)
                            onDismissFallback?.Invoke();
                        else
                            onChoiceSelected?.Invoke(i);
                        GUI.enabled = previousEnabled;
                        break; // Selection can replace or clear the model immediately.
                    }

                    GUI.enabled = previousEnabled;
                }
                GUI.EndScrollView();
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
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            Body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                wordWrap = true,
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
