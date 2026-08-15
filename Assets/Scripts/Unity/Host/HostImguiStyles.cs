using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 羊皮纸按钮与墨色 Label：悬停文字绝不刷白。
    /// </summary>
    public static class HostImguiStyles
    {
        static GUIStyle _parchmentButton;
        static GUIStyle _sideTab;

        static Texture2D _face;
        static Texture2D _faceHover;
        static Texture2D _faceActive;
        static Texture2D _tab;
        static Texture2D _tabHover;
        static Texture2D _tabActive;

        public static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);

        /// <summary>所有交互态文字同色，避免悬停变成皮肤默认白字。</summary>
        public static void LockTextColor(GUIStyle style, Color color)
        {
            if (style == null)
                return;
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
        }

        public static GUIStyle InkLabel(int fontSize, bool bold = false, bool wordWrap = false, Color? ink = null)
        {
            var c = ink ?? Ink;
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wordWrap,
                richText = false
            };
            LockTextColor(s, c);
            return s;
        }

        public static bool ParchmentBtn(Rect r, string label)
        {
            Ensure();
            return GUI.Button(r, label, _parchmentButton);
        }

        public static bool SideTabBtn(Rect r, string label)
        {
            Ensure();
            return GUI.Button(r, label, _sideTab);
        }

        public static bool Button(Rect r, string label, GUIStyle style, Color face, Color hover, Color active)
        {
            // 兼容旧调用：忽略动态色，走不刷白的固定样式。
            Ensure();
            return GUI.Button(r, label, style != null ? style : _parchmentButton);
        }

        public static GUIStyle ParchmentButton
        {
            get
            {
                Ensure();
                return _parchmentButton;
            }
        }

        public static GUIStyle SideTab
        {
            get
            {
                Ensure();
                return _sideTab;
            }
        }

        static void Ensure()
        {
            if (_parchmentButton != null)
                return;

            _face = Solid(new Color(0.86f, 0.78f, 0.62f, 1f));
            _faceHover = Solid(new Color(0.76f, 0.66f, 0.48f, 1f));
            _faceActive = Solid(new Color(0.66f, 0.54f, 0.36f, 1f));
            _tab = Solid(new Color(0.58f, 0.48f, 0.34f, 1f));
            _tabHover = Solid(new Color(0.68f, 0.56f, 0.38f, 1f));
            _tabActive = Solid(new Color(0.80f, 0.66f, 0.32f, 1f));

            _parchmentButton = Make(_face, _faceHover, _faceActive, 13);
            _sideTab = Make(_tab, _tabHover, _tabActive, 12);
            _sideTab.fontStyle = FontStyle.Bold;
            _sideTab.wordWrap = true;
        }

        static GUIStyle Make(Texture2D normal, Texture2D hover, Texture2D active, int fontSize)
        {
            // 空 GUIStyle，绝不拷贝 skin.button 的白色 hover 贴图。
            var s = new GUIStyle
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(6, 6, 4, 4),
                border = new RectOffset(0, 0, 0, 0)
            };
            ApplyState(s.normal, normal);
            ApplyState(s.hover, hover);
            ApplyState(s.active, active);
            ApplyState(s.focused, normal);
            ApplyState(s.onNormal, normal);
            ApplyState(s.onHover, hover);
            ApplyState(s.onActive, active);
            return s;
        }

        static void ApplyState(GUIStyleState state, Texture2D bg)
        {
            state.background = bg;
            state.textColor = Ink;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixels(new[] { c, c, c, c });
            t.Apply(false, true);
            return t;
        }
    }
}
