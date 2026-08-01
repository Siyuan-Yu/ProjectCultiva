using System;

namespace XianXia.Core.Entities
{
    [Flags]
    public enum EntityTag
    {
        None = 0,
        Character = 1 << 0,
        /// <summary>Non-DirectControl NPC (VS0.5 recruit candidates etc.).</summary>
        Npc = 1 << 1
    }
}

