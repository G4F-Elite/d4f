using System;

namespace Engine.UI;

[Flags]
public enum UiAnchor
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Top = 1 << 2,
    Bottom = 1 << 3
}
