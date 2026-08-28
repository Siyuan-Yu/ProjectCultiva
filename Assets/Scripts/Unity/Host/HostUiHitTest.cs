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
        static bool _selectionWholeScreen;

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
            for (var i = 0; i < _current.Count; i++)
            {
                if (_current[i].Contains(guiPoint))
                    return true;
            }

            return false;
        }

        /// <summary><paramref name="screenPoint"/> 为 Input.mousePosition（原点左下）。</summary>
        public static bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (_published.Count == 0)
                return false;
            var gui = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            for (var i = 0; i < _published.Count; i++)
            {
                if (_published[i].Contains(gui))
                    return true;
            }

            return false;
        }
    }
}
