using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    public void ContentMountPak(string pakPath)
    {
        if (string.IsNullOrWhiteSpace(pakPath))
        {
            throw new ArgumentException("Pak path cannot be empty.", nameof(pakPath));
        }

        ThrowIfDisposed();
        NativeStatusGuard.ThrowIfFailed(
            _interop.ContentMountPak(_engine, pakPath),
            "content_mount_pak");
    }

    public void ContentMountDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
        }

        ThrowIfDisposed();
        NativeStatusGuard.ThrowIfFailed(
            _interop.ContentMountDirectory(_engine, directoryPath),
            "content_mount_directory");
    }

    public byte[] ContentReadFile(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));
        }

        ThrowIfDisposed();
        NativeStatusGuard.ThrowIfFailed(
            _interop.ContentReadFile(_engine, assetPath, IntPtr.Zero, 0u, out nuint nativeSize),
            "content_read_file");

        if (nativeSize == 0u)
        {
            return Array.Empty<byte>();
        }

        int size = checked((int)nativeSize);
        var buffer = new byte[size];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                NativeStatusGuard.ThrowIfFailed(
                    _interop.ContentReadFile(
                        _engine,
                        assetPath,
                        (IntPtr)ptr,
                        nativeSize,
                        out nuint nativeReadSize),
                    "content_read_file");

                if (nativeReadSize != nativeSize)
                {
                    throw new InvalidOperationException(
                        $"Native content_read_file returned inconsistent size: expected {nativeSize}, actual {nativeReadSize}.");
                }
            }
        }

        return buffer;
    }
}
