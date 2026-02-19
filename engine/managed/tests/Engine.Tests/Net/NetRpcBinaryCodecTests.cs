using Engine.Net;
using System.Text;

namespace Engine.Tests.Net;

public sealed class NetRpcBinaryCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_ShouldPreserveRpcMessage()
    {
        NetRpcMessage message = new(
            entityId: 19u,
            rpcName: "SpawnProjectile",
            payload: [3, 1, 4, 1, 5],
            channel: NetworkChannel.ReliableOrdered,
            targetClientId: 7u);

        byte[] payload = NetRpcBinaryCodec.Encode(message);
        NetRpcMessage decoded = NetRpcBinaryCodec.Decode(payload);

        Assert.Equal(message.EntityId, decoded.EntityId);
        Assert.Equal(message.RpcName, decoded.RpcName);
        Assert.Equal(message.Payload, decoded.Payload);
        Assert.Equal(message.Channel, decoded.Channel);
        Assert.Equal(message.TargetClientId, decoded.TargetClientId);
    }

    [Fact]
    public void Decode_ShouldFail_WhenMagicIsInvalid()
    {
        NetRpcMessage message = new(
            entityId: 1u,
            rpcName: "Ping",
            payload: [1],
            channel: NetworkChannel.Unreliable);

        byte[] payload = NetRpcBinaryCodec.Encode(message);
        payload[0] ^= 0xA5;

        Assert.Throws<InvalidDataException>(() => NetRpcBinaryCodec.Decode(payload));
    }

    [Fact]
    public void Decode_ShouldFail_WhenChannelIsOutOfRange()
    {
        NetRpcMessage message = new(
            entityId: 2u,
            rpcName: "Burst",
            payload: [8, 9],
            channel: NetworkChannel.Unreliable);

        byte[] payload = NetRpcBinaryCodec.Encode(message);
        int channelOffset = 4 + 4 + 4 + 4 + Encoding.UTF8.GetByteCount(message.RpcName);
        payload[channelOffset] = 255;

        Assert.Throws<InvalidDataException>(() => NetRpcBinaryCodec.Decode(payload));
    }

    [Fact]
    public void Decode_ShouldFail_WhenPayloadIsTruncated()
    {
        NetRpcMessage message = new(
            entityId: 3u,
            rpcName: "Explode",
            payload: [10, 20, 30, 40],
            channel: NetworkChannel.ReliableOrdered,
            targetClientId: 9u);

        byte[] payload = NetRpcBinaryCodec.Encode(message);
        byte[] truncated = payload[..(payload.Length - 2)];

        Assert.ThrowsAny<Exception>(() => NetRpcBinaryCodec.Decode(truncated));
    }
}
