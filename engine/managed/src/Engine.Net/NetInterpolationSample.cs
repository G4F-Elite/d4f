namespace Engine.Net;

public sealed record NetInterpolationSample(
    NetSnapshot From,
    NetSnapshot To,
    float Alpha);
