using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    public sealed class NpcAiRoleComponent : IComponent
    {
        public NpcAiRoleKind Role { get; private set; }

        public void Set(NpcAiRoleKind role) => Role = role;
    }
}
