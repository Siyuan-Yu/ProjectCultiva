using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Read-only presentation of the current Site claim authority on the Surface map.</summary>
    public static class HostSurfaceActualControlRenderer
    {
        static GUIStyle _factionLabel;
        static SimulationWorld _cachedWorld;
        static bool _cachedAuthority;
        static List<FactionActualControlOverlay> _cachedOverlays;
        static readonly List<ClaimStamp> CachedInputs = new List<ClaimStamp>();

        // Derived Host geometry only: compare exact inputs so captures, damage and upgrades
        // invalidate immediately, without partitioning every territory on every repaint.
        struct ClaimStamp
        {
            public (string, string, string, long, float, float, float, float) Claim;
            public (bool, bool, string, string, float, float, float, float) Core;
            public bool Matches(ClaimStamp other) => Claim.Equals(other.Claim) && Core.Equals(other.Core);
        }

        static ClaimStamp ReadStamp(SimulationWorld world, TerritoryClaimState claim)
        {
            if (claim == null) return default;
            var stamp = new ClaimStamp
            {
                Claim = (claim.ClaimId, claim.SiteId, claim.SurfaceId, claim.AcquiredOrder,
                    claim.CenterX, claim.CenterY, claim.Width, claim.Height)
            };
            if (world.Strategic.Sites.TryGet(claim.SiteId, out var site) && site != null)
                stamp.Core = (site.IsCoreActive, site.HasContinuousCore, site.CoreSurfaceId,
                    site.OwnerFactionId, site.CoreWorldX, site.CoreWorldY, site.CoreRangeWidth, site.CoreRangeHeight);
            return stamp;
        }

        static List<FactionActualControlOverlay> GetOverlays(SimulationWorld world)
        {
            var board = world?.Strategic?.TerritoryClaims;
            var claims = board?.Claims;
            var count = claims?.Count ?? 0;
            var authority = board?.HasAuthority ?? false;
            var changed = !ReferenceEquals(world, _cachedWorld) || _cachedOverlays == null ||
                authority != _cachedAuthority || count != CachedInputs.Count;
            if (!changed)
                for (var i = 0; i < count; i++)
                    if (!CachedInputs[i].Matches(ReadStamp(world, claims[i]))) { changed = true; break; }
            if (!changed) return _cachedOverlays;
            _cachedOverlays = WorldSiteActualControlOverlayBuilder.BuildFactionUnion(world);
            _cachedWorld = world;
            _cachedAuthority = authority;
            CachedInputs.Clear();
            for (var i = 0; i < count; i++) CachedInputs.Add(ReadStamp(world, claims[i]));
            return _cachedOverlays;
        }
        public static void Draw(SimulationWorld world, string surfaceId,
            SurfaceWorldMapViewportProjection projection, Texture2D pixel)
        {
            if (Event.current.type != EventType.Repaint || pixel == null) return;
            var overlays = GetOverlays(world);
            foreach (var overlay in overlays)
            {
                if (!string.Equals(overlay.SurfaceId, surfaceId, StringComparison.Ordinal)) continue;
                StrategicFactionCatalog.MapTint(overlay.FactionId, out var r, out var g, out var b);
                foreach (var piece in overlay.Pieces)
                {
                    var a = projection.ProjectWorld(piece.MinX, piece.MinY);
                    var c = projection.ProjectWorld(piece.MaxX, piece.MaxY);
                    Fill(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y),
                        Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)), new Color(r, g, b, .16f), pixel);
                }
                foreach (var edge in overlay.BoundarySegments)
                {
                    var a = projection.ProjectWorld(edge.X0, edge.Y0);
                    var b0 = projection.ProjectWorld(edge.X1, edge.Y1);
                    var rect = Mathf.Abs(a.x - b0.x) < 1f
                        ? new Rect(a.x - 1f, Mathf.Min(a.y, b0.y), 2f, Mathf.Abs(a.y - b0.y))
                        : new Rect(Mathf.Min(a.x, b0.x), a.y - 1f, Mathf.Abs(a.x - b0.x), 2f);
                    Fill(rect, new Color(r, g, b, .95f), pixel);
                }
                // Overview names stay readable in screen pixels; zoomed-in maps already show Site names.
                var minX = float.PositiveInfinity; var minY = float.PositiveInfinity;
                var maxX = float.NegativeInfinity; var maxY = float.NegativeInfinity;
                foreach (var piece in overlay.Pieces)
                {
                    var a = projection.ProjectWorld(piece.MinX, piece.MinY);
                    var c = projection.ProjectWorld(piece.MaxX, piece.MaxY);
                    minX = Mathf.Min(minX, a.x, c.x); minY = Mathf.Min(minY, a.y, c.y);
                    maxX = Mathf.Max(maxX, a.x, c.x); maxY = Mathf.Max(maxY, a.y, c.y);
                }
                if (minX <= maxX && maxX - minX <= 300f && maxY - minY <= 220f)
                {
                    if (_factionLabel == null) _factionLabel = new GUIStyle(GUI.skin.label)
                    { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                    var name = StrategicFactionCatalog.DisplayName(overlay.FactionId);
                    var width = Mathf.Max(62f, name.Length * 13f + 12f);
                    var label = new Rect((minX + maxX - width) * .5f, minY - 22f, width, 20f);
                    Fill(label, new Color(.12f, .14f, .12f, .78f), pixel);
                    _factionLabel.normal.textColor = Color.Lerp(new Color(r, g, b), Color.white, .65f);
                    GUI.Label(label, name, _factionLabel);
                }
            }
        }

        static void Fill(Rect rect, Color color, Texture2D pixel)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old;
        }
    }
}
