using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum StrategicControlSourceKind { WorldSite, FactionFlag }
    public readonly struct StrategicControlSource
    {
        public readonly string FactionId; public readonly StrategicControlSourceKind Kind; public readonly string SourceId;
        public StrategicControlSource(string factionId, StrategicControlSourceKind kind, string sourceId) { FactionId=factionId; Kind=kind; SourceId=sourceId; }
    }
    /// <summary>Control Assets → first-claim derived Hex controller；不是第二份持久化政治真源。</summary>
    public static class StrategicTerritoryCoverageResolver
    {
        sealed class AssetProjection
        {
            public long Order;
            public string Id;
            public string Faction;
            public StrategicControlSourceKind Kind;
            public List<HexCoord> Nominal;
        }
        sealed class ResolutionCache
        {
            public readonly Dictionary<HexCoord, StrategicControlSource> Sources =
                new Dictionary<HexCoord, StrategicControlSource>();
        }

        static readonly ConditionalWeakTable<SimulationWorld, ResolutionCache> CacheByWorld =
            new ConditionalWeakTable<SimulationWorld, ResolutionCache>();

        public static bool TryGetSource(SimulationWorld world, HexCoord hex, out StrategicControlSource source)
        {
            source = default;
            return world != null && CacheByWorld.TryGetValue(world, out var cache) &&
                   cache.Sources.TryGetValue(hex, out source);
        }
        public static void Rebuild(SimulationWorld world)
        {
            if (world?.HexWorld == null || world.Strategic == null) return;
            var sources = CacheByWorld.GetOrCreateValue(world).Sources;
            sources.Clear();
            var assets = new List<AssetProjection>();
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site=pair.Value; if (site==null || string.IsNullOrEmpty(site.OwnerFactionId)) continue;
                assets.Add(new AssetProjection { Order=site.ControlEstablishedOrder, Id=site.SiteId, Faction=site.OwnerFactionId, Kind=StrategicControlSourceKind.WorldSite, Nominal=ExpandOneRing(site.EnumerateFootprintHexes()) });
            }
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag=pair.Value; if (flag==null || string.IsNullOrEmpty(flag.FactionId)) continue;
                assets.Add(new AssetProjection { Order=flag.EstablishedOrder, Id=flag.FlagId, Faction=flag.FactionId, Kind=StrategicControlSourceKind.FactionFlag, Nominal=ExpandOneRing(new[]{flag.AnchorHex}) });
            }
            assets.Sort((a,b)=> { var c=a.Order.CompareTo(b.Order); return c!=0?c:string.CompareOrdinal(a.Id,b.Id); });
            for (var r=0;r<world.HexWorld.Height;r++) for(var q=0;q<world.HexWorld.Width;q++) if(world.HexWorld.TryGetCell(new HexCoord(q,r),out var cell)&&cell!=null) cell.ControlFactionId=string.Empty;
            foreach (var asset in assets) foreach(var hex in asset.Nominal)
            {
                if (!world.HexWorld.Contains(hex) || sources.ContainsKey(hex)) continue;
                sources[hex]=new StrategicControlSource(asset.Faction,asset.Kind,asset.Id);
                if(world.HexWorld.TryGetCell(hex,out var cell)&&cell!=null) cell.ControlFactionId=asset.Faction;
            }
            foreach(var pair in world.Strategic.TerritoryRegions.Regions)
            {
                var region=pair.Value; if(region==null||!world.Strategic.Sites.TryGet(region.PrimaryWorldSiteId,out var site)||site==null) continue;
                var effective=new List<HexCoord>(); foreach(var item in sources) if(item.Value.Kind==StrategicControlSourceKind.WorldSite&&item.Value.SourceId==site.SiteId) effective.Add(item.Key);
                world.Strategic.TerritoryRegions.ReplaceHexes(region.RegionId, effective);
                region.ControlFactionId=site.OwnerFactionId??string.Empty;
            }
        }
        public static List<HexCoord> ExpandOneRing(IEnumerable<HexCoord> bases)
        {
            var set=new HashSet<HexCoord>(); if(bases!=null) foreach(var hex in bases) { set.Add(hex); for(var d=0;d<6;d++) set.Add(HexMath.Neighbor(hex,d)); }
            return new List<HexCoord>(set);
        }
    }
}
