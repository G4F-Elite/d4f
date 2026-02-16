namespace Engine.Testing;

public sealed class GoldenImageComparisonOptions
{
    public static GoldenImageComparisonOptions PixelPerfect { get; } = new()
    {
        PixelPerfectMatch = true
    };

    public static GoldenImageComparisonOptions TolerantDefault { get; } = new()
    {
        PixelPerfectMatch = false,
        MaxMeanAbsoluteError = 1.0,
        MinPsnrDb = 48.0
    };

    public bool PixelPerfectMatch { get; init; }

    public double MaxMeanAbsoluteError { get; init; } = 1.0;

    public double MinPsnrDb { get; init; } = 48.0;
}

public readonly record struct GoldenImageComparisonResult(
    bool IsMatch,
    double MeanAbsoluteError,
    double PsnrDb,
    int MismatchedBytes,
    string Message);

public static class GoldenImageComparer
{
    public static GoldenImageComparisonResult Compare(
        in GoldenImageBuffer expected,
        in GoldenImageBuffer actual,
        GoldenImageComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new GoldenImageComparisonResult(
                IsMatch: false,
                MeanAbsoluteError: double.PositiveInfinity,
                PsnrDb: double.NegativeInfinity,
                MismatchedBytes: int.MaxValue,
                Message: $"Image size mismatch: expected {expected.Width}x{expected.Height}, actual {actual.Width}x{actual.Height}.");
        }

        ReadOnlySpan<byte> expectedBytes = expected.RgbaBytes.Span;
        ReadOnlySpan<byte> actualBytes = actual.RgbaBytes.Span;
        int totalBytes = expectedBytes.Length;
        int mismatchedBytes = 0;
        double absoluteErrorSum = 0.0;
        double squaredErrorSum = 0.0;

        for (int i = 0; i < totalBytes; i++)
        {
            int diff = Math.Abs(expectedBytes[i] - actualBytes[i]);
            if (diff != 0)
            {
                mismatchedBytes++;
            }

            absoluteErrorSum += diff;
            squaredErrorSum += diff * diff;
        }

        double mae = absoluteErrorSum / totalBytes;
        double mse = squaredErrorSum / totalBytes;
        double psnr = mse == 0.0
            ? double.PositiveInfinity
            : 10.0 * Math.Log10((255.0 * 255.0) / mse);

        if (options.PixelPerfectMatch)
        {
            bool exactMatch = mismatchedBytes == 0;
            string message = exactMatch
                ? "Images match exactly."
                : $"Pixel-perfect comparison failed with {mismatchedBytes} mismatched bytes.";
            return new GoldenImageComparisonResult(exactMatch, mae, psnr, mismatchedBytes, message);
        }

        bool maePass = mae <= options.MaxMeanAbsoluteError;
        bool psnrPass = psnr >= options.MinPsnrDb;
        bool isMatch = maePass && psnrPass;
        string tolerantMessage = isMatch
            ? "Tolerant comparison passed."
            : $"Tolerant comparison failed. MAE={mae:F6} (max {options.MaxMeanAbsoluteError:F6}), PSNR={psnr:F6} dB (min {options.MinPsnrDb:F6} dB).";
        return new GoldenImageComparisonResult(isMatch, mae, psnr, mismatchedBytes, tolerantMessage);
    }
}
