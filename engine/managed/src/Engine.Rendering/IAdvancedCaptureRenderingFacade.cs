namespace Engine.Rendering;

public interface IAdvancedCaptureRenderingFacade
{
    bool TryCaptureFrameRgba16Float(uint width, uint height, out byte[] rgba16Float, bool includeAlpha = true);
}
