namespace Engine.Net;

public sealed record NetworkConfig(
    int TickRateHz = 30,
    int MaxPayloadBytes = 4096,
    int MaxRpcPerTickPerClient = 32,
    int MaxEntitiesPerSnapshot = 8192,
    double SimulatedRttMs = 0.0,
    double SimulatedPacketLossPercent = 0.0)
{
    public NetworkConfig Validate()
    {
        if (TickRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TickRateHz), "Tick rate must be greater than zero.");
        }

        if (MaxPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPayloadBytes), "Max payload bytes must be greater than zero.");
        }

        if (MaxRpcPerTickPerClient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRpcPerTickPerClient), "Max RPC per tick per client must be greater than zero.");
        }

        if (MaxEntitiesPerSnapshot <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxEntitiesPerSnapshot), "Max entities per snapshot must be greater than zero.");
        }

        if (SimulatedRttMs < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SimulatedRttMs), "Simulated RTT cannot be negative.");
        }

        if (SimulatedPacketLossPercent < 0.0 || SimulatedPacketLossPercent > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SimulatedPacketLossPercent), "Simulated packet loss must be within [0..100].");
        }

        return this;
    }
}
