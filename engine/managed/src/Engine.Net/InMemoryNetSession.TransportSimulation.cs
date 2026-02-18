namespace Engine.Net;

public sealed partial class InMemoryNetSession
{
    private bool ShouldDropPacket(TransportPacketKind kind, uint endpointId)
    {
        if (_config.SimulatedPacketLossPercent <= 0.0)
        {
            return false;
        }
        if (_config.SimulatedPacketLossPercent >= 100.0)
        {
            return true;
        }

        _packetDecisionCounter = checked(_packetDecisionCounter + 1UL);
        uint ticket = ComputeDropTicket(
            kind,
            endpointId,
            _clock.CurrentTick,
            _packetDecisionCounter);
        double normalized = ticket / (double)uint.MaxValue;
        double lossRatio = _config.SimulatedPacketLossPercent / 100.0;
        return normalized < lossRatio;
    }

    private void ApplySimulatedRtt()
    {
        _serverStats.SetRoundTripTime(_config.SimulatedRttMs);
        _serverStats.RecalculateAverageBandwidth(_clock.CurrentTick, _config.TickRateHz);
        foreach (ClientState client in _clients.Values)
        {
            client.Stats.SetRoundTripTime(_config.SimulatedRttMs);
            client.Stats.RecalculateAverageBandwidth(_clock.CurrentTick, _config.TickRateHz);
        }
    }

    private static uint ComputeDropTicket(
        TransportPacketKind kind,
        uint endpointId,
        long tick,
        ulong counter)
    {
        uint hash = 2166136261u;
        hash = Mix(hash, (uint)kind);
        hash = Mix(hash, endpointId);
        hash = Mix(hash, unchecked((uint)tick));
        hash = Mix(hash, unchecked((uint)(counter & 0xFFFFFFFFu)));
        hash = Mix(hash, unchecked((uint)(counter >> 32)));
        return hash;
    }

    private static uint Mix(uint hash, uint value)
    {
        hash ^= value;
        hash *= 16777619u;
        hash ^= hash >> 13;
        return hash;
    }

    private enum TransportPacketKind : uint
    {
        ClientRpc = 1u,
        Snapshot = 2u,
        ServerRpc = 3u
    }
}
