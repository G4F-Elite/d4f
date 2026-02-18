using Engine.Testing;

namespace Engine.Cli;

internal sealed record GoldenComparisonSummary(
    int ComparedCount,
    IReadOnlyList<string> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

internal static class GoldenArtifactsComparer
{
    public static GoldenComparisonSummary Compare(
        string artifactsDirectory,
        string goldenDirectory,
        IReadOnlyList<TestCaptureArtifact> captures,
        GoldenImageComparisonOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(goldenDirectory);
        ArgumentNullException.ThrowIfNull(captures);
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        int comparedCount = 0;

        foreach (TestCaptureArtifact capture in captures)
        {
            string actualBufferPath = Path.Combine(artifactsDirectory, capture.RelativeBufferPath);
            string goldenBufferPath = Path.Combine(goldenDirectory, capture.RelativeBufferPath);
            if (!File.Exists(goldenBufferPath))
            {
                failures.Add($"Golden buffer is missing: {goldenBufferPath}");
                continue;
            }

            GoldenImageBuffer actual = GoldenImageBufferFileCodec.Read(actualBufferPath);
            GoldenImageBuffer expected = GoldenImageBufferFileCodec.Read(goldenBufferPath);
            GoldenImageComparisonResult result = GoldenImageComparer.Compare(expected, actual, options);
            comparedCount++;
            if (!result.IsMatch)
            {
                failures.Add(
                    $"Golden compare failed for '{capture.RelativeCapturePath}': {result.Message} (mismatched bytes: {result.MismatchedBytes}).");
            }
        }

        return new GoldenComparisonSummary(comparedCount, failures);
    }
}
