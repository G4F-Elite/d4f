using System.Reflection;
using System.Runtime.ExceptionServices;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

public sealed class DffNativeInteropHandleTokenTests
{
    [Fact]
    public void HandleTokenConverters_ShouldRoundtripExpectedHandleValues()
    {
        if (IntPtr.Size < sizeof(long))
        {
            return;
        }

        ulong[] handles =
        [
            0u,
            1u,
            0x0000_0001_FFFF_FFFFUL,
            0x7FFF_FFFF_FFFF_FFFFUL,
            0xFFFF_FFFF_FFFF_FFFFUL
        ];

        foreach (ulong handle in handles)
        {
            IntPtr token = InvokePrivate<IntPtr>("TokenFromHandle", handle);
            ulong roundtrip = InvokePrivate<ulong>("HandleFromToken", token);
            Assert.Equal(handle, roundtrip);
        }
    }

    [Fact]
    public void EnsureHandleInteropSupported_ShouldSucceedOn64BitProcess()
    {
        if (IntPtr.Size < sizeof(long))
        {
            return;
        }

        InvokePrivateVoid("EnsureHandleInteropSupported");
    }

    private static T InvokePrivate<T>(string methodName, params object[] args)
    {
        MethodInfo method = GetPrivateStaticMethod(methodName);
        try
        {
            object? result = method.Invoke(null, args);
            if (result is null)
            {
                throw new InvalidOperationException(
                    $"Method '{methodName}' returned null unexpectedly.");
            }

            return (T)result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void InvokePrivateVoid(string methodName, params object[] args)
    {
        MethodInfo method = GetPrivateStaticMethod(methodName);
        try
        {
            _ = method.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private static MethodInfo GetPrivateStaticMethod(string methodName)
    {
        return typeof(DffNativeInteropApi).GetMethod(
                   methodName,
                   BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException(
                   $"Method '{methodName}' was not found on {nameof(DffNativeInteropApi)}.");
    }
}
