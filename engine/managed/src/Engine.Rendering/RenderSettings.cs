using System;

namespace Engine.Rendering;

public readonly struct RenderSettings
{
    public static RenderSettings Default { get; } = new(RenderDebugViewMode.None);

    public RenderSettings(RenderDebugViewMode debugViewMode)
    {
        if (!Enum.IsDefined(debugViewMode))
        {
            throw new ArgumentOutOfRangeException(nameof(debugViewMode), $"Unsupported debug view mode value: {debugViewMode}.");
        }

        DebugViewMode = debugViewMode;
    }

    public RenderDebugViewMode DebugViewMode { get; }
}
