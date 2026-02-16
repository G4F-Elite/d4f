using Engine.Testing;

namespace Engine.Tests.Testing;

public sealed class GoldenImageComparerTests
{
    [Fact]
    public void Compare_ShouldPassPixelPerfect_WhenBuffersMatch()
    {
        GoldenImageBuffer image = CreateImage([10, 20, 30, 255, 40, 50, 60, 255]);

        GoldenImageComparisonResult result = GoldenImageComparer.Compare(
            image,
            image,
            GoldenImageComparisonOptions.PixelPerfect);

        Assert.True(result.IsMatch);
        Assert.Equal(0, result.MismatchedBytes);
        Assert.True(double.IsPositiveInfinity(result.PsnrDb));
    }

    [Fact]
    public void Compare_ShouldFailPixelPerfect_WhenAnyByteDiffers()
    {
        GoldenImageBuffer expected = CreateImage([10, 20, 30, 255, 40, 50, 60, 255]);
        GoldenImageBuffer actual = CreateImage([10, 20, 31, 255, 40, 50, 60, 255]);

        GoldenImageComparisonResult result = GoldenImageComparer.Compare(
            expected,
            actual,
            GoldenImageComparisonOptions.PixelPerfect);

        Assert.False(result.IsMatch);
        Assert.Equal(1, result.MismatchedBytes);
    }

    [Fact]
    public void Compare_ShouldPassTolerant_WhenMaeAndPsnrWithinThreshold()
    {
        GoldenImageBuffer expected = CreateImage([10, 20, 30, 255, 40, 50, 60, 255]);
        GoldenImageBuffer actual = CreateImage([11, 20, 30, 255, 40, 50, 60, 255]);
        var options = new GoldenImageComparisonOptions
        {
            PixelPerfectMatch = false,
            MaxMeanAbsoluteError = 1.0,
            MinPsnrDb = 40.0
        };

        GoldenImageComparisonResult result = GoldenImageComparer.Compare(expected, actual, options);

        Assert.True(result.IsMatch);
    }

    [Fact]
    public void Compare_ShouldFailTolerant_WhenPsnrIsTooLow()
    {
        GoldenImageBuffer expected = CreateImage([0, 0, 0, 255, 0, 0, 0, 255]);
        GoldenImageBuffer actual = CreateImage([255, 255, 255, 255, 255, 255, 255, 255]);
        var options = new GoldenImageComparisonOptions
        {
            PixelPerfectMatch = false,
            MaxMeanAbsoluteError = 255.0,
            MinPsnrDb = 60.0
        };

        GoldenImageComparisonResult result = GoldenImageComparer.Compare(expected, actual, options);

        Assert.False(result.IsMatch);
        Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_ShouldFail_WhenImageSizeDiffers()
    {
        GoldenImageBuffer expected = new(1, 1, new byte[] { 0, 0, 0, 255 });
        GoldenImageBuffer actual = new(2, 1, new byte[] { 0, 0, 0, 255, 0, 0, 0, 255 });

        GoldenImageComparisonResult result = GoldenImageComparer.Compare(
            expected,
            actual,
            GoldenImageComparisonOptions.TolerantDefault);

        Assert.False(result.IsMatch);
        Assert.Contains("size mismatch", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BufferConstructor_ShouldValidateRgbaLength()
    {
        Assert.Throws<ArgumentException>(() => new GoldenImageBuffer(1, 1, new byte[] { 1, 2, 3 }));
    }

    private static GoldenImageBuffer CreateImage(byte[] bytes)
    {
        return new GoldenImageBuffer(2, 1, bytes);
    }
}
