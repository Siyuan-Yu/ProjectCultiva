using XianXia.Core.Entities;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Binds an entity to a <see cref="ScheduleDefinition"/> id. Schedule is default behavior, not player lock.
    /// </summary>
    public sealed class ScheduleComponent : IComponent
    {
        public ScheduleComponent(string definitionId)
        {
            DefinitionId = definitionId ?? string.Empty;
        }

        public string DefinitionId { get; set; }
    }
}
