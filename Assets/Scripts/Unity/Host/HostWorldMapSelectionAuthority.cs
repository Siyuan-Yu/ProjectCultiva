using System;

namespace XianXia.Unity.Host
{
    public enum HostWorldMapSelectionKind
    {
        PlayerParty = 0,
        FormalArmy = 1,
    }

    /// <summary>
    /// WorldMap 玩家命令选中真源。CW-U4.1 后产品命令权威永远是 PlayerParty；
    /// FormalArmy 枚举和 API 仅保留给旧 Host/诊断源码兼容，不能获得命令权。
    /// </summary>
    public sealed class HostWorldMapSelectionAuthority
    {
        HostWorldMapSelectionKind _kind = HostWorldMapSelectionKind.PlayerParty;
        string _formalArmyId = string.Empty;

        public HostWorldMapSelectionKind Kind => _kind;

        public string FormalArmyId => _formalArmyId;

        public bool IsFormalArmy =>
            _kind == HostWorldMapSelectionKind.FormalArmy &&
            !string.IsNullOrEmpty(_formalArmyId);

        public bool IsFormalArmySelected(string armyId) =>
            IsFormalArmy &&
            string.Equals(_formalArmyId, armyId, StringComparison.Ordinal);

        public void SelectFormalArmy(string armyId)
        {
            _ = armyId;
            SelectPlayerParty();
        }

        public void SelectPlayerParty()
        {
            _kind = HostWorldMapSelectionKind.PlayerParty;
            _formalArmyId = string.Empty;
        }

        public string DescribeKind() =>
            _kind == HostWorldMapSelectionKind.FormalArmy ? "FormalArmy" : "PlayerParty";

        public string DescribeId() =>
            _kind == HostWorldMapSelectionKind.FormalArmy
                ? _formalArmyId
                : string.Empty;
    }
}
