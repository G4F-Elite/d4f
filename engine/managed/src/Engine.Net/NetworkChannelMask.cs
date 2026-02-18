namespace Engine.Net;

[Flags]
public enum NetworkChannelMask
{
    None = 0,
    ReliableOrdered = 1 << 0,
    Unreliable = 1 << 1,
    All = ReliableOrdered | Unreliable
}
