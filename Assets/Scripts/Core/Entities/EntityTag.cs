using System;

namespace XianXia.Core.Entities
{
    [Flags]
    public enum EntityTag
    {
        None = 0,
        Character = 1 << 0
    }
}
