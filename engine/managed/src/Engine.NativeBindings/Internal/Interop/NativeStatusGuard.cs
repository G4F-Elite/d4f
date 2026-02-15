namespace Engine.NativeBindings.Internal.Interop;

internal static class NativeStatusGuard
{
    public static void ThrowIfFailed(EngineNativeStatus status, string operation)
    {
        if (status == EngineNativeStatus.Ok)
        {
            return;
        }

        throw new NativeCallException(operation, status);
    }
}
