namespace Engine.Net;

public sealed class NetPeerStats
{
    public long BytesSent { get; private set; }

    public long BytesReceived { get; private set; }

    public int MessagesSent { get; private set; }

    public int MessagesReceived { get; private set; }

    public int MessagesDropped { get; private set; }

    public double RoundTripTimeMs { get; private set; }

    public double AverageSendBandwidthKbps { get; private set; }

    public double AverageReceiveBandwidthKbps { get; private set; }

    public double PeakSendBandwidthKbps { get; private set; }

    public double PeakReceiveBandwidthKbps { get; private set; }

    public double LossPercent => MessagesSent == 0
        ? 0d
        : (double)MessagesDropped / MessagesSent * 100d;

    internal void RecordSent(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count cannot be negative.");
        }

        BytesSent = checked(BytesSent + byteCount);
        MessagesSent = checked(MessagesSent + 1);
    }

    internal void RecordReceived(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count cannot be negative.");
        }

        BytesReceived = checked(BytesReceived + byteCount);
        MessagesReceived = checked(MessagesReceived + 1);
    }

    internal void RecordDropped()
    {
        MessagesDropped = checked(MessagesDropped + 1);
    }

    internal void SetRoundTripTime(double rttMs)
    {
        if (rttMs < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(rttMs), "Round-trip time cannot be negative.");
        }

        RoundTripTimeMs = rttMs;
    }

    internal void RecalculateAverageBandwidth(long tickCount, int tickRateHz)
    {
        if (tickCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), "Tick count cannot be negative.");
        }

        if (tickRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRateHz), "Tick rate must be greater than zero.");
        }

        if (tickCount == 0)
        {
            AverageSendBandwidthKbps = 0d;
            AverageReceiveBandwidthKbps = 0d;
            return;
        }

        double seconds = tickCount / (double)tickRateHz;
        AverageSendBandwidthKbps = (BytesSent * 8d) / 1000d / seconds;
        AverageReceiveBandwidthKbps = (BytesReceived * 8d) / 1000d / seconds;

        PeakSendBandwidthKbps = Math.Max(PeakSendBandwidthKbps, AverageSendBandwidthKbps);
        PeakReceiveBandwidthKbps = Math.Max(PeakReceiveBandwidthKbps, AverageReceiveBandwidthKbps);
    }

    internal NetPeerStats Clone()
    {
        var clone = new NetPeerStats();
        clone.BytesSent = BytesSent;
        clone.BytesReceived = BytesReceived;
        clone.MessagesSent = MessagesSent;
        clone.MessagesReceived = MessagesReceived;
        clone.MessagesDropped = MessagesDropped;
        clone.RoundTripTimeMs = RoundTripTimeMs;
        clone.AverageSendBandwidthKbps = AverageSendBandwidthKbps;
        clone.AverageReceiveBandwidthKbps = AverageReceiveBandwidthKbps;
        clone.PeakSendBandwidthKbps = PeakSendBandwidthKbps;
        clone.PeakReceiveBandwidthKbps = PeakReceiveBandwidthKbps;
        return clone;
    }
}
