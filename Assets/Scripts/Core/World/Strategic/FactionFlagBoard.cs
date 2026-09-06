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
        public bool Remove(string flagId)
        {
            if (!_byId.TryGetValue(flagId ?? string.Empty, out var flag)) return false;
            _byId.Remove(flagId); _anchorIds.Remove(flag.AnchorHex); return true;
        }
        public void Clear() { _byId.Clear(); _anchorIds.Clear(); }
    }
}
