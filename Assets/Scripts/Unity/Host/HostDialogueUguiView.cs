using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// UGUI 底栏对话 View：占位美术 + 打字机 + 选项列。
    /// 未绑 Prefab 字段时在 Awake 程序化生成，之后可直接替换 Sprite／Prefab 引用。
    /// </summary>
    public sealed class HostDialogueUguiView : MonoBehaviour
    {
        const float PanelWidth = 720f;
        const float BarHeight = 168f;
        const float BottomOffset = 72f;
        const float PortraitSize = 96f;
        const float ChoiceColumnWidth = 228f;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);
        static readonly Color PortraitPlaceholder = new Color(0.55f, 0.50f, 0.44f, 0.92f);
        static readonly Color ChoiceNormal = new Color(0.82f, 0.74f, 0.60f, 1f);
        static readonly Color ChoiceHighlight = new Color(0.92f, 0.84f, 0.66f, 1f);
        static readonly Color ChoiceDisabled = new Color(0.62f, 0.58f, 0.52f, 0.72f);

        [SerializeField] RectTransform panelRoot;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField] Image portraitImage;
        [SerializeField] RectTransform choicesRoot;
        [SerializeField] Button bodySkipButton;

        readonly HostDialogueTypewriter _typewriter = new HostDialogueTypewriter();
        readonly List<Button> _choiceButtons = new List<Button>(4);

        GameObject _uiRoot;
        Canvas _canvas;
        string _contentKey = string.Empty;
        string _choicesKey = string.Empty;
        Action<int> _onChoiceSelected;
        Action _onDismissFallback;
        HostDialogueModel _model;
        Font _font;

        public bool IsVisible => panelRoot != null && panelRoot.gameObject.activeSelf;

        void Awake()
        {
            if (panelRoot == null)
                BuildDefaultUi();
            HideImmediate();
        }

        public void Hide()
        {
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);
            _model = null;
            _contentKey = string.Empty;
            _choicesKey = string.Empty;
        }

        public void Sync(
            HostDialogueModel model,
            Action<int> onChoiceSelected,
            Action onDismissFallback,
            float unscaledDeltaTime)
        {
            if (model == null || !model.IsActive)
            {
                Hide();
                return;
            }

            EnsureBuilt();
            _onChoiceSelected = onChoiceSelected;
            _onDismissFallback = onDismissFallback;
            _model = model;

            if (!panelRoot.gameObject.activeSelf)
                panelRoot.gameObject.SetActive(true);

            SyncContent(model);
            _typewriter.Tick(unscaledDeltaTime);
            if (bodyText != null)
                bodyText.text = _typewriter.VisibleText;

            SyncChoices(model);
            ApplyChoiceInteractable(model);
            HandleSkipInput();
        }

        void SyncContent(HostDialogueModel model)
        {
            var key = model.SpeakerName + "\u001f" + model.Body + "\u001f" + model.PortraitResourceId;
            if (key == _contentKey)
                return;

            _contentKey = key;
            if (speakerText != null)
                speakerText.text = model.SpeakerName ?? string.Empty;
            _typewriter.Begin(model.Body ?? string.Empty);
            SyncPortrait(model.PortraitResourceId);
        }

        void SyncPortrait(string resourceId)
        {
            if (portraitImage == null)
                return;

            portraitImage.color = PortraitPlaceholder;
            portraitImage.sprite = null;
            portraitImage.enabled = true;
            // 美术接入：按 resourceId 加载 Sprite 并 portraitImage.sprite = ...
            if (!string.IsNullOrEmpty(resourceId))
                portraitImage.gameObject.name = "Portrait_" + resourceId;
        }

        void SyncChoices(HostDialogueModel model)
        {
            var key = BuildChoicesKey(model);
            if (key == _choicesKey)
                return;

            _choicesKey = key;
            ClearChoiceButtons();
            if (choicesRoot == null)
                return;

            for (var i = 0; i < model.Choices.Count; i++)
            {
                var index = i;
                var line = model.Choices[i];
                var button = CreateChoiceButton(line.Label, line.Enabled);
                button.onClick.AddListener(() => OnChoiceClicked(index));
                _choiceButtons.Add(button);
            }
        }

        void ApplyChoiceInteractable(HostDialogueModel model)
        {
            var ready = _typewriter.IsComplete;
            for (var i = 0; i < _choiceButtons.Count && i < model.Choices.Count; i++)
            {
                var line = model.Choices[i];
                var enabled = ready && line.Enabled;
                _choiceButtons[i].interactable = enabled;
                ApplyChoiceColors(_choiceButtons[i], enabled);
            }
        }

        void HandleSkipInput()
        {
            if (_typewriter.IsComplete || _model == null)
                return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                SkipTypewriter();
        }

        public void SkipTypewriter()
        {
            if (_typewriter.IsComplete)
                return;
            _typewriter.Skip();
            if (bodyText != null)
                bodyText.text = _typewriter.VisibleText;
            if (_model != null)
                ApplyChoiceInteractable(_model);
        }

        void OnChoiceClicked(int index)
        {
            if (_model == null || !_typewriter.IsComplete)
                return;
            if (index < 0 || index >= _model.Choices.Count)
                return;

            var line = _model.Choices[index];
            if (!line.Enabled)
                return;

            if (_model.IsFallback)
                _onDismissFallback?.Invoke();
            else
                _onChoiceSelected?.Invoke(index);
        }

        void OnBodySkipClicked()
        {
            SkipTypewriter();
        }

        void OnGUI()
        {
            if (!IsVisible || panelRoot == null)
                return;
            HostUiHitTest.Block(GetBlockRect());
        }

        Rect GetBlockRect()
        {
            var corners = new Vector3[4];
            panelRoot.GetWorldCorners(corners);
            var x = corners[0].x;
            var y = Screen.height - corners[1].y;
            var w = corners[2].x - corners[0].x;
            var h = corners[1].y - corners[0].y;
            return new Rect(x, y, w, h);
        }

        static string BuildChoicesKey(HostDialogueModel model)
        {
            if (model.Choices.Count == 0)
                return "0";
            var parts = new string[model.Choices.Count];
            for (var i = 0; i < model.Choices.Count; i++)
            {
                var line = model.Choices[i];
                parts[i] = line.ChoiceId + "|" + line.Label + "|" + line.Enabled;
            }

            return string.Join(";", parts);
        }

        void EnsureBuilt()
        {
            if (panelRoot == null)
                BuildDefaultUi();
        }

        void HideImmediate()
        {
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);
        }

        void ClearChoiceButtons()
        {
            for (var i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] != null)
                    Destroy(_choiceButtons[i].gameObject);
            }

            _choiceButtons.Clear();
        }

        void BuildDefaultUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _uiRoot = new GameObject("HostDialogueUguiRoot");
            _uiRoot.transform.SetParent(transform, false);

            _canvas = _uiRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9500;

            var scaler = _uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _uiRoot.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            panelRoot = CreatePanel(_uiRoot.transform, "DialoguePanel", Parchment);
            ApplyCenteredBottomPanel(panelRoot, PanelWidth, BarHeight, BottomOffset);

            var portraitGo = CreateUiObject("Portrait", panelRoot);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(16f, 0f);
            portraitRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            portraitImage = portraitGo.AddComponent<Image>();
            portraitImage.color = PortraitPlaceholder;
            DrawOutline(portraitGo, ParchmentDark);

            var contentGo = CreateUiObject("Content", panelRoot);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(16f + PortraitSize + 16f, 14f);
            contentRect.offsetMax = new Vector2(-ChoiceColumnWidth - 16f, -14f);

            speakerText = CreateText(contentGo.transform, "Speaker", 16, FontStyle.Bold, TextAnchor.UpperLeft);
            var speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = Vector2.zero;
            speakerRect.sizeDelta = new Vector2(0f, 24f);

            var bodyGo = CreateUiObject("Body", contentGo.transform);
            var bodyRect = bodyGo.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(0f, 0f);
            bodyRect.offsetMax = new Vector2(0f, -28f);
            bodyText = CreateText(bodyGo.transform, "Body", 14, FontStyle.Normal, TextAnchor.UpperLeft);
            StretchFill(bodyText.rectTransform);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            bodySkipButton = bodyGo.AddComponent<Button>();
            bodySkipButton.transition = Selectable.Transition.None;
            bodySkipButton.targetGraphic = bodyGo.AddComponent<Image>();
            bodySkipButton.targetGraphic.color = new Color(1f, 1f, 1f, 0.001f);
            bodySkipButton.onClick.AddListener(OnBodySkipClicked);

            var choicesGo = CreateUiObject("Choices", panelRoot);
            choicesRoot = choicesGo.GetComponent<RectTransform>();
            choicesRoot.anchorMin = new Vector2(1f, 0f);
            choicesRoot.anchorMax = new Vector2(1f, 1f);
            choicesRoot.pivot = new Vector2(1f, 0.5f);
            choicesRoot.anchoredPosition = new Vector2(-16f, 0f);
            choicesRoot.sizeDelta = new Vector2(ChoiceColumnWidth, -28f);

            DrawOutline(panelRoot.gameObject, ParchmentDark);
        }

        Button CreateChoiceButton(string label, bool enabled)
        {
            var go = CreateUiObject("Choice", choicesRoot);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 32f);
            var index = choicesRoot.childCount - 1;
            rect.anchoredPosition = new Vector2(0f, -index * 38f);

            var image = go.AddComponent<Image>();
            image.color = enabled ? ChoiceNormal : ChoiceDisabled;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ChoiceNormal;
            colors.highlightedColor = ChoiceHighlight;
            colors.pressedColor = ChoiceHighlight;
            colors.disabledColor = ChoiceDisabled;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = image;
            button.interactable = enabled;

            var text = CreateText(go.transform, "Label", 13, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchFill(text.rectTransform);
            text.text = label ?? string.Empty;

            return button;
        }

        void ApplyChoiceColors(Button button, bool enabled)
        {
            if (button == null)
                return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = enabled ? ChoiceNormal : ChoiceDisabled;
        }

        static RectTransform CreatePanel(Transform parent, string name, Color bg)
        {
            var go = CreateUiObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = bg;
            return go.GetComponent<RectTransform>();
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        Text CreateText(Transform parent, string name, int size, FontStyle style, TextAnchor anchor)
        {
            var go = CreateUiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Ink;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        static void ApplyCenteredBottomPanel(RectTransform rect, float width, float height, float bottomOffset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, bottomOffset);
        }

        static void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void DrawOutline(GameObject root, Color color)
        {
            var outline = root.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
