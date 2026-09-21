using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>Modern authored NPC group. Runtime identity and roster are Squad-only.</summary>
    public sealed class NpcSquadDefinition
    {
        public DefinitionId Id { get; set; }
        public string SquadId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string AssemblySiteId { get; set; } = string.Empty;
        public NpcSquadInitialSurfacePositionDefinition InitialSurfacePosition { get; set; }
        public NpcSquadInitialSurfaceDeploymentDefinition InitialSurfaceDeployment { get; set; }
        public List<NpcSquadMemberDefinition> Members { get; set; } = new List<NpcSquadMemberDefinition>();
    }

    public sealed class NpcSquadInitialSurfacePositionDefinition
    {
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    public sealed class NpcSquadInitialSurfaceDeploymentDefinition
    {
        public string SurfaceId { get; set; } = string.Empty;
        public string AnchorSiteId { get; set; } = string.Empty;
        public int OffsetCellsX { get; set; }
        public int OffsetCellsY { get; set; }
    }

    public sealed class NpcSquadMemberDefinition
    {
        public string CharacterDefinitionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Leader { get; set; }
        public bool ReuseOpeningSpawn { get; set; }
    }
}
