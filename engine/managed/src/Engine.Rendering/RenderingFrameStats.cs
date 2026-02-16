namespace Engine.Rendering;

public readonly record struct RenderingFrameStats(
    uint DrawItemCount,
    uint UiItemCount,
    uint ExecutedPassCount,
    ulong PresentCount,
    ulong PipelineCacheHits,
    ulong PipelineCacheMisses,
    ulong PassMask)
{
    public static RenderingFrameStats Empty { get; } =
        new(0u, 0u, 0u, 0u, 0u, 0u, 0u);
}
