using System;
using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.UI;

public sealed class NoopUiFacade : IUiFacade
{
    public static NoopUiFacade Instance { get; } = new();

    private NoopUiFacade()
    {
    }

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);
    }
}
