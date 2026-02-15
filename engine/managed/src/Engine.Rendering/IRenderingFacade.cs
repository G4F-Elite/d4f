namespace Engine.Rendering;

public interface IRenderingFacade
{
    void Submit(RenderPacket packet);

    void Present();
}
