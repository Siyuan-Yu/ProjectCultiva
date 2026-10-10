using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// IMGUI 与 RTS 选中抢点击：FormalHud 每帧登记遮挡区，Selection 用上一帧区域忽略点选。
    /// </summary>
    public static class HostUiHitTest
    {
        static readonly List<Rect> _current = new List<Rect>(16);
        static readonly List<Rect> _published = new List<Rect>(16);
        // Ordinary HUD rects are rebuilt by BeginFrame/EndFrame. Floating windows have
        // independent OnGUI ordering, so they need owner-scoped lifetime instead.
        static readonly Dictionary<string, Rect> _persistent = new Dictionary<string, Rect>();
        static readonly List<RetiredRect> _retired = new List<RetiredRect>(4);
        static bool _selectionWholeScreen;

        struct RetiredRect
        {
            public Rect Rect;
            public int Frame;
        }

        public static void BeginFrame()
        {
            _current.Clear();
            _selectionWholeScreen = false;
        }

        /// <summary>登记 IMGUI 遮挡区：本帧 WorldMap 指针 dispatch 与下一帧 Selection 均忽略。</summary>
        public static void Block(Rect guiRect)
        {
            if (guiRect.width > 1f && guiRect.height > 1f)
                _current.Add(guiRect);
        }

        /// <summary>
        /// Register or update one floating IMGUI window in global GUI coordinates (top-left origin).
        /// It is intentionally not cleared by another panel's BeginFrame.
        /// </summary>
        public static void RegisterPersistent(string owner, Rect guiRect)
        {
            if (string.IsNullOrEmpty(owner) || guiRect.width <= 1f || guiRect.height <= 1f)
                return;
            _persistent[owner] = guiRect;
        }

        /// <summary>
        /// Remove a floating window. When retireForCurrentFrame is true, the old rect still
        /// blocks the close click until the next frame, then disappears without a ghost region.
        /// </summary>
        public static void UnregisterPersistent(string owner, bool retireForCurrentFrame = false)
        {
            if (string.IsNullOrEmpty(owner) || !_persistent.TryGetValue(owner, out var rect))
                return;
            _persistent.Remove(owner);
            if (retireForCurrentFrame)
                _retired.Add(new RetiredRect { Rect = rect, Frame = Time.frameCount });
        }

        /// <summary>
        /// 仅交给下一帧 RTS Selection 的全屏占位（大地图打开时挡近景点选）；
        /// 不计入本帧 <see cref="ContainsCurrentGuiPoint"/>，避免误挡 WorldMap 自身输入。
        /// </summary>
        public static void BlockSelectionWholeScreen()
        {
            _selectionWholeScreen = true;
        }

        /// <summary>OnGUI 末尾调用：把本帧遮挡区交给下一帧的 Update 选中逻辑。</summary>
        public static void EndFrame()
        {
            _published.Clear();
            if (_selectionWholeScreen)
                _published.Add(new Rect(0f, 0f, Screen.width, Screen.height));
            for (var i = 0; i < _current.Count; i++)
                _published.Add(_current[i]);
        }

        /// <summary>本帧已登记 IMGUI 遮挡区（GUI 坐标，原点左上）。</summary>
        public static bool ContainsCurrentGuiPoint(Vector2 guiPoint)
        {
            PruneRetired();
            for (var i = 0; i < _current.Count; i++)
            {
                if (_current[i].Contains(guiPoint))
                    return true;
            }

            foreach (var pair in _persistent)
                if (pair.Value.Contains(guiPoint))
                    return true;
            for (var i = 0; i < _retired.Count; i++)
                if (_retired[i].Rect.Contains(guiPoint))
                    return true;

            return false;
        }

        /// <summary><paramref name="screenPoint"/> 为 Input.mousePosition（原点左下）。</summary>
        public static bool ContainsScreenPoint(Vector2 screenPoint)
        {
            PruneRetired();
            var gui = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            for (var i = 0; i < _published.Count; i++)
            {
                if (_published[i].Contains(gui))
                    return true;
            }

            foreach (var pair in _persistent)
                if (pair.Value.Contains(gui))
                    return true;
            for (var i = 0; i < _retired.Count; i++)
                if (_retired[i].Rect.Contains(gui))
                    return true;

            return false;
        }

        static void PruneRetired()
        {
            for (var i = _retired.Count - 1; i >= 0; i--)
                if (_retired[i].Frame != Time.frameCount)
                    _retired.RemoveAt(i);
        }
    }
}
