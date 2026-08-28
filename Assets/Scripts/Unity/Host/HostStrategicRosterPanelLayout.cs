using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Global Strategic UI 军队/角色列表共用尺寸：普通侧栏，不占半屏；高度约 50%～70% 视口。
    /// </summary>
    public static class HostStrategicRosterPanelLayout
    {
        public const float Left = 12f;
        public const float Top = 104f;
        const float WidthFraction = 0.30f;
        const float MinWidth = 300f;
        const float MaxWidth = 420f;
        const float HeightFraction = 0.62f;
        const float MinHeight = 320f;
        const float MaxHeightFraction = 0.68f;
        const float RightMargin = 12f;

        public static Rect Compute(float screenWidth, float screenHeight)
        {
            var width = Mathf.Clamp(screenWidth * WidthFraction, MinWidth, MaxWidth);
            width = Mathf.Min(width, screenWidth - Left - RightMargin);
            var maxHeight = screenHeight * MaxHeightFraction;
            var height = Mathf.Clamp(screenHeight * HeightFraction, MinHeight, maxHeight);
            return new Rect(Left, Top, width, height);
        }
    }
}
