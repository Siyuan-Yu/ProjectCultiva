using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester：Pending Battle 的 BattleArea / SupportArea Hex 高亮（非正式 UI）。</summary>
    public static class BattleEngagementWorldMapDebug
    {
        public static bool ShowOverlay { get; set; } = true;

        public static void Draw(
            HexMapViewportProjection projection,
            SimulationWorld world)
        {
            if (!ShowOverlay || world?.Strategic == null || world.HexWorld == null || !world.HexWorld.HasGrid)
                return;

            var engagement = world.Strategic.PendingEngagement;
            if (engagement == null || !engagement.IsActive || !engagement.HasSupportArea)
                return;

            var supportArea = engagement.SupportArea;
            var battleHexes = supportArea.BattleAreaHexes;
            for (var i = 0; i < battleHexes.Count; i++)
            {
                HostHexWorldRenderer.DrawHexOutline(
                    projection,
                    world.HexWorld,
                    battleHexes[i],
                    new Color(1f, 0.35f, 0.05f, 0.95f),
                    3.5f);
            }

            var supportHexes = supportArea.SupportAreaHexes;
            for (var i = 0; i < supportHexes.Count; i++)
            {
                var hex = supportHexes[i];
                var isBattle = false;
                for (var b = 0; b < battleHexes.Count; b++)
                {
                    if (battleHexes[b].Equals(hex))
                    {
                        isBattle = true;
                        break;
                    }
                }

                if (isBattle)
                    continue;

                HostHexWorldRenderer.DrawHexOutline(
                    projection,
                    world.HexWorld,
                    hex,
                    new Color(0.15f, 0.75f, 1f, 0.85f),
                    2f);
            }
        }
    }
}
