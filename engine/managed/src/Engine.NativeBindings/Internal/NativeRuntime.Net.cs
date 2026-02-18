using System;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    public IReadOnlyList<NativeNetEventData> NetPump()
    {
        ThrowIfDisposed();

        NativeStatusGuard.ThrowIfFailed(_interop.NetPump(_net, out EngineNativeNetEvents nativeEvents), "net_pump");
        if (nativeEvents.EventCount == 0u)
        {
            return Array.Empty<NativeNetEventData>();
        }

        if (nativeEvents.Events == IntPtr.Zero)
        {
            throw new InvalidOperationException("Native net_pump returned event_count > 0 with null events pointer.");
        }

        var events = new NativeNetEventData[checked((int)nativeEvents.EventCount)];
        int eventSize = Marshal.SizeOf<EngineNativeNetEvent>();
        for (var i = 0; i < events.Length; i++)
        {
            IntPtr eventPtr = nativeEvents.Events + checked(i * eventSize);
            EngineNativeNetEvent nativeEvent = Marshal.PtrToStructure<EngineNativeNetEvent>(eventPtr);
            byte[] payload = CopyPayload(nativeEvent, i);
            events[i] = new NativeNetEventData(nativeEvent.Kind, nativeEvent.Channel, nativeEvent.PeerId, payload);
        }

        return events;
    }

    public void NetSend(uint peerId, byte channel, ReadOnlySpan<byte> payload)
    {
        if (peerId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(peerId), "Peer id must be greater than zero.");
        }

        ThrowIfDisposed();

        unsafe
        {
            fixed (byte* payloadPtr = payload)
            {
                var sendDesc = new EngineNativeNetSendDesc
                {
                    PeerId = peerId,
                    Channel = channel,
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0,
                    Payload = payload.IsEmpty ? IntPtr.Zero : (IntPtr)payloadPtr,
                    PayloadSize = checked((uint)payload.Length)
                };

                NativeStatusGuard.ThrowIfFailed(_interop.NetSend(_net, in sendDesc), "net_send");
            }
        }
    }

    private static byte[] CopyPayload(in EngineNativeNetEvent nativeEvent, int eventIndex)
    {
        if (nativeEvent.PayloadSize == 0u)
        {
            return Array.Empty<byte>();
        }

        if (nativeEvent.Payload == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Native net event at index {eventIndex} has payload_size={nativeEvent.PayloadSize} and null payload pointer.");
        }

        var payload = new byte[checked((int)nativeEvent.PayloadSize)];
        Marshal.Copy(nativeEvent.Payload, payload, 0, payload.Length);
        return payload;
    }
}
