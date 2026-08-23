using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// RimWorld 式战略 Hex 尺度与第一版正式地图规格。
    /// Playable V1：100×50 ≈ 5000 cells；架构可扩展至 10万+ lightweight cells。
    /// </summary>
    public static class HexWorldScale
    {
        /// <summary>Pointy-top 外接圆半径（逻辑世界单位）。视觉大小由 Camera Zoom 决定。</summary>
        public const float DefaultHexOuterRadius = 1f;

        /// <summary>兼容旧名。</summary>
        public const float DefaultHexSize = DefaultHexOuterRadius;

        /// <summary>第一版正式验收地图宽（q 方向，格）。</summary>
        public const int PlayableV1Width = 100;

        /// <summary>第一版正式验收地图高（r 方向，格）。</summary>
        public const int PlayableV1Height = 50;

        /// <summary>Development stress fixture：200×100 ≈ 20k cells。</summary>
        public const int StressTestWidth = 200;

        public const int StressTestHeight = 100;

        /// <summary>渲染 Chunk 边长（仅批处理单位，非 Domain）。</summary>
        public const int RenderChunkSize = 16;

        /// <summary>默认战略视角：横向可见 Hex 数（约 80）。</summary>
        public const float DefaultHexesAcross = 80f;

        /// <summary>最大放大：横向约 14 Hex（约为旧版 2× 放大）。</summary>
        public const float CloseHexesAcross = 14f;

        /// <summary>导入 WorldGraph 时，每 1 个 graph world 单位对应多少 Hex 步。</summary>
        public const int WorldGraphHexStepsPerUnit = 4;

        /// <summary>内容 Site 在 100×50 地图内的布局原点（q,r）。</summary>
        public const int PlayableOriginQ = 8;

        public const int PlayableOriginR = 10;

        /// <summary>Pointy-top：相邻格中心水平间距。</summary>
        public static float HorizontalPitch(float outerRadius) =>
            (float)Math.Sqrt(3) * outerRadius;

        /// <summary>根据目标横向可见 Hex 数计算 _viewHalf（世界单位半高）。</summary>
        public static float ViewHalfForHexesAcross(float hexesAcross, float outerRadius = DefaultHexOuterRadius)
        {
            if (hexesAcross < 1f)
                hexesAcross = DefaultHexesAcross;
            return hexesAcross * HorizontalPitch(outerRadius) * 0.5f;
        }
    }
}
