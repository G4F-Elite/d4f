using System;
using Engine.Audio;
using Engine.Core.Abstractions;
using Engine.NativeBindings.Internal;
using Engine.Net;
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
        Timing = NativeFacadeFactory.CreateTimingFacade(runtime);
        Physics = NativeFacadeFactory.CreatePhysicsFacade(runtime);
        Audio = NativeFacadeFactory.CreateAudioFacade(runtime);
        Content = NativeFacadeFactory.CreateContentRuntimeFacade(runtime);
        Net = NativeFacadeFactory.CreateNetFacade(runtime);
        Ui = NativeFacadeFactory.CreateUiFacade(runtime);
        Rendering = NativeFacadeFactory.CreateRenderingFacade(runtime);
    }

    public IPlatformFacade Platform { get; }

    public ITimingFacade Timing { get; }

    public IPhysicsFacade Physics { get; }

    public IAudioFacade Audio { get; }

    public IContentRuntimeFacade Content { get; }

    public INetFacade Net { get; }

    public IUiFacade Ui { get; }

    public IRenderingFacade Rendering { get; }

    public void Dispose()
    {
        _runtime.Dispose();
    }
}
