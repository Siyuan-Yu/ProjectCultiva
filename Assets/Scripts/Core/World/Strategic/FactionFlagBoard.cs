using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public sealed class FactionFlagState
    {
        public string FlagId { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public long EstablishedOrder { get; set; }
        public int CurrentHp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;
        public bool HasLocalPosition { get; set; }
        public float LocalX { get; set; }
        public float LocalZ { get; set; }
        public bool HasWorldPosition { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        /// <summary>Non-empty when this flag is the unique core of a runtime WorldSite.</summary>
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public bool IsSiteCore { get; set; }
        /// <summary>Runtime-only Content template metadata.</summary>
        public bool IsAuthoredSiteCore { get; set; }
        /// <summary>Explicit Content compatibility/fixture marker; hidden from the normal product WorldMap.</summary>
        public bool IsWorldMapDebugOnly { get; set; }
        public string AuthoredSiteDisplayName { get; set; } = string.Empty;
        public string AuthoredSiteType { get; set; } = string.Empty;
        public int AuthoredCoreLevel { get; set; } = 1;
    }

    public sealed class FactionFlagBoard
    {
        readonly Dictionary<string, FactionFlagState> _byId = new Dictionary<string, FactionFlagState>();
        public IReadOnlyDictionary<string, FactionFlagState> Flags => _byId;
        public bool Register(FactionFlagState flag)
        {
            if (flag == null || string.IsNullOrEmpty(flag.FlagId) || _byId.ContainsKey(flag.FlagId)) return false;
            _byId[flag.FlagId] = flag;
            return true;
        }
        /// <summary>用完整 active set 原子替换当前 Board；任何冲突都不会改动现有状态。</summary>
        public bool TryReplaceAll(IReadOnlyList<FactionFlagState> flags, out FactionFlagState rejected)
        {
            rejected = null;
            var byId = new Dictionary<string, FactionFlagState>(StringComparer.Ordinal);
            if (flags != null)
            {
                for (var i = 0; i < flags.Count; i++)
                {
                    var flag = flags[i];
                    if (flag == null || string.IsNullOrEmpty(flag.FlagId) ||
                        byId.ContainsKey(flag.FlagId))
                    {
                        rejected = flag;
                        return false;
                    }
                    byId.Add(flag.FlagId, flag);
                }
            }

            _byId.Clear();
            foreach (var pair in byId)
                _byId.Add(pair.Key, pair.Value);
            return true;
        }
        public bool Remove(string flagId)
        {
            return _byId.Remove(flagId ?? string.Empty);
        }
        public void Clear() => _byId.Clear();
    }

    /// <summary>Formal Core identity query shared by presentation and invariants.</summary>
    public static class FactionFlagSiteCoreQuery
    {
        public static bool TryResolveFlagForSite(
            SimulationWorld world, WorldSite site, out FactionFlagState flag)
        {
            flag = null;
            if (world?.Strategic?.FactionFlags != null && site != null &&
                !string.IsNullOrWhiteSpace(site.CoreAssetId) &&
                world.Strategic.FactionFlags.Flags.TryGetValue(site.CoreAssetId, out var candidate) &&
                candidate != null && candidate.IsSiteCore &&
                string.Equals(candidate.SiteId, site.SiteId, StringComparison.Ordinal))
            {
                flag = candidate;
                return true;
            }
            return false;
        }
    }
}
