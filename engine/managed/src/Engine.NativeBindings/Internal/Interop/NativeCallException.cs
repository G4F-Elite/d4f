using System;

namespace Engine.NativeBindings.Internal.Interop;

internal sealed class NativeCallException : InvalidOperationException
{
    public NativeCallException(string operation, EngineNativeStatus status)
        : base($"Native call '{operation}' failed with status {status} ({(uint)status}).")
    {
        Operation = operation;
        Status = status;
    }

    public string Operation { get; }

    public EngineNativeStatus Status { get; }
}
