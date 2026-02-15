using Engine.Core.Timing;

namespace Engine.Core.Abstractions;

public interface ITimingFacade
{
    FrameTiming NextFrameTiming();
}
