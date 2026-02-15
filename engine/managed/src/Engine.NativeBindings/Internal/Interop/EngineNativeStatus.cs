namespace Engine.NativeBindings.Internal.Interop;

internal enum EngineNativeStatus : uint
{
    Ok = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    VersionMismatch = 3,
    OutOfMemory = 4,
    NotFound = 5,
    InternalError = 100
}
