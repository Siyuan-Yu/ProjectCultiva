using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content.Compatibility
{
    /// <summary>
    /// 旧 FormalArmy Content schema。仅作为兼容输入，由 LegacyArmyContentToSquadMigration 转换为
    /// NpcSquad / SquadWorldMotion；不会创建 FormalArmy runtime 或 ArmyStack UI。
    /// 成员战斗属性仍来自 <see cref="XianXia.Data.Content.CharacterDefinition"/>。
    /// </summary>
    public sealed class LegacyFormalArmyDefinition
    {
        public DefinitionId Id { get; set; }

        /// <summary>兼容内容的展示名。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>兼容转换使用的稳定来源 id。</summary>
        public string RuntimeArmyId { get; set; } = string.Empty;

        /// <summary>兼容转换保留的旧 stack id；不表示运行时存在 ArmyStack。</summary>
        public string RuntimeStackId { get; set; } = string.Empty;

        public string FactionId { get; set; } = string.Empty;

        /// <summary>
        /// 兼容输入的组织所属／assembly origin；没有更精确部署时，由转换器解析为 Squad 初始位置。
        /// </summary>
        public string AssemblySiteId { get; set; } = string.Empty;

        /// <summary>
        /// 旧 authored Hex 部署输入；转换器将其解析为 SquadWorldMotion 兼容位置。
        /// 以 null 表示未声明（(0,0) 仍是合法 hex）。
        /// </summary>
        public LegacyFormalArmyInitialHexDefinition InitialHex { get; set; }
        /// <summary>连续世界初始部署输入；转换为 SquadWorldMotion，优先于旧 initialHex。</summary>
        public LegacyFormalArmyInitialSurfacePositionDefinition InitialSurfacePosition { get; set; }
        /// <summary>Site Core 加 Surface Cell 偏移的部署输入；New Game 时一次性解析到 SquadWorldMotion。</summary>
        public LegacyFormalArmyInitialSurfaceDeploymentDefinition InitialSurfaceDeployment { get; set; }

        public List<LegacyFormalArmyMemberDefinition> Members { get; set; }
            = new List<LegacyFormalArmyMemberDefinition>();
    }

    /// <summary>FormalArmy 可选初始 Hex（axial q/r）。</summary>
    public sealed class LegacyFormalArmyInitialHexDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
    }

    public sealed class LegacyFormalArmyInitialSurfacePositionDefinition
    {
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    public sealed class LegacyFormalArmyInitialSurfaceDeploymentDefinition
    {
        public string SurfaceId { get; set; } = string.Empty;
        public string AnchorSiteId { get; set; } = string.Empty;
        public int OffsetCellsX { get; set; }
        public int OffsetCellsY { get; set; }
    }

    public sealed class LegacyFormalArmyMemberDefinition
    {
        public string CharacterDefinitionId { get; set; } = string.Empty;

        /// <summary>同名 CharacterDefinition 可被多个成员实例复用，靠 DisplayName 区分。</summary>
        public string DisplayName { get; set; } = string.Empty;

        public bool Leader { get; set; }

        /// <summary>复用 OpeningScenario 已生成的唯一角色实体，不重复生成并保留其日程、AI、住处和身份。</summary>
        public bool ReuseOpeningSpawn { get; set; }
    }
}
