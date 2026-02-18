namespace Engine.Rendering;

public readonly record struct RenderingFrameStats(
    uint DrawItemCount,
    uint UiItemCount,
    uint ExecutedPassCount,
    ulong PresentCount,
    ulong PipelineCacheHits,
    ulong PipelineCacheMisses,
    ulong PassMask,
    ulong TriangleCount,
    ulong UploadBytes,
    ulong GpuMemoryBytes)
{
    public RenderingFrameStats(
        uint DrawItemCount,
        uint UiItemCount,
        uint ExecutedPassCount,
        ulong PresentCount,
        ulong PipelineCacheHits,
        ulong PipelineCacheMisses,
        ulong PassMask)
        : this(
            DrawItemCount,
            UiItemCount,
            ExecutedPassCount,
            PresentCount,
            PipelineCacheHits,
            PipelineCacheMisses,
            PassMask,
            0u,
            0u,
            0u)
    {
    }

    public static RenderingFrameStats Empty { get; } =
        new(0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u);
}
