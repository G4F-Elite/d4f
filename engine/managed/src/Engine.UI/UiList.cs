using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiList : UiListBase
{
    public UiList(string id, TextureHandle itemTexture, TextureHandle fontTexture)
        : base(id, itemTexture, fontTexture)
    {
    }

    internal (int StartIndex, int Count) GetVisibleRange()
    {
        return GetVisibleRangeCore();
    }
}
