using System;

namespace XianXia.Unity.Host
{
    public enum HostWorldMapSelectionKind
    {
        PlayerParty = 0,
        FormalArmy = 1,
    }

    /// <summary>WorldMap 命令选中真源：Marker 视觉与右键 Dispatcher 均读取此对象。</summary>
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
            _kind = HostWorldMapSelectionKind.FormalArmy;
            _formalArmyId = armyId ?? string.Empty;
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
