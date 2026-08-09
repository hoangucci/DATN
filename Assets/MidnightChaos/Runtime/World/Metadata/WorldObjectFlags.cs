using System;

namespace MidnightChaos.World
{
    [Flags]
    public enum WorldObjectFlags : byte
    {
        None = 0,
        Interactive = 1 << 0,
        BlocksNavMesh = 1 << 1,
        Networked = 1 << 2,
        Decorative = 1 << 3
    }
}
