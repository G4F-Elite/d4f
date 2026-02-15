using System;
using Engine.Core.Abstractions;
using Engine.NativeBindings.Internal;
using Engine.Physics;
using Engine.Rendering;
using Engine.UI;

namespace Engine.NativeBindings;

public sealed class NativeFacadeSet : IDisposable
{
    private readonly NativeRuntime _runtime;

    internal NativeFacadeSet(NativeRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Platform = NativeFacadeFactory.CreatePlatformFacade(runtime);
        Timing = NativeFacadeFactory.CreateTimingFacade(new NativeTimingApiStub());
        Physics = NativeFacadeFactory.CreatePhysicsFacade(runtime);
        Ui = NativeFacadeFactory.CreateUiFacade(new NativeUiApiStub());
        Rendering = NativeFacadeFactory.CreateRenderingFacade(runtime);
    }

    public IPlatformFacade Platform { get; }

    public ITimingFacade Timing { get; }

    public IPhysicsFacade Physics { get; }

    public IUiFacade Ui { get; }

    public IRenderingFacade Rendering { get; }

    public void Dispose()
    {
        _runtime.Dispose();
    }
}
