using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public sealed class FactionFlagState
    {
        public string FlagId { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public HexCoord AnchorHex { get; set; }
        public long EstablishedOrder { get; set; }
        public int CurrentHp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;
        public bool HasLocalPosition { get; set; }
        public float LocalX { get; set; }
        public float LocalZ { get; set; }
    }

    public sealed class FactionFlagBoard
    {
        readonly Dictionary<string, FactionFlagState> _byId = new Dictionary<string, FactionFlagState>();
        readonly Dictionary<HexCoord, string> _anchorIds = new Dictionary<HexCoord, string>();
        public IReadOnlyDictionary<string, FactionFlagState> Flags => _byId;
        public bool TryGetAt(HexCoord hex, out FactionFlagState flag)
        {
            flag = null;
            return _anchorIds.TryGetValue(hex, out var id) && _byId.TryGetValue(id, out flag);
        }
        public bool Register(FactionFlagState flag)
        {
            if (flag == null || string.IsNullOrEmpty(flag.FlagId) || _byId.ContainsKey(flag.FlagId) || _anchorIds.ContainsKey(flag.AnchorHex)) return false;
            _byId[flag.FlagId] = flag; _anchorIds[flag.AnchorHex] = flag.FlagId; return true;
        }
        /// <summary>用完整 active set 原子替换当前 Board；任何冲突都不会改动现有状态。</summary>
        public bool TryReplaceAll(IReadOnlyList<FactionFlagState> flags, out FactionFlagState rejected)
        {
            rejected = null;
            var byId = new Dictionary<string, FactionFlagState>(StringComparer.Ordinal);
            var anchorIds = new Dictionary<HexCoord, string>();
            if (flags != null)
            {
                for (var i = 0; i < flags.Count; i++)
                {
                    var flag = flags[i];
                    if (flag == null || string.IsNullOrEmpty(flag.FlagId) ||
                        byId.ContainsKey(flag.FlagId) || anchorIds.ContainsKey(flag.AnchorHex))
                    {
                        rejected = flag;
                        return false;
                    }
                    byId.Add(flag.FlagId, flag);
                    anchorIds.Add(flag.AnchorHex, flag.FlagId);
                }
            }

            _byId.Clear();
            _anchorIds.Clear();
            foreach (var pair in byId)
                _byId.Add(pair.Key, pair.Value);
            foreach (var pair in anchorIds)
                _anchorIds.Add(pair.Key, pair.Value);
            return true;
        }
        public bool Remove(string flagId)
        {
            if (!_byId.TryGetValue(flagId ?? string.Empty, out var flag)) return false;
            _byId.Remove(flagId); _anchorIds.Remove(flag.AnchorHex); return true;
        }
        public void Clear() { _byId.Clear(); _anchorIds.Clear(); }
    }
}
