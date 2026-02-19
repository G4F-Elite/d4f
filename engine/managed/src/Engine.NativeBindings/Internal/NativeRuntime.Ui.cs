using System;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);
        _ = timing;

        ThrowIfDisposed();
        NativeStatusGuard.ThrowIfFailed(
            _interop.RendererUiReset(_renderer),
            "renderer_ui_reset");
    }
}
