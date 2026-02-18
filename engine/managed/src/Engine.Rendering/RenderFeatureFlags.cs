using System;

namespace Engine.Rendering;

[Flags]
public enum RenderFeatureFlags : byte
{
    None = 0,
    DisableAutoExposure = 1 << 0,
    DisableJitterEffects = 1 << 1
}
